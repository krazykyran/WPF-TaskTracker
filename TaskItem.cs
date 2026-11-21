namespace TaskTracker;

public sealed class TaskItem
{
    public int Id { get; init; }

    public string Title { get; init; } = string.Empty;

    public string Notes { get; init; } = string.Empty;

    public int Priority { get; init; } = 1;

    public string PriorityLabel => Priority switch
    {
        0 => "Low",
        2 => "High",
        _ => "Normal"
    };

    public DateTime? DueDate { get; init; }

    public bool IsComplete { get; init; }

    public DateTime CreatedAt { get; init; }
}
