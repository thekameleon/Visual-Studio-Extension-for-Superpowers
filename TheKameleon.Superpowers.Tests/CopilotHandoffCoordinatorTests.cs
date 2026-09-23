using TheKameleon.Superpowers.Core.Contracts.Runs;
using TheKameleon.Superpowers.Skills.Execution;

namespace TheKameleon.Superpowers.Tests;

public sealed class CopilotHandoffCoordinatorTests
{
    [Fact]
    public void FallsBackToPreviewCopyWhenPromptSubmissionIsUnavailable()
    {
        var decision = CopilotHandoffCoordinator.Evaluate(
            supportsPromptSubmission: false,
            supportsResponseRetrieval: false,
            supportsEditApplication: false,
            supportsSessionAutomation: false);

        Assert.Equal(CapabilityAvailability.Unavailable, decision.Availability);
        Assert.Equal(HandoffFallbackKind.PreviewCopy, decision.FallbackKind);
        Assert.Equal(AdapterRunState.AwaitingHandoff, decision.ResultingRunState);
    }

    [Fact]
    public void RequiresManualImportWhenOnlyPromptSubmissionIsSupported()
    {
        var decision = CopilotHandoffCoordinator.Evaluate(
            supportsPromptSubmission: true,
            supportsResponseRetrieval: false,
            supportsEditApplication: false,
            supportsSessionAutomation: false);

        Assert.Equal(CapabilityAvailability.ManualOnly, decision.Availability);
        Assert.Equal(HandoffFallbackKind.ManualImport, decision.FallbackKind);
    }

    [Fact]
    public void ReportsFullAutomationOnlyWhenAllCapabilitiesAreSupported()
    {
        var decision = CopilotHandoffCoordinator.Evaluate(
            supportsPromptSubmission: true,
            supportsResponseRetrieval: true,
            supportsEditApplication: true,
            supportsSessionAutomation: true);

        Assert.Equal(CapabilityAvailability.Available, decision.Availability);
        Assert.Equal(HandoffFallbackKind.None, decision.FallbackKind);
        Assert.Equal(AdapterRunState.Running, decision.ResultingRunState);
    }

    [Theory]
    [InlineData(false, true, true)]
    [InlineData(true, false, true)]
    [InlineData(true, true, false)]
    public void RequiresManualImportWhenAnySingleDownstreamCapabilityIsMissing(
        bool responseRetrieval, bool editApplication, bool sessionAutomation)
    {
        var decision = CopilotHandoffCoordinator.Evaluate(
            supportsPromptSubmission: true,
            supportsResponseRetrieval: responseRetrieval,
            supportsEditApplication: editApplication,
            supportsSessionAutomation: sessionAutomation);

        Assert.Equal(CapabilityAvailability.ManualOnly, decision.Availability);
    }
}
