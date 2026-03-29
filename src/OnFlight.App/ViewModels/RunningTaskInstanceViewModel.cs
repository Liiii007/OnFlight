using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using OnFlight.Contracts.Enums;
using OnFlight.Contracts.Models;
using OnFlight.Core.Services;
using System.Collections.Concurrent;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Text.Json;

namespace OnFlight.App.ViewModels;

public partial class RunningTaskInstanceViewModel : ObservableObject
{
    private readonly IRunningTaskService _service;
    private readonly TaskEventBus? _eventBus;
    private readonly ILogger<RunningTaskInstanceViewModel> _logger;
    public const int PageSize = 7;

    public event Action<RunningTaskInstanceViewModel>? AllDoneReached;

    public Guid InstanceId { get; }
    public Guid SourceListId { get; }
    public DateTime CreatedAt { get; }

    [ObservableProperty] private string _listName = string.Empty;
    [ObservableProperty] private int _completedCount;
    [ObservableProperty] private int _totalCount;
    [ObservableProperty] private string _progressText = "0/0";
    [ObservableProperty] private double _progress;
    [ObservableProperty] private int _flatPageStart;
    [ObservableProperty] private bool _canFlatPagePrev;
    [ObservableProperty] private bool _canFlatPageNext;
    [ObservableProperty] private bool _isAllDone;
    [ObservableProperty] private bool _allowOutOfOrder;
    [ObservableProperty] private bool _isPendingArchive;
    [ObservableProperty] private int _archiveCountdown;

    private CancellationTokenSource? _archiveCts;
    private readonly ConcurrentDictionary<Guid, Process> _runningProcesses = new();
    private const int MaxAutoAdvanceDepth = 50;

    /// <summary>
    /// Raised when a console command exits with a non-zero code.
    /// Parameters: item title, exit code, stderr output.
    /// </summary>
    public event Func<string, int, string?, Task>? ConsoleCommandFailed;

    /// <summary>Tree roots — the primary structural representation.</summary>
    public List<TodoItemViewModel> TopLevelItems { get; } = new();

    /// <summary>All items DFS-flattened from TopLevelItems — used by stats, persist, transient flags, etc.</summary>
    public ObservableCollection<TodoItemViewModel> Items { get; } = new();

    /// <summary>Only Task-type items with fork/join context baked in — used by floating window.</summary>
    public ObservableCollection<TodoItemViewModel> FlatTaskItems { get; } = new();

    /// <summary>Current page of FlatTaskItems.</summary>
    public ObservableCollection<TodoItemViewModel> FlatPageItems { get; } = new();

    private RunningTaskInstanceViewModel(Guid instanceId, Guid sourceListId, string listName,
        DateTime createdAt, bool allowOutOfOrder, IRunningTaskService service, TaskEventBus? eventBus,
        ILogger<RunningTaskInstanceViewModel>? logger = null)
    {
        InstanceId = instanceId;
        SourceListId = sourceListId;
        _listName = listName;
        CreatedAt = createdAt;
        _allowOutOfOrder = allowOutOfOrder;
        _service = service;
        _eventBus = eventBus;
        _logger = logger ?? NullLogger<RunningTaskInstanceViewModel>.Instance;
    }

    public static RunningTaskInstanceViewModel FromDto(RunningInstanceDto dto, IRunningTaskService service,
        TaskEventBus? eventBus = null, ILogger<RunningTaskInstanceViewModel>? logger = null)
    {
        var vm = new RunningTaskInstanceViewModel(dto.Id, dto.SourceListId, dto.ListName, dto.CreatedAt, dto.AllowOutOfOrder, service, eventBus, logger);

        foreach (var item in dto.Items.OrderBy(i => i.SortOrder))
            vm.TopLevelItems.Add(BuildVmTree(item, depth: 0, isForkChild: false, parentForkTitle: null,
                parentForkItemId: null, isSubTaskChild: false, parentTaskTitle: null,
                forkTargetListName: null));

        DfsFlatten(vm.TopLevelItems, vm.Items);

        vm.UpdateForkJoinStates();
        vm.UpdateReadyStates();
        vm.RebuildFlatTaskItems();
        vm.UpdateStats();
        vm.NavigateToSmartStart();
        return vm;
    }

