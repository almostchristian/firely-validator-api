﻿/* 
 * Copyright (C) 2021, Firely (info@fire.ly) - All Rights Reserved
 * Proprietary and confidential. Unauthorized copying of this file, 
 * via any medium is strictly prohibited.
 */

using FluentAssertions;
using Hl7.Fhir.ElementModel;
using Hl7.Fhir.Support;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.Threading.Tasks;

namespace Firely.Fhir.Validation.Tests
{
    [TestClass]
    public class ReferenceTests
    {
        private static readonly ResourceSchema SCHEMA = new(new StructureDefinitionInformation("http://test.org/patientschema", null!, "Patient", null, false),
                new ChildrenValidator(true,
                    ("id", new ElementSchema("#Patient.id")),
                    ("contained", new SchemaReferenceValidator("http://test.org/patientschema")),
                    ("other", new ReferencedInstanceValidator(new SchemaReferenceValidator("http://test.org/patientschema")))
                ),
                ResultAssertion.SUCCESS
            );


        [TestMethod]
        public void CircularInReferencedResources()
        {
            // circular in contained patients
            var pat1 = new
            {
                resourceType = "Patient",
                id = "http://example.com/pat1",
                other = new { _type = "Reference", reference = "http://example.com/pat2" }
            }.ToTypedElement();

            var pat2 = new
            {
                resourceType = "Patient",
                id = "http://example.com/pat2",
                other = new { _type = "Reference", reference = "http://example.com/pat1" }
            }.ToTypedElement();

            var resolver = new TestResolver() { SCHEMA };
            var vc = ValidationContext.BuildMinimalContext(schemaResolver: resolver);

            Task<ITypedElement?> resolveExample(string example) =>
                Task.FromResult(example switch
                {
                    "http://example.com/pat1" => pat1,
                    "http://example.com/pat2" => pat2,
                    _ => null
                });

            vc.ExternalReferenceResolver = resolveExample;
            var result = SCHEMA.Validate(pat1, vc);
            result.IsSuccessful.Should().BeTrue();  // this is a warning
            result.Evidence.Should().ContainSingle().Which.Should().BeOfType<IssueAssertion>()
                .Which.IssueNumber.Should().Be(Issue.CONTENT_REFERENCE_CYCLE_DETECTED.Code);
        }

        [TestMethod]
        public void CircularInContainedResources()
        {
            // circular in contained patients
            var pat = new
            {
                resourceType = "Patient",
                id = "pat1",
                contained = new[]
                {
                    new
                    {
                        resourceType = "Patient",
                        id = "pat2a",
                        other = new { _type = "Reference", reference = "#pat2b" }
                    },
                    new
                    {
                        resourceType = "Patient",
                        id = "pat2b",
                        other = new { _type = "Reference", reference = "#pat2a" }
                    }
                }
            };

            var result = test(SCHEMA, pat.ToTypedElement("Patient"));
            result.IsSuccessful.Should().BeTrue();
            result.Evidence.Should().Contain(ass => (ass as IssueAssertion)!.IssueNumber == Issue.CONTENT_REFERENCE_CYCLE_DETECTED.Code);
        }

        [TestMethod]
        public void MultipleReferencesToResource()
        {
            var pat = new
            {
                resourceType = "Patient",
                id = "pat1",
                contained = new[]
                {
                    new
                    {
                        resourceType = "Patient",
                        id = "pat2a",
                    }
                },
                other = new[]
                {
                    new { _type = "Reference", reference = "#pat2a" },
                    new { _type = "Reference", reference = "#pat2a" }
                }
            };

            var result = test(SCHEMA, pat.ToTypedElement("Patient"));
            result.IsSuccessful.Should().BeTrue();
        }

        private static ResultReport test(ElementSchema schema, ITypedElement instance)
        {
            var resolver = new TestResolver() { schema };
            var vc = ValidationContext.BuildMinimalContext(schemaResolver: resolver);
            return schema.Validate(instance, vc);
        }
    }
}
