# MCP CLI - Design Document

> **Status**: Phase 1 - Initial Implementation
> **Created**: 2025-12-30
> **Purpose**: A .NET tool providing a command-line interface for interacting with Model Context Protocol (MCP) servers

## Overview

MCP CLI (`mcp-cli`) is a cross-platform .NET tool that enables direct interaction with MCP servers from the command line. Like the Magus wielding the wand to direct elemental forces, this tool channels the power of MCP servers through the terminal.

### Goals

| Goal | Description |
|------|-------------|
| **Universal Client** | Connect to any MCP server via stdio transport |
| **Tool Discovery** | List available tools and their schemas |
| **Tool Invocation** | Call tools with typed arguments |
| **Batch Operations** | Execute multiple commands in sequence |
| **Developer Friendly** | Intuitive CLI syntax and helpful output |

### Non-Goals (Phase 1)

- Persistent connections (each invocation starts fresh)
- SSE/HTTP transport (stdio only)
- Resource/prompt support (tools only for now)
- Interactive REPL mode

---

## Architecture

```
┌─────────────────────────────────────────────────────────────┐
│                     CLI Layer (Program.cs)                   │
│  - Argument parsing (System.CommandLine)                    │
│  - Command routing (list, help, call, batch)                │
└─────────────────────────────┬───────────────────────────────┘
                              │
┌─────────────────────────────▼───────────────────────────────┐
│                   McpClientService                           │
│  - Process spawning and lifecycle                           │
│  - JSON-RPC message framing                                 │
│  - Request/response correlation                             │
└─────────────────────────────┬───────────────────────────────┘
                              │
        ┌─────────────────────┼─────────────────────┐
        │                     │                     │
┌───────▼───────┐    ┌────────▼────────┐    ┌──────▼──────┐
│ JsonRpcCodec  │    │ OutputFormatter │    │ ArgParser   │
│ (Serialize)   │    │ (Pretty print)  │    │ (key=value) │
└───────────────┘    └─────────────────┘    └─────────────┘
```

### Core Components

#### 1. McpClientService

The heart of the system - manages MCP server processes and JSON-RPC communication.

```csharp
public class McpClientService : IAsyncDisposable
{
    // Start an MCP server process
    Task<bool> StartServerAsync(string command, string[] args, CancellationToken ct);

    // Protocol handshake
    Task<InitializeResult> InitializeAsync(CancellationToken ct);

    // Tool operations
    Task<ToolListResult> ListToolsAsync(CancellationToken ct);
    Task<ToolCallResult> CallToolAsync(string name, Dictionary<string, object?> args, CancellationToken ct);

    // Lifecycle
    ValueTask DisposeAsync();
}
```

#### 2. JsonRpcCodec

Handles MCP's JSON-RPC 2.0 message format (newline-delimited JSON).

```csharp
public static class JsonRpcCodec
{
    // Serialize request to JSON line
    string SerializeRequest(int id, string method, object? @params);

    // Parse response from JSON line
    JsonRpcResponse ParseResponse(string line);
}
```

#### 3. ArgumentParser

Parses CLI arguments in the `key=value` format with type coercion.

```csharp
public static class ArgumentParser
{
    // Parse ["foo=bar", "count=42", "enabled=true"] into dictionary
    Dictionary<string, object?> ParseToolArguments(string[] args);
}
```

#### 4. OutputFormatter

Formats MCP responses for human-readable terminal output.

```csharp
public static class OutputFormatter
{
    // Format tools/list as table
    string FormatToolList(ToolListResult tools);

    // Format tool help with parameters
    string FormatToolHelp(ToolInfo tool);

    // Format tool call result
    string FormatToolResult(ToolCallResult result);
}
```

---

## CLI Commands

### List Tools

```bash
# List all available tools
mcp-cli list <server-command>

# Short form
mcp-cli <server-command>
```

**Example:**
```bash
$ mcp-cli list roslyn-codex
  Tool                          Description
  ----------------------------- --------------------------------------------------
  roslyn_get_status             Get workspace status and loaded project info
  roslyn_find_type              Find types by name or pattern
  roslyn_get_type_info          Get detailed information about a type
  ...
```

### Tool Help

```bash
# Get help for specific tool
mcp-cli help <server-command> <tool-name>

# Short forms
mcp-cli <server-command> ? <tool-name>
mcp-cli <server-command> <tool-name> --help
```

