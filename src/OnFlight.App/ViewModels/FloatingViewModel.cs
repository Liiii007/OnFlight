using System.Collections.Specialized;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;
using OnFlight.Core.Services;
using System.Collections.ObjectModel;

namespace OnFlight.App.ViewModels;

public partial class FloatingViewModel : ObservableObject
{
    private readonly ITodoService _todoService;
    private readonly ILogger<FloatingViewModel> _logger;

    public RunningTaskManager Manager { get; }
    public ObservableCollection<RunningTaskInstanceViewModel> Instances => Manager.Instances;
    public ObservableCollection<RunningTaskInstanceViewModel> ArchivedInstances => Manager.ArchivedInstances;
    public ObservableCollection<RootListItem> AvailableLists { get; } = new();

    [ObservableProperty] private RootListItem? _selectedNewList;
    [ObservableProperty] private int _currentInstanceIndex;
    [ObservableProperty] private RunningTaskInstanceViewModel? _currentInstance;

    public string InstanceIndicator => Instances.Count > 0
        ? $"{CurrentInstanceIndex + 1}/{Instances.Count}"
        : "0/0";

    public bool HasInstances => Instances.Count > 0;

    public FloatingViewModel(RunningTaskManager manager, ITodoService todoService, ILogger<FloatingViewModel> logger)
    {
        Manager = manager;
        _todoService = todoService;
        _logger = logger;
        Instances.CollectionChanged += OnInstancesChanged;
        UpdateCurrentInstance();
    }

    private void OnInstancesChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        if (CurrentInstanceIndex >= Instances.Count)
            CurrentInstanceIndex = Math.Max(0, Instances.Count - 1);
        UpdateCurrentInstance();
    }

    partial void OnCurrentInstanceIndexChanged(int value)
    {
        UpdateCurrentInstance();
    }

    private void UpdateCurrentInstance()
    {
        CurrentInstance = Instances.Count > 0 && CurrentInstanceIndex < Instances.Count
            ? Instances[CurrentInstanceIndex]
            : null;
        OnPropertyChanged(nameof(InstanceIndicator));
        OnPropertyChanged(nameof(HasInstances));
        PrevInstanceCommand.NotifyCanExecuteChanged();
        NextInstanceCommand.NotifyCanExecuteChanged();
    }

    [RelayCommand(CanExecute = nameof(CanGoPrev))]
    private void PrevInstance()
    {
        if (CurrentInstanceIndex > 0)
            CurrentInstanceIndex--;
    }
    private bool CanGoPrev() => CurrentInstanceIndex > 0;

    [RelayCommand(CanExecute = nameof(CanGoNext))]
    private void NextInstance()
    {
        if (CurrentInstanceIndex < Instances.Count - 1)
            CurrentInstanceIndex++;
    }
    private bool CanGoNext() => CurrentInstanceIndex < Instances.Count - 1;

    public async Task LoadAvailableListsAsync()
    {
        AvailableLists.Clear();
        var lists = await _todoService.GetRootListsAsync();
        foreach (var l in lists)
            AvailableLists.Add(new RootListItem { Id = l.Id, Name = l.Name });
        SelectedNewList = AvailableLists.FirstOrDefault();
    }

    [RelayCommand]
    private async Task CreateNewRunAsync()
    {
        if (SelectedNewList == null) return;
        await Manager.CreateInstanceAsync(SelectedNewList.Id);
        CurrentInstanceIndex = Instances.Count - 1;
    }

    [RelayCommand]
    private async Task RemoveInstanceAsync(RunningTaskInstanceViewModel? instance)
    {
        if (instance != null)
            await Manager.RemoveInstanceByIdAsync(instance.InstanceId);
    }
}
