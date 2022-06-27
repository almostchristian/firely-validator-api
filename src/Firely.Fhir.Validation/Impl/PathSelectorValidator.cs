﻿/* 
 * Copyright (C) 2021, Firely (info@fire.ly) - All Rights Reserved
 * Proprietary and confidential. Unauthorized copying of this file, 
 * via any medium is strictly prohibited.
 */

using Hl7.Fhir.ElementModel;
using Hl7.FhirPath;
using Newtonsoft.Json.Linq;
using System.Linq;
using System.Runtime.Serialization;

namespace Firely.Fhir.Validation
{

    /// <summary>
    /// Asserts another assertion on a subset of an instance given by a FhirPath expression. 
    /// Used internally only for discriminating the cases of a <see cref="SliceValidator"/>.
    /// </summary>
    [DataContract]
    public class PathSelectorValidator : IValidatable
    {
        /// <summary>
        /// The FhirPath statement used to select a value to validate.
        /// </summary>
        [DataMember]
        public string Path { get; private set; }

        /// <summary>
        /// The assertion to run on the value produced by evaluating the <see cref="Path" />
        /// </summary>
        [DataMember]
        public IAssertion Other { get; private set; }

        /// <summary>
        /// Constructs a validator given the FhirPath and an assertion to run.
        /// </summary>
        public PathSelectorValidator(string path, IAssertion other)
        {
            Path = path;
            Other = other;
        }

        /// <inheritdoc/>
        /// <remarks>Note that this validator is only used internally to represent the checks for
        /// the path-based discriminated cases in a <see cref="SliceValidator" />, so this validator
        /// does not produce standard Issue-based errors.</remarks>
        public ResultAssertion Validate(ITypedElement input, ValidationContext vc, ValidationState state)
        {
            var selected = input.Select(Path).ToList();

            return selected switch
            {
                // 0, 1 or more results are ok for group validatables. Even an empty result is valid for, say, cardinality constraints.
                _ when Other is IGroupValidatable igv => igv.Validate(selected, Path, vc, state),

                // A non-group validatable cannot be used with 0 results.
                { Count: 0 } => new ResultAssertion(ValidationResult.Failure,
                        new TraceAssertion(input.Location, $"The FhirPath selector {Path} did not return any results.")),

                // 1 is ok for non group validatables
                { Count: 1 } => Other.ValidateMany(selected, selected.Single().Location, vc, state),

                // Otherwise we have too many results for a non-group validatable.
                _ => new ResultAssertion(ValidationResult.Failure,
                        new TraceAssertion(input.Location, $"The FhirPath selector {Path} returned too many ({selected.Count}) results."))
            };
        }

        /// <inheritdoc/>
        public JToken ToJson()
        {
            var props = new JObject()
            {
                new JProperty("path", Path),
                new JProperty("assertion", new JObject(Other.ToJson()))

            };

            return new JProperty("pathSelector", props);
        }
    }
}
