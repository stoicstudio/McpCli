using System.Diagnostics;
using System.Text;
using McpCli.Protocol;

namespace McpCli.Services;

/// <summary>
/// Manages an MCP server process and handles JSON-RPC communication.
/// Like the Charioteer controlling the steeds of spirit and matter,
/// this service directs the flow of messages between CLI and server.
/// </summary>
public sealed class McpClientService : IAsyncDisposable
{
    private IMcpTransport? _transport;
    private int _requestId;
    private bool _disposed;

    /// <summary>
    /// Default timeout for initialization (10 seconds).
    /// </summary>
    public TimeSpan InitTimeout { get; set; } = TimeSpan.FromSeconds(10);

    /// <summary>
    /// Default timeout for tool calls (30 seconds).
    /// </summary>
    public TimeSpan CallTimeout { get; set; } = TimeSpan.FromSeconds(30);

    /// <summary>
    /// Whether to print verbose debug output (raw JSON-RPC messages).
    /// </summary>
    public bool Verbose { get; set; }

    /// <summary>
    /// Create a client that will start a server process.
    /// </summary>
    public McpClientService()
    {
    }

    /// <summary>
    /// Create a client with an injected transport (for testing).
    /// </summary>
    public McpClientService(IMcpTransport transport)
    {
        _transport = transport;
    }

    /// <summary>
    /// Start an MCP server process.
    /// </summary>
    /// <param name="command">The command to execute (e.g., "roslyn-codex").</param>
    /// <param name="args">Arguments to pass to the command.</param>
    /// <param name="workingDirectory">Optional working directory.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>True if the process started successfully.</returns>
    public async Task<bool> StartServerAsync(
        string command,
        string[] args,
        string? workingDirectory = null,
        CancellationToken ct = default)
    {
        if (_transport is not null)
        {
            throw new InvalidOperationException("Server already started");
        }

        var psi = new ProcessStartInfo
        {
            FileName = command,
            Arguments = string.Join(" ", args.Select(EscapeArgument)),
            UseShellExecute = false,
            RedirectStandardInput = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true,
            StandardOutputEncoding = Encoding.UTF8,
            StandardInputEncoding = Encoding.UTF8
        };

        if (!string.IsNullOrEmpty(workingDirectory))
        {
            psi.WorkingDirectory = workingDirectory;
        }

        try
        {
            var process = Process.Start(psi);
            if (process is null)
            {
                return false;
            }

            // Give the process a moment to start
            await Task.Delay(100, ct);

            if (process.HasExited)
            {
                return false;
            }

            _transport = new ProcessTransport(process);
            return true;
        }
        catch (Exception)
        {
            return false;
        }
    }

    /// <summary>
    /// Perform the MCP initialization handshake.
    /// </summary>
    public async Task<InitializeResult> InitializeAsync(CancellationToken ct = default)
    {
        EnsureStarted();

        var initParams = new InitializeParams();
        var response = await SendRequestAsync<InitializeResult>(
            "initialize",
            initParams,
            InitTimeout,
            ct);

        return response;
    }

    /// <summary>
    /// List all available tools from the server.
    /// </summary>
    public async Task<ToolListResult> ListToolsAsync(CancellationToken ct = default)
    {
        EnsureStarted();

        return await SendRequestAsync<ToolListResult>(
            "tools/list",
            new { },
            CallTimeout,
            ct);
    }

    /// <summary>
    /// Call a tool with the specified arguments.
    /// </summary>
    /// <param name="name">Tool name.</param>
    /// <param name="arguments">Tool arguments.</param>
    /// <param name="timeout">Optional custom timeout for this call.</param>
    /// <param name="ct">Cancellation token.</param>
    public async Task<ToolCallResult> CallToolAsync(
        string name,
        Dictionary<string, object?> arguments,
        TimeSpan? timeout = null,
        CancellationToken ct = default)
    {
        EnsureStarted();

        var callParams = new ToolCallParams
        {
            Name = name,
            Arguments = arguments
        };

        return await SendRequestAsync<ToolCallResult>(
            "tools/call",
            callParams,
            timeout ?? CallTimeout,
            ct);
    }

    /// <summary>
    /// Send a JSON-RPC request and wait for the response.
    /// </summary>
    internal async Task<T> SendRequestAsync<T>(
        string method,
        object @params,
        TimeSpan timeout,
        CancellationToken ct) where T : class
    {
        var id = Interlocked.Increment(ref _requestId);
        var requestJson = JsonRpcCodec.SerializeRequest(id, method, @params);

        // Print verbose output (raw request)
        if (Verbose)
        {
            Console.Error.WriteLine(Formatting.OutputFormatter.FormatVerbose(">>> ", requestJson));
        }

        // Send request
        await _transport!.SendLineAsync(requestJson, ct);

        // Read response with timeout
        using var timeoutCts = new CancellationTokenSource(timeout);
        using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(ct, timeoutCts.Token);

        try
        {
            var responseLine = await _transport.ReadLineAsync(linkedCts.Token);
            if (string.IsNullOrEmpty(responseLine))
            {
                throw new McpException(-1, "Empty response from server");
            }

            // Print verbose output (raw response)
            if (Verbose)
            {
                Console.Error.WriteLine(Formatting.OutputFormatter.FormatVerbose("<<< ", responseLine));
            }

            var response = JsonRpcCodec.ParseResponse(responseLine);

            if (response.Id != id)
            {
                throw new McpException(-1, $"Response ID mismatch: expected {id}, got {response.Id}");
            }

            return JsonRpcCodec.GetResult<T>(response);
        }
        catch (OperationCanceledException) when (timeoutCts.IsCancellationRequested)
        {
            throw new TimeoutException($"Timeout waiting for response to {method}");
        }
    }

    /// <summary>
    /// Escape an argument for command-line use.
    /// </summary>
    internal static string EscapeArgument(string arg)
    {
        if (string.IsNullOrEmpty(arg))
        {
            return "\"\"";
        }

        // If it contains spaces or quotes, wrap in quotes and escape inner quotes
        if (arg.Contains(' ') || arg.Contains('"'))
        {
            return "\"" + arg.Replace("\"", "\\\"") + "\"";
        }

        return arg;
    }

    private void EnsureStarted()
    {
        if (_transport is null || !_transport.IsConnected)
        {
            throw new InvalidOperationException("Server not started or has exited");
        }
    }

    public async ValueTask DisposeAsync()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;

        if (_transport is not null)
        {
            await _transport.DisposeAsync();
        }
    }
}
