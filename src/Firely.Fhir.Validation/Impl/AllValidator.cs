﻿/* 
 * Copyright (C) 2021, Firely (info@fire.ly) - All Rights Reserved
 * Proprietary and confidential. Unauthorized copying of this file, 
 * via any medium is strictly prohibited.
 */

using Hl7.Fhir.ElementModel;
using Newtonsoft.Json.Linq;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Serialization;

namespace Firely.Fhir.Validation
{
    /// <summary>
    /// An assertion that expresses that all member assertions should hold.
    /// </summary>
    [DataContract]
    public class AllValidator : IGroupValidatable
    {
        /// <summary>
        /// The member assertions the instance should be validated against.
        /// </summary>
        [DataMember]
        public IReadOnlyList<IAssertion> Members { get; private set; }

        /// <summary>
        /// Construct an <see cref="AllValidator"/> based on its members.
        /// </summary>
        /// <param name="members"></param>
        public AllValidator(IEnumerable<IAssertion> members)
        {
            Members = members.ToArray();
        }

        /// <summary>
        /// Construct an <see cref="AllValidator"/> based on its members.
        /// </summary>
        /// <param name="members"></param>
        public AllValidator(params IAssertion[] members) : this(members.AsEnumerable())
        {
        }

        /// <inheritdoc cref="IGroupValidatable.Validate(IEnumerable{ITypedElement}, string, ValidationContext, ValidationState)"/>
        public ResultAssertion Validate(
            IEnumerable<ITypedElement> input,
            string groupLocation,
            ValidationContext vc,
            ValidationState state) =>
                ResultAssertion.FromEvidence(Members
                    .Select(ma => ma.ValidateMany(input, groupLocation, vc, state)));


        /// <inheritdoc />
        public ResultAssertion Validate(ITypedElement input, ValidationContext vc, ValidationState state) =>
                  ResultAssertion.FromEvidence(Members.Select(ma => ma.ValidateOne(input, vc, state)));


        /// <inheritdoc />
        public JToken ToJson() =>
            new JProperty("allOf", new JArray(Members.Select(m => new JObject(m.ToJson()))));

    }
}