    private static TodoItemViewModel BuildVmTree(RunningInstanceItemDto dto, int depth,
        bool isForkChild, string? parentForkTitle, Guid? parentForkItemId,
        bool isSubTaskChild, string? parentTaskTitle, string? forkTargetListName)
    {
        var vm = new TodoItemViewModel
        {
            Id = dto.Id,
            SourceItemId = dto.SourceItemId,
            Title = dto.Title,
            Description = dto.Description,
            Status = dto.Status,
            SortOrder = dto.SortOrder,
            NodeType = dto.NodeType,
            Depth = depth,
            IsForkChild = isForkChild,
            ParentForkTitle = parentForkTitle,
            ParentForkItemId = parentForkItemId,
            IsSubTaskChild = isSubTaskChild,
            ParentTaskTitle = parentTaskTitle,
            FlowConfigJson = dto.FlowConfigJson,
            ForkTargetListName = forkTargetListName
        };

        if (dto.NodeType == FlowNodeType.Fork)
        {
            foreach (var child in dto.Children.OrderBy(c => c.SortOrder))
                vm.Children.Add(BuildVmTree(child, depth + 1,
                    isForkChild: true, parentForkTitle: dto.Title, parentForkItemId: dto.Id,
                    isSubTaskChild: false, parentTaskTitle: null,
                    forkTargetListName: dto.ForkTargetListName));
        }
        else if (dto.Children.Count > 0)
        {
            foreach (var child in dto.Children.OrderBy(c => c.SortOrder))
                vm.Children.Add(BuildVmTree(child, depth + 1,
                    isForkChild: isForkChild, parentForkTitle: parentForkTitle, parentForkItemId: parentForkItemId,
                    isSubTaskChild: true, parentTaskTitle: dto.Title,
                    forkTargetListName: forkTargetListName));
        }

        return vm;
    }

    private static void DfsFlatten(IEnumerable<TodoItemViewModel> nodes, ObservableCollection<TodoItemViewModel> target)
    {
        foreach (var node in nodes)
        {
            target.Add(node);
            if (node.Children.Count > 0)
                DfsFlatten(node.Children, target);
        }
    }

    private void RebuildItems()
    {
        Items.Clear();
        DfsFlatten(TopLevelItems, Items);
    }

    private static readonly string[] ForkColors =
    [
        "#1E88E5", "#E53935", "#43A047", "#FB8C00",
        "#8E24AA", "#00ACC1", "#D81B60", "#6D4C41",
        "#3949AB", "#00897B", "#F4511E", "#7CB342"
    ];

    private readonly Dictionary<string, string> _forkColorMap = new();

    private string GetForkColor(string forkTitle)
    {
        if (_forkColorMap.TryGetValue(forkTitle, out var color))
            return color;
        color = ForkColors[_forkColorMap.Count % ForkColors.Length];
        _forkColorMap[forkTitle] = color;
        return color;
    }

    private void RebuildFlatTaskItems()
    {
        FlatTaskItems.Clear();
        RebuildFlatTaskItemsDfs(TopLevelItems);
    }

    private void RebuildFlatTaskItemsDfs(IEnumerable<TodoItemViewModel> nodes)
    {
        foreach (var item in nodes)
        {
            if (item.NodeType == FlowNodeType.Task || item.NodeType == FlowNodeType.ConsoleExecute)
            {
                string? tag = null;
                string? forkName = null;
                if (item.IsForkChild)
                {
                    forkName = item.ParentForkTitle;
                    tag = string.IsNullOrEmpty(item.ForkTargetListName)
                        ? $"Fork: {forkName}"
                        : $"Fork: {forkName} ({item.ForkTargetListName})";
                }
                else if (item.IsSubTaskChild)
                {
                    tag = $"Sub: {item.ParentTaskTitle}";
                }

                item.ForkJoinTag = tag;
                item.ForkColorHex = forkName != null ? GetForkColor(forkName) : "#FF9800";
                FlatTaskItems.Add(item);
            }

            if (item.Children.Count > 0)
                RebuildFlatTaskItemsDfs(item.Children);
        }
    }

    private void SyncFlatStatus()
    {
        // FlatTaskItems holds direct references to Items' VMs,
        // so property changes propagate automatically via ObservableProperty.
    }

