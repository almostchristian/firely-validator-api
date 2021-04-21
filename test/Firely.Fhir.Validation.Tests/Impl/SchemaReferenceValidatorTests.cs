﻿/* 
 * Copyright (C) 2021, Firely (info@fire.ly) - All Rights Reserved
 * Proprietary and confidential. Unauthorized copying of this file, 
 * via any medium is strictly prohibited.
 */

using FluentAssertions;
using Hl7.Fhir.ElementModel;
using Hl7.Fhir.Support;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Firely.Fhir.Validation.Tests
{
    [TestClass]
    public class SchemaReferenceValidatorTests : BasicValidatorDataAttribute
    {
        public override IEnumerable<object?[]> GetData()
        {
            yield return new object?[] { new Uri("http://someotherschema"), new SchemaReferenceValidator(new Uri("http://someotherschema")) };
            yield return new object?[] { new Uri("http://extensionschema.nl"), new DynamicSchemaReferenceValidator("url") };
            yield return new object?[] { new Uri("http://hl7.org/fhir/StructureDefinition/Extension"), new RuntimeTypeValidator() };
        }

        [SchemaReferenceValidatorTests]
        [DataTestMethod]
        public async Task InvokesCorrectSchema(Uri schemaUri, IAssertion testee)
        {
            var schema = new ElementSchema(schemaUri, new ChildrenValidator(true, ("value", new FixedValidator("hi"))));
            var resolver = new TestResolver() { schema };
            var vc = ValidationContext.BuildMinimalContext(schemaResolver: resolver);

            var instance = new
            {
                _type = "Extension",
                url = "http://extensionschema.nl",
                value = "hi"
            };

            var result = await testee.Validate(instance.ToTypedElement(), vc);
            Assert.IsTrue(result.Result.IsSuccessful);
            Assert.IsTrue(resolver.ResolvedSchemas.Contains(schemaUri));
            Assert.AreEqual(1, resolver.ResolvedSchemas.Count);
        }

        private readonly ITypedElement _dummyData =
            (new
            {
                _type = "Boolean",
                value = true
            }).ToTypedElement();

        [TestMethod]
        public async Task InvokesMissingSchema()
        {
            var schema = new SchemaReferenceValidator("http://example.org/non-existant");
            var resolver = new TestResolver(); // empty resolver with no profiles installed
            var vc = ValidationContext.BuildMinimalContext(schemaResolver: resolver);

            var result = await schema.Validate(_dummyData, vc);
            result.Result.Evidence.Should().ContainSingle().Which.Should().BeOfType<IssueAssertion>().Which
                .IssueNumber.Should().Be(Issue.UNAVAILABLE_REFERENCED_PROFILE.Code);
        }

        [DataTestMethod]
        [DataRow("#Subschema1", true)]
        [DataRow("#Subschema2", true)]
        [DataRow("#Subschema3", true)]
        [DataRow("#Subschema4", false)]
        public async Task InvokedSubschema(string subschema, bool success)
        {
            var schema = new ElementSchema("http://example.org/rootSchema",
                new DefinitionsAssertion(
                    new ElementSchema("#Subschema1", ResultAssertion.SUCCESS),
                    new ElementSchema("#Subschema2", ResultAssertion.SUCCESS)
                    ),
                new DefinitionsAssertion(
                    new ElementSchema("#Subschema3", ResultAssertion.SUCCESS)
                    )
                );

            var resolver = new TestResolver(new[] { schema });
            var vc = ValidationContext.BuildMinimalContext(schemaResolver: resolver);

            var refSchema = new SchemaReferenceValidator(schema.Id!, subschema: subschema);
            var result = await refSchema.Validate(_dummyData, vc);
            Assert.AreEqual(success, result.Result.IsSuccessful);
        }

        [DataTestMethod]
        [DataRow("nonsense", null)]
        [DataRow("$this", "value")]
        [DataRow("child", "child")]
        [DataRow("child2", null)] // no _value
        [DataRow("child2.child3", "value3")]
        [DataRow("rep", null)] // no _value
        [DataRow("rep.child4", "value4a")]
        public void WalksInstanceCorrectly(string path, string? expected)
        {
            var instance = new
            {
                _value = "value",
                child = "child",
                child2 = new
                {
                    child3 = new
                    {
                        _value = "value3"
                    }
                },
                rep = new[] {
                    new { child4 = new { _value = "value4a" }},
                    new { child4 = new { _value = "value4b" }}
                }
            };

            var instanceTE = instance.ToTypedElement();
            Assert.AreEqual(DynamicSchemaReferenceValidator.GetStringByMemberName(instanceTE, path), expected);
        }
    }
}
