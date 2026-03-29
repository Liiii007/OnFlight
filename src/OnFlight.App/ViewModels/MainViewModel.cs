using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;
using OnFlight.Contracts.Enums;
using OnFlight.Core.Models;
using OnFlight.Core.Services;
using System.Collections.ObjectModel;

namespace OnFlight.App.ViewModels;

public partial class MainViewModel : ObservableObject
{
    private readonly ITodoService _todoService;
    private readonly TaskEventBus _eventBus;
    private readonly ILogger<MainViewModel> _logger;

    [ObservableProperty] private TodoItemViewModel? _selectedItem;
    [ObservableProperty] private string _newItemTitle = string.Empty;
    [ObservableProperty] private Guid _currentListId;
    [ObservableProperty] private string _currentListName = "My Tasks";
    [ObservableProperty] private bool _isLoading;
    [ObservableProperty] private int _completedCount;
    [ObservableProperty] private int _totalCount;
    [ObservableProperty] private double _progress;
    [ObservableProperty] private RootListItem? _selectedRootList;
    [ObservableProperty] private bool _isRenamingList;
    [ObservableProperty] private string _renamingListName = string.Empty;
    [ObservableProperty] private bool _isDraft = true;
    private Guid? _pendingSelectItemId;
    [ObservableProperty] private RunningTaskInstanceViewModel? _linkedRunningInstance;

    public ObservableCollection<TodoItemViewModel> Items { get; } = new();
    public ObservableCollection<BreadcrumbItem> Breadcrumbs { get; } = new();
    public bool IsInSubList => Breadcrumbs.Count > 1;
    public ObservableCollection<RootListItem> RootLists { get; } = new();
    public ItemConfigViewModel ConfigViewModel { get; }
    public HistoryViewModel HistoryViewModel { get; }
    public RunningTaskManager RunningTaskManager { get; }

    /// <summary>
    /// Invoked before Items is cleared during a view switch.
    /// The View hooks this to play slide-out animation.
    /// </summary>
    public Func<Task>? OnBeforeItemsSwitch { get; set; }

    /// <summary>
    /// Invoked after Items has been populated during a view switch.
    /// The View hooks this to play slide-in animation.
    /// </summary>
    public Func<Task>? OnAfterItemsLoaded { get; set; }

    public MainViewModel(
        ITodoService todoService,
        ILogService logService,
        TaskEventBus eventBus,
        RunningTaskManager runningTaskManager,
        ILogger<MainViewModel> logger,
        ILogger<ItemConfigViewModel> configLogger)
    {
        _todoService = todoService;
        _eventBus = eventBus;
        _logger = logger;
        RunningTaskManager = runningTaskManager;
        ConfigViewModel = new ItemConfigViewModel(todoService, configLogger);
        ConfigViewModel.SaveCompleted += async () => await LoadItemsAsync();
        HistoryViewModel = new HistoryViewModel(logService);
        Breadcrumbs.CollectionChanged += (_, _) => OnPropertyChanged(nameof(IsInSubList));

        _eventBus.EventPublished += OnTaskEventAsync;
    }

    private async Task OnTaskEventAsync(TaskEvent evt)
    {
        await Avalonia.Threading.Dispatcher.UIThread.InvokeAsync(async () =>
        {
            if (evt.ListId == CurrentListId)
                await HistoryViewModel.LoadAsync(CurrentListId);

            if (LinkedRunningInstance != null && evt.ListId == LinkedRunningInstance.SourceListId)
                await SyncFromRunningInstanceAsync();
        });
    }

    public async Task InitializeAsync()
    {
        await RunningTaskManager.InitializeAsync();
        await RefreshRootListsAsync();
        var rootList = RootLists.FirstOrDefault();
        if (rootList == null)
        {
            var created = await _todoService.CreateListAsync("My Tasks");
            rootList = new RootListItem { Id = created.Id, Name = created.Name };
            RootLists.Add(rootList);
        }
        SelectedRootList = rootList;
    }

    partial void OnSelectedRootListChanged(RootListItem? value)
    {
        SwitchToDraft(value);
    }

