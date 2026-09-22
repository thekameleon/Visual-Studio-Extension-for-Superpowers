using TheKameleon.Superpowers.Core.Contracts.Catalog;
using TheKameleon.Superpowers.Core.Contracts.Runs;

namespace TheKameleon.Superpowers.Skills.Composition;

public static class SkillCompositionCoordinator
{
    public static CompositionResolutionResult ComposePlan(
        LoadedCatalogRelease release,
        AdapterRunRecord run,
        IReadOnlyList<AcceptedPlanTask> acceptedTasks,
        IReadOnlyList<CapabilitySnapshot> capabilities)
    {
        ArgumentNullException.ThrowIfNull(release);
        ArgumentNullException.ThrowIfNull(run);
        ArgumentNullException.ThrowIfNull(acceptedTasks);
        ArgumentNullException.ThrowIfNull(capabilities);

        var diagnostics = new List<ParseDiagnostic>();
        var planMetadata = release.PlanMetadata;
        if (planMetadata is null)
        {
            diagnostics.Add(new ParseDiagnostic(ParseDiagnosticSeverity.Error, "SPRUN101", $"Release '{release.ReleaseTag}' does not include plan composition metadata."));
            return new CompositionResolutionResult(null, run, diagnostics);
        }

        var steps = new List<ComposedSkillStep>();
        var visitedPaths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var step in planMetadata.Composition)
        {
            if (!visitedPaths.Add(step.SkillPath))
            {
                diagnostics.Add(new ParseDiagnostic(ParseDiagnosticSeverity.Error, "SPRUN102", $"Plan composition contains a duplicate or cyclic skill reference '{step.SkillPath}'."));
                continue;
            }

            var skill = release.Skills.FirstOrDefault(candidate => string.Equals(candidate.RelativePath, step.SkillPath, StringComparison.OrdinalIgnoreCase));
            if (skill is null)
            {
                diagnostics.Add(new ParseDiagnostic(ParseDiagnosticSeverity.Error, "SPRUN103", $"Plan composition step '{step.SkillPath}' was not loaded from release '{release.ReleaseTag}'."));
                continue;
            }

            steps.Add(new ComposedSkillStep(step.Order, step.Purpose, skill));
        }

        var handoff = ResolveHandoff(capabilities);
        diagnostics.AddRange(ValidateAcceptedTasks(acceptedTasks));
        var composition = new SkillCompositionRecord(planMetadata.EntryPoint, steps, acceptedTasks, handoff);
        var resultingRun = new AdapterRunRecord(
            run.RunId,
            run.SelectedReleaseTag,
            run.AdapterSchemaVersion,
            run.ExecutionMode,
            handoff?.ResultingRunState ?? run.State,
            run.SchemaVersion,
            run.TrustBasis,
            run.Tasks,
            composition,
            capabilities);

        return new CompositionResolutionResult(composition, resultingRun, diagnostics);
    }

    private static HandoffDecision? ResolveHandoff(IReadOnlyList<CapabilitySnapshot> capabilities)
    {
        var capability = capabilities.FirstOrDefault(snapshot => string.Equals(snapshot.CapabilityId, "copilot-handoff", StringComparison.OrdinalIgnoreCase));
        if (capability is null)
        {
            return new HandoffDecision(
                "copilot-handoff",
                CapabilityAvailability.ManualOnly,
                HandoffFallbackKind.PreviewCopy,
                AdapterRunState.AwaitingHandoff,
                "Supported direct Copilot handoff is unavailable; preview/copy/manual handoff is required.");
        }

        return capability.Availability switch
        {
            CapabilityAvailability.Available => new HandoffDecision(capability.CapabilityId, capability.Availability, capability.FallbackKind, AdapterRunState.Running, capability.Detail),
            CapabilityAvailability.ManualOnly => new HandoffDecision(capability.CapabilityId, capability.Availability, capability.FallbackKind, AdapterRunState.AwaitingHandoff, capability.Detail),
            CapabilityAvailability.Unavailable => new HandoffDecision(capability.CapabilityId, capability.Availability, capability.FallbackKind, AdapterRunState.AwaitingHandoff, capability.Detail),
            CapabilityAvailability.Blocked => new HandoffDecision(capability.CapabilityId, capability.Availability, capability.FallbackKind, AdapterRunState.Paused, capability.Detail),
            _ => new HandoffDecision(capability.CapabilityId, capability.Availability, capability.FallbackKind, AdapterRunState.AwaitingHandoff, capability.Detail)
        };
    }

    private static IReadOnlyList<ParseDiagnostic> ValidateAcceptedTasks(IReadOnlyList<AcceptedPlanTask> acceptedTasks)
    {
        var diagnostics = new List<ParseDiagnostic>();
        var seenIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var task in acceptedTasks)
        {
            if (!seenIds.Add(task.TaskId))
            {
                diagnostics.Add(new ParseDiagnostic(ParseDiagnosticSeverity.Error, "SPRUN104", $"Accepted plan task identifier '{task.TaskId}' is duplicated."));
            }
        }

        return diagnostics;
    }
}
