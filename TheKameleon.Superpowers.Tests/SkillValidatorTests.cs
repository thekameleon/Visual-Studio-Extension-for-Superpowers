using System.Text;
using TheKameleon.Superpowers.Skills.Install;

namespace TheKameleon.Superpowers.Tests;

public sealed class SkillValidatorTests
{
    [Fact]
    public void AcceptsUpstreamStyleSkill()
    {
        var package = TestSupport.SkillWithMarkdown(
            "brainstorming",
            "---\nname: brainstorming\ndescription: \"You MUST use this before any creative work - creating features.\"\n---\n\n# Brainstorming\n");

        Assert.Empty(SkillValidator.Validate(package));
    }

    [Fact]
    public void AcceptsFoldedMultilineDescriptionAndBom()
    {
        var markdown = "﻿---\r\nname: aspire\r\ndescription: >-\r\n  First line\r\n  second line.\r\nmetadata:\r\n  owner: team\r\n---\r\nBody\r\n";
        var package = new SkillPackage("aspire", new Dictionary<string, byte[]> { ["SKILL.md"] = Encoding.UTF8.GetBytes(markdown) });

        Assert.Empty(SkillValidator.Validate(package));
        Assert.Equal("First line second line.", SkillFrontMatter.Read(markdown)!.Description);
    }

    [Theory]
    [InlineData("---\nname: Brainstorming\ndescription: x\n---\n", "brainstorming", "lowercase")]
    [InlineData("---\nname: other\ndescription: x\n---\n", "brainstorming", "does not match folder")]
    [InlineData("---\ndescription: x\n---\n", "brainstorming", "'name' is missing")]
    [InlineData("---\nname: brainstorming\n---\n", "brainstorming", "'description' is missing")]
    [InlineData("# no front matter\n", "brainstorming", "no YAML front matter")]
    public void RejectsInvalidFrontMatter(string markdown, string folder, string expectedFragment)
    {
        var problems = SkillValidator.Validate(TestSupport.SkillWithMarkdown(folder, markdown));

        Assert.Contains(problems, problem => problem.Contains(expectedFragment, StringComparison.Ordinal));
    }

    [Fact]
    public void RejectsDescriptionLongerThan1024Characters()
    {
        var markdown = $"---\nname: long\ndescription: {new string('a', 1025)}\n---\n";

        Assert.Contains(SkillValidator.Validate(TestSupport.SkillWithMarkdown("long", markdown)), problem => problem.Contains("1024", StringComparison.Ordinal));
    }

    [Fact]
    public void RejectsMissingSkillFile()
    {
        var package = new SkillPackage("empty", new Dictionary<string, byte[]> { ["README.md"] = Array.Empty<byte>() });

        Assert.Contains(SkillValidator.Validate(package), problem => problem.Contains("SKILL.md is missing", StringComparison.Ordinal));
    }
}
