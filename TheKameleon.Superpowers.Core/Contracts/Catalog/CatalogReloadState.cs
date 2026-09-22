namespace TheKameleon.Superpowers.Core.Contracts.Catalog;

public sealed record CatalogReloadState(
    string? SelectedSkillId,
    IReadOnlyList<ActiveRunPin> ActiveRunPins);
