using System.Text;
using TheKameleon.Superpowers.Core.Contracts.Catalog;
using TheKameleon.Superpowers.Core.Contracts.Context;
using TheKameleon.Superpowers.Core.Contracts.Settings;

namespace TheKameleon.Superpowers.Skills.Workflow;

/// <summary>
/// Pure, deterministic assembly of a skill's instructions, the current execution mode and a
/// privacy-filtered context snapshot into a single editable prompt string. Performs no I/O and
/// invents no facts: any unavailable/redacted/excluded context is described honestly rather than
/// silently omitted or fabricated.
/// </summary>
public static class PromptComposer
{
    public static ComposedPrompt Compose(
        ParsedSkillDocument skill,
        ExecutionMode executionMode,
        ContextCaptureSnapshot context)
    {
        ArgumentNullException.ThrowIfNull(skill);
        ArgumentNullException.ThrowIfNull(context);

        var builder = new StringBuilder();

        builder.AppendLine($"# {skill.Name}");
        builder.AppendLine();
        if (!string.IsNullOrWhiteSpace(skill.Description))
        {
            builder.AppendLine(skill.Description);
            builder.AppendLine();
        }

        builder.AppendLine($"Execution mode: {executionMode}");
        builder.AppendLine();
        builder.AppendLine(skill.Body);
        builder.AppendLine();

        builder.AppendLine("## Context");
        AppendSolution(builder, context.Solution);
        AppendDocument(builder, "Active document", context.ActiveDocument);
        AppendSelection(builder, context.Selection);

        if (context.Diagnostics.Count > 0)
        {
            builder.AppendLine();
            builder.AppendLine("## Context diagnostics");
            foreach (var diagnostic in context.Diagnostics)
            {
                builder.AppendLine($"- [{diagnostic.Severity}] {diagnostic.Code}: {diagnostic.Message}");
            }
        }

        return new ComposedPrompt(builder.ToString().TrimEnd() + Environment.NewLine, skill.Name, executionMode);
    }

    private static void AppendSolution(StringBuilder builder, SolutionContextSnapshot solution)
    {
        builder.AppendLine($"- Solution: {DescribeState(solution.State, solution.Name)}");
    }

    private static void AppendDocument(StringBuilder builder, string label, DocumentContextSnapshot? document)
    {
        if (document is null)
        {
            builder.AppendLine($"- {label}: not available.");
            return;
        }

        builder.AppendLine($"- {label}: {DescribeState(document.State, document.DisplayName ?? document.FilePath)}");
        if (document.Content is { State: ContextValueState.Available } content && !string.IsNullOrEmpty(content.Value))
        {
            builder.AppendLine("```");
            builder.AppendLine(content.Value);
            builder.AppendLine("```");
        }
    }

    private static void AppendSelection(StringBuilder builder, SelectionContextSnapshot? selection)
    {
        if (selection is null)
        {
            builder.AppendLine("- Selection: not available.");
            return;
        }

        builder.AppendLine($"- Selection: {DescribeState(selection.State, null)}");
    }

    private static string DescribeState(ContextValueState state, string? name)
    {
        return state switch
        {
            ContextValueState.Available => string.IsNullOrWhiteSpace(name) ? "captured." : $"captured ({name}).",
            ContextValueState.Redacted => "redacted by privacy rules.",
            ContextValueState.Unavailable => "unavailable.",
            ContextValueState.Truncated => "truncated.",
            ContextValueState.Stale => "stale.",
            ContextValueState.Partial => "partially captured.",
            ContextValueState.Manual => "provided manually.",
            _ => "unknown state.",
        };
    }
}

public sealed record ComposedPrompt(string Text, string SkillName, ExecutionMode ExecutionMode);
