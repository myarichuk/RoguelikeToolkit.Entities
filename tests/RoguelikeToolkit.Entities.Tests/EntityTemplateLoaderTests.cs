using System.Runtime.InteropServices;
using RoguelikeToolkit.Entities.Exceptions;
using RoguelikeToolkit.Entities.Repository;
// ReSharper disable StringLiteralTypo
// ReSharper disable ComplexConditionExpression
#pragma warning disable CS1591

// ReSharper disable ExceptionNotDocumented
// ReSharper disable MethodTooLong
// ReSharper disable ExceptionNotDocumentedOptional
namespace RoguelikeToolkit.Entities.Tests
{
    public class EntityTemplateLoaderTests
    {
        private readonly EntityTemplateLoader _loader = new();

        [Fact]
        public void Should_fail_loading_empty_yaml()
        {
            Assert.Throws<FailedToParseException>(() =>
                _loader.LoadFrom(new FileInfo(Path.Combine("TemplatesForLoading", "empty-y.yaml"))));
        }

        [Fact]
        public void Should_fail_loading_empty_json()
        {
            Assert.Throws<FailedToParseException>(() =>
                _loader.LoadFrom(new FileInfo(Path.Combine("TemplatesForLoading", "empty-j.json"))));
        }

        [Fact]
        public void Bare_ref_resolves_against_template_directory()
        {
            // other.yaml exists only next to main.yaml, not in the working directory,
            // so this passes only when $ref is resolved against the template's own folder
            var template = _loader.LoadFrom(new FileInfo(Path.Combine("TemplatesForLoading", "Refs", "main.yaml")));
            Assert.NotNull(template);

            var child = Assert.Single(template.EmbeddedTemplates);
            var grandchild = Assert.Single(child.EmbeddedTemplates);
            Assert.Contains("other-foobar", grandchild.Components.Keys);
        }

        [Fact]
        public void Should_fail_loading_ref_to_empty_template()
        {
            Assert.Throws<FailedToParseException>(() =>
                _loader.LoadFrom(new FileInfo(Path.Combine("TemplatesForLoading", "Refs", "ref-to-empty.yaml"))));
        }

        [Fact]
        public void Should_fail_loading_cyclic_ref()
        {
            Assert.Throws<FailedToParseException>(() =>
                _loader.LoadFrom(new FileInfo(Path.Combine("TemplatesForLoading", "Refs", "cycle-a.yaml"))));
        }

        [Fact]
        public void Should_fail_loading_non_string_tags()
        {
            using var stream = new MemoryStream(
                System.Text.Encoding.UTF8.GetBytes("Tags:\n - 123\nComponents:\n foo: bar\n"));
            using var reader = new StreamReader(stream);
            Assert.Throws<FailedToParseException>(() => _loader.LoadFrom(reader));
        }

        [Fact]
        public void Should_fail_loading_scalar_tags()
        {
            using var stream = new MemoryStream(
                System.Text.Encoding.UTF8.GetBytes("Tags: tag1\nComponents:\n foo: bar\n"));
            using var reader = new StreamReader(stream);
            Assert.Throws<FailedToParseException>(() => _loader.LoadFrom(reader));
        }

        [Fact]
        public void Should_fail_loading_scalar_components()
        {
            using var stream = new MemoryStream(
                System.Text.Encoding.UTF8.GetBytes("Components: nope\n"));
            using var reader = new StreamReader(stream);
            Assert.Throws<FailedToParseException>(() => _loader.LoadFrom(reader));
        }

        [Fact]
        public void Should_fail_loading_with_unexpected_property()
        {
            Assert.Throws<FailedToParseException>(() =>
                _loader.LoadFrom(new FileInfo(Path.Combine("TemplatesForLoading", "template-with-invalid-fields.yaml"))));
        }

        [SkippableFact]
        public void Should_fail_loading_with_invalid_path_chars()
        {
            Skip.If(!RuntimeInformation.IsOSPlatform(OSPlatform.Windows));

            Assert.Throws<IOException>(() =>
                _loader.LoadFrom(Path.Combine("TemplatesForLoading", "template-with-reference-invalid-path-chars.yaml")));
        }

        [Theory]
        [InlineData("template-with-reference-nonexisting-ref.yaml")]
        [InlineData("template-with-reference-nonexisting-merge-ref.yaml")]
        public void Should_fail_loading_with_non_existing_template_ref_file(string templateFilename)
        {
            Assert.Throws<FileNotFoundException>(() =>
                _loader.LoadFrom(new FileInfo(Path.Combine("TemplatesForLoading", templateFilename))));
        }