    public async void SwitchToDraft(RootListItem? value)
    {
        if (value == null) return;

        if (OnBeforeItemsSwitch != null)
            await OnBeforeItemsSwitch.Invoke();

        CurrentListId = value.Id;
        CurrentListName = value.Name;
        Breadcrumbs.Clear();
        Breadcrumbs.Add(new BreadcrumbItem { Id = value.Id, Name = value.Name });
        LinkedRunningInstance = null;
        IsDraft = true;
        await LoadItemsAsDraftAsync();
        _ = HistoryViewModel.LoadAsync(value.Id);

        if (OnAfterItemsLoaded != null)
            await OnAfterItemsLoaded.Invoke();
    }

    private async Task RefreshRootListsAsync()
    {
        var lists = await _todoService.GetRootListsAsync();
        RootLists.Clear();
        foreach (var l in lists)
            RootLists.Add(new RootListItem { Id = l.Id, Name = l.Name });
    }

    [RelayCommand]
    private async Task CreateNewListAsync()
    {
        var list = await _todoService.CreateListAsync("New List");
        var item = new RootListItem { Id = list.Id, Name = list.Name };
        RootLists.Add(item);
        SelectedRootList = item;
        BeginRenameList();
    }

    [RelayCommand]
    private void BeginRenameList()
    {
        if (SelectedRootList == null) return;
        RenamingListName = SelectedRootList.Name;
        IsRenamingList = true;
    }

    [RelayCommand]
    private async Task CommitRenameListAsync()
    {
        if (!IsRenamingList || SelectedRootList == null) return;
        var trimmed = RenamingListName.Trim();
        if (string.IsNullOrEmpty(trimmed)) trimmed = SelectedRootList.Name;

        IsRenamingList = false;

        if (trimmed == SelectedRootList.Name) return;

        SelectedRootList.Name = trimmed;
        CurrentListName = trimmed;

        var todoList = await _todoService.GetListAsync(SelectedRootList.Id);
        if (todoList != null)
        {
            todoList.Name = trimmed;
            await _todoService.UpdateListAsync(todoList);
        }

        if (Breadcrumbs.Count > 0)
            Breadcrumbs[0] = new BreadcrumbItem { Id = SelectedRootList.Id, Name = trimmed };

        var idx = RootLists.IndexOf(SelectedRootList);
        if (idx >= 0)
        {
            var updated = new RootListItem { Id = SelectedRootList.Id, Name = trimmed };
            RootLists[idx] = updated;
            SelectedRootList = updated;
        }
    }

    [RelayCommand]
    private void CancelRenameList()
    {
        IsRenamingList = false;
    }

    [RelayCommand]
    private async Task DeleteCurrentListAsync()
    {
        if (RootLists.Count <= 1) return;
        await _todoService.DeleteListAsync(CurrentListId);
        var toRemove = RootLists.FirstOrDefault(r => r.Id == CurrentListId);
        if (toRemove != null) RootLists.Remove(toRemove);
        SelectedRootList = RootLists.FirstOrDefault();
    }

    [RelayCommand]
    private async Task LoadItemsAsync()
    {
        IsLoading = true;
        try
        {
            var list = await _todoService.GetListAsync(CurrentListId);
            Items.Clear();
            if (list != null)
            {
                foreach (var item in list.Items.OrderBy(i => i.SortOrder))
                {
                    var vm = TodoItemViewModel.FromModel(item);
                    if (item.SubListId.HasValue)
                        await SyncParentStatusFromSubListAsync(vm, item.SubListId.Value);
                    Items.Add(vm);

                    await ExpandChildrenRecursiveAsync(item, depth: 1);
                }
            }
            await UpdateProgressAsync();
            ApplyPendingSelection();
        }
        finally
        {
            IsLoading = false;
        }
    }

    private async Task LoadItemsAsDraftAsync()
    {
        IsLoading = true;
        try
        {
            var list = await _todoService.GetListAsync(CurrentListId);
            Items.Clear();
            if (list != null)
            {
                foreach (var item in list.Items.OrderBy(i => i.SortOrder))
                {
                    var vm = TodoItemViewModel.FromModel(item);
                    vm.Status = TodoStatus.Pending;
                    Items.Add(vm);

                    await ExpandChildrenRecursiveDraftAsync(item, depth: 1);
                }
            }
            await UpdateProgressAsync();
            ApplyPendingSelection();
        }
        finally
        {
            IsLoading = false;
        }
    }

    private void ApplyPendingSelection()
    {
        if (!_pendingSelectItemId.HasValue) return;
        var id = _pendingSelectItemId.Value;
        _pendingSelectItemId = null;
        var match = Items.FirstOrDefault(i => i.Id == id);
        if (match != null)
            SelectedItem = match;
    }

