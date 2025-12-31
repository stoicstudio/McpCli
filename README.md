# mcp-cli

A command-line interface for interacting with Model Context Protocol (MCP) servers.

## Overview

`mcp-cli` is a .NET tool that enables direct interaction with MCP servers from the command line. It supports:

- **Listing tools** - Discover what tools are available on any MCP server
- **Getting help** - View detailed documentation for specific tools
- **Calling tools** - Invoke tools with typed arguments
- **Batch operations** - Execute multiple commands in sequence

## Installation

mcp-cli is distributed as a [.NET tool](https://learn.microsoft.com/en-us/dotnet/core/tools/global-tools) via GitHub Packages.

### Prerequisites

- .NET 9.0 SDK or later

### Global Tool (Recommended)

```bash
# One-time: Add GitHub Packages source
dotnet nuget add source https://nuget.pkg.github.com/stoicstudio/index.json \
  --name stoic \
  --username YOUR_GITHUB_USERNAME \
  --password $(gh auth token)

# Install the tool
dotnet tool install --global mcp-cli
```

The tool is installed to `~/.dotnet/tools` and added to your PATH. You can run it from anywhere:

```bash
mcp-cli --help
```

### Updating

```bash
dotnet tool update --global mcp-cli
```

## Usage

### List Tools

List all available tools from an MCP server:

```bash
mcp-cli list <server>
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

### Get Tool Help

Get detailed help for a specific tool:

```bash
mcp-cli help <server> <tool>
```

**Example:**
```bash
$ mcp-cli help roslyn-codex roslyn_find_type
roslyn_find_type
================

Find types matching a pattern. Supports wildcards (*), regex (/pattern/), and short names.

Parameters:
  pattern [string] (required)
    The search pattern. Examples: "*Service", "MyApp.Models.*", "/^I[A-Z]/"
  ...
```

### Call a Tool

Call a tool with arguments:

```bash
mcp-cli call <server> <tool> [arg=value ...]
```

**Arguments:**
- Strings: `name=John`
- Integers: `count=42`
- Booleans: `enabled=true`
- Arrays: `files='["a.cs","b.cs"]'`
- Objects: `config='{"timeout":30}'`

**Example:**
```bash
$ mcp-cli call roslyn-codex roslyn_find_type pattern="*Controller"
Found 5 types matching '*Controller':
  - MyApp.Controllers.HomeController (src/Controllers/HomeController.cs:12)
  - MyApp.Controllers.ApiController (src/Controllers/ApiController.cs:8)
  ...
```

### Batch Mode

Execute multiple commands on a single server instance:

```bash
mcp-cli batch <server> "<cmd1>" "<cmd2>" ...
```

Use `wait:<ms>` between commands to add delays:

**Example:**
```bash
$ mcp-cli batch roslyn-codex "roslyn_get_status" "wait:500" "roslyn_find_type pattern=*Service"
--- [roslyn_get_status] ---
Workspace: MyApp.sln
Projects: 5

--- [roslyn_find_type] ---
Found 3 types matching '*Service':
...
```

### Options

| Option | Description |
|--------|-------------|
| `--timeout <seconds>` | Timeout for tool calls (default: 30) |
| `--verbose` | Show verbose output including server messages |

## Server Commands

The `<server>` argument is the command to start the MCP server:

```bash
# Simple command (installed as global tool)
mcp-cli list roslyn-codex

# Command with arguments
mcp-cli list "roslyn-codex --solution /path/to/sln"

# Dotnet run
mcp-cli list "dotnet run --project /path/to/mcp/project"
```

## Tested MCP Servers

mcp-cli has been tested with:

- [perforce-athanor](https://github.com/stoicstudio/PerforceAthanor.Mcp) - Perforce MCP server
- [flash-platform-grimoire](https://github.com/stoicstudio/FlashPlatformGrimoire.Mcp) - ActionScript 3 documentation MCP
- [roslyn-codex](https://github.com/stoicstudio/RoslynCodex.Mcp) - C# semantic analysis MCP

## Development

```bash
# Clone and build
git clone https://github.com/stoicstudio/McpCli.git
cd McpCli
dotnet build

# Run tests
dotnet test

# Run directly from source
dotnet run --project src/McpCli -- list <server>
```

## Architecture

```
mcp-cli
├── Commands/          # CLI command handlers
├── Services/          # MCP client service
├── Protocol/          # JSON-RPC and MCP types
└── Formatting/        # Argument parsing and output formatting
```

## License

MIT

## Links

- [GitHub Repository](https://github.com/stoicstudio/McpCli)
- [Model Context Protocol](https://modelcontextprotocol.io/)
- [MCP Specification](https://modelcontextprotocol.io/specification)
