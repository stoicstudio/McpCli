using McpCli.Protocol;
using System.Text.Json;

namespace McpCli.Tests;

/// <summary>
/// Tests for JsonRpcCodec - the scribe's art of encoding and decoding messages.
/// </summary>
public class JsonRpcCodecTests
{
    [Fact]
    public void SerializeRequest_BasicRequest_ProducesValidJson()
    {
        var json = JsonRpcCodec.SerializeRequest(1, "test/method");

        Assert.Contains("\"jsonrpc\":\"2.0\"", json);
        Assert.Contains("\"id\":1", json);
        Assert.Contains("\"method\":\"test/method\"", json);
    }

    [Fact]
    public void SerializeRequest_WithParams_IncludesParams()
    {
        var @params = new { name = "test", count = 42 };
        var json = JsonRpcCodec.SerializeRequest(1, "test/method", @params);

        Assert.Contains("\"params\":", json);
        Assert.Contains("\"name\":\"test\"", json);
        Assert.Contains("\"count\":42", json);
    }

    [Fact]
    public void SerializeRequest_NoParams_OmitsParams()
    {
        var json = JsonRpcCodec.SerializeRequest(1, "test/method");

        // params should be null and omitted from serialization
        Assert.DoesNotContain("\"params\"", json);
    }

    [Fact]
    public void SerializeRequest_UsesCompactJson()
    {
        var json = JsonRpcCodec.SerializeRequest(1, "test/method");

        // Should be single line (no newlines)
        Assert.DoesNotContain("\n", json);
    }

    [Fact]
    public void ParseResponse_SuccessResponse_ParsesCorrectly()
    {
        var json = "{\"jsonrpc\":\"2.0\",\"id\":1,\"result\":{\"value\":42}}";

        var response = JsonRpcCodec.ParseResponse(json);

        Assert.Equal("2.0", response.Jsonrpc);
        Assert.Equal(1, response.Id);
        Assert.Null(response.Error);
        Assert.True(response.IsSuccess);
    }

    [Fact]
    public void ParseResponse_ErrorResponse_ParsesCorrectly()
    {
        var json = "{\"jsonrpc\":\"2.0\",\"id\":1,\"error\":{\"code\":-32600,\"message\":\"Invalid Request\"}}";

        var response = JsonRpcCodec.ParseResponse(json);

        Assert.NotNull(response.Error);
        Assert.Equal(-32600, response.Error.Code);
        Assert.Equal("Invalid Request", response.Error.Message);
        Assert.False(response.IsSuccess);
    }

    [Fact]
    public void GetResult_SuccessResponse_ReturnsTypedResult()
    {
        var json = "{\"jsonrpc\":\"2.0\",\"id\":1,\"result\":{\"tools\":[{\"name\":\"test\"}]}}";
        var response = JsonRpcCodec.ParseResponse(json);

        var result = JsonRpcCodec.GetResult<ToolListResult>(response);

        Assert.NotNull(result);
        Assert.Single(result.Tools);
        Assert.Equal("test", result.Tools[0].Name);
    }

    [Fact]
    public void GetResult_ErrorResponse_ThrowsMcpException()
    {
        var json = "{\"jsonrpc\":\"2.0\",\"id\":1,\"error\":{\"code\":-32600,\"message\":\"Invalid Request\"}}";
        var response = JsonRpcCodec.ParseResponse(json);

        var ex = Assert.Throws<McpException>(() => JsonRpcCodec.GetResult<ToolListResult>(response));

        Assert.Equal(-32600, ex.Code);
        Assert.Equal("Invalid Request", ex.Message);
    }

    [Fact]
    public void GetResult_NullResult_ThrowsJsonException()
    {
        var json = "{\"jsonrpc\":\"2.0\",\"id\":1}";
        var response = JsonRpcCodec.ParseResponse(json);

        Assert.Throws<JsonException>(() => JsonRpcCodec.GetResult<ToolListResult>(response));
    }

    [Fact]
    public void ParseResponse_InvalidJson_ThrowsException()
    {
        Assert.Throws<JsonException>(() => JsonRpcCodec.ParseResponse("not valid json"));
    }

    [Fact]
    public void McpException_FormatsCorrectly()
    {
        var ex = new McpException(-32600, "Invalid Request");

        Assert.Equal("MCP Error -32600: Invalid Request", ex.ToString());
    }
}