    private async Task ExpandChildrenRecursiveDraftAsync(TodoItem parentItem, int depth)
    {
        if (parentItem.NodeType == FlowNodeType.Fork)
        {
            var targetListId = ParseTargetListId(parentItem.FlowConfigJson);
            if (targetListId == null) return;

            var targetList = await _todoService.GetListAsync(targetListId.Value);
            if (targetList == null) return;

            foreach (var child in targetList.Items.OrderBy(i => i.SortOrder))
            {
                var vm = TodoItemViewModel.FromModel(child, depth: depth);
                vm.IsForkChild = true;
                vm.ParentForkTitle = parentItem.Title;
                vm.ParentForkItemId = parentItem.Id;
                vm.ForkTargetListName = targetList.Name;
                vm.Status = TodoStatus.Pending;
                Items.Add(vm);

                await ExpandChildrenRecursiveDraftAsync(child, depth + 1);
            }
        }
        else if (parentItem.NodeType == FlowNodeType.Task && parentItem.SubListId.HasValue)
        {
            var subList = await _todoService.GetListAsync(parentItem.SubListId.Value);
            if (subList == null || subList.Items.Count == 0) return;

            foreach (var child in subList.Items.OrderBy(i => i.SortOrder))
            {
                var vm = TodoItemViewModel.FromModel(child, depth: depth);
                vm.IsSubTaskChild = true;
                vm.ParentTaskTitle = parentItem.Title;
                vm.Status = TodoStatus.Pending;
                Items.Add(vm);

                await ExpandChildrenRecursiveDraftAsync(child, depth + 1);
            }
        }
    }

    [RelayCommand]
    private async Task AttachRunningInstanceAsync(RunningTaskInstanceViewModel? instance)
    {
        if (instance == null) return;

        if (OnBeforeItemsSwitch != null)
            await OnBeforeItemsSwitch.Invoke();

        var rootList = RootLists.FirstOrDefault(r => r.Id == instance.SourceListId);
        if (rootList != null && SelectedRootList?.Id != rootList.Id)
        {
            CurrentListId = rootList.Id;
            CurrentListName = rootList.Name;
            Breadcrumbs.Clear();
            Breadcrumbs.Add(new BreadcrumbItem { Id = rootList.Id, Name = rootList.Name });
        }

        LinkedRunningInstance = instance;
        IsDraft = false;
        await SyncFromRunningInstanceAsync();

        if (OnAfterItemsLoaded != null)
            await OnAfterItemsLoaded.Invoke();
    }

    private async Task SyncFromRunningInstanceAsync()
    {
        if (LinkedRunningInstance == null) return;

        IsLoading = true;
        try
        {
            var list = await _todoService.GetListAsync(CurrentListId);
            Items.Clear();
            if (list != null)
            {
                foreach (var item in list.Items.OrderBy(i => i.SortOrder))
                {
                    var vm = TodoItemViewModel.FromModel(item);
                    var runMatch = FindRunningItem(vm.Id, null, null);
                    if (runMatch != null)
                        vm.Status = runMatch.Status;
                    if (item.SubListId.HasValue)
                        await SyncParentStatusFromSubListAsync(vm, item.SubListId.Value);
                    Items.Add(vm);

                    await ExpandChildrenRecursiveWithStatusAsync(item, depth: 1, null, null);
                }
            }
            await UpdateProgressAsync();
        }
        finally
        {
            IsLoading = false;
        }
    }

