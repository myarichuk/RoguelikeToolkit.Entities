using System.Reflection;
using RoguelikeToolkit.Entities.Repository;
// ReSharper disable TooManyDeclarations
#pragma warning disable CS8604
#pragma warning disable CS8600
#pragma warning disable CS1591

// ReSharper disable ExceptionNotDocumented
namespace RoguelikeToolkit.Entities.Tests
{
    public class EntityTemplateRepositoryTests
    {
        private readonly EntityTemplateRepository _repository = new();

        [Fact]
        public void Can_load_folder()
        {
            _repository.LoadTemplateFolder("FolderForTemplateRepository");
            Assert.Collection(
                _repository.TemplateNames.OrderBy(x => x),
                name => Assert.Equal("template-only-tags", name),
                name => Assert.Equal("template-only-tags.test.foobar", name),
                name => Assert.Equal("template-simple-case-sensitive-props-1", name),
                name => Assert.Equal("template-simple-case-sensitive-props-2", name),
                name => Assert.Equal("template-simple-case-sensitive-props-subfolder", name));
        }

        [Theory]
        [InlineData("template-only-tags.txt")]
        [InlineData("template-only-tags")]
        public void Should_fail_load_single_template_invalid_extension(string templateFilename)
        {
            string currentFolder = new FileInfo(Assembly.GetExecutingAssembly().Location).DirectoryName;
            var path = Path.Combine(currentFolder, "FolderForTemplateRepository", templateFilename);
            Assert.Throws<InvalidOperationException>(() => _repository.LoadTemplate(path));
        }

        [Fact]
        public void Can_query_by_tags()
        {
            _repository.LoadTemplateFolder("FolderForTemplateRepository");

            Assert.Collection(
                _repository.GetByTags("tag1"),
                template => template.Tags.Contains("xyz"));

            Assert.Collection(
                _repository.GetByTags("tag3"),
                template => template.Tags.Contains("xyz"),
                template => template.Tags.Contains("aaa"));

            Assert.Collection(
                _repository.GetByTags("tag2"),
                template => template.Tags.Contains("aaa"));
        }

        [Fact]
        public void Can_load_single_template()
        {
            string currentFolder = new FileInfo(Assembly.GetExecutingAssembly().Location).DirectoryName;
            var path = Path.Combine(currentFolder, "FolderForTemplateRepository", "template-only-tags.yaml");
            _repository.LoadTemplate(path);

            Assert.True(_repository.TryGetByName("template-only-tags", out _));
        }

        [Fact]
        public void LoadTemplate_backfills_name_and_lookup_preserves_it()
        {
            var repository = new EntityTemplateRepository();
            using var stream = new MemoryStream(
                System.Text.Encoding.UTF8.GetBytes("Components:\n foo: bar\n"));
            using var reader = new StreamReader(stream);
            repository.LoadTemplate("MyTemplate", reader);

            Assert.True(repository.TryGetByName("mytemplate", out var first));
            Assert.Equal("MyTemplate", first.Name);

            Assert.True(repository.TryGetByName("MYTEMPLATE", out var second));
            Assert.Same(first, second);
            Assert.Equal("MyTemplate", second.Name);
        }

        [Fact]
        public void Can_load_single_template_multiple_dots_in_filename()
        {
            string currentFolder = new FileInfo(Assembly.GetExecutingAssembly().Location).DirectoryName;
            var path = Path.Combine(currentFolder, "FolderForTemplateRepository", "template-only-tags.test.foobar.yaml");
            _repository.LoadTemplate(path);

            Assert.True(_repository.TryGetByName("template-only-tags.test.foobar", out _));
        }

        [Fact]
        public void Can_load_template_with_uppercase_extension()
        {
            _repository.LoadTemplateFolder("FolderForCaseInsensitiveExtension");

            Assert.True(_repository.TryGetByName("template-upper", out var template));
            Assert.Contains("tag1", template.Tags);
        }

        [Fact]
        public void Folder_load_collects_per_file_errors_and_leaves_repo_untouched()
        {
            var exception = Assert.Throws<AggregateException>(() =>
                _repository.LoadTemplateFolder("FolderWithInvalidTemplates"));

            // one inner exception per failed file (bad1 + bad2), good.yaml must not leak into the repo
            Assert.Equal(2, exception.InnerExceptions.Count);
            Assert.Empty(_repository.TemplateNames);
        }

        [Fact]
        public async Task Can_load_folder_async()
        {
            await _repository.LoadTemplateFolderAsync("FolderForTemplateRepository");

            Assert.True(_repository.TryGetByName("template-only-tags", out _));
        }

        [Fact]
        public async Task Folder_load_async_observes_cancellation()
        {
            await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
                _repository.LoadTemplateFolderAsync(
                    "FolderForTemplateRepository", new CancellationToken(canceled: true)));
        }

        [Fact]
        public void Can_query_by_tags_case_insensitively()
        {
            _repository.LoadTemplateFolder("FolderForTemplateRepository");

            Assert.NotEmpty(_repository.GetByTags("TAG1"));
            Assert.Empty(_repository.GetByTags("no-such-tag"));

            // an empty query matches everything (preserved historical behavior)
            Assert.Equal(
                _repository.TemplateNames.Count(),
                _repository.GetByTags().Count());
        }
    }
}
