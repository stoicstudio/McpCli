using System.Text.Json;
using McpCli.Formatting;
using McpCli.Protocol;
using McpCli.Services;

namespace McpCli.Commands;

/// <summary>
/// Handler for the 'list' command - lists all available tools from an MCP server.
/// </summary>
public static class ListCommandHandler
{
    public static async Task ExecuteAsync(string server, int timeout, bool verbose, bool quiet, string output)
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
            var initResult = await client.InitializeAsync();

            if (verbose && initResult.ServerInfo is not null)
            {
                Console.Error.WriteLine($"Server: {initResult.ServerInfo.Name} v{initResult.ServerInfo.Version}");
            }

            // List tools
            if (verbose)
            {
                Console.Error.WriteLine("Fetching tools...");
            }

            var toolsResult = await client.ListToolsAsync();

            // Format and output
            if (output == "json")
            {
                Console.WriteLine(JsonSerializer.Serialize(toolsResult, new JsonSerializerOptions { WriteIndented = true }));
            }
            else
            {
                Console.WriteLine(OutputFormatter.FormatToolList(toolsResult, quiet));
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
