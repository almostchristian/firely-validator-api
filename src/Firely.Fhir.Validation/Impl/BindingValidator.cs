﻿/* 
 * Copyright (C) 2021, Firely (info@fire.ly) - All Rights Reserved
 * Proprietary and confidential. Unauthorized copying of this file, 
 * via any medium is strictly prohibited.
 */

using Hl7.Fhir.ElementModel;
using Hl7.Fhir.Introspection;
using Hl7.Fhir.Model;
using Hl7.Fhir.Rest;
using Hl7.Fhir.Specification.Terminology;
using Hl7.Fhir.Support;
using Hl7.Fhir.Utility;
using Hl7.FhirPath.Sprache;
using Newtonsoft.Json.Linq;
using System;
using System.Linq;
using System.Runtime.Serialization;

namespace Firely.Fhir.Validation
{
    /// <summary>
    /// An assertion that expresses terminology binding requirements for a coded element.
    /// </summary>
    [DataContract]
    public class BindingValidator : IValidatable
    {
        /// <summary>
        /// How strongly use of the valueset specified in the binding is encouraged or enforced.
        /// </summary>
        public enum BindingStrength
        {
            /// <summary>
            /// To be conformant, instances of this element SHALL include a code from the specified value set.<br/>
            /// (system: http://hl7.org/fhir/binding-strength)
            /// </summary>
            [EnumLiteral("required", "http://hl7.org/fhir/binding-strength"), Description("Required")]
            Required,

            /// <summary>
            /// To be conformant, instances of this element SHALL include a code from the specified value set if any of the codes within the value set can apply to the concept being communicated.  If the valueset does not cover the concept (based on human review), alternate codings (or, data type allowing, text) may be included instead.<br/>
            /// (system: http://hl7.org/fhir/binding-strength)
            /// </summary>
            [EnumLiteral("extensible", "http://hl7.org/fhir/binding-strength"), Description("Extensible")]
            Extensible,

            /// <summary>
            /// Instances are encouraged to draw from the specified codes for interoperability purposes but are not required to do so to be considered conformant.<br/>
            /// (system: http://hl7.org/fhir/binding-strength)
            /// </summary>
            [EnumLiteral("preferred", "http://hl7.org/fhir/binding-strength"), Description("Preferred")]
            Preferred,

            /// <summary>
            /// Instances are not expected or even encouraged to draw from the specified value set.  The value set merely provides examples of the types of concepts intended to be included.<br/>
            /// (system: http://hl7.org/fhir/binding-strength)
            /// </summary>
            [EnumLiteral("example", "http://hl7.org/fhir/binding-strength"), Description("Example")]
            Example,
        }

        /// <summary>
        /// Uri for the valueset to validate the code in the instance against.
        /// </summary>
        [DataMember]
        public Canonical ValueSetUri { get; private set; }

        /// <summary>
        /// Binding strength for the binding - determines whether an incorrect code is an error.
        /// </summary>
        [DataMember]
        public BindingStrength? Strength { get; private set; }

        /// <summary>
        /// Whether abstract codes (that exist mostly for subsumption queries) may be used
        /// in an instance.
        /// </summary>
        [DataMember]
        public bool AbstractAllowed { get; private set; }

        /// <summary>
        /// The context of the value set, so that the server can resolve this to a value set to 
        /// validate against. 
        /// </summary>
        [DataMember]
        public string? Context { get; private set; }

        /// <summary>
        /// Constructs a validator for validating a coded element.
        /// </summary>
        /// <param name="valueSetUri">Value set Canonical URL</param>
        /// <param name="strength">Indicates the degree of conformance expectations associated with this binding</param>
        /// <param name="abstractAllowed"></param>
        /// <param name="context">The context of the value set, so that the server can resolve this to a value set to validate against.</param>
        public BindingValidator(Canonical valueSetUri, BindingStrength? strength, bool abstractAllowed = true, string? context = null)
        {
            ValueSetUri = valueSetUri;
            Strength = strength;
            AbstractAllowed = abstractAllowed;
            Context = context;
        }

