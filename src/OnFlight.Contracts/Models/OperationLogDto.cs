using System.Text.Json.Serialization;
using OnFlight.Contracts.Enums;

namespace OnFlight.Contracts.Models;

public class OperationLogDto
{
    [JsonPropertyName("id")]
    public Guid Id { get; set; }

    [JsonPropertyName("listId")]
    public Guid ListId { get; set; }

    [JsonPropertyName("operationType")]
    [JsonConverter(typeof(JsonStringEnumConverter))]
    public OperationType OperationType { get; set; }

    [JsonPropertyName("detail")]
    public string? Detail { get; set; }

    [JsonPropertyName("timestamp")]
    public DateTime Timestamp { get; set; }

    [JsonPropertyName("deviceId")]
    public string DeviceId { get; set; } = string.Empty;
}
