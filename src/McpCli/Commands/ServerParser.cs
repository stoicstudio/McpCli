namespace McpCli.Commands;

/// <summary>
/// Parses server command strings into command and arguments.
/// </summary>
public static class ServerParser
{
    /// <summary>
    /// Parse a server command string into the executable command and its arguments.
    /// </summary>
    /// <param name="serverCommand">
    /// Server command string. Can be:
    /// - Simple command: "roslyn-codex"
    /// - Command with args: "roslyn-codex --solution C:/path/to/sln"
    /// - Dotnet run: "dotnet run --project /path/to/project"
    /// </param>
    /// <returns>Tuple of (command, arguments array).</returns>
    public static (string Command, string[] Args) ParseServerCommand(string serverCommand)
    {
        var parts = SplitCommand(serverCommand);

        if (parts.Count == 0)
        {
            return (serverCommand, Array.Empty<string>());
        }

        var command = parts[0];
        var args = parts.Skip(1).ToArray();

        return (command, args);
    }

    /// <summary>
    /// Split a command string by spaces, respecting quoted strings.
    /// </summary>
    private static List<string> SplitCommand(string input)
    {
        var parts = new List<string>();
        var current = new System.Text.StringBuilder();
        var inQuotes = false;
        var quoteChar = '\0';

        for (var i = 0; i < input.Length; i++)
        {
            var c = input[i];

            if (c == '"' || c == '\'')
            {
                if (inQuotes && c == quoteChar)
                {
                    inQuotes = false;
                    quoteChar = '\0';
                }
                else if (!inQuotes)
                {
                    inQuotes = true;
                    quoteChar = c;
                }
                else
                {
                    current.Append(c);
                }
            }
            else if (c == ' ' && !inQuotes)
            {
                if (current.Length > 0)
                {
                    parts.Add(current.ToString());
                    current.Clear();
                }
            }
            else
            {
                current.Append(c);
            }
        }

        if (current.Length > 0)
        {
            parts.Add(current.ToString());
        }

        return parts;
    }
}
