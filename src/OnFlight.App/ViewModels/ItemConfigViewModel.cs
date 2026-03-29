using CommunityToolkit.Mvvm.ComponentModel;
using Microsoft.Extensions.Logging;
using OnFlight.Contracts.Enums;
using OnFlight.Core.Models;
using OnFlight.Core.Services;
using System.Collections.ObjectModel;

namespace OnFlight.App.ViewModels;

public partial class ItemConfigViewModel : ObservableObject
{
    private readonly ITodoService _todoService;
    private readonly ILogger<ItemConfigViewModel> _logger;
    private TodoItem? _currentItem;
    private Guid _currentListId;
    private bool _isLoading;
    private CancellationTokenSource? _debounceCts;
    private static readonly TimeSpan DebounceDelay = TimeSpan.FromMilliseconds(500);
    private RootListItem? _lastValidForkTarget;

    [ObservableProperty] private string _title = string.Empty;
    [ObservableProperty] private string? _description;
    [ObservableProperty] private TodoStatus _status;
    [ObservableProperty] private FlowNodeType _nodeType;
    [ObservableProperty] private string? _condition;
    [ObservableProperty] private int _loopCount = 1;
    [ObservableProperty] private bool _isVisible;
    [ObservableProperty] private bool _isLoopNode;
    [ObservableProperty] private bool _isForkNode;
    [ObservableProperty] private bool _isBranchNode;
    [ObservableProperty] private bool _isJoinNode;
    [ObservableProperty] private bool _isConsoleExecuteNode;
    [ObservableProperty] private bool _isTaskOrConsoleNode;
    [ObservableProperty] private string? _command;
    [ObservableProperty] private string? _workingDirectory;
    [ObservableProperty] private bool _autoStartWhenPredecessorDone;
    [ObservableProperty] private bool _autoAdvanceSuccessor;

    [ObservableProperty] private RootListItem? _selectedForkTargetList;

    public ObservableCollection<RootListItem> AvailableLists { get; } = new();
    public ObservableCollection<ForkOptionItem> AvailableForkItems { get; } = new();

    /// <summary>
    /// Raised when a fork target selection would create a cycle.
    /// The View subscribes to this to show a warning dialog.
    /// Parameter: target list name that caused the cycle.
    /// </summary>
    public event Func<string, Task>? CycleDetected;

    public ItemConfigViewModel(ITodoService todoService, ILogger<ItemConfigViewModel> logger)
    {
        _todoService = todoService;
        _logger = logger;
    }

    public void LoadItem(TodoItem? item, Guid currentListId,
        ObservableCollection<RootListItem> rootLists,
        ObservableCollection<TodoItemViewModel> currentItems)
    {
        _isLoading = true;
        try
        {
            _currentItem = item;
            _currentListId = currentListId;

            if (item == null)
            {
                IsVisible = false;
                return;
            }

            IsVisible = true;
            Title = item.Title;
            Description = item.Description;
            Status = item.Status;
            NodeType = item.NodeType;
            UpdateNodeTypeFlags();

            AvailableLists.Clear();
            foreach (var l in rootLists)
                AvailableLists.Add(l);

            AvailableForkItems.Clear();
            foreach (var i in currentItems.Where(i => i.NodeType == FlowNodeType.Fork && i.Id != item.Id))
                AvailableForkItems.Add(new ForkOptionItem { Id = i.Id, Title = i.Title });

            ParseFlowConfig(item.FlowConfigJson);
        }
        finally
        {
            _isLoading = false;
        }
    }

    public void LoadItem(TodoItem? item)
    {
        LoadItem(item, Guid.Empty, new ObservableCollection<RootListItem>(), new ObservableCollection<TodoItemViewModel>());
    }

    private void ParseFlowConfig(string? json)
    {
        SelectedForkTargetList = null;
        _lastValidForkTarget = null;
        foreach (var f in AvailableForkItems) f.IsSelected = false;
        Condition = null;
        LoopCount = 1;
        Command = null;
        WorkingDirectory = null;
        AutoStartWhenPredecessorDone = false;
        AutoAdvanceSuccessor = false;

        if (string.IsNullOrEmpty(json)) return;
        try
        {
            var config = System.Text.Json.JsonSerializer.Deserialize<Dictionary<string, System.Text.Json.JsonElement>>(json);
            if (config == null) return;

            if (config.TryGetValue("condition", out var cond))
                Condition = cond.GetString();
            if (config.TryGetValue("loopCount", out var lc))
                LoopCount = lc.GetInt32();
            if (config.TryGetValue("targetListId", out var tlid))
            {
                var id = Guid.Parse(tlid.GetString()!);
                SelectedForkTargetList = AvailableLists.FirstOrDefault(l => l.Id == id);
                _lastValidForkTarget = SelectedForkTargetList;
            }
            if (config.TryGetValue("forkItemIds", out var fids))
            {
                var ids = new HashSet<Guid>();
                foreach (var el in fids.EnumerateArray())
                    if (Guid.TryParse(el.GetString(), out var fid))
                        ids.Add(fid);
                foreach (var f in AvailableForkItems)
                    f.IsSelected = ids.Contains(f.Id);
            }
            else if (config.TryGetValue("forkItemId", out var singleFid))
            {
                if (Guid.TryParse(singleFid.GetString(), out var fid))
                {
                    var match = AvailableForkItems.FirstOrDefault(f => f.Id == fid);
                    if (match != null) match.IsSelected = true;
                }
            }
            if (config.TryGetValue("command", out var cmd))
                Command = cmd.GetString();
            if (config.TryGetValue("workingDirectory", out var wd))
                WorkingDirectory = wd.GetString();
            if (config.TryGetValue("autoStartWhenPredecessorDone", out var asw) && asw.GetBoolean())
                AutoStartWhenPredecessorDone = true;
            if (config.TryGetValue("autoAdvanceSuccessor", out var aas) && aas.GetBoolean())
                AutoAdvanceSuccessor = true;
        }
        catch (Exception ex) { _logger.LogWarning(ex, "Failed to parse FlowConfig for item {ItemId}", _currentItem?.Id); }
    }

