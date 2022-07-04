﻿/* 
 * Copyright (C) 2021, Firely (info@fire.ly) - All Rights Reserved
 * Proprietary and confidential. Unauthorized copying of this file, 
 * via any medium is strictly prohibited.
 */

using Hl7.Fhir.ElementModel;
using Hl7.Fhir.Serialization;
using Hl7.Fhir.Support;
using System;
using System.Runtime.Serialization;
using System.Text.RegularExpressions;

namespace Firely.Fhir.Validation
{
    /// <summary>
    /// Asserts that the value of an element (converted to a string) matches a given regular expression.
    /// </summary>
    [DataContract]
    public class RegExValidator : BasicValidator
    {
        /// <summary>
        /// The regex pattern used to validate the instance against.
        /// </summary>
        [DataMember]
        public string Pattern { get; private set; }

        private readonly Regex _regex;

        /// <summary>
        /// Initializes a new RegExValidator given a pattern.
        /// </summary>
        /// <param name="pattern"></param>
        public RegExValidator(string pattern)
        {
            Pattern = pattern ?? throw new ArgumentNullException(nameof(pattern));
            _regex = new Regex($"^{pattern}$", RegexOptions.Compiled);
        }

        /// <inheritdoc />
        public override string Key => "regex";

        /// <inheritdoc />
        public override object Value => Pattern;

        /// <inheritdoc />
        public override ResultAssertion Validate(ITypedElement input, ValidationContext _, ValidationState __)
        {
            var value = toStringRepresentation(input);
            var success = _regex.Match(value).Success;

            return !success
                ? ResultAssertion.FromEvidence(new IssueAssertion(Issue.CONTENT_ELEMENT_INVALID_PRIMITIVE_VALUE, input.Location, $"Value '{value}' does not match regex '{Pattern}'"))
                : ResultAssertion.SUCCESS;
        }

        private static string? toStringRepresentation(ITypedElement vp)
        {
            return vp == null || vp.Value == null ?
                null :
                PrimitiveTypeConverter.ConvertTo<string>(vp.Value);
        }
    }
}
