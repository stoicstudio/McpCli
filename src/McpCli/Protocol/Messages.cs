using System.Text.Json.Serialization;

namespace McpCli.Protocol;

/// <summary>
/// JSON-RPC 2.0 request message.
/// </summary>
public record JsonRpcRequest(
    [property: JsonPropertyName("jsonrpc")] string Jsonrpc,
    [property: JsonPropertyName("id")] int Id,
    [property: JsonPropertyName("method")] string Method,
    [property: JsonPropertyName("params")] object? Params = null
)
{
    public static JsonRpcRequest Create(int id, string method, object? @params = null)
        => new("2.0", id, method, @params);
}

/// <summary>
/// JSON-RPC 2.0 response message.
/// </summary>
public record JsonRpcResponse
{
    [JsonPropertyName("jsonrpc")]
    public string Jsonrpc { get; init; } = "2.0";

    [JsonPropertyName("id")]
    public int Id { get; init; }

    [JsonPropertyName("result")]
    public object? Result { get; init; }

    [JsonPropertyName("error")]
    public JsonRpcError? Error { get; init; }

    public bool IsSuccess => Error is null;
}

/// <summary>
/// JSON-RPC 2.0 error object.
/// </summary>
public record JsonRpcError(
    [property: JsonPropertyName("code")] int Code,
    [property: JsonPropertyName("message")] string Message,
    [property: JsonPropertyName("data")] object? Data = null
);
