﻿using Newtonsoft.Json.Linq;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Serialization;

namespace Firely.Fhir.Validation
{
    /// <summary>
    /// Represents a collection of sub-schema's that can be invoked by other assertions. 
    /// </summary>
    /// <remarks>These rules are not actively run, unless invoked by another assertion.</remarks>
    [DataContract]
    public class Definitions : IAssertion
    {
#if MSGPACK_KEY
        [DataMember(Order = 0)]
        public readonly ElementSchema[] Schemas;
#else
        [DataMember]
        public readonly ElementSchema[] Schemas;
#endif

        public Definitions(params ElementSchema[] schemas) : this(schemas.AsEnumerable()) { }

        public Definitions(IEnumerable<ElementSchema> schemas) => Schemas = schemas.ToArray();

        public JToken ToJson() =>
            new JProperty("definitions", new JArray(
                Schemas.Select(s => s.ToJson())));
    }
}
