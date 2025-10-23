using System.Collections.Generic;
using YamlDotNet.Serialization;

public class Config
{
    [YamlMember(Alias = "username")]
    public string Username { get; set; } = string.Empty;

    [YamlMember(Alias = "checkIntervalSeconds")]
    public int CheckIntervalSeconds { get; set; } = 10;

    [YamlMember(Alias = "gamePersonaNames")]
    public Dictionary<string, string> GamePersonaNames { get; set; } = new();

    [YamlMember(Alias = "defaultPersonaName")]
    public string DefaultPersonaName { get; set; } = "Not Gaming";

    [YamlIgnore]
    public string Password { get; set; } = string.Empty;
}
