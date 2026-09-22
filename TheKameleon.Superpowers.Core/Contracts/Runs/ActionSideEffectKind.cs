namespace TheKameleon.Superpowers.Core.Contracts.Runs;

public enum ActionSideEffectKind
{
    None = 0,
    PromptHandoff = 1,
    BuildExecution = 2,
    TestExecution = 3,
    EditApplication = 4,
    ManualEvidenceImport = 5
}
