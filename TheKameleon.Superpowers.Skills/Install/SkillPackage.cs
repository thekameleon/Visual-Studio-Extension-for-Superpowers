namespace TheKameleon.Superpowers.Skills.Install;

public sealed record SkillPackage(string Name, IReadOnlyDictionary<string, byte[]> Files);
