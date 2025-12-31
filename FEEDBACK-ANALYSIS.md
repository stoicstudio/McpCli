# Feedback Analysis: mcp-cli Enhancements

## Summary

| Category | Count | Items |
|----------|-------|-------|
| **Accept (High Priority)** | 5 | Aliases, Output Formats, --version, Quiet Mode, Pipe Support |
| **Accept (Medium Priority)** | 4 | Shell Completion, Color Output, Dry-Run, Progress Indicator |
| **Accept (Low Priority)** | 3 | REPL Mode, History, Better Errors |
| **Modify** | 1 | Short Flags (simplified) |
| **Reject** | 3 | Positional Args, Watch Mode, Clipboard |

---

## Accepted - High Priority

### 1. Server Aliases / Configuration File
**Verdict: ACCEPT - HIGH PRIORITY**

**Rationale:** This is universally applicable and addresses real pain. Typing `dotnet run --project C:\long\path\to\server` repeatedly is tedious.

**Proposed Implementation:**
```
~/.mcp-cli/config.json (or %USERPROFILE%\.mcp-cli\config.json on Windows)
```

```json
{
  "servers": {
    "flash": "flash-platform-grimoire",
    "p4": "perforce-athanor"
  },
  "defaults": {
    "timeout": 30
  }
}
```

**Commands:**
- `mcp-cli alias set flash flash-platform-grimoire`
- `mcp-cli alias list`
- `mcp-cli alias remove flash`
- `mcp-cli call flash get_api_reference query=Sprite`

### 4. Output Format Options
**Verdict: ACCEPT - HIGH PRIORITY**

**Rationale:** Essential for scripting and integration. Universal to all CLIs.

**Implementation:**
```
--output json    # Raw JSON from server (for jq, scripting)
--output text    # Current formatted output (default)
```

Reject `--output table` - our text output already uses tables where appropriate. Keep it simple: json or text.

### Quick Win: --version
**Verdict: ACCEPT - HIGH PRIORITY**

**Rationale:** Expected standard behavior. System.CommandLine provides this nearly for free.

```
mcp-cli --version
# Output: mcp-cli 0.1.0
```

### Quick Win: Quiet Mode (-q)
**Verdict: ACCEPT - HIGH PRIORITY**

**Rationale:** Universal CLI pattern. Essential for scripting.

```
mcp-cli call server tool arg=value -q  # Only outputs result, no banners
```

### 8. Pipe Support (stdin)
**Verdict: ACCEPT - HIGH PRIORITY**

**Rationale:** Standard Unix philosophy. Universal applicability.

```
echo "Sprite" | mcp-cli call flash get_api_reference --stdin query
```

Implementation: `--stdin <param-name>` reads stdin and assigns to that parameter.

---

## Accepted - Medium Priority

### 5. Shell Completion Generation
**Verdict: ACCEPT - MEDIUM PRIORITY**

**Rationale:** System.CommandLine has built-in support via `dotnet-suggest`. We can expose it with minimal effort.

```
mcp-cli completion bash
mcp-cli completion powershell
mcp-cli completion zsh
```

Note: Dynamic tool name completion (querying the server) is complex and may not be practical for v0.2.

### 9. Color Output
**Verdict: ACCEPT - MEDIUM PRIORITY**

**Rationale:** Improves readability. Universal enhancement.

**Implementation:**
- Tool names: Cyan
- Required params: Yellow
- Errors: Red
- Add `--no-color` flag for CI environments
- Respect `NO_COLOR` environment variable (standard)

### 12. Dry-Run Mode
**Verdict: ACCEPT - MEDIUM PRIORITY**

**Rationale:** Universal debugging/testing feature. Safe for all servers.

```
mcp-cli call flash rebuild_index all=true --dry-run
# Output:
# Would call: rebuild_index
# Arguments: {"all": true}
# Server: flash-platform-grimoire
```

### 10. Progress Indicator
**Verdict: ACCEPT - MEDIUM PRIORITY**

**Rationale:** Universal UX improvement for long-running operations.

```
⠋ Calling rebuild_index... (5s)
✓ Complete (15.2s)
```

Only show for calls exceeding 2 seconds. Suppress with `-q`.

---

## Accepted - Low Priority

