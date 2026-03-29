using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using OnFlight.Core.Models;
using OnFlight.Core.Services;
using System.Collections.ObjectModel;

namespace OnFlight.App.ViewModels;

public partial class HistoryViewModel : ObservableObject
{
    private readonly ILogService _logService;

    [ObservableProperty] private string _searchKeyword = string.Empty;
    [ObservableProperty] private DateTime? _filterFrom;
    [ObservableProperty] private DateTime? _filterTo;

    public ObservableCollection<OperationLog> Logs { get; } = new();

    public HistoryViewModel(ILogService logService)
    {
        _logService = logService;
    }

    public async Task LoadAsync(Guid listId)
    {
        Logs.Clear();
        var logs = await _logService.GetLogsAsync(listId);
        foreach (var l in logs) Logs.Add(l);
    }

    [RelayCommand]
    private async Task SearchLogsAsync(Guid listId)
    {
        Logs.Clear();
        var logs = await _logService.SearchLogsAsync(listId, SearchKeyword, FilterFrom, FilterTo);
        foreach (var l in logs) Logs.Add(l);
    }
}
