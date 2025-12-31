using McpCli.Protocol;
using McpCli.Services;
using System.Text.Json;

namespace McpCli.Tests;

/// <summary>
/// Tests for McpClientService using MockTransport.
/// High ROI: Tests the core JSON-RPC flow without spawning real processes.
/// Like testing the Charioteer's commands without actual horses.
/// </summary>
public class McpClientServiceTests
{
    [Fact]
    public async Task InitializeAsync_SendsCorrectRequest_ReturnsResult()
    {
        // Arrange
        var transport = new MockTransport();
        var expectedResponse = JsonSerializer.Serialize(new
        {
            jsonrpc = "2.0",
            id = 1,
            result = new
            {
                protocolVersion = "2024-11-05",
                serverInfo = new { name = "test-server", version = "1.0.0" },
                capabilities = new { tools = new { } }
            }
        });
        transport.QueueResponse(expectedResponse);

        await using var client = new McpClientService(transport);

        // Act
        var result = await client.InitializeAsync();

        // Assert
        Assert.NotNull(result);
        Assert.Equal("2024-11-05", result.ProtocolVersion);
        Assert.Equal("test-server", result.ServerInfo?.Name);
        Assert.Single(transport.SentMessages);

        var sentRequest = JsonSerializer.Deserialize<JsonRpcRequest>(transport.SentMessages[0]);
        Assert.Equal("initialize", sentRequest?.Method);
    }

    [Fact]
    public async Task ListToolsAsync_SendsCorrectRequest_ReturnsTools()
    {
        // Arrange
        var transport = new MockTransport();
        var expectedResponse = JsonSerializer.Serialize(new
        {
            jsonrpc = "2.0",
            id = 1,
            result = new
            {
                tools = new[]
                {
                    new
                    {
                        name = "test_tool",
                        description = "A test tool",
                        inputSchema = new { type = "object", properties = new { } }
                    }
                }
            }
        });
        transport.QueueResponse(expectedResponse);

        await using var client = new McpClientService(transport);

        // Act
        var result = await client.ListToolsAsync();

        // Assert
        Assert.NotNull(result);
        Assert.Single(result.Tools);
        Assert.Equal("test_tool", result.Tools[0].Name);
        Assert.Equal("A test tool", result.Tools[0].Description);

        var sentRequest = JsonSerializer.Deserialize<JsonRpcRequest>(transport.SentMessages[0]);
        Assert.Equal("tools/list", sentRequest?.Method);
    }

    [Fact]
    public async Task CallToolAsync_SendsCorrectRequest_ReturnsResult()
    {
        // Arrange
        var transport = new MockTransport();
        var expectedResponse = JsonSerializer.Serialize(new
        {
            jsonrpc = "2.0",
            id = 1,
            result = new
            {
                content = new[]
                {
                    new { type = "text", text = "Tool output" }
                }
            }
        });
        transport.QueueResponse(expectedResponse);

        await using var client = new McpClientService(transport);

        var args = new Dictionary<string, object?>
        {
            ["param1"] = "value1",
            ["param2"] = 42
        };

        // Act
        var result = await client.CallToolAsync("test_tool", args);

        // Assert
        Assert.NotNull(result);
        Assert.Single(result.Content);
        Assert.Equal("text", result.Content[0].Type);
        Assert.Equal("Tool output", result.Content[0].Text);

        var sentRequest = JsonSerializer.Deserialize<JsonRpcRequest>(transport.SentMessages[0]);
        Assert.Equal("tools/call", sentRequest?.Method);
    }

    [Fact]
    public async Task CallToolAsync_WithErrorResponse_ThrowsMcpException()
    {
        // Arrange
        var transport = new MockTransport();
        var errorResponse = JsonSerializer.Serialize(new
        {
            jsonrpc = "2.0",
            id = 1,
            error = new
            {
                code = -32600,
                message = "Invalid Request"
            }
        });
        transport.QueueResponse(errorResponse);

        await using var client = new McpClientService(transport);

        // Act & Assert
        var ex = await Assert.ThrowsAsync<McpException>(
            () => client.CallToolAsync("bad_tool", new Dictionary<string, object?>()));

        Assert.Equal(-32600, ex.Code);
        Assert.Equal("Invalid Request", ex.Message);
    }

