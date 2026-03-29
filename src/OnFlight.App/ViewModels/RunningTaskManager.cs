using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;
using OnFlight.Core.Services;
using System.Collections.ObjectModel;

namespace OnFlight.App.ViewModels;

public partial class RunningTaskManager : ObservableObject
{
    private readonly IRunningTaskService _service;
    private readonly TaskEventBus _eventBus;
    private readonly ILogger<RunningTaskInstanceViewModel> _instanceLogger;

    [ObservableProperty] private RunningTaskInstanceViewModel? _activeInstance;

    public ObservableCollection<RunningTaskInstanceViewModel> Instances { get; } = new();
    public ObservableCollection<RunningTaskInstanceViewModel> ArchivedInstances { get; } = new();

    [ObservableProperty] private bool _showArchived;

    public RunningTaskManager(IRunningTaskService service, TaskEventBus eventBus,
        ILogger<RunningTaskInstanceViewModel> instanceLogger)
    {
        _service = service;
        _eventBus = eventBus;
        _instanceLogger = instanceLogger;
    }

    public async Task InitializeAsync()
    {
        var dtos = await _service.GetAllInstancesAsync();
        Instances.Clear();
        ArchivedInstances.Clear();
        foreach (var dto in dtos)
        {
            var vm = RunningTaskInstanceViewModel.FromDto(dto, _service, _eventBus, _instanceLogger);
            if (vm.IsAllDone)
                ArchivedInstances.Add(vm);
            else
            {
                vm.AllDoneReached += OnInstanceAllDone;
                Instances.Add(vm);
            }
        }
        ActiveInstance = Instances.FirstOrDefault();
    }

    public async Task<RunningTaskInstanceViewModel> CreateInstanceAsync(Guid listId)
    {
        var dto = await _service.CreateInstanceAsync(listId);
        var vm = RunningTaskInstanceViewModel.FromDto(dto, _service, _eventBus, _instanceLogger);
        vm.AllDoneReached += OnInstanceAllDone;
        Instances.Insert(0, vm);
        ActiveInstance = vm;
        return vm;
    }

    public async Task RemoveInstanceByIdAsync(Guid instanceId)
    {
        await _service.DeleteInstanceAsync(instanceId);
        var vm = Instances.FirstOrDefault(i => i.InstanceId == instanceId);
        if (vm != null) Instances.Remove(vm);
        else
        {
            var archived = ArchivedInstances.FirstOrDefault(i => i.InstanceId == instanceId);
            if (archived != null) ArchivedInstances.Remove(archived);
        }
        if (ActiveInstance?.InstanceId == instanceId)
            ActiveInstance = Instances.FirstOrDefault();
    }

    [RelayCommand]
    private async Task RemoveInstanceAsync(RunningTaskInstanceViewModel? instance)
    {
        if (instance == null) return;
        await RemoveInstanceByIdAsync(instance.InstanceId);
    }

    public void ArchiveInstance(RunningTaskInstanceViewModel instance)
    {
        if (Instances.Remove(instance))
        {
            ArchivedInstances.Insert(0, instance);
            if (ActiveInstance == instance)
                ActiveInstance = Instances.FirstOrDefault();
        }
    }

    [RelayCommand]
    private void ToggleShowArchived()
    {
        ShowArchived = !ShowArchived;
    }

    private void OnInstanceAllDone(RunningTaskInstanceViewModel instance)
    {
        instance.AllDoneReached -= OnInstanceAllDone;
        ArchiveInstance(instance);
    }

    public void SetActive(RunningTaskInstanceViewModel? instance)
    {
        ActiveInstance = instance;
    }
}
