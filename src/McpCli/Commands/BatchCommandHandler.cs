using System.Text.RegularExpressions;
using McpCli.Formatting;
using McpCli.Protocol;
using McpCli.Services;

namespace McpCli.Commands;

/// <summary>
/// Handler for the 'batch' command - executes multiple tool calls in sequence.
/// Like a ritual with multiple invocations, each command flows into the next.
/// </summary>
public static partial class BatchCommandHandler
{
    public static async Task ExecuteAsync(string server, string[] commands, int timeout, bool verbose, bool quiet)
    {
        if (commands.Length == 0)
        {
            Console.Error.WriteLine(OutputFormatter.FormatError("No commands provided"));
            Console.Error.WriteLine("Usage: mcp-cli batch <server> \"tool1 arg=val\" \"wait:1000\" \"tool2\"");
            Environment.ExitCode = 1;
            return;
        }

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

            if (!quiet)
            {
                Console.WriteLine($"Batch mode: {commands.Length} command(s)");
            }

            foreach (var cmd in commands)
            {
                // Handle wait command
                var waitMatch = WaitCommandRegex().Match(cmd);
                if (waitMatch.Success)
                {
                    var waitMs = int.Parse(waitMatch.Groups[1].Value);
                    if (verbose)
                    {
                        Console.Error.WriteLine($"  Waiting {waitMs}ms...");
                    }
                    await Task.Delay(waitMs);
                    continue;
                }

                // Parse and execute tool command
                var (toolName, arguments) = ArgumentParser.ParseBatchCommand(cmd);
                if (string.IsNullOrEmpty(toolName))
                {
                    if (!quiet)
                    {
                        Console.Error.WriteLine("  Skipping empty command");
                    }
                    continue;
                }

                if (!quiet)
                {
                    Console.WriteLine();
                    Console.WriteLine($"--- [{toolName}] ---");
                }

                if (verbose)
                {
                    Console.Error.WriteLine($"Calling: {toolName} {System.Text.Json.JsonSerializer.Serialize(arguments)}");
                }

                try
                {
                    var result = await client.CallToolAsync(toolName, arguments);
                    Console.WriteLine(OutputFormatter.FormatToolResult(result));
                }
                catch (McpException ex)
                {
                    Console.Error.WriteLine(OutputFormatter.FormatError(ex));
                    // Continue with next command in batch
                }
                catch (TimeoutException ex)
                {
                    Console.Error.WriteLine(OutputFormatter.FormatError(ex.Message));
                    // Continue with next command in batch
                }
            }

            if (!quiet)
            {
                Console.WriteLine();
                Console.WriteLine("--- Batch complete ---");
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

    [GeneratedRegex(@"^wait:(\d+)$", RegexOptions.IgnoreCase)]
    private static partial Regex WaitCommandRegex();
}