    private async Task ExpandChildrenRecursiveWithStatusAsync(TodoItem parentItem, int depth,
        Guid? forkItemId, string? forkTitle)
    {
        if (parentItem.NodeType == FlowNodeType.Fork)
        {
            var targetListId = ParseTargetListId(parentItem.FlowConfigJson);
            if (targetListId == null) return;

            var targetList = await _todoService.GetListAsync(targetListId.Value);
            if (targetList == null) return;

            foreach (var child in targetList.Items.OrderBy(i => i.SortOrder))
            {
                var vm = TodoItemViewModel.FromModel(child, depth: depth);
                vm.IsForkChild = true;
                vm.ParentForkTitle = parentItem.Title;
                vm.ParentForkItemId = parentItem.Id;
                vm.ForkTargetListName = targetList.Name;
                var runMatch = FindRunningItem(vm.Id, parentItem.Id, parentItem.Title);
                if (runMatch != null)
                    vm.Status = runMatch.Status;
                Items.Add(vm);

                await ExpandChildrenRecursiveWithStatusAsync(child, depth + 1, parentItem.Id, parentItem.Title);
            }
        }
        else if (parentItem.NodeType == FlowNodeType.Task && parentItem.SubListId.HasValue)
        {
            var subList = await _todoService.GetListAsync(parentItem.SubListId.Value);
            if (subList == null || subList.Items.Count == 0) return;

            foreach (var child in subList.Items.OrderBy(i => i.SortOrder))
            {
                var vm = TodoItemViewModel.FromModel(child, depth: depth);
                vm.IsSubTaskChild = true;
                vm.ParentTaskTitle = parentItem.Title;
                if (forkItemId != null)
                {
                    vm.IsForkChild = true;
                    vm.ParentForkItemId = forkItemId;
                    vm.ParentForkTitle = forkTitle;
                }
                var runMatch = FindRunningItem(vm.Id, forkItemId, forkTitle);
                if (runMatch != null)
                    vm.Status = runMatch.Status;
                Items.Add(vm);

                await ExpandChildrenRecursiveWithStatusAsync(child, depth + 1, forkItemId, forkTitle);
            }
        }
    }

    /// <summary>
    /// Find the matching item in LinkedRunningInstance by SourceItemId + fork identity.
    /// Uses ParentForkItemId when available; falls back to ParentForkTitle for legacy data.
    /// </summary>
    private TodoItemViewModel? FindRunningItem(Guid sourceItemId, Guid? parentForkItemId, string? parentForkTitle)
    {
        if (LinkedRunningInstance == null) return null;

        foreach (var i in LinkedRunningInstance.Items)
        {
            if (i.SourceItemId != sourceItemId) continue;
            if (i.ParentForkItemId != null)
            {
                if (i.ParentForkItemId == parentForkItemId) return i;
            }
            else
            {
                if (i.ParentForkTitle == parentForkTitle) return i;
            }
        }
        return null;
    }

    private async Task ExpandChildrenRecursiveAsync(TodoItem parentItem, int depth)
    {
        if (parentItem.NodeType == FlowNodeType.Fork)
        {
            var targetListId = ParseTargetListId(parentItem.FlowConfigJson);
            if (targetListId == null) return;

            var targetList = await _todoService.GetListAsync(targetListId.Value);
            if (targetList == null) return;

            foreach (var child in targetList.Items.OrderBy(i => i.SortOrder))
            {
                var vm = TodoItemViewModel.FromModel(child, depth: depth);
                vm.IsForkChild = true;
                vm.ParentForkTitle = parentItem.Title;
                vm.ParentForkItemId = parentItem.Id;
                vm.ForkTargetListName = targetList.Name;
                Items.Add(vm);

                await ExpandChildrenRecursiveAsync(child, depth + 1);
            }
        }
        else if (parentItem.NodeType == FlowNodeType.Task && parentItem.SubListId.HasValue)
        {
            var subList = await _todoService.GetListAsync(parentItem.SubListId.Value);
            if (subList == null || subList.Items.Count == 0) return;

            foreach (var child in subList.Items.OrderBy(i => i.SortOrder))
            {
                var vm = TodoItemViewModel.FromModel(child, depth: depth);
                vm.IsSubTaskChild = true;
                vm.ParentTaskTitle = parentItem.Title;
                if (parentItem.SubListId.HasValue)
                    await SyncParentStatusFromSubListAsync(vm, parentItem.SubListId.Value);
                Items.Add(vm);

                await ExpandChildrenRecursiveAsync(child, depth + 1);
            }
        }
    }

    private async Task SyncParentStatusFromSubListAsync(TodoItemViewModel parentVm, Guid subListId)
    {
        var subList = await _todoService.GetListAsync(subListId);
        if (subList == null || subList.Items.Count == 0) return;

        var tasks = subList.Items.Where(i => i.NodeType == FlowNodeType.Task).ToList();
        if (tasks.Count == 0) return;

        var allDone = tasks.All(i => i.Status == TodoStatus.Done || i.Status == TodoStatus.Skipped);
        parentVm.Status = allDone ? TodoStatus.Done : TodoStatus.Pending;
    }

