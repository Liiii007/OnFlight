using System.Text.Json.Serialization;
using OnFlight.Contracts.Enums;

namespace OnFlight.Contracts.Models;

public class TodoItemDto
{
    [JsonPropertyName("id")]
    public Guid Id { get; set; }

    [JsonPropertyName("title")]
    public string Title { get; set; } = string.Empty;

    [JsonPropertyName("description")]
    public string? Description { get; set; }

    [JsonPropertyName("status")]
    [JsonConverter(typeof(JsonStringEnumConverter))]
    public TodoStatus Status { get; set; }

    [JsonPropertyName("sortOrder")]
    public int SortOrder { get; set; }

    [JsonPropertyName("parentListId")]
    public Guid ParentListId { get; set; }

    [JsonPropertyName("subListId")]
    public Guid? SubListId { get; set; }

    [JsonPropertyName("nodeType")]
    [JsonConverter(typeof(JsonStringEnumConverter))]
    public FlowNodeType NodeType { get; set; }

    [JsonPropertyName("flowConfigJson")]
    public string? FlowConfigJson { get; set; }

    [JsonPropertyName("createdAt")]
    public DateTime CreatedAt { get; set; }

    [JsonPropertyName("updatedAt")]
    public DateTime UpdatedAt { get; set; }

    [JsonPropertyName("deviceId")]
    public string DeviceId { get; set; } = string.Empty;

    [JsonPropertyName("isDeleted")]
    public bool IsDeleted { get; set; }
}
