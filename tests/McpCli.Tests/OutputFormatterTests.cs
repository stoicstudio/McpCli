using McpCli.Formatting;
using McpCli.Protocol;

namespace McpCli.Tests;

/// <summary>
/// Tests for OutputFormatter - the illumination of raw data into readable wisdom.
/// </summary>
public class OutputFormatterTests
{
    [Fact]
    public void FormatToolList_EmptyList_ReturnsNoToolsMessage()
    {
        var result = new ToolListResult { Tools = new List<ToolInfo>() };

        var output = OutputFormatter.FormatToolList(result);

        Assert.Equal("No tools available.", output);
    }

    [Fact]
    public void FormatToolList_SingleTool_FormatsProperly()
    {
        var result = new ToolListResult
        {
            Tools = new List<ToolInfo>
            {
                new() { Name = "test_tool", Description = "A test tool" }
            }
        };

        var output = OutputFormatter.FormatToolList(result);

        Assert.Contains("test_tool", output);
        Assert.Contains("A test tool", output);
        Assert.Contains("Tool", output);
        Assert.Contains("Description", output);
    }

    [Fact]
    public void FormatToolList_MultipleTools_SortsByName()
    {
        var result = new ToolListResult
        {
            Tools = new List<ToolInfo>
            {
                new() { Name = "zebra_tool" },
                new() { Name = "alpha_tool" },
                new() { Name = "middle_tool" }
            }
        };

        var output = OutputFormatter.FormatToolList(result);
        var alphaIndex = output.IndexOf("alpha_tool");
        var middleIndex = output.IndexOf("middle_tool");
        var zebraIndex = output.IndexOf("zebra_tool");

        Assert.True(alphaIndex < middleIndex, "alpha should come before middle");
        Assert.True(middleIndex < zebraIndex, "middle should come before zebra");
    }

    [Fact]
    public void FormatToolList_LongDescription_Truncates()
    {
        var longDesc = new string('x', 200);
        var result = new ToolListResult
        {
            Tools = new List<ToolInfo>
            {
                new() { Name = "test", Description = longDesc }
            }
        };

        var output = OutputFormatter.FormatToolList(result);

        // Description should be truncated with ellipsis
        Assert.Contains("...", output);
        // Original description should NOT appear in full
        Assert.DoesNotContain(longDesc, output);
    }

    [Fact]
    public void FormatToolHelp_BasicTool_FormatsCorrectly()
    {
        var tool = new ToolInfo
        {
            Name = "my_tool",
            Description = "Does something useful"
        };

        var output = OutputFormatter.FormatToolHelp(tool);

        Assert.Contains("my_tool", output);
        Assert.Contains("=", output); // Title underline
        Assert.Contains("Does something useful", output);
        Assert.Contains("Example:", output);
    }

    [Fact]
    public void FormatToolHelp_WithParameters_ShowsParameters()
    {
        var tool = new ToolInfo
        {
            Name = "search",
            Description = "Search for items",
            InputSchema = new JsonSchema
            {
                Properties = new Dictionary<string, JsonSchemaProperty>
                {
                    ["query"] = new JsonSchemaProperty
                    {
                        Type = "string",
                        Description = "The search query"
                    },
                    ["limit"] = new JsonSchemaProperty
                    {
                        Type = "integer",
                        Description = "Max results",
                        Default = 10
                    }
                },
                Required = new List<string> { "query" }
            }
        };

        var output = OutputFormatter.FormatToolHelp(tool);

        Assert.Contains("Parameters:", output);
        Assert.Contains("query", output);
        Assert.Contains("[string]", output);
        Assert.Contains("(required)", output);
        Assert.Contains("limit", output);
        Assert.Contains("[integer]", output);
        Assert.Contains("Default: 10", output);
    }

    [Fact]
    public void FormatToolHelp_WithEnumParameter_ShowsValues()
    {
        var tool = new ToolInfo
        {
            Name = "sort",
            InputSchema = new JsonSchema
            {
                Properties = new Dictionary<string, JsonSchemaProperty>
                {
                    ["order"] = new JsonSchemaProperty
                    {
                        Type = "string",
                        Enum = new List<string> { "asc", "desc" }
                    }
                }
            }
        };

        var output = OutputFormatter.FormatToolHelp(tool);

        Assert.Contains("Values: asc, desc", output);
    }

    [Fact]
    public void FormatToolResult_TextContent_ReturnsText()
    {
        var result = new ToolCallResult
        {
            Content = new List<ContentItem>
            {
                new() { Type = "text", Text = "Hello World" }
            }
        };

        var output = OutputFormatter.FormatToolResult(result);

        Assert.Equal("Hello World", output);
    }

    [Fact]
    public void FormatToolResult_MultipleTextContent_ConcatenatesWithNewlines()
    {
        var result = new ToolCallResult
        {
            Content = new List<ContentItem>
            {
                new() { Type = "text", Text = "Line 1" },
                new() { Type = "text", Text = "Line 2" }
            }
        };

        var output = OutputFormatter.FormatToolResult(result);

        Assert.Contains("Line 1", output);
        Assert.Contains("Line 2", output);
    }

    [Fact]
    public void FormatToolResult_ImageContent_ShowsPlaceholder()
    {
        var result = new ToolCallResult
        {
            Content = new List<ContentItem>
            {
                new() { Type = "image", MimeType = "image/png" }
            }
        };

        var output = OutputFormatter.FormatToolResult(result);

        Assert.Contains("[Image: image/png]", output);
    }

    [Fact]
    public void FormatToolResult_ErrorResult_PrefixesWithError()
    {
        var result = new ToolCallResult
        {
            Content = new List<ContentItem>
            {
                new() { Type = "text", Text = "Something went wrong" }
            },
            IsError = true
        };

        var output = OutputFormatter.FormatToolResult(result);

        Assert.StartsWith("Error:", output);
    }

    [Fact]
    public void FormatError_SimpleMessage_FormatsCorrectly()
    {
        var output = OutputFormatter.FormatError("Something broke");

        Assert.Equal("Error: Something broke", output);
    }

    [Fact]
    public void FormatError_McpException_IncludesCode()
    {
        var ex = new McpException(-32600, "Invalid Request");

        var output = OutputFormatter.FormatError(ex);

        Assert.Equal("MCP Error [-32600]: Invalid Request", output);
    }
}