    private void UpdateNodeTypeFlags()
    {
        IsLoopNode = NodeType == FlowNodeType.Loop;
        IsForkNode = NodeType == FlowNodeType.Fork;
        IsBranchNode = NodeType == FlowNodeType.Branch;
        IsJoinNode = NodeType == FlowNodeType.Join;
        IsConsoleExecuteNode = NodeType == FlowNodeType.ConsoleExecute;
        IsTaskOrConsoleNode = NodeType == FlowNodeType.Task || NodeType == FlowNodeType.ConsoleExecute;
    }

    partial void OnNodeTypeChanged(FlowNodeType value)
    {
        UpdateNodeTypeFlags();
        ScheduleAutoSave();
    }

    partial void OnTitleChanged(string value) => ScheduleAutoSave();
    partial void OnDescriptionChanged(string? value) => ScheduleAutoSave();
    partial void OnStatusChanged(TodoStatus value) => ScheduleAutoSave();
    partial void OnConditionChanged(string? value) => ScheduleAutoSave();
    partial void OnLoopCountChanged(int value) => ScheduleAutoSave();
    partial void OnSelectedForkTargetListChanged(RootListItem? value) => ScheduleAutoSave();
    partial void OnCommandChanged(string? value) => ScheduleAutoSave();
    partial void OnWorkingDirectoryChanged(string? value) => ScheduleAutoSave();
    partial void OnAutoStartWhenPredecessorDoneChanged(bool value) => ScheduleAutoSave();
    partial void OnAutoAdvanceSuccessorChanged(bool value) => ScheduleAutoSave();

    private void ScheduleAutoSave()
    {
        if (_isLoading || _currentItem == null) return;

        _debounceCts?.Cancel();
        _debounceCts = new CancellationTokenSource();
        var token = _debounceCts.Token;

        _ = Task.Run(async () =>
        {
            try
            {
                await Task.Delay(DebounceDelay, token);
                if (!token.IsCancellationRequested)
                    await Avalonia.Threading.Dispatcher.UIThread.InvokeAsync(() => PersistAsync());
            }
            catch (TaskCanceledException) { }
        }, token);
    }

    private async Task PersistAsync()
    {
        if (_currentItem == null) return;
        try
        {
            if (IsForkNode && SelectedForkTargetList != null && _currentListId != Guid.Empty)
            {
                bool wouldCycle = await _todoService.WouldCreateCycleAsync(_currentListId, SelectedForkTargetList.Id);
                if (wouldCycle)
                {
                    var targetName = SelectedForkTargetList.Name;
                    _logger.LogWarning("Cycle detected: fork in list {ListId} targeting {TargetName} ({TargetId})",
                        _currentListId, targetName, SelectedForkTargetList.Id);

                    _isLoading = true;
                    try { SelectedForkTargetList = _lastValidForkTarget; }
                    finally { _isLoading = false; }

                    if (CycleDetected != null)
                        await CycleDetected.Invoke(targetName);
                    return;
                }
            }

            _currentItem.Title = Title;
            _currentItem.Description = Description;
            _currentItem.Status = Status;
            _currentItem.NodeType = NodeType;

            var config = new Dictionary<string, object>();
            if (!string.IsNullOrEmpty(Condition))
                config["condition"] = Condition;
            if (IsLoopNode)
                config["loopCount"] = LoopCount;
            if (IsForkNode && SelectedForkTargetList != null)
            {
                config["targetListId"] = SelectedForkTargetList.Id.ToString();
                _lastValidForkTarget = SelectedForkTargetList;
            }
            if (IsConsoleExecuteNode && !string.IsNullOrEmpty(Command))
                config["command"] = Command;
            if (IsConsoleExecuteNode && !string.IsNullOrEmpty(WorkingDirectory))
                config["workingDirectory"] = WorkingDirectory;
            if (IsTaskOrConsoleNode && AutoStartWhenPredecessorDone)
                config["autoStartWhenPredecessorDone"] = true;
            if (IsTaskOrConsoleNode && AutoAdvanceSuccessor)
                config["autoAdvanceSuccessor"] = true;

            _currentItem.FlowConfigJson = config.Count > 0
                ? System.Text.Json.JsonSerializer.Serialize(config) : null;

            await _todoService.UpdateItemAsync(_currentItem);
            SaveCompleted?.Invoke();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to auto-save item config for {ItemId}", _currentItem.Id);
        }
    }

    public event Action? SaveCompleted;

    public Array StatusValues => Enum.GetValues(typeof(TodoStatus));
    public Array NodeTypeValues => Enum.GetValues(typeof(FlowNodeType));
}

public partial class ForkOptionItem : ObservableObject
{
    public Guid Id { get; set; }
    public string Title { get; set; } = string.Empty;
    [ObservableProperty] private bool _isSelected;
    public override string ToString() => Title;
}