### 6. Interactive/REPL Mode
**Verdict: ACCEPT - LOW PRIORITY**

**Rationale:** Valuable for exploration, but significant implementation effort.

```
mcp-cli repl flash
flash> list
flash> call get_api_reference query=Sprite
flash> exit
```

Benefits: Persistent connection, command history, faster iteration.
Defer to v0.4 or later.

### 11. History File
**Verdict: ACCEPT - LOW PRIORITY**

**Rationale:** Nice convenience, low impact. Defer to when REPL mode is implemented.

### Quick Win: Better Error Messages
**Verdict: ACCEPT - LOW PRIORITY**

**Rationale:** Good UX, but fuzzy matching adds complexity.

```
Error: Tool 'get_api_ref' not found.
Did you mean: get_api_reference?
```

Requires Levenshtein distance or similar. Lower priority than core features.

---

## Modified

### 2. Short Flags for Boolean Parameters
**Verdict: MODIFY - Accept `--flag`, Reject `-ds` combined flags**

**Original proposal:**
```
mcp-cli call flash get_api_reference Sprite --description --source
mcp-cli call flash get_api_reference Sprite -ds  # Combined short flags
```

**Accepted portion:**
```
mcp-cli call flash get_api_reference query=Sprite --description --source
```

Boolean parameters can be specified as flags without `=true`.

**Rejected portion:**
Combined short flags (`-ds`) require:
1. Assigning single-letter aliases to each boolean param
2. Handling conflicts across different tools
3. Runtime schema introspection to determine mappings

This is fragile and tool-specific. Different servers have different parameters.

**Implementation:** Parse schema, if param is boolean type, allow `--paramname` as shorthand for `paramname=true`.

---

## Rejected

### 3. Positional Arguments
**Verdict: REJECT**

**Rationale:**
1. **JSON Schema doesn't define parameter order** - Object properties are unordered by spec
2. **Ambiguity** - Which positional arg maps to which param? `search_documentation Bitmap 10` - is 10 the `maxResults` or something else?
3. **Tool-specific** - The example `search_documentation Bitmap 10` is flash-platform-grimoire specific. Other servers have different tools with different signatures.
4. **Error-prone** - Easy to get wrong, especially with optional params

**Alternative:** Named parameters are explicit and work universally. The verbosity is acceptable for correctness.

### 7. Watch Mode
**Verdict: REJECT**

**Rationale:**
1. **Not universal** - The example `get_index_status --watch` is flash-platform-grimoire specific
2. **Niche use case** - Most MCP tool calls are one-shot operations
3. **External tools exist** - Unix `watch` command, PowerShell loops
4. **Scope creep** - A CLI tool shouldn't try to be a monitoring system

**Alternative:** Users can achieve this with:
```bash
watch -n 5 'mcp-cli call flash get_index_status'  # Unix
while ($true) { mcp-cli call flash get_index_status; sleep 5 }  # PowerShell
```

### Quick Win: Copy to Clipboard (--clip)
**Verdict: REJECT**

**Rationale:**
1. **Platform-specific** - Windows (`clip`), macOS (`pbcopy`), Linux (varies: `xclip`, `xsel`, Wayland)
2. **Adds complexity** - Need to detect platform, handle missing utilities
3. **External tools exist** - Users can pipe to clipboard utilities

**Alternative:**
```bash
mcp-cli call flash get_api_reference query=Sprite | clip      # Windows
mcp-cli call flash get_api_reference query=Sprite | pbcopy    # macOS
```

---

## Implementation Roadmap

| Version | Features | Effort |
|---------|----------|--------|
| **v0.2** | Aliases, --output json, -q quiet, --version, boolean flags | 1-2 days |
| **v0.3** | Pipe support (stdin), dry-run, shell completion | 1-2 days |
| **v0.4** | Color output, progress indicator | 1 day |
| **v0.5** | REPL mode, history, better errors | 3-4 days |

---

## Rationale Summary

**Accepted features share these qualities:**
- Universal to ALL MCP servers (not tool-specific)
- Follow standard CLI conventions
- Improve scripting/automation capabilities
- Reasonable implementation effort

**Rejected features share these qualities:**
- Tool-specific or server-specific assumptions
- Platform-dependent implementation complexity
- Achievable through external tools
- Introduce ambiguity or error-prone behavior
