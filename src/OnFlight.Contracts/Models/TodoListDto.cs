using System.Text.Json.Serialization;

namespace OnFlight.Contracts.Models;

public class TodoListDto
{
    [JsonPropertyName("id")]
    public Guid Id { get; set; }

    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    [JsonPropertyName("parentItemId")]
    public Guid? ParentItemId { get; set; }

    [JsonPropertyName("createdAt")]
    public DateTime CreatedAt { get; set; }

    [JsonPropertyName("updatedAt")]
    public DateTime UpdatedAt { get; set; }

    [JsonPropertyName("deviceId")]
    public string DeviceId { get; set; } = string.Empty;

    [JsonPropertyName("isDeleted")]
    public bool IsDeleted { get; set; }

    [JsonPropertyName("items")]
    public List<TodoItemDto> Items { get; set; } = new();
}
