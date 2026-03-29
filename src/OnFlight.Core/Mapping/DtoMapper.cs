using System.Text.Json;
using OnFlight.Contracts.Models;
using OnFlight.Core.Models;

namespace OnFlight.Core.Mapping;

public static class DtoMapper
{
    public static TodoItemDto ToDto(this TodoItem item)
    {
        ArgumentNullException.ThrowIfNull(item);
        return new()
        {
            Id = item.Id,
            Title = item.Title,
            Description = item.Description,
            Status = item.Status,
            SortOrder = item.SortOrder,
            ParentListId = item.ParentListId,
            SubListId = item.SubListId,
            NodeType = item.NodeType,
            FlowConfigJson = item.FlowConfigJson,
            CreatedAt = item.CreatedAt,
            UpdatedAt = item.UpdatedAt,
            DeviceId = item.DeviceId,
            IsDeleted = item.IsDeleted
        };
    }

    public static TodoItem ToDomain(this TodoItemDto dto)
    {
        ArgumentNullException.ThrowIfNull(dto);
        return new()
        {
            Id = dto.Id,
            Title = dto.Title,
            Description = dto.Description,
            Status = dto.Status,
            SortOrder = dto.SortOrder,
            ParentListId = dto.ParentListId,
            SubListId = dto.SubListId,
            NodeType = dto.NodeType,
            FlowConfigJson = dto.FlowConfigJson,
            CreatedAt = dto.CreatedAt,
            UpdatedAt = dto.UpdatedAt,
            DeviceId = dto.DeviceId,
            IsDeleted = dto.IsDeleted
        };
    }

    public static TodoListDto ToDto(this TodoList list)
    {
        ArgumentNullException.ThrowIfNull(list);
        return new()
        {
            Id = list.Id,
            Name = list.Name,
            ParentItemId = list.ParentItemId,
            CreatedAt = list.CreatedAt,
            UpdatedAt = list.UpdatedAt,
            DeviceId = list.DeviceId,
            IsDeleted = list.IsDeleted,
            Items = list.Items.Select(i => i.ToDto()).ToList()
        };
    }

    public static TodoList ToDomain(this TodoListDto dto)
    {
        ArgumentNullException.ThrowIfNull(dto);
        return new()
        {
            Id = dto.Id,
            Name = dto.Name,
            ParentItemId = dto.ParentItemId,
            CreatedAt = dto.CreatedAt,
            UpdatedAt = dto.UpdatedAt,
            DeviceId = dto.DeviceId,
            IsDeleted = dto.IsDeleted,
            Items = dto.Items.Select(i => i.ToDomain()).ToList()
        };
    }

    public static OperationLogDto ToDto(this OperationLog log)
    {
        ArgumentNullException.ThrowIfNull(log);
        return new()
        {
            Id = log.Id,
            ListId = log.ListId,
            OperationType = log.OperationType,
            Detail = log.Detail,
            Timestamp = log.Timestamp,
            DeviceId = log.DeviceId
        };
    }

    public static OperationLog ToDomain(this OperationLogDto dto)
    {
        ArgumentNullException.ThrowIfNull(dto);
        return new()
        {
            Id = dto.Id,
            ListId = dto.ListId,
            OperationType = dto.OperationType,
            Detail = dto.Detail,
            Timestamp = dto.Timestamp,
            DeviceId = dto.DeviceId
        };
    }

    private static readonly JsonSerializerOptions RunningJsonOptions = new() { WriteIndented = false };

    public static RunningInstanceDto ToDto(this RunningInstance entity)
    {
        ArgumentNullException.ThrowIfNull(entity);
        var dto = JsonSerializer.Deserialize<RunningInstanceDto>(entity.StateJson, RunningJsonOptions)
                  ?? new RunningInstanceDto();
        dto.Id = entity.Id;
        dto.SourceListId = entity.SourceListId;
        dto.ListName = entity.ListName;
        dto.CreatedAt = entity.CreatedAt;
        return dto;
    }

    public static RunningInstance ToEntity(this RunningInstanceDto dto)
    {
        ArgumentNullException.ThrowIfNull(dto);
        return new()
        {
            Id = dto.Id,
            SourceListId = dto.SourceListId,
            ListName = dto.ListName,
            StateJson = JsonSerializer.Serialize(dto, RunningJsonOptions),
            CreatedAt = dto.CreatedAt
        };
    }
}