    private Guid? ParseTargetListId(string? json)
    {
        if (string.IsNullOrEmpty(json)) return null;
        try
        {
            var config = System.Text.Json.JsonSerializer.Deserialize<Dictionary<string, System.Text.Json.JsonElement>>(json);
            if (config != null && config.TryGetValue("targetListId", out var el))
                return Guid.Parse(el.GetString()!);
        }
        catch (Exception ex) { _logger.LogWarning(ex, "Failed to parse targetListId"); }
        return null;
    }

    [RelayCommand]
    private async Task AddItemAsync()
    {
        if (string.IsNullOrWhiteSpace(NewItemTitle)) return;
        var item = await _todoService.AddItemAsync(CurrentListId, NewItemTitle.Trim());
        Items.Add(TodoItemViewModel.FromModel(item));
        NewItemTitle = string.Empty;
        await UpdateProgressAsync();
    }

    [RelayCommand]
    private async Task ToggleItemAsync(TodoItemViewModel? vm)
    {
        if (vm == null || vm.NodeType != FlowNodeType.Task) return;

        if (LinkedRunningInstance != null)
        {
            var runItem = LinkedRunningInstance.Items.FirstOrDefault(i =>
                i.SourceItemId == vm.Id
                && (i.ParentForkItemId != null
                    ? i.ParentForkItemId == vm.ParentForkItemId
                    : i.ParentForkTitle == vm.ParentForkTitle));
            if (runItem != null)
                await LinkedRunningInstance.ToggleDoneCommand.ExecuteAsync(runItem);
            return;
        }

        if (IsDraft)
        {
            var newDraftStatus = vm.Status == TodoStatus.Done ? TodoStatus.Pending : TodoStatus.Done;
            vm.Status = newDraftStatus;
            var kind = newDraftStatus == TodoStatus.Done ? TaskEventKind.TaskChecked : TaskEventKind.TaskUnchecked;
            await _eventBus.PublishAsync(new TaskEvent(kind, CurrentListId, vm.Id, vm.Title));
            await UpdateProgressAsync();
            await AutoCompleteParentIfNeededAsync();
            return;
        }

        var newStatus = vm.Status == TodoStatus.Done ? TodoStatus.Pending : TodoStatus.Done;
        await _todoService.SetItemStatusAsync(vm.Id, newStatus);
        vm.Status = newStatus;
        var evtKind = newStatus == TodoStatus.Done ? TaskEventKind.TaskChecked : TaskEventKind.TaskUnchecked;
        await _eventBus.PublishAsync(new TaskEvent(evtKind, CurrentListId, vm.Id, vm.Title));
        await UpdateProgressAsync();
        await AutoCompleteParentIfNeededAsync();
    }

    private async Task AutoCompleteParentIfNeededAsync()
    {
        if (Breadcrumbs.Count <= 1) return;

        var list = await _todoService.GetListAsync(CurrentListId);
        if (list?.ParentItemId == null) return;

        var allDone = list.Items
            .Where(i => i.NodeType == FlowNodeType.Task)
            .All(i =>
            {
                var editorVm = Items.FirstOrDefault(e => e.Id == i.Id);
                return editorVm != null
                    ? editorVm.Status == TodoStatus.Done || editorVm.Status == TodoStatus.Skipped
                    : i.Status == TodoStatus.Done || i.Status == TodoStatus.Skipped;
            });

        var parentItem = await _todoService.GetItemAsync(list.ParentItemId.Value);
        if (parentItem == null) return;

        var targetStatus = allDone ? TodoStatus.Done : TodoStatus.Pending;
        if (parentItem.Status == targetStatus) return;

        if (!IsDraft)
            await _todoService.SetItemStatusAsync(parentItem.Id, targetStatus);

        parentItem.Status = targetStatus;
    }

    [RelayCommand]
    private void BeginRenameItem(TodoItemViewModel? vm)
    {
        if (vm == null) return;
        vm.RenamingTitle = vm.Title;
        vm.IsRenaming = true;
    }

    [RelayCommand]
    private async Task CommitRenameItemAsync(TodoItemViewModel? vm)
    {
        if (vm == null || !vm.IsRenaming) return;
        vm.IsRenaming = false;
        var trimmed = vm.RenamingTitle.Trim();
        if (string.IsNullOrEmpty(trimmed) || trimmed == vm.Title) return;

        vm.Title = trimmed;
        var item = await _todoService.GetItemAsync(vm.Id);
        if (item != null)
        {
            item.Title = trimmed;
            await _todoService.UpdateItemAsync(item);
        }
    }