**Example:**
```bash
$ mcp-cli roslyn-codex ? roslyn_find_type
roslyn_find_type
================

Find types matching a pattern. Supports wildcards (*), regex (/pattern/), and short names.

Parameters:
  pattern [string] (required)
    The search pattern. Examples: "*Service", "MyApp.Models.*", "/^I[A-Z]/"

  accessibility [string]
    Filter by accessibility: public, internal, private, protected
    Default: null

  scope [string]
    Filter by scope: all, solution, project:{name}
    Default: solution

Example:
  mcp-cli roslyn-codex roslyn_find_type pattern="*Service"
```

### Call Tool

```bash
# Call a tool with arguments
mcp-cli call <server-command> <tool-name> [arg=value ...]

# Short form (implicit call)
mcp-cli <server-command> <tool-name> [arg=value ...]
```

**Example:**
```bash
$ mcp-cli roslyn-codex roslyn_find_type pattern="*Service" scope="solution"
Found 15 types matching '*Service':
  - MyApp.Services.UserService (src/Services/UserService.cs:12)
  - MyApp.Services.AuthService (src/Services/AuthService.cs:8)
  ...
```

### Batch Mode

```bash
# Execute multiple commands in sequence
mcp-cli batch <server-command> "<cmd1>" "<cmd2>" ...

# With delays between commands
mcp-cli batch <server-command> "tool1 arg=val" "wait:1000" "tool2"
```

**Example:**
```bash
$ mcp-cli batch roslyn-codex "roslyn_get_status" "wait:500" "roslyn_find_type pattern=*Service"
--- [roslyn_get_status] ---
Workspace: Transmutas.sln
Projects: 12
Types: 1,234

--- [roslyn_find_type] ---
Found 15 types matching '*Service':
...
```

---

## Data Models

### JSON-RPC Messages

```csharp
public record JsonRpcRequest(
    string Jsonrpc,  // Always "2.0"
    int Id,
    string Method,
    object? Params
);

public record JsonRpcResponse(
    string Jsonrpc,
    int Id,
    object? Result,
    JsonRpcError? Error
);

public record JsonRpcError(
    int Code,
    string Message,
    object? Data
);
```

### MCP Protocol Types

```csharp
public record InitializeParams(
    string ProtocolVersion,
    ClientCapabilities Capabilities,
    ClientInfo ClientInfo
);

public record ToolInfo(
    string Name,
    string? Description,
    JsonSchema InputSchema
);

public record ToolCallResult(
    IReadOnlyList<ContentItem> Content,
    bool? IsError
);

public record ContentItem(
    string Type,  // "text" or "image"
    string? Text,
    string? MimeType,
    string? Data
);
```

---

## Argument Type Coercion

The CLI supports smart type coercion for tool arguments:

| Input | Coerced Type | Value |
|-------|--------------|-------|
| `"hello"` | string | `"hello"` |
| `42` | int | `42` |
| `3.14` | double | `3.14` |
| `true` / `false` | bool | `true` / `false` |
| `null` | null | `null` |
| `[1,2,3]` | array | `[1, 2, 3]` |
| `{"key":"val"}` | object | `{"key": "val"}` |

**Examples:**
```bash
# String (no quotes needed for simple strings)
mcp-cli server tool name=John

# String with spaces (quotes required in shell)
mcp-cli server tool name="John Doe"

# Integer
mcp-cli server tool count=42

# Boolean
mcp-cli server tool enabled=true

# Array (JSON syntax)
mcp-cli server tool files='["a.cs","b.cs"]'

# Complex types (JSON syntax)
mcp-cli server tool config='{"timeout":30,"retries":3}'
```

---

## Server Process Management

### Startup Sequence

```
1. Parse CLI arguments
2. Spawn server process (command + args)
3. Connect to stdin/stdout
4. Send initialize request
5. Wait for initialize response
6. Execute requested operation
7. Clean shutdown
```

### Process Lifecycle

```csharp
// Spawn process with stdio redirection
var psi = new ProcessStartInfo
{
    FileName = command,
    Arguments = string.Join(" ", args),
    UseShellExecute = false,
    RedirectStandardInput = true,
    RedirectStandardOutput = true,
    RedirectStandardError = true,
    CreateNoWindow = true
};

var process = Process.Start(psi);

// JSON-RPC over stdio
await SendJsonRpcAsync(process.StandardInput, request);
var response = await ReadJsonRpcAsync(process.StandardOutput);

// Clean shutdown
process.StandardInput.Close();
process.WaitForExit(timeout);
if (!process.HasExited)
{
    process.Kill();
}
```

