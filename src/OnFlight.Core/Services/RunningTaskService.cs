using System.Text.Json;
using Microsoft.Extensions.Logging;
using OnFlight.Contracts.Enums;
using OnFlight.Contracts.Models;
using OnFlight.Core.Data.Repositories;
using OnFlight.Core.Mapping;
using OnFlight.Core.Models;

namespace OnFlight.Core.Services;

public class RunningTaskService : IRunningTaskService
{
    private readonly IRunningInstanceRepository _repo;
    private readonly ITodoService _todoService;
    private readonly ILogger<RunningTaskService> _logger;
    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = false };

    private class SortCounter { public int Value; }

    public RunningTaskService(IRunningInstanceRepository repo, ITodoService todoService, ILogger<RunningTaskService> logger)
    {
        _repo = repo;
        _todoService = todoService;
        _logger = logger;
    }

    public async Task<RunningInstanceDto> CreateInstanceAsync(Guid listId)
    {
        var list = await _todoService.GetListAsync(listId)
                   ?? throw new InvalidOperationException("List not found");

        var sortIndex = new SortCounter();
        var items = await ExpandItemsAsync(list.Items, sortIndex);

        var dto = new RunningInstanceDto
        {
            Id = Guid.NewGuid(),
            SourceListId = listId,
            ListName = list.Name,
            State = RunningState.Running,
            CreatedAt = DateTime.UtcNow,
            Items = items
        };

        var entity = dto.ToEntity();
        await _repo.InsertAsync(entity);
        _logger.LogInformation("Created running instance {InstanceId} from list {ListName} with {ItemCount} top-level items",
            dto.Id, list.Name, items.Count);
        return dto;
    }

    private async Task<List<RunningInstanceItemDto>> ExpandItemsAsync(
        List<TodoItem> sourceItems, SortCounter sortIndex)
    {
        var result = new List<RunningInstanceItemDto>();

        foreach (var item in sourceItems.Where(i => !i.IsDeleted).OrderBy(i => i.SortOrder))
        {
            var dto = new RunningInstanceItemDto
            {
                Id = Guid.NewGuid(),
                SourceItemId = item.Id,
                Title = item.Title,
                Description = item.Description,
                Status = TodoStatus.Pending,
                SortOrder = sortIndex.Value++,
                NodeType = item.NodeType,
                FlowConfigJson = item.FlowConfigJson
            };

            if (item.NodeType == FlowNodeType.Fork)
            {
                var targetListId = ParseTargetListId(item.FlowConfigJson);
                if (targetListId.HasValue)
                {
                    var targetList = await _todoService.GetListAsync(targetListId.Value);
                    if (targetList != null)
                    {
                        dto.ForkTargetListName = targetList.Name;
                        dto.Children = await ExpandItemsAsync(targetList.Items, sortIndex);
                    }
                }
            }

            if (item.NodeType == FlowNodeType.Task && item.SubListId.HasValue)
            {
                var subList = await _todoService.GetListAsync(item.SubListId.Value);
                if (subList != null && subList.Items.Any(i => !i.IsDeleted))
                    dto.Children = await ExpandItemsAsync(subList.Items, sortIndex);
            }

            result.Add(dto);
        }

        return result;
    }

    public async Task<IEnumerable<RunningInstanceDto>> GetAllInstancesAsync()
    {
        var entities = await _repo.GetAllAsync();
        return entities.Select(e => e.ToDto());
    }

    public async Task SaveInstanceAsync(Guid instanceId, RunningInstanceDto dto)
    {
        var entity = await _repo.GetByIdAsync(instanceId)
                     ?? throw new InvalidOperationException("Running instance not found");

        entity.StateJson = JsonSerializer.Serialize(dto, JsonOptions);
        await _repo.UpdateAsync(entity);
    }

    public async Task DeleteInstanceAsync(Guid instanceId)
    {
        await _repo.DeleteAsync(instanceId);
        _logger.LogInformation("Deleted running instance {InstanceId}", instanceId);
    }

    private Guid? ParseTargetListId(string? json)
    {
        if (string.IsNullOrEmpty(json)) return null;
        try
        {
            var config = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(json);
            if (config != null && config.TryGetValue("targetListId", out var el))
                return Guid.Parse(el.GetString()!);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to parse targetListId from FlowConfigJson");
        }
        return null;
    }
}
