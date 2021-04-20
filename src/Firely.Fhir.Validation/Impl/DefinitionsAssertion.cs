﻿/* 
 * Copyright (C) 2021, Firely (info@fire.ly) - All Rights Reserved
 * Proprietary and confidential. Unauthorized copying of this file, 
 * via any medium is strictly prohibited.
 */

using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Serialization;

namespace Firely.Fhir.Validation
{
    /// <summary>
    /// Represents a collection of sub-schema's that can be invoked by other assertions. 
    /// </summary>
    /// <remarks>This <see cref="IAssertion"/> does not itself validate the subschema's, but they can be 
    /// retrieved and invoked by a <see cref="SchemaReferenceValidator"/>. This is done by appending an 
    /// anchor to the absolute uri of top-level <see cref="ElementSchema"/>. A top-level ElementSchema will
    /// then go through each child <see cref="DefinitionsAssertion"/> and look for the first schema within that
    /// DefinitionAssertion with an id that is exactly equal to the anchor.
    /// string of the anchor. 
    /// </remarks>
    [DataContract]
    public class DefinitionsAssertion : IAssertion
    {
#if MSGPACK_KEY
        /// <summary>
        /// The list of subschemas.
        /// </summary>
        [DataMember(Order = 0)]
        public readonly ElementSchema[] Schemas;
#else
        /// <summary>
        /// The list of subschemas.
        /// </summary>
        [DataMember]
        public readonly ElementSchema[] Schemas;
#endif

        /// <summary>
        /// Constructs a <see cref="DefinitionsAssertion"/> with the given set of subschemas.
        /// </summary>
        /// <param name="schemas"></param>
        public DefinitionsAssertion(params ElementSchema[] schemas) : this(schemas.AsEnumerable()) { }

        /// <inheritdoc cref="DefinitionsAssertion.DefinitionsAssertion(ElementSchema[])"/>
        public DefinitionsAssertion(IEnumerable<ElementSchema> schemas)
        {
            Schemas = schemas.ToArray();
            if (Schemas.Select(s => s.Id).Distinct().Count() != Schemas.Length)
                throw new ArgumentException("Subschemas must have unique ids.", nameof(schemas));
        }

        /// <summary>
        /// Find the first subschema with the given anchor.
        /// </summary>
        /// <returns>An <see cref="ElementSchema"/> if found, otherwise <c>null</c>.</returns>
        public ElementSchema FindFirstByAnchor(string anchor) => Schemas.FirstOrDefault(s => s.Id.OriginalString == anchor);

        /// <inheritdoc cref="IJsonSerializable.ToJson"/>
        public JToken ToJson() =>
            new JProperty("definitions", new JArray(
                Schemas.Select(s => s.ToJson())));
    }
}
