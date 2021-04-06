﻿using Firely.Fhir.Validation;
using FluentAssertions;
using FluentAssertions.Primitives;
using Hl7.Fhir.Model;
using System;
using System.Collections.Generic;
using Xunit;

namespace Firely.Validation.Compilation.Tests
{
    public static class SchemaFluentAssertionsExtensions
    {
        public static AndConstraint<ObjectAssertions> BeASchemaAssertionFor(this ObjectAssertions me, string uri) =>
                me.BeOfType<SchemaAssertion>().Which
                .SchemaUri.Should().Be(uri);

        public static AndConstraint<ObjectAssertions> BeAFailureResult(this ObjectAssertions me) =>
                me.BeOfType<ResultAssertion>().Which.Result.Should().Be(ValidationResult.Failure);
    }

    public class TypeRefConverterTests : IClassFixture<SchemaConverterFixture>
    {
        internal SchemaConverterFixture _fixture;

        public TypeRefConverterTests(SchemaConverterFixture fixture) => _fixture = fixture;

        const string HL7SDPREFIX = "http://hl7.org/fhir/StructureDefinition/";
        const string REFERENCE_PROFILE = HL7SDPREFIX + "Reference";
        const string CODE_PROFILE = HL7SDPREFIX + "Code";
        const string IDENTIFIER_PROFILE = HL7SDPREFIX + "Identifier";
        const string MYPROFILE1 = "http://example.org/myProfile";
        const string MYPROFILE2 = "http://example.org/myProfile2";


        [Fact]
        public void TypRefProfileShouldResultInASingleSchemaAssertion()
        {
            var sch = convert("Identifier", profiles: new[] { MYPROFILE1 });
            sch.Should().BeASchemaAssertionFor(MYPROFILE1);
        }

        [Fact]
        public void TypRefWithMultipleProfilesShouldResultInASliceWithSchemaAssertions()
        {
            var sch = convert("Identifier", profiles: new[] { MYPROFILE1, MYPROFILE2 });

            var sa = sch.Should().BeOfType<SliceAssertion>().Subject;
            sa.Slices.Should().HaveCount(2);

            sa.Slices[0].Condition.Should().BeASchemaAssertionFor(MYPROFILE1);
            sa.Slices[0].Assertion.Should().BeOfType<ResultAssertion>().Which.Result.Should().Be(ValidationResult.Success);

            sa.Slices[1].Condition.Should().BeASchemaAssertionFor(MYPROFILE2);
            sa.Slices[1].Assertion.Should().BeOfType<ResultAssertion>().Which.Result.Should().Be(ValidationResult.Success);

            sa.Default.Should().BeAFailureResult();
        }

        [Fact]
        public void TypRefShouldHaveADefaultProfile()
        {
            var sch = convert("Identifier");
            sch.Should().BeASchemaAssertionFor(IDENTIFIER_PROFILE);
        }

        [Fact]
        public void MultipleTypRefsShouldResultInATypeSlice()
        {
            var sch = convert(new[] { build("Identifier"), build("Code") });

            var sa = sch.Should().BeOfType<SliceAssertion>().Subject;
            sa.Slices.Should().HaveCount(2);

            sa.Slices[0].Condition.Should().BeOfType<FhirTypeLabel>().Which.Label.Should().Be("Identifier");
            sa.Slices[0].Assertion.Should().BeASchemaAssertionFor(IDENTIFIER_PROFILE);
            sa.Slices[1].Condition.Should().BeOfType<FhirTypeLabel>().Which.Label.Should().Be("Code");
            sa.Slices[1].Assertion.Should().BeASchemaAssertionFor(CODE_PROFILE);

            sa.Default.Should().BeAFailureResult();
        }

        [Fact]
        public void NakedReferenceTypeShouldHaveReferenceValidationAgainstDefaults()
        {
            var sch = convert("Reference");
            var all = sch.Should().BeOfType<AllAssertion>().Subject;

            all.Members.Should().HaveCount(2);
            all.Members[0].Should().BeASchemaAssertionFor(REFERENCE_PROFILE);
            all.Members[1].Should().BeEquivalentTo(
                new ResourceReferenceAssertion("reference",
                    new AllAssertion(
                        TypeReferenceConverter.FOR_RUNTIME_TYPE,
                        TypeReferenceConverter.META_PROFILE_ASSERTION)),
                options => options.IncludingAllRuntimeProperties());
        }

