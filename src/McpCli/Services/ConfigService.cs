using System.Text.Json;
using System.Text.Json.Serialization;

namespace McpCli.Services;

/// <summary>
/// Manages mcp-cli configuration including server aliases.
/// The configuration is stored at ~/.mcp-cli/config.json.
///
/// As the Book of Names records the true names of spirits,
/// so this service records the aliases by which servers may be invoked.
/// </summary>
public static class ConfigService
{
    private static readonly string ConfigDirectory = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
        ".mcp-cli");

    private static readonly string ConfigFilePath = Path.Combine(ConfigDirectory, "config.json");

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    /// <summary>
    /// Load the configuration from disk, or return a default if none exists.
    /// </summary>
    public static CliConfig Load()
    {
        if (!File.Exists(ConfigFilePath))
        {
            return new CliConfig();
        }

        try
        {
            var json = File.ReadAllText(ConfigFilePath);
            return JsonSerializer.Deserialize<CliConfig>(json, JsonOptions) ?? new CliConfig();
        }
        catch
        {
            // Corrupted config - return default
            return new CliConfig();
        }
    }

    /// <summary>
    /// Save the configuration to disk.
    /// </summary>
    public static void Save(CliConfig config)
    {
        // Ensure directory exists
        if (!Directory.Exists(ConfigDirectory))
        {
            Directory.CreateDirectory(ConfigDirectory);
        }

        var json = JsonSerializer.Serialize(config, JsonOptions);
        File.WriteAllText(ConfigFilePath, json);
    }

    /// <summary>
    /// Set a server alias.
    /// </summary>
    public static void SetAlias(string alias, string serverCommand)
    {
        var config = Load();
        config.Servers[alias] = serverCommand;
        Save(config);
    }

    /// <summary>
    /// Remove a server alias.
    /// </summary>
    public static bool RemoveAlias(string alias)
    {
        var config = Load();
        var removed = config.Servers.Remove(alias);
        if (removed)
        {
            Save(config);
        }
        return removed;
    }

    /// <summary>
    /// Get all server aliases.
    /// </summary>
    public static Dictionary<string, string> GetAliases()
    {
        return Load().Servers;
    }

    /// <summary>
    /// Resolve a server argument - if it's an alias, return the full command.
    /// </summary>
    public static string ResolveServer(string serverArg)
    {
        var config = Load();

        // Check if the argument is an alias (case-insensitive)
        foreach (var (alias, command) in config.Servers)
        {
            if (string.Equals(alias, serverArg, StringComparison.OrdinalIgnoreCase))
            {
                return command;
            }
        }

        // Not an alias - return as-is
        return serverArg;
    }

    /// <summary>
    /// Get the path to the config file (for display purposes).
    /// </summary>
    public static string GetConfigPath() => ConfigFilePath;
}

/// <summary>
/// CLI configuration model.
/// </summary>
public class CliConfig
{
    /// <summary>
    /// Server aliases mapping short names to full commands.
    /// </summary>
    public Dictionary<string, string> Servers { get; set; } = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// Default settings (reserved for future use).
    /// </summary>
    public ConfigDefaults? Defaults { get; set; }
}

/// <summary>
/// Default configuration values.
/// </summary>
public class ConfigDefaults
{
    /// <summary>
    /// Default timeout in seconds.
    /// </summary>
    public int? Timeout { get; set; }
}