    [RelayCommand]
    private async Task DeleteItemAsync(TodoItemViewModel? vm)
    {
        if (vm == null) return;
        await _todoService.DeleteItemAsync(vm.Id);
        Items.Remove(vm);
        if (SelectedItem == vm)
        {
            SelectedItem = null;
            ConfigViewModel.LoadItem(null);
        }
        await UpdateProgressAsync();
    }

    [RelayCommand]
    private async Task NavigateToSubListAsync(TodoItemViewModel? vm)
    {
        if (vm == null || !vm.HasSubList || !vm.SubListId.HasValue) return;

        if (OnBeforeItemsSwitch != null)
            await OnBeforeItemsSwitch.Invoke();

        CurrentListId = vm.SubListId.Value;
        var list = await _todoService.GetListAsync(CurrentListId);
        if (list != null)
        {
            CurrentListName = list.Name;
            Breadcrumbs.Add(new BreadcrumbItem { Id = list.Id, Name = list.Name });
        }
        await LoadItemsAsync();

        if (OnAfterItemsLoaded != null)
            await OnAfterItemsLoaded.Invoke();
    }

    [RelayCommand]
    private async Task NavigateToBreadcrumbAsync(BreadcrumbItem? crumb)
    {
        if (crumb == null) return;

        if (OnBeforeItemsSwitch != null)
            await OnBeforeItemsSwitch.Invoke();

        CurrentListId = crumb.Id;
        CurrentListName = crumb.Name;
        while (Breadcrumbs.Count > 0 && Breadcrumbs.Last().Id != crumb.Id)
            Breadcrumbs.RemoveAt(Breadcrumbs.Count - 1);
        await LoadItemsAsync();

        if (OnAfterItemsLoaded != null)
            await OnAfterItemsLoaded.Invoke();
    }

    [RelayCommand]
    private async Task CreateSubListAsync(TodoItemViewModel? vm)
    {
        if (vm == null) return;
        if (!vm.HasSubList)
        {
            var subList = await _todoService.CreateSubListAsync(vm.Id, $"{vm.Title} - Sub");
            vm.SubListId = subList.Id;
            vm.HasSubList = true;
        }
        await NavigateToSubListAsync(vm);
    }

    [RelayCommand]
    private async Task NavigateToForkTargetAsync(TodoItemViewModel? vm)
    {
        if (vm == null || vm.NodeType != FlowNodeType.Fork) return;

        var targetListId = ParseTargetListId(vm.FlowConfigJson);
        if (targetListId == null) return;

        var rootList = RootLists.FirstOrDefault(r => r.Id == targetListId.Value);
        if (rootList != null)
        {
            SelectedRootList = rootList;
            return;
        }

        var list = await _todoService.GetListAsync(targetListId.Value);
        if (list == null) return;

        var newRoot = new RootListItem { Id = list.Id, Name = list.Name };
        RootLists.Add(newRoot);
        SelectedRootList = newRoot;
    }

    [RelayCommand]
    private async Task StartNewRunAsync()
    {
        await RunningTaskManager.CreateInstanceAsync(CurrentListId);
    }

    public void MoveItem(int fromIndex, int toIndex)
    {
        if (fromIndex < 0 || fromIndex >= Items.Count) return;
        if (toIndex < 0 || toIndex >= Items.Count) return;
        if (fromIndex == toIndex) return;

        var item = Items[fromIndex];
        if (item.IsForkChild) return;

        Items.Move(fromIndex, toIndex);
        _ = PersistAndReloadAsync();
    }

    private async Task PersistAndReloadAsync()
    {
        await PersistItemOrderAsync();
        if (IsDraft)
            await LoadItemsAsDraftAsync();
        else if (LinkedRunningInstance != null)
            await SyncFromRunningInstanceAsync();
        else
            await LoadItemsAsync();
    }

    public async Task PersistItemOrderAsync()
    {
        var ownItemIds = Items.Where(i => !i.IsForkChild).Select(i => i.Id).ToList();
        await _todoService.ReorderItemsAsync(CurrentListId, ownItemIds);
    }

    partial void OnSelectedItemChanged(TodoItemViewModel? value)
    {
        if (value != null)
        {
            if (value.IsForkChild || value.IsSubTaskChild)
            {
                _ = NavigateToExpandedChildSourceAsync(value);
                return;
            }

            var item = new TodoItem
            {
                Id = value.Id,
                Title = value.Title,
                Description = value.Description,
                Status = value.Status,
                NodeType = value.NodeType,
                SortOrder = value.SortOrder,
                ParentListId = CurrentListId,
                SubListId = value.SubListId,
                FlowConfigJson = value.FlowConfigJson
            };
            ConfigViewModel.LoadItem(item, CurrentListId, RootLists, Items);
        }
        else
        {
            ConfigViewModel.LoadItem(null);
        }
    }