### Timeout Handling

| Operation | Default Timeout | Configurable |
|-----------|-----------------|--------------|
| Initialize | 10 seconds | `--init-timeout` |
| Tool call | 30 seconds | `--timeout` |
| Shutdown | 2 seconds | No |

---

## Project Structure

```
McpCli/
├── src/
│   └── McpCli/
│       ├── McpCli.csproj
│       ├── Program.cs                 # Entry point, CLI routing
│       ├── Commands/
│       │   ├── ListCommand.cs         # tools/list handler
│       │   ├── HelpCommand.cs         # Tool help display
│       │   ├── CallCommand.cs         # tools/call handler
│       │   └── BatchCommand.cs        # Batch execution
│       ├── Services/
│       │   └── McpClientService.cs    # Process & JSON-RPC management
│       ├── Protocol/
│       │   ├── JsonRpcCodec.cs        # Message serialization
│       │   ├── Messages.cs            # Request/response types
│       │   └── McpTypes.cs            # MCP-specific types
│       └── Formatting/
│           ├── ArgumentParser.cs      # key=value parsing
│           └── OutputFormatter.cs     # Result formatting
├── tests/
│   └── McpCli.Tests/
│       ├── McpCli.Tests.csproj
│       ├── ArgumentParserTests.cs
│       ├── OutputFormatterTests.cs
│       ├── JsonRpcCodecTests.cs
│       └── IntegrationTests.cs
├── .github/
│   └── workflows/
│       └── ci.yml
├── McpCli.sln
├── DESIGN.md
├── README.md
└── .gitignore
```

---

## Dependencies

| Package | Version | Purpose |
|---------|---------|---------|
| `System.CommandLine` | 2.0.0-beta4 | CLI argument parsing |
| `System.Text.Json` | (built-in) | JSON serialization |

**No external MCP SDK dependency** - we implement the minimal JSON-RPC client directly for simplicity and to avoid version conflicts.

---

## NuGet Package Configuration

```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <OutputType>Exe</OutputType>
    <TargetFramework>net9.0</TargetFramework>
    <ImplicitUsings>enable</ImplicitUsings>
    <Nullable>enable</Nullable>

    <!-- .NET Tool Packaging -->
    <PackAsTool>true</PackAsTool>
    <ToolCommandName>mcp-cli</ToolCommandName>
    <PackageId>mcp-cli</PackageId>
    <Authors>StoicStudio</Authors>
    <Description>A command-line interface for interacting with Model Context Protocol (MCP) servers. List tools, get help, and invoke tools from your terminal.</Description>
    <PackageProjectUrl>https://github.com/stoicstudio/McpCli</PackageProjectUrl>
    <RepositoryUrl>https://github.com/stoicstudio/McpCli</RepositoryUrl>
    <PackageLicenseExpression>MIT</PackageLicenseExpression>
    <PackageReadmeFile>README.md</PackageReadmeFile>
    <PackageTags>mcp;cli;model-context-protocol;ai;llm;claude;tools</PackageTags>
  </PropertyGroup>
</Project>
```

---

## CI/CD Pipeline

### GitHub Actions Workflow

```yaml
name: CI

on:
  push:
    branches: [ master, main ]
    tags:
      - 'v*'
  pull_request:
    branches: [ master, main ]

jobs:
  build-and-test:
    runs-on: windows-latest

    steps:
    - uses: actions/checkout@v4
    - uses: actions/setup-dotnet@v4
      with:
        dotnet-version: '9.0.x'

    - name: Restore
      run: dotnet restore

    - name: Build
      run: dotnet build --configuration Release --no-restore

    - name: Test
      run: dotnet test --configuration Release --no-build --verbosity normal

    - name: Pack (on tag)
      if: startsWith(github.ref, 'refs/tags/v')
      run: dotnet pack src/McpCli/McpCli.csproj --configuration Release --no-build -p:Version=${GITHUB_REF_NAME#v} --output ./artifacts

    - name: Upload artifact
      if: startsWith(github.ref, 'refs/tags/v')
      uses: actions/upload-artifact@v4
      with:
        name: nuget-package
        path: ./artifacts/*.nupkg

  publish:
    needs: build-and-test
    if: startsWith(github.ref, 'refs/tags/v')
    runs-on: windows-latest
    permissions:
      contents: read
      packages: write

    steps:
    - uses: actions/download-artifact@v4
      with:
        name: nuget-package
        path: ./artifacts

    - name: Push to GitHub Packages
      env:
        GITHUB_TOKEN: ${{ secrets.GITHUB_TOKEN }}
      run: |
        dotnet nuget push ./artifacts/*.nupkg \
          --source "https://nuget.pkg.github.com/stoicstudio/index.json" \
          --api-key $GITHUB_TOKEN \
          --skip-duplicate
```