        /// <inheritdoc />
        public ResultReport Validate(ITypedElement input, ValidationContext vc, ValidationState s)
        {
            if (input is null) throw Error.ArgumentNull(nameof(input));
            if (input.InstanceType is null) throw Error.Argument(nameof(input), "Binding validation requires input to have an instance type.");
            if (vc.ValidateCodeService is null)
                throw new InvalidOperationException($"Encountered a ValidationContext that does not have" +
                    $"its non-null {nameof(ValidationContext.ValidateCodeService)} set.");

            // This would give informational messages even if the validation was run on a choice type with a binding, which is then
            // only applicable to an instance which is bindable. So instead of a warning, we should just return as validation is
            // not applicable to this instance.
            if (!ModelInspector.Common.IsBindable(input.InstanceType))
            {
                return vc.TraceResult(() =>
                    new TraceAssertion(input.Location,
                        $"Validation of binding with non-bindable instance type '{input.InstanceType}' always succeeds."));
            }

            if (input.ParseBindable() is { } bindable)
            {
                var result = verifyContentRequirements(input, bindable, s);

                return result.IsSuccessful ?
                    validateCode(input, bindable, vc, s)
                    : result;
            }
            else
            {
                return new IssueAssertion(
                        Strength == BindingStrength.Required ?
                            Issue.CONTENT_INVALID_FOR_REQUIRED_BINDING :
                            Issue.CONTENT_INVALID_FOR_NON_REQUIRED_BINDING,
                            $"Type '{input.InstanceType}' is bindable, but could not be parsed.").AsResult(input.Location, s);
            }
        }

        /// <summary>
        /// Validates whether the instance has the minimum required coded content, depending on the binding.
        /// </summary>
        /// <remarks>Will throw an <c>InvalidOperationException</c> when the input is not of a bindeable type.</remarks>
        private ResultReport verifyContentRequirements(ITypedElement source, Element bindable, ValidationState s)
        {
            switch (bindable)
            {
                case Code code when string.IsNullOrEmpty(code.Value) && Strength == BindingStrength.Required:
                case Coding cd when string.IsNullOrEmpty(cd.Code) && Strength == BindingStrength.Required:
                case CodeableConcept cc when !codeableConceptHasCode(cc) && Strength == BindingStrength.Required:
                    return new IssueAssertion(Issue.TERMINOLOGY_NO_CODE_IN_INSTANCE,
                        $"No code found in {source.InstanceType} with a required binding.").AsResult(source.Location, s);
                case CodeableConcept cc when !codeableConceptHasCode(cc) && string.IsNullOrEmpty(cc.Text) &&
                                Strength == BindingStrength.Extensible:
                    return new IssueAssertion(Issue.TERMINOLOGY_NO_CODE_IN_INSTANCE,
                        $"Extensible binding requires code or text.").AsResult(source.Location, s);
                default:
                    return ResultReport.SUCCESS;      // nothing wrong then
            }

            // Can't end up here
        }

        private static bool codeableConceptHasCode(CodeableConcept cc) =>
            cc.Coding.Any(cd => !string.IsNullOrEmpty(cd.Code));


        private ResultReport validateCode(ITypedElement source, Element bindable, ValidationContext vc, ValidationState s)
        {
            //EK 20170605 - disabled inclusion of warnings/errors for all but required bindings since this will 
            // 1) create superfluous messages (both saying the code is not valid) coming from the validateResult + the outcome.AddIssue() 
            // 2) add the validateResult as warnings for preferred bindings, which are confusing in the case where the slicing entry is 
            //    validating the binding against the core and slices will refine it: if it does not generate warnings against the slice, 
            //    it should not generate warnings against the slicing entry.
            if (Strength != BindingStrength.Required) return ResultReport.SUCCESS;

            var parameters = buildParams()
                .WithValueSet(ValueSetUri.ToString())
                .WithAbstract(AbstractAllowed);

            ValidateCodeParameters buildParams()
            {
                var parameters = new ValidateCodeParameters();

                return bindable switch
                {
                    FhirString str => parameters.WithCode(str.Value, system: null, display: null, context: Context),
                    FhirUri uri => parameters.WithCode(uri.Value, system: null, display: null, context: Context),
                    Code co => parameters.WithCode(co.Value, system: null, display: null, context: Context),
                    Coding cd => parameters.WithCoding(cd),
                    CodeableConcept cc => parameters.WithCodeableConcept(cc),
                    _ => throw Error.InvalidOperation($"Parsed bindable was of unexpected instance type '{bindable.TypeName}'.")
                };
            }

            return callService(parameters, vc, source.Location, s);
        }