    [Fact]
    public async Task SendRequestAsync_EmptyResponse_ThrowsMcpException()
    {
        // Arrange
        var transport = new MockTransport();
        // Queue null/empty response
        transport.QueueResponse(null!);

        await using var client = new McpClientService(transport);

        // Act & Assert
        await Assert.ThrowsAsync<McpException>(() => client.ListToolsAsync());
    }

    [Fact]
    public async Task SendRequestAsync_Timeout_ThrowsTimeoutException()
    {
        // Arrange
        var transport = new SlowMockTransport(TimeSpan.FromSeconds(5));
        await using var client = new McpClientService(transport);
        client.CallTimeout = TimeSpan.FromMilliseconds(100);

        // Act & Assert
        await Assert.ThrowsAsync<TimeoutException>(
            () => client.CallToolAsync("slow_tool", new Dictionary<string, object?>()));
    }

    [Fact]
    public async Task SendRequestAsync_IdMismatch_ThrowsMcpException()
    {
        // Arrange
        var transport = new MockTransport();
        var badResponse = JsonSerializer.Serialize(new
        {
            jsonrpc = "2.0",
            id = 999, // Wrong ID
            result = new { tools = Array.Empty<object>() }
        });
        transport.QueueResponse(badResponse);

        await using var client = new McpClientService(transport);

        // Act & Assert
        var ex = await Assert.ThrowsAsync<McpException>(() => client.ListToolsAsync());
        Assert.Contains("ID mismatch", ex.Message);
    }

    [Fact]
    public async Task CallToolAsync_MultipleContentItems_ReturnsAll()
    {
        // Arrange
        var transport = new MockTransport();
        var expectedResponse = JsonSerializer.Serialize(new
        {
            jsonrpc = "2.0",
            id = 1,
            result = new
            {
                content = new[]
                {
                    new { type = "text", text = "First item" },
                    new { type = "text", text = "Second item" }
                }
            }
        });
        transport.QueueResponse(expectedResponse);

        await using var client = new McpClientService(transport);

        // Act
        var result = await client.CallToolAsync("multi_output", new Dictionary<string, object?>());

        // Assert
        Assert.Equal(2, result.Content.Count);
        Assert.Equal("First item", result.Content[0].Text);
        Assert.Equal("Second item", result.Content[1].Text);
    }

    [Fact]
    public async Task EnsureStarted_WhenDisconnected_Throws()
    {
        // Arrange
        var transport = new MockTransport { IsConnected = false };
        await using var client = new McpClientService(transport);

        // Act & Assert
        await Assert.ThrowsAsync<InvalidOperationException>(() => client.ListToolsAsync());
    }

    [Fact]
    public async Task MultipleRequests_IncrementIds()
    {
        // Arrange
        var transport = new MockTransport();

        // Queue responses for two calls
        transport.QueueResponse(JsonSerializer.Serialize(new
        {
            jsonrpc = "2.0",
            id = 1,
            result = new { tools = Array.Empty<object>() }
        }));
        transport.QueueResponse(JsonSerializer.Serialize(new
        {
            jsonrpc = "2.0",
            id = 2,
            result = new { tools = Array.Empty<object>() }
        }));

        await using var client = new McpClientService(transport);

        // Act
        await client.ListToolsAsync();
        await client.ListToolsAsync();

        // Assert
        Assert.Equal(2, transport.SentMessages.Count);

        var request1 = JsonSerializer.Deserialize<JsonRpcRequest>(transport.SentMessages[0]);
        var request2 = JsonSerializer.Deserialize<JsonRpcRequest>(transport.SentMessages[1]);

        Assert.Equal(1, request1?.Id);
        Assert.Equal(2, request2?.Id);
    }
}

/// <summary>
/// A mock transport that delays responses to test timeouts.
/// </summary>
public sealed class SlowMockTransport : IMcpTransport
{
    private readonly TimeSpan _delay;

    public SlowMockTransport(TimeSpan delay) => _delay = delay;

    public bool IsConnected => true;

    public Task SendLineAsync(string line, CancellationToken ct = default) => Task.CompletedTask;

    public async Task<string?> ReadLineAsync(CancellationToken ct = default)
    {
        await Task.Delay(_delay, ct);
        return null;
    }

    public ValueTask DisposeAsync() => ValueTask.CompletedTask;
}
