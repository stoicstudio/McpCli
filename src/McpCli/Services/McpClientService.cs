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
    private Process? _process;
    private StreamWriter? _stdin;
    private StreamReader? _stdout;
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
        if (_process is not null)
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
            _process = Process.Start(psi);
            if (_process is null)
            {
                return false;
            }

            _stdin = _process.StandardInput;
            _stdout = _process.StandardOutput;

            // Give the process a moment to start
            await Task.Delay(100, ct);

            return !_process.HasExited;
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
    private async Task<T> SendRequestAsync<T>(
        string method,
        object @params,
        TimeSpan timeout,
        CancellationToken ct) where T : class
    {
        var id = Interlocked.Increment(ref _requestId);
        var requestJson = JsonRpcCodec.SerializeRequest(id, method, @params);

        // Send request
        await _stdin!.WriteLineAsync(requestJson.AsMemory(), ct);
        await _stdin.FlushAsync(ct);

        // Read response with timeout
        using var timeoutCts = new CancellationTokenSource(timeout);
        using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(ct, timeoutCts.Token);

        try
        {
            var responseLine = await ReadLineAsync(linkedCts.Token);
            if (string.IsNullOrEmpty(responseLine))
            {
                throw new McpException(-1, "Empty response from server");
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
    /// Read a line from the server's stdout with cancellation support.
    /// </summary>
    private async Task<string?> ReadLineAsync(CancellationToken ct)
    {
        // Use Task.Run to make ReadLineAsync cancellable
        var readTask = _stdout!.ReadLineAsync(ct);
        return await readTask.AsTask();
    }

    /// <summary>
    /// Escape an argument for command-line use.
    /// </summary>
    private static string EscapeArgument(string arg)
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
        if (_process is null || _process.HasExited)
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

        if (_stdin is not null)
        {
            try
            {
                _stdin.Close();
            }
            catch { }
        }

        if (_process is not null)
        {
            try
            {
                // Give the process a moment to exit gracefully
                await Task.Delay(100);

                if (!_process.HasExited)
                {
                    _process.Kill(entireProcessTree: true);
                }

                _process.Dispose();
            }
            catch { }
        }
    }
}
