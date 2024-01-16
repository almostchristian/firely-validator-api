﻿/* 
 * Copyright (C) 2021, Firely (info@fire.ly) - All Rights Reserved
 * Proprietary and confidential. Unauthorized copying of this file, 
 * via any medium is strictly prohibited.
 */

using Hl7.Fhir.Model;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Runtime.Serialization;

namespace Firely.Fhir.Validation
{
    /// <summary>
    /// Asserts the validity of an element against a fixed schema.
    /// </summary>
    [DataContract]
    public class SchemaReferenceValidator : IGroupValidatable
    {
        /// <summary>
        /// A singleton <see cref="SchemaReferenceValidator"/> representing a schema reference to <see cref="Resource"/>.
        /// </summary>
        public static readonly SchemaReferenceValidator ForResource = new(Canonical.ForCoreType("Resource"));

        /// <summary>
        /// A fixed uri that is to resolve the schema
        /// using a <see cref="IElementSchemaResolver" />.
        /// </summary>
        [DataMember]
        public Canonical SchemaUri { get; private set; }

        /// <summary>
        /// Construct a <see cref="SchemaReferenceValidator"/> for a fixed uri.
        /// </summary>
        public SchemaReferenceValidator(Canonical schemaUri)
        {
            if (schemaUri.Uri is null) throw new ArgumentException("Canonical must contain a url, not just an anchor", nameof(schemaUri));
            SchemaUri = schemaUri;
        }

        /// <inheritdoc cref="IGroupValidatable.Validate(IEnumerable{IScopedNode}, ValidationSettings, ValidationState)" />
        ResultReport IGroupValidatable.Validate(IEnumerable<IScopedNode> input, ValidationSettings vc, ValidationState state)
        {
            if (vc.ElementSchemaResolver is null)
                throw new ArgumentException($"Cannot validate because {nameof(ValidationSettings)} does not contain an ElementSchemaResolver.");

            return FhirSchemaGroupAnalyzer.FetchSchema(vc.ElementSchemaResolver, state, SchemaUri) switch
            {
                (var schema, null, _) => schema!.ValidateInternal(input, vc, state),
                (_, var error, _) => error
            };
        }

        /// <inheritdoc/>
        ResultReport IValidatable.Validate(IScopedNode input, ValidationSettings vc, ValidationState state) => ((IGroupValidatable)this).Validate(new[] { input }, vc, state);


        /// <inheritdoc cref="IJsonSerializable.ToJson"/>
        public JToken ToJson() => new JProperty("ref", SchemaUri.ToString());
    }
}
