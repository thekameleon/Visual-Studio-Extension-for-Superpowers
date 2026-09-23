using System.Text;
using System.Text.RegularExpressions;

namespace TheKameleon.Superpowers.Skills.Install;

/// <summary>Visual Studio Agent Skills front-matter rules.</summary>
public static class SkillValidator
{
    public const int MaxNameLength = 64;
    public const int MaxDescriptionLength = 1024;

    private static readonly Regex NamePattern = new("^[a-z0-9-]+$", RegexOptions.CultureInvariant);

    public static IReadOnlyList<string> Validate(SkillPackage package)
    {
        ArgumentNullException.ThrowIfNull(package);
        var problems = new List<string>();
        if (!package.Files.TryGetValue("SKILL.md", out var bytes))
        {
            problems.Add("SKILL.md is missing.");
            return problems;
        }

        var frontMatter = SkillFrontMatter.Read(Encoding.UTF8.GetString(bytes));
        if (frontMatter is null)
        {
            problems.Add("SKILL.md has no YAML front matter.");
            return problems;
        }

        if (string.IsNullOrEmpty(frontMatter.Name))
        {
            problems.Add("Front matter 'name' is missing.");
        }
        else
        {
            if (frontMatter.Name.Length > MaxNameLength || !NamePattern.IsMatch(frontMatter.Name))
            {
                problems.Add($"Name '{frontMatter.Name}' must be 1-64 lowercase letters, digits or hyphens.");
            }

            if (!string.Equals(frontMatter.Name, package.Name, StringComparison.Ordinal))
            {
                problems.Add($"Name '{frontMatter.Name}' does not match folder '{package.Name}'.");
            }
        }

        if (string.IsNullOrWhiteSpace(frontMatter.Description))
        {
            problems.Add("Front matter 'description' is missing.");
        }
        else if (frontMatter.Description.Length > MaxDescriptionLength)
        {
            problems.Add($"Description is {frontMatter.Description.Length} characters; the limit is 1024.");
        }

        return problems;
    }
}
