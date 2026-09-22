using System.Text.Json.Serialization;

namespace TheKameleon.Superpowers.Core.Contracts.Runs;

public sealed record AcceptedPlanTask
{
    [JsonConstructor]
    public AcceptedPlanTask(string taskId, string title, int order)
    {
        if (string.IsNullOrWhiteSpace(taskId))
        {
            throw new ArgumentException("Accepted task identifier is required.", nameof(taskId));
        }

        if (string.IsNullOrWhiteSpace(title))
        {
            throw new ArgumentException("Accepted task title is required.", nameof(title));
        }

        if (order <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(order), "Accepted task order must be greater than zero.");
        }

        TaskId = taskId;
        Title = title;
        Order = order;
    }

    public string TaskId { get; }

    public string Title { get; }

    public int Order { get; }
}
