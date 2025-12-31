using System.Text.Json;
using System.Text.RegularExpressions;

namespace McpCli.Formatting;

/// <summary>
/// Parses CLI arguments in key=value format with smart type coercion.
/// As the alchemist transforms base metals, this parser transmutes
/// string arguments into their proper elemental forms.
/// </summary>
public static partial class ArgumentParser
{
    /// <summary>
    /// Parse an array of "key=value" arguments into a dictionary with type coercion.
    /// </summary>
    /// <param name="args">Arguments in key=value format.</param>
    /// <param name="firstPositionalKey">Key to use for the first positional (non key=value) argument.</param>
    /// <returns>Dictionary of parsed arguments.</returns>
    public static Dictionary<string, object?> ParseToolArguments(
        string[] args,
        string? firstPositionalKey = "query")
    {
        var result = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);
        var positionalArgs = new List<string>();

        foreach (var arg in args)
        {
            // Check for boolean flag shorthand: --paramName or --param-name
            if (arg.StartsWith("--") && !arg.Contains('='))
            {
                var flagName = arg[2..]; // Remove leading --
                // Convert kebab-case to camelCase if needed (e.g., --show-details -> showDetails)
                flagName = KebabToCamelCase(flagName);
                result[flagName] = true;
                continue;
            }

            // Check for key=value pattern
            var equalsIndex = arg.IndexOf('=');
            if (equalsIndex > 0)
            {
                var key = arg[..equalsIndex];
                var value = arg[(equalsIndex + 1)..];
                result[key] = CoerceValue(value);
            }
            else
            {
                // Positional argument
                positionalArgs.Add(arg);
            }
        }

        // Map first positional arg to the specified key if not already set
        if (positionalArgs.Count > 0 &&
            !string.IsNullOrEmpty(firstPositionalKey) &&
            !result.ContainsKey(firstPositionalKey))
        {
            result[firstPositionalKey] = CoerceValue(positionalArgs[0]);
        }

        return result;
    }

    /// <summary>
    /// Coerce a string value to an appropriate .NET type.
    /// </summary>
    public static object? CoerceValue(string value)
    {
        // Handle null
        if (string.Equals(value, "null", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(value, "$null", StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        // Handle boolean
        if (string.Equals(value, "true", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(value, "$true", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        if (string.Equals(value, "false", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(value, "$false", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        // Handle integer
        if (IntegerRegex().IsMatch(value) && int.TryParse(value, out var intValue))
        {
            return intValue;
        }

        // Handle floating point
        if (FloatRegex().IsMatch(value) && double.TryParse(value, out var doubleValue))
        {
            return doubleValue;
        }

        // Handle JSON array
        if (value.StartsWith('[') && value.EndsWith(']'))
        {
            try
            {
                return JsonSerializer.Deserialize<JsonElement>(value);
            }
            catch
            {
                // Not valid JSON, treat as string
            }
        }

        // Handle JSON object
        if (value.StartsWith('{') && value.EndsWith('}'))
        {
            try
            {
                return JsonSerializer.Deserialize<JsonElement>(value);
            }
            catch
            {
                // Not valid JSON, treat as string
            }
        }

        // Default to string
        return value;
    }

    /// <summary>
    /// Parse a batch command string into tool name and arguments.
    /// </summary>
    /// <param name="commandString">Command string like "tool_name arg1=val1 arg2=val2"</param>
    /// <returns>Tuple of (toolName, arguments).</returns>
    public static (string? ToolName, Dictionary<string, object?> Arguments) ParseBatchCommand(string commandString)
    {
        var parts = SplitCommand(commandString);

        if (parts.Count == 0)
        {
            return (null, new Dictionary<string, object?>());
        }

        var toolName = parts[0];
        var argStrings = parts.Skip(1).ToArray();
        var arguments = ParseToolArguments(argStrings);

        return (toolName, arguments);
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
                    // End of quoted section
                    inQuotes = false;
                    quoteChar = '\0';
                }
                else if (!inQuotes)
                {
                    // Start of quoted section
                    inQuotes = true;
                    quoteChar = c;
                }
                else
                {
                    // Different quote inside quotes, treat as literal
                    current.Append(c);
                }
            }
            else if (c == ' ' && !inQuotes)
            {
                // Space outside quotes - separator
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

        // Add final part
        if (current.Length > 0)
        {
            parts.Add(current.ToString());
        }

        return parts;
    }

    /// <summary>
    /// Convert kebab-case to camelCase.
    /// </summary>
    private static string KebabToCamelCase(string input)
    {
        if (string.IsNullOrEmpty(input) || !input.Contains('-'))
        {
            return input;
        }

        var parts = input.Split('-');
        var result = parts[0];
        for (var i = 1; i < parts.Length; i++)
        {
            if (parts[i].Length > 0)
            {
                result += char.ToUpperInvariant(parts[i][0]) + parts[i][1..];
            }
        }
        return result;
    }

    [GeneratedRegex(@"^-?\d+$")]
    private static partial Regex IntegerRegex();

    [GeneratedRegex(@"^-?\d+\.\d+$")]
    private static partial Regex FloatRegex();
}
