using System.Text.Json;
using McpCli.Formatting;
using McpCli.Protocol;
using McpCli.Services;

namespace McpCli.Commands;

/// <summary>
/// Handler for the 'call' command - invokes a tool with arguments.
/// </summary>
public static class CallCommandHandler
{
    public static async Task ExecuteAsync(string server, string toolName, string[] toolArgs, int timeout, bool verbose, bool quiet, string output)
    {
        // quiet parameter reserved for future use (call output is already minimal)
        _ = quiet;
        var (command, args) = ServerParser.ParseServerCommand(server);

        if (verbose)
        {
            Console.Error.WriteLine($"Starting MCP server: {command} {string.Join(" ", args)}");
        }

        await using var client = new McpClientService
        {
            CallTimeout = TimeSpan.FromSeconds(timeout)
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

            // Parse arguments
            var arguments = ArgumentParser.ParseToolArguments(toolArgs);

            if (verbose)
            {
                Console.Error.WriteLine($"Calling: {toolName} {System.Text.Json.JsonSerializer.Serialize(arguments)}");
            }

            // Call tool
            var result = await client.CallToolAsync(toolName, arguments);

            // Format and output
            if (output == "json")
            {
                Console.WriteLine(JsonSerializer.Serialize(result, new JsonSerializerOptions { WriteIndented = true }));
            }
            else
            {
                Console.WriteLine(OutputFormatter.FormatToolResult(result));
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
