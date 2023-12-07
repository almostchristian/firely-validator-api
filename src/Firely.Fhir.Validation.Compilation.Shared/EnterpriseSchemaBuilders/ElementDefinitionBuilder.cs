﻿using Hl7.Fhir.Specification.Navigation;
using System.Collections.Generic;

namespace Firely.Fhir.Validation.Compilation
{
    /// <summary>
    /// The schema builder for the <see cref="ElementDefinitionValidator"/>.
    /// </summary>
    internal class ElementDefinitionBuilder : ISchemaBuilder
    {
        /// <inheritdoc/>
        public IEnumerable<IAssertion> Build(ElementDefinitionNavigator nav, ElementConversionMode? conversionMode = ElementConversionMode.Full)
        {
            if (nav.Current.Path == "ElementDefinition")
            {
                yield return new ElementDefinitionValidator();
            };
        }
    }
}