    private void NavigateToSmartStart()
    {
        int lastConsecutiveDone = -1;
        for (int i = 0; i < FlatTaskItems.Count; i++)
        {
            if (FlatTaskItems[i].Status == TodoStatus.Done || FlatTaskItems[i].Status == TodoStatus.Skipped)
                lastConsecutiveDone = i;
            else
                break;
        }

        int smartStart = Math.Max(0, lastConsecutiveDone);
        smartStart = Math.Min(smartStart, Math.Max(0, FlatTaskItems.Count - PageSize));
        FlatPageStart = smartStart;
        RefreshFlatPage();
    }

    [RelayCommand]
    private async Task ToggleDoneAsync(TodoItemViewModel? item)
    {
        if (item == null) return;
        if (!Items.Contains(item)) return;
        if (!item.IsCheckableNode) return;
        if (item.Status != TodoStatus.Ready && item.Status != TodoStatus.Done
            && item.Status != TodoStatus.Running) return;
        if (!item.IsSubTaskChild && HasSubTaskChildren(item)) return;

        ClearTransientFlags();

        if (item.IsConsoleExecuteNode)
        {
            switch (item.Status)
            {
                case TodoStatus.Ready:
                    await ExecuteConsoleCommandAsync(item);
                    return;
                case TodoStatus.Running:
                    ForceKillProcess(item);
                    await MarkDoneAndAdvanceAsync(item);
                    return;
                case TodoStatus.Done:
                    item.Status = TodoStatus.Pending;
                    item.IsJustChanged = true;
                    await RefreshAfterStatusChangeAsync(item, TaskEventKind.TaskUnchecked);
                    return;
            }
            return;
        }

        var newStatus = item.Status == TodoStatus.Done ? TodoStatus.Pending : TodoStatus.Done;
        item.Status = newStatus;
        item.IsJustChanged = true;

        await RefreshAfterStatusChangeAsync(item,
            newStatus == TodoStatus.Done ? TaskEventKind.TaskChecked : TaskEventKind.TaskUnchecked);

        if (newStatus == TodoStatus.Done)
            await TryAutoAdvanceAsync(item, 0);
    }

    private async Task RefreshAfterStatusChangeAsync(TodoItemViewModel item, TaskEventKind kind)
    {
        var prevStatuses = Items.ToDictionary(i => i, i => i.Status);

        UpdateForkJoinStates();
        UpdateReadyStates();

        foreach (var it in Items)
        {
            if (prevStatuses.TryGetValue(it, out var prev) && prev != TodoStatus.Ready && it.Status == TodoStatus.Ready)
                it.IsJustUnlocked = true;
        }

        SyncFlatStatus();
        UpdateStats();
        RefreshFlatPage();
        await PersistAsync();

        if (_eventBus != null)
            await _eventBus.PublishAsync(new TaskEvent(kind, SourceListId, item.Id, item.Title, InstanceId));
    }

    private async Task ExecuteConsoleCommandAsync(TodoItemViewModel item)
    {
        var (command, workingDir) = ParseConsoleConfig(item.FlowConfigJson);
        if (string.IsNullOrWhiteSpace(command))
        {
            await MarkDoneAndAdvanceAsync(item);
            return;
        }

        item.Status = TodoStatus.Running;
        item.IsJustChanged = true;
        await RefreshAfterStatusChangeAsync(item, TaskEventKind.TaskChecked);

        _ = Task.Run(async () =>
        {
            int exitCode = -1;
            string? stderr = null;
            try
            {
                var psi = new ProcessStartInfo
                {
                    FileName = "powershell",
                    ArgumentList = { "-NoProfile", "-Command", command },
                    UseShellExecute = false,
                    CreateNoWindow = true,
                    RedirectStandardError = true,
                    WorkingDirectory = string.IsNullOrEmpty(workingDir) ? "" : workingDir,
                };

                var process = new Process { StartInfo = psi };
                _runningProcesses[item.Id] = process;

                process.Start();
                stderr = await process.StandardError.ReadToEndAsync();
                await process.WaitForExitAsync();
                exitCode = process.ExitCode;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Console command failed for item {ItemId}: {Command}", item.Id, command);
                stderr = ex.Message;
            }
            finally
            {
                _runningProcesses.TryRemove(item.Id, out _);
            }

            await Avalonia.Threading.Dispatcher.UIThread.InvokeAsync(async () =>
            {
                ClearTransientFlags();
                await MarkDoneAndAdvanceAsync(item);

                if (exitCode != 0)
                {
                    if (ConsoleCommandFailed != null)
                        await ConsoleCommandFailed.Invoke(item.Title, exitCode, stderr);
                }
                else
                {
                    await TryAutoAdvanceAsync(item, 0);
                }
            });
        });
    }

