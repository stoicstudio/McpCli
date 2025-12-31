<#
.SYNOPSIS
    Test an MCP tool by calling it directly via the MCP protocol.

.PARAMETER ToolName
    The name of the tool to call (e.g., "get_api", "search_as3_documentation")
    Use "--list" to list all available tools, or "--list <tool>" for tool help.

.EXAMPLE
    ./test-mcp-tool.ps1 --list

.EXAMPLE
    ./test-mcp-tool.ps1 --list get_api

.EXAMPLE
    ./test-mcp-tool.ps1 get_api --help

.EXAMPLE
    ./test-mcp-tool.ps1 get_api query="(top-level)"

.EXAMPLE
    ./test-mcp-tool.ps1 get_api query="flash.display.Sprite" description=true

.EXAMPLE
    # Batch mode: multiple commands on one server instance
    ./test-mcp-tool.ps1 --batch "get_index_status" "wait:1000" "get_logs mode=tail lines=5"

.EXAMPLE
    # Batch mode with waits between commands
    ./test-mcp-tool.ps1 --batch "get_index_status" "wait:500" "get_logs mode=tail"
#>
param(
    [Parameter(ValueFromRemainingArguments=$true)]
    [string[]]$AllArgs
)

# Parse arguments manually to handle --list, --help, --batch, etc.
$listAll = $false
$helpTool = $null
$callTool = $null
$toolArgs = @()
$batchMode = $false
$batchCommands = @()

if (-not $AllArgs -or $AllArgs.Count -eq 0) {
    $listAll = $true
} elseif ($AllArgs[0] -eq "--batch" -or $AllArgs[0] -eq "-b") {
    # Batch mode: multiple commands on one server instance
    $batchMode = $true
    $batchCommands = $AllArgs | Select-Object -Skip 1
    if ($batchCommands.Count -eq 0) {
        Write-Host "Error: --batch requires at least one command" -ForegroundColor Red
        Write-Host "Usage: ./test-mcp-tool.ps1 --batch `"tool1 arg=val`" `"wait:1000`" `"tool2`"" -ForegroundColor Yellow
        exit 1
    }
} elseif ($AllArgs[0] -eq "--list" -or $AllArgs[0] -eq "-l" -or $AllArgs[0] -eq "list" -or $AllArgs[0] -eq "?") {
    if ($AllArgs.Count -gt 1) {
        # --list <tool> => show help for specific tool
        $helpTool = $AllArgs[1]
    } else {
        $listAll = $true
    }
} elseif ($AllArgs.Count -ge 2 -and ($AllArgs[1] -eq "--help" -or $AllArgs[1] -eq "-h" -or $AllArgs[1] -eq "?")) {
    # <tool> --help => show help for tool
    $helpTool = $AllArgs[0]
} else {
    # <tool> [args...] => call tool
    $callTool = $AllArgs[0]
    $toolArgs = $AllArgs | Select-Object -Skip 1
}

$ErrorActionPreference = "Stop"

# Helper function to parse a command string into tool name and arguments hashtable
function Parse-ToolCommand {
    param([string]$CommandString)

    # Split by spaces, respecting quoted strings
    $parts = @()
    $current = ""
    $inQuotes = $false
    $quoteChar = $null

    for ($i = 0; $i -lt $CommandString.Length; $i++) {
        $char = $CommandString[$i]
        if ($char -eq '"' -or $char -eq "'") {
            if ($inQuotes -and $char -eq $quoteChar) {
                $inQuotes = $false
                $quoteChar = $null
            } elseif (-not $inQuotes) {
                $inQuotes = $true
                $quoteChar = $char
            } else {
                $current += $char
            }
        } elseif ($char -eq ' ' -and -not $inQuotes) {
            if ($current) {
                $parts += $current
                $current = ""
            }
        } else {
            $current += $char
        }
    }
    if ($current) { $parts += $current }

    if ($parts.Count -eq 0) {
        return @{ ToolName = $null; Arguments = @{} }
    }

    $toolName = $parts[0]
    $args = @{}
    $positionalArgs = @()

    for ($i = 1; $i -lt $parts.Count; $i++) {
        $arg = $parts[$i]
        if ($arg -match '^([^=]+)=(.*)$') {
            $key = $Matches[1]
            $value = $Matches[2]

            if ($value -eq '$true' -or $value -eq 'true') {
                $args[$key] = $true
            } elseif ($value -eq '$false' -or $value -eq 'false') {
                $args[$key] = $false
            } elseif ($value -eq '$null' -or $value -eq 'null') {
                $args[$key] = $null
            } elseif ($value -match '^\d+$') {
                $args[$key] = [int]$value
            } else {
                $args[$key] = $value
            }
        } else {
            $positionalArgs += $arg
        }
    }

    # Map first positional arg to 'query'
    if ($positionalArgs.Count -gt 0 -and -not $args.ContainsKey('query')) {
        $args['query'] = $positionalArgs[0]
    }

    return @{ ToolName = $toolName; Arguments = $args }
}

