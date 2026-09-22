using TheKameleon.Superpowers.Skills.Parsing;

namespace TheKameleon.Superpowers.Tests;

public sealed class AdapterManifestParserTests
{
    [Fact]
    public void ParsesValidManifest()
    {
        const string json = """
        {
          "SchemaVersion": 1,
          "Actions": [
            {
              "ActionId": "Plan",
              "SkillPath": "brainstorming/SKILL.md",
              "RequiresApproval": true
            }
          ]
        }
        """;

        var result = AdapterManifestParser.Parse(json);

        Assert.False(result.HasErrors);
        Assert.Single(result.Actions);
        Assert.Equal("Plan", result.Actions[0].ActionId);
        Assert.Equal("brainstorming/SKILL.md", result.Actions[0].SkillPath);
        Assert.True(result.Actions[0].RequiresApproval);
    }

    [Fact]
    public void RejectsInvalidJson()
    {
        var result = AdapterManifestParser.Parse("{");

        Assert.True(result.HasErrors);
        Assert.Contains(result.Diagnostics, diagnostic => diagnostic.Code == "SPCAT102");
    }

    [Fact]
    public void RejectsUnsupportedSchemaVersion()
    {
        const string json = """
        {
          "SchemaVersion": 2,
          "Actions": []
        }
        """;

        var result = AdapterManifestParser.Parse(json);

        Assert.True(result.HasErrors);
        Assert.Contains(result.Diagnostics, diagnostic => diagnostic.Code == "SPCAT104");
    }

    [Fact]
    public void RejectsUnsafeSkillReference()
    {
        const string json = """
        {
          "SchemaVersion": 1,
          "Actions": [
            {
              "ActionId": "Plan",
              "SkillPath": "../outside.md",
              "RequiresApproval": false
            }
          ]
        }
        """;

        var result = AdapterManifestParser.Parse(json);

        Assert.True(result.HasErrors);
        Assert.Contains(result.Diagnostics, diagnostic => diagnostic.Code == "SPCAT106");
    }

    [Fact]
    public void RejectsOversizedManifest()
    {
        var content = new string('a', AdapterManifestParser.MaxManifestLength + 1);

        var result = AdapterManifestParser.Parse(content);

        Assert.True(result.HasErrors);
        Assert.Contains(result.Diagnostics, diagnostic => diagnostic.Code == "SPCAT101");
    }
}
