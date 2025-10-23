using System.Collections.Generic;
using System.Text.Json.Serialization;

public class Config
{
    [JsonPropertyName("username")]
    public string Username { get; set; } = string.Empty;

    [JsonPropertyName("password")]
    public string Password { get; set; } = string.Empty;

    [JsonPropertyName("checkIntervalSeconds")]
    public int CheckIntervalSeconds { get; set; } = 10;

    [JsonPropertyName("gamePersonaNames")]
    public Dictionary<string, string> GamePersonaNames { get; set; } = new();

    [JsonPropertyName("defaultPersonaName")]
    public string DefaultPersonaName { get; set; } = "Not Gaming";
}