        [Theory]
        [InlineData("template-with-embedded.yaml")]
        [InlineData("template-with-reference-embedded.yaml")]
        public void Can_load_template_with_embedded_templates(string templateFilename)
        {
            var template =
                _loader.LoadFrom(new FileInfo(Path.Combine("TemplatesForLoading", templateFilename)));
            Assert.NotNull(template); // sanity check

            Assert.Collection(
                template.Components,
                kvp =>
                {
                    Assert.Equal("foobar", kvp.Key);

                    // embedded objects yaml deserializer loads as Dictionary<object, object>
                    var valueAsDict = (Dictionary<object, object>)kvp.Value;
                    Assert.Equal("abcdef", valueAsDict["stringProperty"]);
                    Assert.Equal((byte)123, valueAsDict["numProperty"]);
                });

            Assert.Equal(2, template.EmbeddedTemplates.Count);

            var embeddedTemplate1 = template.EmbeddedTemplates.First();
            Assert.Collection(
                embeddedTemplate1.Components,
                kvp =>
                {
                    Assert.Equal("barfoo", kvp.Key);
                    var valueAsDict = (Dictionary<object, object>)kvp.Value;
                    Assert.Equal("defgh", valueAsDict["anotherStringProperty"]);
                    Assert.Equal((byte)234, valueAsDict["anotherNumProperty"]);
                });

            var embeddedTemplate2 = template.EmbeddedTemplates.Skip(1).First();
            Assert.Contains(
                embeddedTemplate2.Components,
                kvp => kvp.Key == "foo" && kvp.Value.Equals("this is a test!"));

            // since dictionary merging doesn't guarantee order we have to do checks like this
            Assert.Contains(
                embeddedTemplate2.Components,
                kvp =>
                {
                    if (kvp.Key != "barfoo")
                    {
                        return false;
                    }

                    var valueAsDict = (Dictionary<object, object>)kvp.Value;
                    if (!valueAsDict.TryGetValue("anotherStringProperty", out var stringValue) || !stringValue.Equals("defgh"))
                    {
                        return false;
                    }

                    return valueAsDict.TryGetValue("anotherNumProperty", out var numValue) && numValue.Equals((byte)234);
                });

            Assert.Single(embeddedTemplate2.EmbeddedTemplates);

            var embeddedTemplate3 = embeddedTemplate2.EmbeddedTemplates.First();
            Assert.Collection(
                embeddedTemplate3.Components,
                kvp =>
                {
                    Assert.Equal("foobar", kvp.Key);

                    // embedded objects yaml deserializer loads as Dictionary<object, object>
                    var valueAsDict = (Dictionary<object, object>)kvp.Value;
                    Assert.Equal("abcdef", valueAsDict["stringProperty"]);
                    Assert.Equal((byte)123, valueAsDict["numProperty"]);
                });
        }

        [Theory]
        [InlineData("template-simple-case-sensitive-props-1.json")]
        [InlineData("template-simple-case-sensitive-props-2.yaml")]
        [InlineData("template-simple-case-insensitive-props-1.json")]
        [InlineData("template-simple-case-insensitive-props-2.yaml")]
        public void Can_load_simple_template(string templateFilename)
        {
            var template = _loader.LoadFrom(new FileInfo(Path.Combine("TemplatesForLoading", templateFilename)));
            Assert.NotNull(template); // sanity check

            Assert.Collection(
                template.Tags,
                item => Assert.Equal("tag1", item),
                item => Assert.Equal("tag2", item),
                item => Assert.Equal("tag3", item));

            Assert.Collection(
                template.Inherits,
                item => Assert.Equal("aa", item),
                item => Assert.Equal("bb", item));

            Assert.Collection(
                template.Components,
                kvp =>
                {
                    Assert.Equal("foobar", kvp.Key);

                    // embedded objects yaml deserializer loads as Dictionary<object, object>
                    var valueAsDict = (Dictionary<object, object>)kvp.Value;
                    Assert.Equal("abcdef", valueAsDict["stringProperty"]);
                    Assert.Equal((byte)123, valueAsDict["numProperty"]);
                },
                kvp =>
                {
                    Assert.Equal("barfoo", kvp.Key);
                    var valueAsDict = (Dictionary<object, object>)kvp.Value;
                    Assert.Equal("defgh", valueAsDict["anotherStringProperty"]);
                    Assert.Equal((byte)234, valueAsDict["anotherNumProperty"]);
                },
                kvp =>
                {
                    // yaml deserializer loads simple objects as key-value pairs
                    Assert.Equal("foo", kvp.Key);
                    Assert.Equal("this is a test!", kvp.Value);
                });
        }
    }
}
