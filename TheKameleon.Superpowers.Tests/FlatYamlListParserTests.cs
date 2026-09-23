using TheKameleon.Superpowers.Skills.Models;

namespace TheKameleon.Superpowers.Tests;

public sealed class FlatYamlListParserTests
{
    [Fact]
    public void ParsesQuotedScalarFields()
    {
        const string yaml = """
            # comment
            - name: 'GPT-5 mini'
              provider: 'OpenAI'
              release_status: 'GA'

            - name: 'Claude Opus 5.5'
              provider: 'Anthropic'
              release_status: 'GA'
            """;

        var records = FlatYamlListParser.Parse(yaml);

        Assert.Equal(2, records.Count);
        Assert.Equal("GPT-5 mini", records[0]["name"]);
        Assert.Equal("OpenAI", records[0]["provider"]);
        Assert.Equal("Claude Opus 5.5", records[1]["name"]);
    }

    [Fact]
    public void ParsesUnquotedAndBooleanFields()
    {
        const string yaml = """
            - name: Claude Haiku 4.5
              pro: true
              business: false
            """;

        var records = FlatYamlListParser.Parse(yaml);

        Assert.Single(records);
        Assert.Equal("Claude Haiku 4.5", records[0]["name"]);
        Assert.Equal("true", records[0]["pro"]);
        Assert.Equal("false", records[0]["business"]);
    }

    [Fact]
    public void ThrowsFormatExceptionOnUnsupportedShape()
    {
        const string yaml = """
            models:
              - name: nested under a key, not a top-level list
            """;

        Assert.Throws<FormatException>(() => FlatYamlListParser.Parse(yaml));
    }
}
