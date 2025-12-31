namespace McpCli.Services;

/// <summary>
/// Abstraction for MCP transport layer.
/// Enables mocking for high-value unit tests of the JSON-RPC flow.
/// </summary>
public interface IMcpTransport : IAsyncDisposable
{
    /// <summary>
    /// Send a line to the server.
    /// </summary>
    Task SendLineAsync(string line, CancellationToken ct = default);

    /// <summary>
    /// Read a line from the server.
    /// </summary>
    Task<string?> ReadLineAsync(CancellationToken ct = default);

    /// <summary>
    /// Whether the transport is connected.
    /// </summary>
    bool IsConnected { get; }
}

/// <summary>
/// Process-based transport that spawns an MCP server process.
/// </summary>
public sealed class ProcessTransport : IMcpTransport
{
    private readonly System.Diagnostics.Process _process;
    private readonly StreamWriter _stdin;
    private readonly StreamReader _stdout;

    public bool IsConnected => !_process.HasExited;

    public ProcessTransport(System.Diagnostics.Process process)
    {
        _process = process;
        _stdin = process.StandardInput;
        _stdout = process.StandardOutput;
    }

    public async Task SendLineAsync(string line, CancellationToken ct = default)
    {
        await _stdin.WriteLineAsync(line.AsMemory(), ct);
        await _stdin.FlushAsync(ct);
    }

    public async Task<string?> ReadLineAsync(CancellationToken ct = default)
    {
        return await _stdout.ReadLineAsync(ct);
    }

    public async ValueTask DisposeAsync()
    {
        try
        {
            _stdin.Close();
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

/// <summary>
/// In-memory transport for testing. Simulates server responses.
/// </summary>
public sealed class MockTransport : IMcpTransport
{
    private readonly Queue<string> _responses = new();
    private readonly List<string> _sentMessages = new();

    public bool IsConnected { get; set; } = true;

    /// <summary>
    /// Messages sent by the client.
    /// </summary>
    public IReadOnlyList<string> SentMessages => _sentMessages;

    /// <summary>
    /// Queue a response to be returned by ReadLineAsync.
    /// </summary>
    public void QueueResponse(string response) => _responses.Enqueue(response);

    public Task SendLineAsync(string line, CancellationToken ct = default)
    {
        _sentMessages.Add(line);
        return Task.CompletedTask;
    }

    public Task<string?> ReadLineAsync(CancellationToken ct = default)
    {
        if (_responses.Count > 0)
        {
            return Task.FromResult<string?>(_responses.Dequeue());
        }
        return Task.FromResult<string?>(null);
    }

    public ValueTask DisposeAsync() => ValueTask.CompletedTask;
}