    private async Task NavigateToExpandedChildSourceAsync(TodoItemViewModel child)
    {
        var dbItem = await _todoService.GetItemAsync(child.Id);
        if (dbItem == null) return;

        var targetListId = dbItem.ParentListId;

        if (targetListId == CurrentListId)
        {
            SelectedItem = Items.FirstOrDefault(i => i.Id == child.Id && !i.IsForkChild && !i.IsSubTaskChild)
                           ?? child;
            return;
        }

        var rootList = RootLists.FirstOrDefault(r => r.Id == targetListId);
        if (rootList != null)
        {
            _pendingSelectItemId = child.Id;
            SelectedRootList = rootList;
            return;
        }

        var list = await _todoService.GetListAsync(targetListId);
        if (list == null) return;

        _pendingSelectItemId = child.Id;

        if (OnBeforeItemsSwitch != null)
            await OnBeforeItemsSwitch.Invoke();

        CurrentListId = list.Id;
        CurrentListName = list.Name;
        Breadcrumbs.Add(new BreadcrumbItem { Id = list.Id, Name = list.Name });
        await LoadItemsAsync();

        if (OnAfterItemsLoaded != null)
            await OnAfterItemsLoaded.Invoke();
    }

    private async Task UpdateProgressAsync()
    {
        int total = 0, completed = 0;
        await CountTasksRecursiveAsync(CurrentListId, Items, new HashSet<Guid>(),
            (t, c) => { total += t; completed += c; });
        TotalCount = total;
        CompletedCount = completed;
        Progress = TotalCount > 0 ? (double)CompletedCount / TotalCount : 0;
    }

    /// <summary>
    /// Recursively count all Task nodes including fork target lists and sub-task lists.
    /// Uses <paramref name="visited"/> to prevent cycles and duplicate counting.
    /// When <paramref name="editorItems"/> is provided, uses editor VM status for current-level items.
    /// </summary>
    private async Task CountTasksRecursiveAsync(Guid listId,
        ObservableCollection<TodoItemViewModel>? editorItems, HashSet<Guid> visited,
        Action<int, int> accumulate)
    {
        if (!visited.Add(listId)) return;

        if (editorItems != null)
        {
            foreach (var item in editorItems)
            {
                if (item.NodeType == FlowNodeType.Task)
                {
                    if (item.IsForkChild || item.IsSubTaskChild)
                        continue;

                    if (item.HasSubList && item.SubListId.HasValue)
                    {
                        await CountTasksRecursiveAsync(item.SubListId.Value, null, visited, accumulate);
                    }
                    else
                    {
                        int t = 1, c = (item.Status == TodoStatus.Done || item.Status == TodoStatus.Skipped) ? 1 : 0;
                        accumulate(t, c);
                    }
                }
                else if (item.NodeType == FlowNodeType.Fork)
                {
                    var targetId = ParseTargetListId(item.FlowConfigJson);
                    if (targetId.HasValue)
                        await CountTasksRecursiveAsync(targetId.Value, null, visited, accumulate);
                }
            }
        }
        else
        {
            var list = await _todoService.GetListAsync(listId);
            if (list == null) return;

            foreach (var item in list.Items.Where(i => !i.IsDeleted))
            {
                if (item.NodeType == FlowNodeType.Task)
                {
                    if (item.SubListId.HasValue)
                    {
                        await CountTasksRecursiveAsync(item.SubListId.Value, null, visited, accumulate);
                    }
                    else
                    {
                        int t = 1, c = (item.Status == TodoStatus.Done || item.Status == TodoStatus.Skipped) ? 1 : 0;
                        accumulate(t, c);
                    }
                }
                else if (item.NodeType == FlowNodeType.Fork)
                {
                    var targetId = ParseTargetListId(item.FlowConfigJson);
                    if (targetId.HasValue)
                        await CountTasksRecursiveAsync(targetId.Value, null, visited, accumulate);
                }
            }
        }
    }
}

public class BreadcrumbItem
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
}

public class RootListItem
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public override string ToString() => Name;
}
