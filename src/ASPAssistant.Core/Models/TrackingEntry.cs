using System.Text.Json.Serialization;

namespace ASPAssistant.Core.Models;

public enum TrackingType
{
    Operator,
    Equipment
}

public class TrackingEntry
{
    [JsonPropertyName("name")]
    public string Name { get; set; } = "";

    [JsonPropertyName("type")]
    public TrackingType Type { get; set; }

    [JsonPropertyName("transformedCovenant")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? TransformedCovenant { get; set; }

    [JsonPropertyName("equipments")]
    public List<string> Equipments { get; set; } = [];

    [JsonPropertyName("isElite")]
    public bool IsElite { get; set; }

    [JsonPropertyName("isAcquired")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    public bool IsAcquired { get; set; }

    [JsonPropertyName("isManual")]
    public bool IsManual { get; set; } = true;
}
