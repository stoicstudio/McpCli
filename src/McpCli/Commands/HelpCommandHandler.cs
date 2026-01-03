using System.Text.Json;
using McpCli.Formatting;
using McpCli.Protocol;
using McpCli.Services;

namespace McpCli.Commands;

/// <summary>
/// Handler for the 'help' command - gets detailed help for a specific tool.
/// </summary>
public static class HelpCommandHandler
{
    public static async Task ExecuteAsync(string server, string toolName, int timeout, bool verbose, bool quiet, string output)
    {
        // Resolve alias if applicable
        server = ConfigService.ResolveServer(server);
        var (command, args) = ServerParser.ParseServerCommand(server);

        if (verbose)
        {
            Console.Error.WriteLine($"Starting MCP server: {command} {string.Join(" ", args)}");
        }

        await using var client = new McpClientService
        {
            CallTimeout = TimeSpan.FromSeconds(timeout),
            Verbose = verbose
        };

        // Start server
        var started = await client.StartServerAsync(command, args);
        if (!started)
        {
            Console.Error.WriteLine(OutputFormatter.FormatError($"Failed to start server: {server}"));
            Environment.ExitCode = 1;
            return;
        }

        if (verbose)
        {
            Console.Error.WriteLine("Initializing MCP protocol...");
        }

        try
        {
            // Initialize
            await client.InitializeAsync();

            // List tools to find the one we want
            if (verbose)
            {
                Console.Error.WriteLine($"Looking up tool: {toolName}");
            }

            var toolsResult = await client.ListToolsAsync();

            // Find the tool (case-insensitive)
            var tool = toolsResult.Tools.FirstOrDefault(t =>
                string.Equals(t.Name, toolName, StringComparison.OrdinalIgnoreCase));

            if (tool is null)
            {
                Console.Error.WriteLine(OutputFormatter.FormatError($"Tool not found: {toolName}"));
                Console.Error.WriteLine();
                Console.Error.WriteLine("Available tools:");
                foreach (var t in toolsResult.Tools.OrderBy(x => x.Name))
                {
                    Console.Error.WriteLine($"  {t.Name}");
                }
                Environment.ExitCode = 1;
                return;
            }

            // Format and output
            if (output == "json")
            {
                Console.WriteLine(JsonSerializer.Serialize(tool, new JsonSerializerOptions { WriteIndented = true }));
            }
            else
            {
                Console.WriteLine(OutputFormatter.FormatToolHelp(tool, quiet));
            }
        }
        catch (McpException ex)
        {
            Console.Error.WriteLine(OutputFormatter.FormatError(ex));
            Environment.ExitCode = 1;
        }
        catch (TimeoutException ex)
        {
            Console.Error.WriteLine(OutputFormatter.FormatError(ex.Message));
            Environment.ExitCode = 1;
        }
    }
}