    private static (string? command, string? workingDir) ParseConsoleConfig(string? json)
    {
        if (string.IsNullOrEmpty(json)) return (null, null);
        try
        {
            var config = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(json);
            if (config == null) return (null, null);
            string? cmd = config.TryGetValue("command", out var c) ? c.GetString() : null;
            string? dir = config.TryGetValue("workingDirectory", out var d) ? d.GetString() : null;
            return (cmd, dir);
        }
        catch { return (null, null); }
    }

    private async Task MarkDoneAndAdvanceAsync(TodoItemViewModel item)
    {
        item.Status = TodoStatus.Done;
        item.IsJustChanged = true;
        await RefreshAfterStatusChangeAsync(item, TaskEventKind.TaskChecked);
    }

    private async Task TryAutoAdvanceAsync(TodoItemViewModel completedItem, int depth)
    {
        if (depth >= MaxAutoAdvanceDepth) return;

        bool hasAutoAdvance = ParseAutoAdvanceFlag(completedItem.FlowConfigJson, "autoAdvanceSuccessor");

        var nextItem = FindNextSequentialCheckableNode(completedItem);
        if (nextItem == null) return;

        bool canAutoStart = ParseAutoAdvanceFlag(nextItem.FlowConfigJson, "autoStartWhenPredecessorDone");
        if (!hasAutoAdvance && !canAutoStart) return;

        ClearTransientFlags();

        if (nextItem.IsConsoleExecuteNode)
        {
            await ExecuteConsoleCommandAsync(nextItem);
        }
        else
        {
            nextItem.Status = TodoStatus.Done;
            nextItem.IsJustChanged = true;
            await RefreshAfterStatusChangeAsync(nextItem, TaskEventKind.TaskChecked);
            await TryAutoAdvanceAsync(nextItem, depth + 1);
        }
    }

    private static bool ParseAutoAdvanceFlag(string? json, string key)
    {
        if (string.IsNullOrEmpty(json)) return false;
        try
        {
            var config = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(json);
            return config != null && config.TryGetValue(key, out var val) && val.GetBoolean();
        }
        catch { return false; }
    }

    private TodoItemViewModel? FindNextSequentialCheckableNode(TodoItemViewModel current)
    {
        var siblings = FindSiblingList(current);
        if (siblings == null) return null;

        bool found = false;
        foreach (var sibling in siblings)
        {
            if (sibling == current) { found = true; continue; }
            if (!found) continue;
            if (!sibling.IsCheckableNode) continue;
            if (sibling.Status == TodoStatus.Done || sibling.Status == TodoStatus.Skipped) continue;
            return sibling;
        }
        return null;
    }

    private IList<TodoItemViewModel>? FindSiblingList(TodoItemViewModel target)
    {
        if (TopLevelItems.Contains(target)) return TopLevelItems;
        return FindSiblingListDfs(TopLevelItems, target);
    }

    private static IList<TodoItemViewModel>? FindSiblingListDfs(IList<TodoItemViewModel> nodes, TodoItemViewModel target)
    {
        foreach (var node in nodes)
        {
            if (node.Children.Contains(target)) return node.Children;
            var found = FindSiblingListDfs(node.Children, target);
            if (found != null) return found;
        }
        return null;
    }

    private void ForceKillProcess(TodoItemViewModel item)
    {
        if (_runningProcesses.TryRemove(item.Id, out var process))
        {
            try { process.Kill(entireProcessTree: true); }
            catch (Exception ex) { _logger.LogWarning(ex, "Failed to kill process for item {ItemId}", item.Id); }
        }
    }

    private void ClearTransientFlags()
    {
        foreach (var it in Items)
        {
            it.IsJustChanged = false;
            it.IsJustUnlocked = false;
        }
    }

    private static bool HasSubTaskChildren(TodoItemViewModel item) => item.Children.Count > 0;

    // ─── Tree-recursive fork/join state computation ───

    private void UpdateForkJoinStates()
    {
        UpdateForkJoinStatesForList(TopLevelItems);
    }