        /// <inheritdoc/>
        public JToken ToJson()
        {
            var props = new JObject(new JProperty("abstractAllowed", AbstractAllowed));
            if (Strength is not null)
                props.Add(new JProperty("strength", Strength!.GetLiteral()));
            if (ValueSetUri is not null)
                props.Add(new JProperty("valueSet", (string)ValueSetUri));

            return new JProperty("binding", props);
        }


        private static ResultReport toResultReport(Parameters parameters, string location, ValidationState s)
        {
            var result = parameters.GetSingleValue<FhirBoolean>("result")?.Value ?? false;
            var message = parameters.GetSingleValue<FhirString>("message")?.Value;

            return message switch
            {
                not null => new IssueAssertion(result ? Issue.TERMINOLOGY_OUTPUT_WARNING : Issue.TERMINOLOGY_OUTPUT_ERROR, message).AsResult(location, s),
                null when result => ResultReport.SUCCESS,
                _ => new IssueAssertion(Issue.TERMINOLOGY_OUTPUT_ERROR, "Terminology service indicated failure, but returned no error message for explanation.").AsResult(location, s)
            };
        }

        private static ResultReport callService(ValidateCodeParameters parameters, ValidationContext ctx, string location, ValidationState s)
        {
            try
            {
                var callParams = parameters.Build();
                return toResultReport(TaskHelper.Await(() => ctx.ValidateCodeService.ValueSetValidateCode(callParams)), location, s);
            }
            catch (FhirOperationException tse)
            {
                var desiredResult = ctx.OnValidateCodeServiceFailure?.Invoke(parameters, tse)
                    ?? ValidationContext.TerminologyServiceExceptionResult.Warning;

                var failureIssue = desiredResult switch
                {
                    ValidationContext.TerminologyServiceExceptionResult.Error => Issue.TERMINOLOGY_OUTPUT_ERROR,
                    ValidationContext.TerminologyServiceExceptionResult.Warning => Issue.TERMINOLOGY_OUTPUT_WARNING,
                    _ => throw new NotSupportedException("Logic error: unknown terminology service exception result.")
                };

                var message = buildErrorText(parameters, desiredResult, tse);
                return new IssueAssertion(failureIssue, message).AsResult(location, s);
            }

            static string buildErrorText(ValidateCodeParameters p, ValidationContext.TerminologyServiceExceptionResult er, FhirOperationException tse)
            {
                return p switch
                {
                    { Code: not null } code => $"Terminology service failed while validating code {codeToString(p.Code.Value, p.System?.Value)}: {tse.Message}",
                    { Coding: { } coding } => $"Terminology service failed while validating coding {codeToString(coding.Code, coding.System)}: {tse.Message}",
                    { CodeableConcept: { } cc } => $"Terminology service failed while validating concept {cc.Text} with codings '{ccToString(cc)}'): {tse.Message}",
                    _ => throw new NotSupportedException("Logic error: one of code/coding/cc should have been not null.")
                };

                static string codeToString(string code, string? system)
                {
                    var systemAddition = system is null ? string.Empty : $" (system '{system}')";
                    return $"'{code}'{systemAddition}";
                }

                static string ccToString(CodeableConcept cc) =>
                    string.Join(',', cc.Coding?.Select(c => codeToString(c.Code, c.System)) ?? Enumerable.Empty<string>());
            }
        }
    }
}