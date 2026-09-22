namespace TheKameleon.Superpowers.Core.Contracts.Catalog;

public sealed record AdapterManifestAction(
    string ActionId,
    string SkillPath,
    bool RequiresApproval);
