using McpCli.Services;

namespace McpCli.Commands;

/// <summary>
/// Handler for the 'alias' command - manages server aliases.
///
/// Like the sigils inscribed in the Book of Names,
/// aliases bind short invocations to the true names of servers.
/// </summary>
public static class AliasCommandHandler
{
    /// <summary>
    /// List all configured aliases.
    /// </summary>
    public static void ExecuteList()
    {
        var aliases = ConfigService.GetAliases();

        if (aliases.Count == 0)
        {
            Console.WriteLine("No aliases configured.");
            Console.WriteLine();
            Console.WriteLine("Add an alias with:");
            Console.WriteLine("  mcp-cli alias set <name> <server-command>");
            Console.WriteLine();
            Console.WriteLine("Example:");
            Console.WriteLine("  mcp-cli alias set roslyn roslyn-codex");
            return;
        }

        Console.WriteLine("Server Aliases:");
        Console.WriteLine();

        // Calculate column width
        var maxAliasLength = aliases.Keys.Max(k => k.Length);
        var format = $"  {{0,-{maxAliasLength}}}  →  {{1}}";

        foreach (var (alias, command) in aliases.OrderBy(kvp => kvp.Key))
        {
            Console.WriteLine(format, alias, command);
        }

        Console.WriteLine();
        Console.WriteLine($"Config: {ConfigService.GetConfigPath()}");
    }

    /// <summary>
    /// Set an alias.
    /// </summary>
    public static void ExecuteSet(string alias, string serverCommand)
    {
        // Validate alias name (no spaces, reasonable chars)
        if (string.IsNullOrWhiteSpace(alias))
        {
            Console.Error.WriteLine("Error: Alias name cannot be empty.");
            Environment.ExitCode = 1;
            return;
        }

        if (alias.Contains(' '))
        {
            Console.Error.WriteLine("Error: Alias name cannot contain spaces.");
            Environment.ExitCode = 1;
            return;
        }

        if (string.IsNullOrWhiteSpace(serverCommand))
        {
            Console.Error.WriteLine("Error: Server command cannot be empty.");
            Environment.ExitCode = 1;
            return;
        }

        // Check if updating existing
        var existing = ConfigService.GetAliases();
        var isUpdate = existing.ContainsKey(alias);

        ConfigService.SetAlias(alias, serverCommand);

        if (isUpdate)
        {
            Console.WriteLine($"Updated alias '{alias}' → {serverCommand}");
        }
        else
        {
            Console.WriteLine($"Created alias '{alias}' → {serverCommand}");
        }
    }

    /// <summary>
    /// Remove an alias.
    /// </summary>
    public static void ExecuteRemove(string alias)
    {
        if (ConfigService.RemoveAlias(alias))
        {
            Console.WriteLine($"Removed alias '{alias}'");
        }
        else
        {
            Console.Error.WriteLine($"Error: Alias '{alias}' not found.");
            Environment.ExitCode = 1;
        }
    }
}
