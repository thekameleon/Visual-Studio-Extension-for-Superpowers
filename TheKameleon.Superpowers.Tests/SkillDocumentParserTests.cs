using TheKameleon.Superpowers.Core.Contracts.Catalog;
using TheKameleon.Superpowers.Skills.Parsing;

namespace TheKameleon.Superpowers.Tests;

public sealed class SkillDocumentParserTests
{
    [Fact]
    public void ParsesValidSkillDocument()
    {
        const string content = """
        ---
        name: Plan
        description: Create a plan.
        ---
        Use [reference](references/guide.md).
        """;

        var result = SkillDocumentParser.Parse(content);

        Assert.False(result.HasErrors);
        Assert.Equal("Plan", result.Name);
        Assert.Equal("Create a plan.", result.Description);
        Assert.Single(result.References);
        Assert.Equal("references/guide.md", result.References[0].RelativePath);
    }

    [Fact]
    public void RejectsMissingFrontMatter()
    {
        var result = SkillDocumentParser.Parse("No front matter");

        Assert.True(result.HasErrors);
        Assert.Contains(result.Diagnostics, diagnostic => diagnostic.Code == "SPCAT002");
    }

    [Fact]
    public void AllowsSiblingSkillDirectoryReference()
    {
        // Upstream cross-skill references such as executing-plans -> ../requesting-code-review/code-reviewer.md
        // must remain supported. True path-escape containment is enforced later, at archive-relative
        // resolution, where the escaped path can be checked against the release root.
        const string content = """
        ---
        name: Plan
        description: Create a plan.
        ---
        Use [reference](../requesting-code-review/code-reviewer.md).
        """;

        var result = SkillDocumentParser.Parse(content);

        Assert.False(result.HasErrors);
        Assert.Single(result.References);
        Assert.Equal("../requesting-code-review/code-reviewer.md", result.References[0].RelativePath);
    }

    [Fact]
    public void RejectsUnsafeReference()
    {
        const string content = """
        ---
        name: Plan
        description: Create a plan.
        ---
        Use [reference](C:\outside.md).
        """;

        var result = SkillDocumentParser.Parse(content);

        Assert.True(result.HasErrors);
        Assert.Contains(result.Diagnostics, diagnostic => diagnostic.Code == "SPCAT007");
    }

    [Fact]
    public void RejectsOversizedDocument()
    {
        var content = new string('a', SkillDocumentParser.MaxDocumentLength + 1);

        var result = SkillDocumentParser.Parse(content);

        Assert.True(result.HasErrors);
        Assert.Contains(result.Diagnostics, diagnostic => diagnostic.Code == "SPCAT001");
    }
}