### Release Process

1. Update `VersionInfo.cs` with new version
2. Update `CHANGELOG.md` with changes
3. Commit: `git commit -m "Release v1.0.0"`
4. Tag: `git tag v1.0.0`
5. Push: `git push origin main --tags`
6. CI automatically builds, tests, and publishes to GitHub Packages

---

## Testing Strategy

### Unit Tests

| Component | Test Coverage |
|-----------|---------------|
| `ArgumentParser` | Type coercion, edge cases, malformed input |
| `JsonRpcCodec` | Serialization, parsing, error handling |
| `OutputFormatter` | Table formatting, help display |

### Integration Tests

| Test | Description |
|------|-------------|
| List tools | Verify tools/list against real MCP server |
| Call tool | Verify tools/call and result formatting |
| Batch mode | Verify sequential execution with waits |
| Error handling | Verify graceful handling of server errors |

### Manual Testing

Test against connected MCP servers:
- `perforce-athanor` (Perforce MCP)
- `flash-platform-grimoire` (AS3 documentation MCP)
- `roslyn-codex` (when available - currently disconnected)

---

## Implementation Plan

### Phase 1: Core Infrastructure

- [x] Create design document (DESIGN.md)
- [ ] Create solution structure
- [ ] Implement `JsonRpcCodec` (serialize/parse)
- [ ] Implement `McpClientService` (process + communication)
- [ ] Implement basic `list` command
- [ ] Implement `help` command
- [ ] Implement `call` command
- [ ] Implement `ArgumentParser` with type coercion
- [ ] Basic unit tests

### Phase 2: Polish & Batch Mode

- [ ] Implement `batch` command with wait support
- [ ] Enhanced error messages
- [ ] Timeout configuration
- [ ] Output formatting improvements
- [ ] Integration tests with live servers

### Phase 3: Release

- [ ] Create README.md with installation/usage
- [ ] Configure CI/CD workflow
- [ ] Test release process
- [ ] Tag v0.1.0

### Future Phases

- [ ] REPL/interactive mode
- [ ] Server configuration file support
- [ ] Result filtering/piping
- [ ] Colored output
- [ ] Completion scripts (bash/zsh/PowerShell)

---

## Usage Examples

### Quick Start

```bash
# Install the tool
dotnet tool install --global mcp-cli

# List tools from an MCP server
mcp-cli roslyn-codex list

# Get help for a specific tool
mcp-cli roslyn-codex roslyn_find_type --help

# Call a tool
mcp-cli roslyn-codex roslyn_find_type pattern="*Controller"
```

### With Server Arguments

```bash
# Server that requires arguments
mcp-cli "roslyn-codex --solution C:/Projects/MyApp.sln" list
mcp-cli "roslyn-codex --solution C:/Projects/MyApp.sln" roslyn_find_type pattern="*"
```

### Batch Operations

```bash
# Multiple operations on one server instance
mcp-cli batch roslyn-codex \
  "roslyn_get_status" \
  "wait:500" \
  "roslyn_find_type pattern=*Service" \
  "roslyn_get_type_info fqn=MyApp.Services.UserService"
```

---

## References

- [MCP Specification](https://modelcontextprotocol.io/specification)
- [JSON-RPC 2.0 Specification](https://www.jsonrpc.org/specification)
- [.NET CLI Tools Documentation](https://learn.microsoft.com/en-us/dotnet/core/tools/global-tools)
- [System.CommandLine Documentation](https://learn.microsoft.com/en-us/dotnet/standard/commandline/)

---

*"As the Magician stands between the Above and the Below, channeling celestial forces into terrestrial form, so too does this CLI stand between the developer and the MCP server - transmuting protocol incantations into comprehensible wisdom."*