        [Fact]
        public void ReferenceWithTargetProfilesShouldHaveReferenceValidationAgainstProfiles()
        {
            var sch = convert("Reference", targets: new[] { MYPROFILE1 });
            var all = sch.Should().BeOfType<AllAssertion>().Subject;

            all.Members.Should().HaveCount(2);
            all.Members[0].Should().BeASchemaAssertionFor(REFERENCE_PROFILE);
            all.Members[1].Should().BeEquivalentTo(
                new ResourceReferenceAssertion("reference",
                    new AllAssertion(
                        new SchemaAssertion(new Uri(MYPROFILE1)),
                        TypeReferenceConverter.META_PROFILE_ASSERTION)),
                options => options.IncludingAllRuntimeProperties());
        }

        [Fact]
        public void AggregationConstraintsForReferenceShouldBeGenerated()
        {
            var tr = build("Reference", targets: new[] { MYPROFILE1 });
            tr.AggregationElement.Add(new Code<ElementDefinition.AggregationMode>(ElementDefinition.AggregationMode.Bundled));
            tr.Versioning = ElementDefinition.ReferenceVersionRules.Independent;

            var sch = TypeReferenceConverter.ConvertTypeReference(tr);
            var rr = sch.Should().BeOfType<AllAssertion>().Subject
                .Members[1].Should().BeOfType<ResourceReferenceAssertion>().Subject;

            rr.VersioningRules.Should().Be(ElementDefinition.ReferenceVersionRules.Independent);
            rr.AggregationRules.Should().ContainInOrder(ElementDefinition.AggregationMode.Bundled);
        }

        [Fact]
        public void ExtensionTypeShouldHaveReferenceValidationAgainstUrl()
        {
            var sch = convert("Extension", profiles: new[] { MYPROFILE2 });
            var all = sch.Should().BeOfType<AllAssertion>().Subject;

            all.Members.Should().HaveCount(2);
            all.Members[0].Should().BeASchemaAssertionFor(MYPROFILE2);
            all.Members[1].Should().BeEquivalentTo(TypeReferenceConverter.URL_PROFILE_ASSERTION,
                options => options.IncludingAllRuntimeProperties());
        }

        [Fact]
        public void NakedContainedResourceShouldHaveReferenceValidationAgainstRTT()
        {
            var sch = convert("Resource");
            var all = sch.Should().BeOfType<AllAssertion>().Subject;

            all.Members.Should().HaveCount(2);
            all.Members[0].Should().BeEquivalentTo(TypeReferenceConverter.FOR_RUNTIME_TYPE,
                options => options.IncludingAllRuntimeProperties());
            all.Members[1].Should().BeEquivalentTo(TypeReferenceConverter.META_PROFILE_ASSERTION,
                options => options.IncludingAllRuntimeProperties());
        }

        [Fact]
        public void ContainedResourceShouldHaveReferenceValidationAgainstProfiles()
        {
            var sch = convert("Resource", profiles: new[] { MYPROFILE2 });
            var all = sch.Should().BeOfType<AllAssertion>().Subject;

            all.Members.Should().HaveCount(2);
            all.Members[0].Should().BeASchemaAssertionFor(MYPROFILE2);
            all.Members[1].Should().BeEquivalentTo(TypeReferenceConverter.META_PROFILE_ASSERTION,
                options => options.IncludingAllRuntimeProperties());
        }

        static ElementDefinition.TypeRefComponent build(string code, string[]? profiles = null, string[]? targets = null)
         => new() { Code = code, Profile = profiles, TargetProfile = targets };

        static IAssertion convert(string code, string[]? profiles = null, string[]? targets = null)
             => TypeReferenceConverter.ConvertTypeReference(build(code, profiles, targets));

        static IAssertion convert(IEnumerable<ElementDefinition.TypeRefComponent> trs) =>
            TypeReferenceConverter.ConvertTypeReferences(trs);
    }
}

