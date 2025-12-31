using System.Text.Json;
using System.Text.Json.Serialization;

namespace McpCli.Protocol;

/// <summary>
/// Handles JSON-RPC 2.0 message serialization and parsing.
/// MCP uses newline-delimited JSON over stdio.
/// </summary>
public static class JsonRpcCodec
{
    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = false
    };

    private static readonly JsonSerializerOptions DeserializerOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    /// <summary>
    /// Serialize a JSON-RPC request to a single-line JSON string.
    /// </summary>
    public static string SerializeRequest(JsonRpcRequest request)
    {
        return JsonSerializer.Serialize(request, SerializerOptions);
    }

    /// <summary>
    /// Serialize a JSON-RPC request with the given parameters.
    /// </summary>
    public static string SerializeRequest(int id, string method, object? @params = null)
    {
        var request = JsonRpcRequest.Create(id, method, @params);
        return SerializeRequest(request);
    }

    /// <summary>
    /// Parse a JSON-RPC response from a JSON string.
    /// </summary>
    public static JsonRpcResponse ParseResponse(string json)
    {
        return JsonSerializer.Deserialize<JsonRpcResponse>(json, DeserializerOptions)
            ?? throw new JsonException("Failed to parse JSON-RPC response");
    }

    /// <summary>
    /// Extract a typed result from a JSON-RPC response.
    /// </summary>
    public static T GetResult<T>(JsonRpcResponse response) where T : class
    {
        if (response.Error is not null)
        {
            throw new McpException(response.Error.Code, response.Error.Message, response.Error.Data);
        }

        if (response.Result is null)
        {
            throw new JsonException("Response has no result");
        }

        // Response.Result is a JsonElement when deserialized
        if (response.Result is JsonElement element)
        {
            return element.Deserialize<T>(DeserializerOptions)
                ?? throw new JsonException($"Failed to deserialize result as {typeof(T).Name}");
        }

        throw new JsonException($"Unexpected result type: {response.Result.GetType().Name}");
    }
}

/// <summary>
/// Exception thrown when an MCP server returns an error.
/// </summary>
public class McpException : Exception
{
    public int Code { get; }
    public object? ErrorData { get; }

    public McpException(int code, string message, object? data = null)
        : base(message)
    {
        Code = code;
        ErrorData = data;
    }

    public override string ToString()
    {
        return $"MCP Error {Code}: {Message}";
    }
}