# Parse tool args into hashtable (for tool calls)
$arguments = @{}
if ($callTool) {
    $positionalArgs = @()
    foreach ($arg in $toolArgs) {
        if ($arg -match '^([^=]+)=(.*)$') {
            $key = $Matches[1]
            $value = $Matches[2]

            if ($value -eq '$true' -or $value -eq 'true') {
                $arguments[$key] = $true
            } elseif ($value -eq '$false' -or $value -eq 'false') {
                $arguments[$key] = $false
            } elseif ($value -eq '$null' -or $value -eq 'null') {
                $arguments[$key] = $null
            } elseif ($value -match '^\d+$') {
                $arguments[$key] = [int]$value
            } else {
                $arguments[$key] = $value
            }
        } else {
            # Positional argument (no = sign)
            $positionalArgs += $arg
        }
    }

    # Map first positional arg to 'query' (most common primary parameter)
    if ($positionalArgs.Count -gt 0 -and -not $arguments.ContainsKey('query')) {
        $arguments['query'] = $positionalArgs[0]
    }
}

# Find project directory
$scriptDir = Split-Path -Parent $MyInvocation.MyCommand.Path
$projectDir = Split-Path -Parent $scriptDir
$serverDir = Join-Path $projectDir "FlashPlatformGrimoire.Mcp"

Write-Host "Starting MCP server..." -ForegroundColor DarkGray

# Pre-build to avoid build output polluting stdout
$buildOutput = & dotnet build --no-restore -v q $serverDir 2>&1
if ($LASTEXITCODE -ne 0) {
    Write-Host "Build failed:" -ForegroundColor Red
    Write-Host $buildOutput
    exit 1
}

$psi = New-Object System.Diagnostics.ProcessStartInfo
$psi.FileName = "dotnet"
$psi.Arguments = "run --no-build"
$psi.WorkingDirectory = $serverDir
$psi.UseShellExecute = $false
$psi.RedirectStandardInput = $true
$psi.RedirectStandardOutput = $true
$psi.RedirectStandardError = $true
$psi.CreateNoWindow = $true

$proc = [System.Diagnostics.Process]::Start($psi)

function Read-LineWithTimeout {
    param($reader, $timeoutMs = 10000)

    $task = $reader.ReadLineAsync()
    if ($task.Wait($timeoutMs)) {
        return $task.Result
    }
    return $null
}

