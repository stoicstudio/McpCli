using System.CommandLine;
using System.CommandLine.Builder;
using System.CommandLine.Parsing;
using McpCli.Commands;
using McpCli.Services;

namespace McpCli;

/// <summary>
/// MCP CLI - Command-line interface for Model Context Protocol servers.
///
/// As the Magician channels the forces Above to manifest Below,
/// this tool channels MCP server capabilities to the terminal.
/// </summary>
public static class Program
{
    public static async Task<int> Main(string[] args)
    {
        var rootCommand = new RootCommand("MCP CLI - Command-line interface for Model Context Protocol servers")
        {
            Name = "mcp-cli"
        };

        // Common options
        var timeoutOption = new Option<int>("--timeout", () => 30, "Timeout in seconds for tool calls");
        var verboseOption = new Option<bool>("--verbose", () => false, "Show verbose output including server messages");
        var quietOption = new Option<bool>(["--quiet", "-q"], () => false, "Minimal output, suitable for scripting");
        var outputOption = new Option<string>("--output", () => "text", "Output format: 'text' (default) or 'json'");
        outputOption.AddValidator(result =>
        {
            var value = result.GetValueOrDefault<string>();
            if (value != "text" && value != "json")
            {
                result.ErrorMessage = "Output format must be 'text' or 'json'";
            }
        });

        // === ALIAS COMMAND ===
        var aliasCommand = new Command("alias", "Manage server aliases");

        // alias list
        var aliasListCommand = new Command("list", "List all configured aliases");
        aliasListCommand.SetHandler(AliasCommandHandler.ExecuteList);
        aliasCommand.AddCommand(aliasListCommand);

        // alias set
        var aliasSetCommand = new Command("set", "Set a server alias");
        var aliasNameArg = new Argument<string>("name", "Alias name (e.g., 'flash')");
        var aliasServerArg = new Argument<string>("server", "Server command (e.g., 'flash-platform-grimoire')");
        aliasSetCommand.AddArgument(aliasNameArg);
        aliasSetCommand.AddArgument(aliasServerArg);
        aliasSetCommand.SetHandler(AliasCommandHandler.ExecuteSet, aliasNameArg, aliasServerArg);
        aliasCommand.AddCommand(aliasSetCommand);

        // alias remove
        var aliasRemoveCommand = new Command("remove", "Remove a server alias");
        var aliasRemoveNameArg = new Argument<string>("name", "Alias name to remove");
        aliasRemoveCommand.AddArgument(aliasRemoveNameArg);
        aliasRemoveCommand.SetHandler(AliasCommandHandler.ExecuteRemove, aliasRemoveNameArg);
        aliasCommand.AddCommand(aliasRemoveCommand);

        // Default alias handler (shows list)
        aliasCommand.SetHandler(AliasCommandHandler.ExecuteList);
        rootCommand.AddCommand(aliasCommand);

        // === LIST COMMAND ===
        var listCommand = new Command("list", "List all available tools from the server");
        var listServerArg = new Argument<string>("server", "MCP server command (e.g., 'roslyn-codex' or 'dotnet run --project /path')");
        listCommand.AddArgument(listServerArg);
        listCommand.AddOption(timeoutOption);
        listCommand.AddOption(verboseOption);
        listCommand.AddOption(quietOption);
        listCommand.AddOption(outputOption);
        listCommand.SetHandler(ListCommandHandler.ExecuteAsync, listServerArg, timeoutOption, verboseOption, quietOption, outputOption);
        rootCommand.AddCommand(listCommand);

        // === HELP COMMAND ===
        var helpToolCommand = new Command("help", "Get detailed help for a specific tool");
        var helpServerArg = new Argument<string>("server", "MCP server command");
        var toolNameArg = new Argument<string>("tool", "Tool name to get help for");
        helpToolCommand.AddArgument(helpServerArg);
        helpToolCommand.AddArgument(toolNameArg);
        helpToolCommand.AddOption(timeoutOption);
        helpToolCommand.AddOption(verboseOption);
        helpToolCommand.AddOption(quietOption);
        helpToolCommand.AddOption(outputOption);
        helpToolCommand.SetHandler(HelpCommandHandler.ExecuteAsync, helpServerArg, toolNameArg, timeoutOption, verboseOption, quietOption, outputOption);
        rootCommand.AddCommand(helpToolCommand);

        // === CALL COMMAND ===
        var callCommand = new Command("call", "Call a tool with arguments");
        var callServerArg = new Argument<string>("server", "MCP server command");
        var callToolArg = new Argument<string>("tool", "Tool name to call");
        var callArgsArg = new Argument<string[]>("args", () => Array.Empty<string>(), "Tool arguments in key=value format");
        var stdinOption = new Option<string?>("--stdin", "Read stdin and assign to this parameter name");
        callCommand.AddArgument(callServerArg);
        callCommand.AddArgument(callToolArg);
        callCommand.AddArgument(callArgsArg);
        callCommand.AddOption(timeoutOption);
        callCommand.AddOption(verboseOption);
        callCommand.AddOption(quietOption);
        callCommand.AddOption(outputOption);
        callCommand.AddOption(stdinOption);
        callCommand.SetHandler(CallCommandHandler.ExecuteAsync, callServerArg, callToolArg, callArgsArg, timeoutOption, verboseOption, quietOption, outputOption, stdinOption);
        rootCommand.AddCommand(callCommand);

        // === BATCH COMMAND ===
        var batchCommand = new Command("batch", "Execute multiple tool calls in sequence");
        var batchServerArg = new Argument<string>("server", "MCP server command");
        var batchCommandsArg = new Argument<string[]>("commands", "Commands to execute (e.g., 'tool1 arg=val' 'wait:1000' 'tool2')");
        batchCommand.AddArgument(batchServerArg);
        batchCommand.AddArgument(batchCommandsArg);
        batchCommand.AddOption(timeoutOption);
        batchCommand.AddOption(verboseOption);
        batchCommand.AddOption(quietOption);
        batchCommand.SetHandler(BatchCommandHandler.ExecuteAsync, batchServerArg, batchCommandsArg, timeoutOption, verboseOption, quietOption);
        rootCommand.AddCommand(batchCommand);

        // === DEFAULT HANDLER ===
        rootCommand.SetHandler(() =>
        {
            Console.WriteLine("Usage: mcp-cli <command> <server> [options]");
            Console.WriteLine();
            Console.WriteLine("Commands:");
            Console.WriteLine("  list <server>              List all available tools");
            Console.WriteLine("  help <server> <tool>       Get detailed help for a tool");
            Console.WriteLine("  call <server> <tool>       Call a tool with arguments");
            Console.WriteLine("  batch <server> <commands>  Execute multiple commands");
            Console.WriteLine("  alias                      Manage server aliases");
            Console.WriteLine();
            Console.WriteLine("Examples:");
            Console.WriteLine("  mcp-cli list roslyn-codex");
            Console.WriteLine("  mcp-cli help roslyn-codex roslyn_find_type");
            Console.WriteLine("  mcp-cli call roslyn-codex roslyn_find_type pattern=\"*Service\"");
            Console.WriteLine("  mcp-cli batch roslyn-codex \"roslyn_get_status\" \"wait:500\" \"roslyn_find_type pattern=*\"");
            Console.WriteLine();
            Console.WriteLine("Aliases:");
            Console.WriteLine("  mcp-cli alias set roslyn roslyn-codex");
            Console.WriteLine("  mcp-cli call roslyn roslyn_find_type pattern=\"*Service\"");
            Console.WriteLine();
            Console.WriteLine("Pipe input:");
            Console.WriteLine("  echo \"Sprite\" | mcp-cli call flash get_api_reference --stdin query");
        });

        var parser = new CommandLineBuilder(rootCommand)
            .UseDefaults()
            .UseExceptionHandler((ex, ctx) =>
            {
                Console.Error.WriteLine($"Error: {ex.Message}");
                ctx.ExitCode = 1;
            })
            .Build();

        return await parser.InvokeAsync(args);
    }
}
