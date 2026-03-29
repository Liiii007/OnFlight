using System.Text.Json.Serialization;
using OnFlight.Contracts.Enums;

namespace OnFlight.Contracts.Models;

public class RunningInstanceDto
{
    [JsonPropertyName("id")]
    public Guid Id { get; set; }

    [JsonPropertyName("sourceListId")]
    public Guid SourceListId { get; set; }

    [JsonPropertyName("listName")]
    public string ListName { get; set; } = string.Empty;

    [JsonPropertyName("state")]
    [JsonConverter(typeof(JsonStringEnumConverter))]
    public RunningState State { get; set; }

    [JsonPropertyName("currentItemId")]
    public Guid? CurrentItemId { get; set; }

    [JsonPropertyName("createdAt")]
    public DateTime CreatedAt { get; set; }

    [JsonPropertyName("allowOutOfOrder")]
    public bool AllowOutOfOrder { get; set; }

    [JsonPropertyName("items")]
    public List<RunningInstanceItemDto> Items { get; set; } = new();
}

public class RunningInstanceItemDto
{
    [JsonPropertyName("id")]
    public Guid Id { get; set; }

    [JsonPropertyName("sourceItemId")]
    public Guid SourceItemId { get; set; }

    [JsonPropertyName("title")]
    public string Title { get; set; } = string.Empty;

    [JsonPropertyName("description")]
    public string? Description { get; set; }

    [JsonPropertyName("status")]
    [JsonConverter(typeof(JsonStringEnumConverter))]
    public TodoStatus Status { get; set; }

    [JsonPropertyName("sortOrder")]
    public int SortOrder { get; set; }

    [JsonPropertyName("nodeType")]
    [JsonConverter(typeof(JsonStringEnumConverter))]
    public FlowNodeType NodeType { get; set; }

    [JsonPropertyName("flowConfigJson")]
    public string? FlowConfigJson { get; set; }

    [JsonPropertyName("forkTargetListName")]
    public string? ForkTargetListName { get; set; }

    [JsonPropertyName("children")]
    public List<RunningInstanceItemDto> Children { get; set; } = new();
}
