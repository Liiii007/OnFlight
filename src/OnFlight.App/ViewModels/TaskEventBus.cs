using Microsoft.Extensions.Logging;
using OnFlight.Contracts.Enums;
using OnFlight.Core.Services;

namespace OnFlight.App.ViewModels;

public enum TaskEventKind
{
    TaskChecked,
    TaskUnchecked,
    RunArchived
}

public record TaskEvent(
    TaskEventKind Kind,
    Guid ListId,
    Guid? ItemId = null,
    string? ItemTitle = null,
    Guid? InstanceId = null);

/// <summary>
/// Singleton event bus for task state changes.
/// Centralizes logging and UI refresh across MainWindow and FloatingWindow.
/// </summary>
public class TaskEventBus
{
    private readonly ILogService _logService;
    private readonly ILogger<TaskEventBus> _logger;

    public event Func<TaskEvent, Task>? EventPublished;

    public TaskEventBus(ILogService logService, ILogger<TaskEventBus> logger)
    {
        _logService = logService;
        _logger = logger;
    }

    public async Task PublishAsync(TaskEvent evt)
    {
        _logger.LogInformation("Publishing event {Kind} for list {ListId}", evt.Kind, evt.ListId);

        var (op, detail) = evt.Kind switch
        {
            TaskEventKind.TaskChecked => (OperationType.Check, $"{evt.ItemTitle} -> Done"),
            TaskEventKind.TaskUnchecked => (OperationType.Uncheck, $"{evt.ItemTitle} -> Pending"),
            TaskEventKind.RunArchived => (OperationType.Check, "All tasks completed — run archived"),
            _ => (OperationType.Check, evt.Kind.ToString())
        };

        await _logService.LogAsync(evt.ListId, op, detail);

        if (EventPublished != null)
        {
            foreach (var handler in EventPublished.GetInvocationList().Cast<Func<TaskEvent, Task>>())
            {
                try { await handler(evt); }
                catch (Exception ex) { _logger.LogError(ex, "Event handler failed for {Kind}", evt.Kind); }
            }
        }
    }
}