    private void UpdateForkJoinStatesForList(IList<TodoItemViewModel> siblings)
    {
        int i = 0;
        while (i < siblings.Count)
        {
            var item = siblings[i];

            if (item.NodeType == FlowNodeType.Fork)
            {
                var (forks, joinIndex) = CollectForkGroup(siblings, i);

                bool allForksDone = true;
                foreach (var fork in forks)
                {
                    UpdateForkJoinStatesForList(fork.Children);

                    bool childrenDone = fork.Children.Count > 0
                        && fork.Children.All(c => c.Status == TodoStatus.Done || c.Status == TodoStatus.Skipped);
                    fork.Status = childrenDone ? TodoStatus.Done : TodoStatus.Pending;
                    if (!childrenDone) allForksDone = false;
                }

                if (joinIndex >= 0)
                {
                    siblings[joinIndex].Status = allForksDone ? TodoStatus.Done : TodoStatus.Pending;
                    i = joinIndex + 1;
                }
                else
                {
                    return;
                }
            }
            else if (item.NodeType == FlowNodeType.Join)
            {
                i++;
            }
            else if (item.IsCheckableNode && item.Children.Count > 0)
            {
                UpdateForkJoinStatesForList(item.Children);
                bool allChildrenDone = item.Children.All(c => c.Status == TodoStatus.Done || c.Status == TodoStatus.Skipped);
                if (allChildrenDone)
                    item.Status = TodoStatus.Done;
                else if (item.Status == TodoStatus.Done)
                    item.Status = TodoStatus.Pending;
                i++;
            }
            else
            {
                i++;
            }
        }
    }

    /// <summary>
    /// Starting at index <paramref name="startIndex"/> in siblings, collect consecutive
    /// Fork nodes and find the matching Join node.
    /// </summary>
    private static (List<TodoItemViewModel> forks, int joinIndex) CollectForkGroup(
        IList<TodoItemViewModel> siblings, int startIndex)
    {
        var forks = new List<TodoItemViewModel>();
        int i = startIndex;
        while (i < siblings.Count && siblings[i].NodeType == FlowNodeType.Fork)
        {
            forks.Add(siblings[i]);
            i++;
        }
        int joinIndex = -1;
        if (i < siblings.Count && siblings[i].NodeType == FlowNodeType.Join)
            joinIndex = i;
        return (forks, joinIndex);
    }

    // ─── Tree-recursive ready state computation ───

    private void UpdateReadyStates()
    {
        foreach (var item in Items)
        {
            if (item.Status == TodoStatus.Ready)
                item.Status = TodoStatus.Pending;
        }
        MarkReadyForList(TopLevelItems);
    }

    private void MarkReadyForList(IList<TodoItemViewModel> siblings)
    {
        bool sequentialFilled = false;

        int i = 0;
        while (i < siblings.Count)
        {
            var item = siblings[i];

            if (item.NodeType == FlowNodeType.Fork)
            {
                var (forks, joinIndex) = CollectForkGroup(siblings, i);

                if (!sequentialFilled || AllowOutOfOrder)
                {
                    foreach (var fork in forks)
                        MarkReadyForList(fork.Children);
                }

                if (joinIndex >= 0)
                {
                    bool joinDone = siblings[joinIndex].Status == TodoStatus.Done
                                    || siblings[joinIndex].Status == TodoStatus.Skipped;
                    if (joinDone)
                    {
                        i = joinIndex + 1;
                        sequentialFilled = false;
                        continue;
                    }
                }
                return;
            }

            if (item.NodeType == FlowNodeType.Join)
            {
                i++;
                continue;
            }

            if (!item.IsCheckableNode)
            {
                i++;
                continue;
            }

            if (item.Children.Count > 0)
            {
                MarkReadyForList(item.Children);
                if (!item.Children.All(c => c.Status == TodoStatus.Done || c.Status == TodoStatus.Skipped))
                    sequentialFilled = true;
                i++;
                continue;
            }

            if (item.Status == TodoStatus.Done || item.Status == TodoStatus.Skipped
                || item.Status == TodoStatus.Running)
            {
                i++;
                continue;
            }

            if (AllowOutOfOrder || !sequentialFilled)
            {
                item.Status = TodoStatus.Ready;
                sequentialFilled = true;
            }
            i++;
        }
    }

    [RelayCommand]
    private void FlatPagePrev()
    {
        FlatPageStart = Math.Max(0, FlatPageStart - PageSize);
        RefreshFlatPage();
    }

    [RelayCommand]
    private void FlatPageNext()
    {
        FlatPageStart = Math.Min(FlatTaskItems.Count - 1, FlatPageStart + PageSize);
        RefreshFlatPage();
    }

