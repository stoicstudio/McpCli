using System.Text.Json.Serialization;

namespace McpCli.Protocol;

/// <summary>
/// MCP initialize request parameters.
/// </summary>
public record InitializeParams
{
    [JsonPropertyName("protocolVersion")]
    public string ProtocolVersion { get; init; } = "2024-11-05";

    [JsonPropertyName("capabilities")]
    public ClientCapabilities Capabilities { get; init; } = new();

    [JsonPropertyName("clientInfo")]
    public ClientInfo ClientInfo { get; init; } = new();
}

/// <summary>
/// Client capabilities (empty for now - we're a simple CLI).
/// </summary>
public record ClientCapabilities;

/// <summary>
/// Client identification info.
/// </summary>
public record ClientInfo
{
    [JsonPropertyName("name")]
    public string Name { get; init; } = "mcp-cli";

    [JsonPropertyName("version")]
    public string Version { get; init; } = "1.0.0";
}

/// <summary>
/// MCP initialize response result.
/// </summary>
public record InitializeResult
{
    [JsonPropertyName("protocolVersion")]
    public string? ProtocolVersion { get; init; }

    [JsonPropertyName("capabilities")]
    public ServerCapabilities? Capabilities { get; init; }

    [JsonPropertyName("serverInfo")]
    public ServerInfo? ServerInfo { get; init; }
}

/// <summary>
/// Server capabilities.
/// </summary>
public record ServerCapabilities
{
    [JsonPropertyName("tools")]
    public ToolsCapability? Tools { get; init; }
}

/// <summary>
/// Tools capability (indicates server supports tools).
/// </summary>
public record ToolsCapability;

/// <summary>
/// Server identification info.
/// </summary>
public record ServerInfo
{
    [JsonPropertyName("name")]
    public string? Name { get; init; }

    [JsonPropertyName("version")]
    public string? Version { get; init; }
}

/// <summary>
/// tools/list response result.
/// </summary>
public record ToolListResult
{
    [JsonPropertyName("tools")]
    public List<ToolInfo> Tools { get; init; } = new();
}

/// <summary>
/// Tool definition from the server.
/// </summary>
public record ToolInfo
{
    [JsonPropertyName("name")]
    public string Name { get; init; } = "";

    [JsonPropertyName("description")]
    public string? Description { get; init; }

    [JsonPropertyName("inputSchema")]
    public JsonSchema? InputSchema { get; init; }
}

/// <summary>
/// JSON Schema for tool parameters.
/// </summary>
public record JsonSchema
{
    [JsonPropertyName("type")]
    public string? Type { get; init; }

    [JsonPropertyName("properties")]
    public Dictionary<string, JsonSchemaProperty>? Properties { get; init; }

    [JsonPropertyName("required")]
    public List<string>? Required { get; init; }
}

/// <summary>
/// JSON Schema property definition.
/// </summary>
public record JsonSchemaProperty
{
    [JsonPropertyName("type")]
    public string? Type { get; init; }

    [JsonPropertyName("description")]
    public string? Description { get; init; }

    [JsonPropertyName("default")]
    public object? Default { get; init; }

    [JsonPropertyName("enum")]
    public List<string>? Enum { get; init; }
}

/// <summary>
/// tools/call request parameters.
/// </summary>
public record ToolCallParams
{
    [JsonPropertyName("name")]
    public string Name { get; init; } = "";

    [JsonPropertyName("arguments")]
    public Dictionary<string, object?> Arguments { get; init; } = new();
}

/// <summary>
/// tools/call response result.
/// </summary>
public record ToolCallResult
{
    [JsonPropertyName("content")]
    public List<ContentItem> Content { get; init; } = new();

    [JsonPropertyName("isError")]
    public bool? IsError { get; init; }
}

/// <summary>
/// Content item in a tool result.
/// </summary>
public record ContentItem
{
    [JsonPropertyName("type")]
    public string Type { get; init; } = "text";

    [JsonPropertyName("text")]
    public string? Text { get; init; }

    [JsonPropertyName("mimeType")]
    public string? MimeType { get; init; }

    [JsonPropertyName("data")]
    public string? Data { get; init; }
}
