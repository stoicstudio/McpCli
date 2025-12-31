using System.Text;
using McpCli.Protocol;

namespace McpCli.Formatting;

/// <summary>
/// Formats MCP responses for human-readable terminal output.
/// As the scribe illuminates the sacred texts, this formatter
/// reveals the hidden structure of the MCP responses.
/// </summary>
public static class OutputFormatter
{
    /// <summary>
    /// Format a tools/list result as a table.
    /// </summary>
    /// <param name="result">The tool list result.</param>
    /// <param name="quiet">If true, output only tool names (one per line).</param>
    public static string FormatToolList(ToolListResult result, bool quiet = false)
    {
        var tools = result.Tools.OrderBy(t => t.Name).ToList();

        if (tools.Count == 0)
        {
            return quiet ? "" : "No tools available.";
        }

        // Quiet mode: just tool names
        if (quiet)
        {
            return string.Join(Environment.NewLine, tools.Select(t => t.Name));
        }

        var sb = new StringBuilder();

        // Calculate column widths
        var maxNameLen = tools.Max(t => t.Name.Length);
        var nameWidth = Math.Max(maxNameLen + 2, 30);
        var consoleWidth = GetConsoleWidth();
        var descWidth = Math.Max(consoleWidth - nameWidth - 4, 30);

        // Header
        sb.AppendLine();
        sb.Append("  ");
        sb.Append("Tool".PadRight(nameWidth));
        sb.AppendLine("Description");

        sb.Append("  ");
        sb.Append(new string('-', nameWidth - 1));
        sb.Append(' ');
        sb.AppendLine(new string('-', descWidth));

        // Tools
        foreach (var tool in tools)
        {
            sb.Append("  ");
            sb.Append(tool.Name.PadRight(nameWidth));

            var desc = tool.Description ?? "";
            desc = desc.Replace("\n", " ").Replace("\r", "");
            desc = CollapseWhitespace(desc);

            if (desc.Length > descWidth)
            {
                desc = desc[..(descWidth - 3)] + "...";
            }

            sb.AppendLine(desc);
        }

        sb.AppendLine();
        sb.AppendLine($"Use 'mcp-cli help <server> <tool>' for detailed help");

        return sb.ToString();
    }

    /// <summary>
    /// Format detailed help for a single tool.
    /// </summary>
    /// <param name="tool">The tool info.</param>
    /// <param name="quiet">If true, output minimal format (name and params only).</param>
    public static string FormatToolHelp(ToolInfo tool, bool quiet = false)
    {
        var sb = new StringBuilder();

        // Quiet mode: minimal output
        if (quiet)
        {
            sb.AppendLine(tool.Name);
            if (tool.InputSchema?.Properties is { Count: > 0 } props)
            {
                var required = tool.InputSchema.Required ?? new List<string>();
                foreach (var (name, schema) in props)
                {
                    var req = required.Contains(name) ? "*" : "";
                    sb.AppendLine($"  {name}{req}:{schema.Type ?? "any"}");
                }
            }
            return sb.ToString().TrimEnd();
        }

        // Title
        sb.AppendLine(tool.Name);
        sb.AppendLine(new string('=', tool.Name.Length));
        sb.AppendLine();

        // Description
        if (!string.IsNullOrEmpty(tool.Description))
        {
            sb.AppendLine(tool.Description);
            sb.AppendLine();
        }

        // Parameters
        if (tool.InputSchema?.Properties is { Count: > 0 } properties)
        {
            var required = tool.InputSchema.Required ?? new List<string>();

            sb.AppendLine("Parameters:");
            foreach (var (name, schema) in properties)
            {
                var isRequired = required.Contains(name);
                var reqMark = isRequired ? " (required)" : "";
                var typeStr = !string.IsNullOrEmpty(schema.Type) ? $"[{schema.Type}]" : "";

                sb.AppendLine($"  {name} {typeStr}{reqMark}");

                if (!string.IsNullOrEmpty(schema.Description))
                {
                    var desc = CollapseWhitespace(schema.Description);
                    sb.AppendLine($"    {desc}");
                }

                if (schema.Default is not null)
                {
                    sb.AppendLine($"    Default: {schema.Default}");
                }

                if (schema.Enum is { Count: > 0 })
                {
                    sb.AppendLine($"    Values: {string.Join(", ", schema.Enum)}");
                }

                sb.AppendLine();
            }
        }

        // Example
        sb.AppendLine("Example:");
        var exampleArgs = "";
        if (tool.InputSchema?.Properties is { Count: > 0 } exProps)
        {
            var firstProp = exProps.FirstOrDefault();
            if (!string.IsNullOrEmpty(firstProp.Key))
            {
                exampleArgs = $" {firstProp.Key}=\"value\"";
            }
        }
        sb.AppendLine($"  mcp-cli call <server> {tool.Name}{exampleArgs}");

        return sb.ToString();
    }

    /// <summary>
    /// Format a tool call result.
    /// </summary>
    public static string FormatToolResult(ToolCallResult result)
    {
        var sb = new StringBuilder();

        foreach (var content in result.Content)
        {
            if (content.Type == "text" && !string.IsNullOrEmpty(content.Text))
            {
                sb.AppendLine(content.Text);
            }
            else if (content.Type == "image")
            {
                sb.AppendLine($"[Image: {content.MimeType ?? "unknown"}]");
            }
        }

        if (result.IsError == true)
        {
            // Prefix with error indicator if not already obvious
            var text = sb.ToString();
            if (!text.StartsWith("Error", StringComparison.OrdinalIgnoreCase))
            {
                return $"Error: {text}";
            }
        }

        return sb.ToString().TrimEnd();
    }

    /// <summary>
    /// Format an error message.
    /// </summary>
    public static string FormatError(string message)
    {
        return $"Error: {message}";
    }

    /// <summary>
    /// Format an MCP exception.
    /// </summary>
    public static string FormatError(McpException ex)
    {
        return $"MCP Error [{ex.Code}]: {ex.Message}";
    }

    /// <summary>
    /// Collapse multiple whitespace characters into single spaces.
    /// </summary>
    private static string CollapseWhitespace(string text)
    {
        return System.Text.RegularExpressions.Regex.Replace(text, @"\s+", " ").Trim();
    }

    /// <summary>
    /// Get the console width, defaulting to 100 if unavailable.
    /// </summary>
    private static int GetConsoleWidth()
    {
        try
        {
            return Console.WindowWidth;
        }
        catch
        {
            return 100;
        }
    }
}