    private void RefreshFlatPage()
    {
        FlatPageItems.Clear();
        foreach (var item in FlatTaskItems.Skip(FlatPageStart).Take(PageSize))
            FlatPageItems.Add(item);

        CanFlatPagePrev = FlatPageStart > 0;
        CanFlatPageNext = FlatPageStart + PageSize < FlatTaskItems.Count;
    }

    public RunningInstanceDto ToDto() => new()
    {
        Id = InstanceId,
        SourceListId = SourceListId,
        ListName = ListName,
        State = IsAllDone ? RunningState.AllDone : RunningState.Running,
        CreatedAt = CreatedAt,
        AllowOutOfOrder = AllowOutOfOrder,
        Items = TopLevelItems.Select(ItemToDto).ToList()
    };

    private static RunningInstanceItemDto ItemToDto(TodoItemViewModel vm) => new()
    {
        Id = vm.Id,
        SourceItemId = vm.SourceItemId,
        Title = vm.Title,
        Description = vm.Description,
        Status = vm.Status,
        SortOrder = vm.SortOrder,
        NodeType = vm.NodeType,
        FlowConfigJson = vm.FlowConfigJson,
        Children = vm.Children.Select(ItemToDto).ToList()
    };

    private async Task PersistAsync()
    {
        try { await _service.SaveInstanceAsync(InstanceId, ToDto()); }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to persist running instance {InstanceId}", InstanceId);
        }
    }

    private void UpdateStats()
    {
        var taskItems = Items.Where(i => i.IsCheckableNode).ToList();
        TotalCount = taskItems.Count;
        CompletedCount = taskItems.Count(i => i.Status == TodoStatus.Done || i.Status == TodoStatus.Skipped);
        ProgressText = $"{CompletedCount}/{TotalCount}";
        Progress = TotalCount > 0 ? (double)CompletedCount / TotalCount : 0;
        bool wasAllDone = IsAllDone;
        IsAllDone = CompletedCount == TotalCount && TotalCount > 0;

        if (IsAllDone && !wasAllDone)
            StartArchiveCountdown();
        else if (!IsAllDone && wasAllDone)
            CancelArchiveCountdown();
    }

    private void StartArchiveCountdown()
    {
        CancelArchiveCountdown();
        IsPendingArchive = true;
        ArchiveCountdown = 5;
        _archiveCts = new CancellationTokenSource();
        var token = _archiveCts.Token;

        _ = Task.Run(async () =>
        {
            try
            {
                for (int i = 5; i > 0; i--)
                {
                    token.ThrowIfCancellationRequested();
                    Avalonia.Threading.Dispatcher.UIThread.Invoke(() => ArchiveCountdown = i);
                    await Task.Delay(1000, token);
                }
                await Avalonia.Threading.Dispatcher.UIThread.InvokeAsync(async () =>
                {
                    IsPendingArchive = false;
                    if (_eventBus != null)
                        await _eventBus.PublishAsync(new TaskEvent(TaskEventKind.RunArchived, SourceListId, InstanceId: InstanceId));
                    AllDoneReached?.Invoke(this);
                });
            }
            catch (OperationCanceledException) { }
        }, token);
    }

    private void CancelArchiveCountdown()
    {
        _archiveCts?.Cancel();
        _archiveCts?.Dispose();
        _archiveCts = null;
        IsPendingArchive = false;
        ArchiveCountdown = 0;
    }

    [RelayCommand]
    private void UndoArchive()
    {
        if (!IsPendingArchive) return;
        CancelArchiveCountdown();

        var lastDone = Items.LastOrDefault(i => i.IsCheckableNode
            && (i.Status == TodoStatus.Done || i.Status == TodoStatus.Skipped));
        if (lastDone != null)
        {
            lastDone.Status = TodoStatus.Pending;
            lastDone.IsJustChanged = true;

            UpdateForkJoinStates();
            UpdateReadyStates();
            SyncFlatStatus();
            UpdateStats();
            RefreshFlatPage();
            _ = PersistAsync();
        }
    }

    partial void OnAllowOutOfOrderChanged(bool value)
    {
        UpdateReadyStates();
        SyncFlatStatus();
        RefreshFlatPage();
        _ = PersistAsync();
    }

    public override string ToString() => $"{ListName} ({CreatedAt:HH:mm})";
}