function Format-ToolHelp {
    param($tool)

    $output = @()
    $output += "$($tool.name)"
    $output += "=" * $tool.name.Length
    $output += ""

    if ($tool.description) {
        $output += $tool.description
        $output += ""
    }

    if ($tool.inputSchema -and $tool.inputSchema.properties) {
        $output += "Parameters:"
        $props = $tool.inputSchema.properties.PSObject.Properties
        $required = @()
        if ($tool.inputSchema.required) {
            $required = $tool.inputSchema.required
        }

        foreach ($prop in $props) {
            $name = $prop.Name
            $schema = $prop.Value
            $isRequired = $required -contains $name
            $reqMark = if ($isRequired) { " (required)" } else { "" }
            $type = if ($schema.type) { "[$($schema.type)]" } else { "" }

            $output += "  $name $type$reqMark"
            if ($schema.description) {
                # Word wrap description at ~70 chars
                $desc = $schema.description -replace "`n", " " -replace "\s+", " "
                $output += "    $desc"
            }
            if ($schema.default -ne $null) {
                $output += "    Default: $($schema.default)"
            }
            $output += ""
        }
    }

    $output += "Example:"
    $exampleArgs = @()
    if ($tool.inputSchema -and $tool.inputSchema.properties) {
        $props = $tool.inputSchema.properties.PSObject.Properties | Select-Object -First 1
        if ($props) {
            $exampleArgs += "$($props.Name)=`"value`""
        }
    }
    $output += "  ./test-mcp-tool.ps1 $($tool.name) $($exampleArgs -join ' ')"

    return $output -join "`n"
}

try {
    # Send initialize request
    $init = @{
        jsonrpc = "2.0"
        id = 1
        method = "initialize"
        params = @{
            protocolVersion = "2024-11-05"
            capabilities = @{}
            clientInfo = @{ name = "test"; version = "1.0" }
        }
    } | ConvertTo-Json -Compress -Depth 10

    $proc.StandardInput.WriteLine($init)

    # Read init response
    $initResponse = Read-LineWithTimeout $proc.StandardOutput 10000
    if (-not $initResponse) {
        Write-Host "Timeout waiting for init response" -ForegroundColor Red
        exit 1
    }

    # Batch mode: execute multiple commands on one server instance
    if ($batchMode) {
        Write-Host "Batch mode: $($batchCommands.Count) command(s)" -ForegroundColor Cyan
        $requestId = 1

        foreach ($cmd in $batchCommands) {
            # Handle wait command
            if ($cmd -match '^wait:(\d+)$') {
                $waitMs = [int]$Matches[1]
                Write-Host "  Waiting ${waitMs}ms..." -ForegroundColor DarkGray
                Start-Sleep -Milliseconds $waitMs
                continue
            }

            # Parse and execute tool command
            $parsed = Parse-ToolCommand $cmd
            if (-not $parsed.ToolName) {
                Write-Host "  Skipping empty command" -ForegroundColor Yellow
                continue
            }

            $requestId++
            Write-Host ""
            Write-Host "--- [$($parsed.ToolName)] ---" -ForegroundColor Cyan
            Write-Host "Calling: $($parsed.ToolName) $($parsed.Arguments | ConvertTo-Json -Compress)" -ForegroundColor DarkGray

            $request = @{
                jsonrpc = "2.0"
                id = $requestId
                method = "tools/call"
                params = @{
                    name = $parsed.ToolName
                    arguments = $parsed.Arguments
                }
            } | ConvertTo-Json -Compress -Depth 10

            $proc.StandardInput.WriteLine($request)

            # Read response
            $timeoutMs = if ($parsed.ToolName -eq "rebuild_index") { 120000 } else { 15000 }
            $response = Read-LineWithTimeout $proc.StandardOutput $timeoutMs
            if (-not $response) {
                Write-Host "Timeout waiting for response" -ForegroundColor Red
                continue
            }

            $json = $response | ConvertFrom-Json
            if ($json.error) {
                Write-Host "Error: $($json.error.message)" -ForegroundColor Red
                continue
            }

            # Output result
            if ($json.result.content) {
                foreach ($content in $json.result.content) {
                    if ($content.type -eq "text") {
                        Write-Output $content.text
                    }
                }
            }
        }

        Write-Host ""
        Write-Host "--- Batch complete ---" -ForegroundColor Cyan
        return
    }

    # For list or help, we need tools/list; for call, we need tools/call
    if ($listAll -or $helpTool) {
        $request = @{
            jsonrpc = "2.0"
            id = 2
            method = "tools/list"
            params = @{}
        } | ConvertTo-Json -Compress -Depth 10

        if ($helpTool) {
            Write-Host "Getting help for: $helpTool" -ForegroundColor DarkGray
        } else {
            Write-Host "Listing tools..." -ForegroundColor DarkGray
        }
    } else {
        $request = @{
            jsonrpc = "2.0"
            id = 2
            method = "tools/call"
            params = @{
                name = $callTool
                arguments = $arguments
            }
        } | ConvertTo-Json -Compress -Depth 10
        Write-Host "Calling: $callTool $($arguments | ConvertTo-Json -Compress)" -ForegroundColor DarkGray
    }

    $proc.StandardInput.WriteLine($request)

    # Read response (longer timeout for rebuild_index)
    $timeoutMs = if ($callTool -eq "rebuild_index") { 120000 } else { 15000 }
    $response = Read-LineWithTimeout $proc.StandardOutput $timeoutMs
    if (-not $response) {
        Write-Host "Timeout waiting for response" -ForegroundColor Red
        exit 1
    }

    # Parse response
    $json = $response | ConvertFrom-Json
    if ($json.error) {
        Write-Host "Error: $($json.error.message)" -ForegroundColor Red
        exit 1
    }

    if ($listAll) {
        # Format tools list as table (sorted alphabetically)
        $tools = $json.result.tools | Sort-Object -Property name

        # Find max tool name length for column width
        $maxNameLen = ($tools | ForEach-Object { $_.name.Length } | Measure-Object -Maximum).Maximum
        $nameColWidth = [Math]::Max($maxNameLen + 2, 30)

        # Calculate description width based on console
        $consoleWidth = 100
        try { $consoleWidth = $Host.UI.RawUI.WindowSize.Width } catch {}
        $descWidth = $consoleWidth - $nameColWidth - 4
        if ($descWidth -lt 30) { $descWidth = 50 }

        Write-Output ""
        Write-Output ("  " + "Tool".PadRight($nameColWidth) + "Description")
        Write-Output ("  " + ("-" * ($nameColWidth - 1)) + " " + ("-" * $descWidth))

        foreach ($tool in $tools) {
            $name = $tool.name.PadRight($nameColWidth)
            $desc = ""
            if ($tool.description) {
                $desc = $tool.description -replace "`n", " " -replace "\s+", " "
                if ($desc.Length -gt $descWidth) { $desc = $desc.Substring(0, $descWidth - 3) + "..." }
            }
            Write-Output ("  " + $name + $desc)
        }
        Write-Output ""
        Write-Output "Use './test-mcp-tool.ps1 ? <tool>' for detailed help"
    } elseif ($helpTool) {
        # Find and show help for specific tool
        $tool = $json.result.tools | Where-Object { $_.name -eq $helpTool }
        if ($tool) {
            Write-Output (Format-ToolHelp $tool)
        } else {
            Write-Host "Tool not found: $helpTool" -ForegroundColor Red
            Write-Host "Use './test-mcp-tool.ps1 --list' to see available tools"
            exit 1
        }
    } else {
        # Tool call result
        if ($json.result.content) {
            foreach ($content in $json.result.content) {
                if ($content.type -eq "text") {
                    Write-Output $content.text
                }
            }
        }
    }

} finally {
    $proc.StandardInput.Close()
    Start-Sleep -Milliseconds 100
    if (-not $proc.HasExited) {
        $proc.Kill()
    }
    $proc.Dispose()
}
