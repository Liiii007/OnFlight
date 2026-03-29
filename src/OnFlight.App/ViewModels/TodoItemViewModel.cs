using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using OnFlight.Contracts.Enums;
using OnFlight.Core.Models;
using System.Collections.ObjectModel;

namespace OnFlight.App.ViewModels;

public partial class TodoItemViewModel : ObservableObject
{
    [ObservableProperty] private Guid _id;
    [ObservableProperty] private Guid _sourceItemId;
    [ObservableProperty] private string _title = string.Empty;
    [ObservableProperty] private string? _description;
    [ObservableProperty] private TodoStatus _status;
    [ObservableProperty] private FlowNodeType _nodeType;
    [ObservableProperty] private int _sortOrder;
    [ObservableProperty] private bool _isExpanded = true;
    [ObservableProperty] private bool _hasSubList;
    [ObservableProperty] private Guid? _subListId;
    [ObservableProperty] private int _depth;
    [ObservableProperty] private string? _flowConfigJson;
    [ObservableProperty] private bool _isForkChild;
    [ObservableProperty] private string? _parentForkTitle;
    [ObservableProperty] private Guid? _parentForkItemId;
    [ObservableProperty] private bool _isSubTaskChild;
    [ObservableProperty] private string? _parentTaskTitle;
    [ObservableProperty] private string? _forkTargetListName;
    [ObservableProperty] private string? _forkJoinTag;
    [ObservableProperty] private string _forkColorHex = "#FF9800";
    [ObservableProperty] private bool _isLocked = true;
    [ObservableProperty] private bool _isJustChanged;
    [ObservableProperty] private bool _isJustUnlocked;
    [ObservableProperty] private bool _isRenaming;
    [ObservableProperty] private string _renamingTitle = string.Empty;

    public ObservableCollection<TodoItemViewModel> Children { get; } = new();

    public bool IsDone => Status == TodoStatus.Done;
    public bool IsSkipped => Status == TodoStatus.Skipped;
    public bool HasForkJoinTag => !string.IsNullOrEmpty(ForkJoinTag);
    public bool IsNotLocked => !IsLocked;
    public bool ShowStrikethroughAnim => IsDone && IsJustChanged;
    public bool ShowUndoneAnim => !IsDone && IsJustChanged;
    public bool IsTaskNode => NodeType == FlowNodeType.Task;
    public bool IsConsoleExecuteNode => NodeType == FlowNodeType.ConsoleExecute;
    public bool IsCheckableNode => NodeType == FlowNodeType.Task || NodeType == FlowNodeType.ConsoleExecute;
    public bool IsRunning => Status == TodoStatus.Running;
    public bool ShowSubListButton => IsTaskNode && HasSubList;
    public bool CanHaveSubList => IsTaskNode && !IsForkChild;

    public static TodoItemViewModel FromModel(TodoItem item, int depth = 0) => new()
    {
        Id = item.Id,
        Title = item.Title,
        Description = item.Description,
        Status = item.Status,
        NodeType = item.NodeType,
        SortOrder = item.SortOrder,
        HasSubList = item.SubListId.HasValue,
        SubListId = item.SubListId,
        Depth = depth,
        FlowConfigJson = item.FlowConfigJson
    };

    partial void OnStatusChanged(TodoStatus value)
    {
        IsLocked = value == TodoStatus.Pending;
        OnPropertyChanged(nameof(IsDone));
        OnPropertyChanged(nameof(IsSkipped));
        OnPropertyChanged(nameof(IsRunning));
        OnPropertyChanged(nameof(ShowStrikethroughAnim));
        OnPropertyChanged(nameof(ShowUndoneAnim));
    }

    partial void OnIsJustChangedChanged(bool value)
    {
        OnPropertyChanged(nameof(ShowStrikethroughAnim));
        OnPropertyChanged(nameof(ShowUndoneAnim));
    }

    partial void OnNodeTypeChanged(FlowNodeType value)
    {
        OnPropertyChanged(nameof(IsTaskNode));
        OnPropertyChanged(nameof(IsConsoleExecuteNode));
        OnPropertyChanged(nameof(IsCheckableNode));
        OnPropertyChanged(nameof(ShowSubListButton));
        OnPropertyChanged(nameof(CanHaveSubList));
    }

    partial void OnHasSubListChanged(bool value)
    {
        OnPropertyChanged(nameof(ShowSubListButton));
    }

    partial void OnForkJoinTagChanged(string? value)
    {
        OnPropertyChanged(nameof(HasForkJoinTag));
    }

    partial void OnIsLockedChanged(bool value)
    {
        OnPropertyChanged(nameof(IsNotLocked));
    }
}
