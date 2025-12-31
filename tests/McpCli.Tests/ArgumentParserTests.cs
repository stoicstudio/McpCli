using McpCli.Formatting;
using System.Text.Json;

namespace McpCli.Tests;

/// <summary>
/// Tests for ArgumentParser - the alchemical transmutation of strings into typed values.
/// </summary>
public class ArgumentParserTests
{
    [Fact]
    public void ParseToolArguments_EmptyArgs_ReturnsEmptyDictionary()
    {
        var result = ArgumentParser.ParseToolArguments(Array.Empty<string>());

        Assert.Empty(result);
    }

    [Fact]
    public void ParseToolArguments_StringValue_ParsesCorrectly()
    {
        var result = ArgumentParser.ParseToolArguments(new[] { "name=John" });

        Assert.Single(result);
        Assert.Equal("John", result["name"]);
    }

    [Fact]
    public void ParseToolArguments_IntegerValue_ParsesAsInt()
    {
        var result = ArgumentParser.ParseToolArguments(new[] { "count=42" });

        Assert.Single(result);
        Assert.IsType<int>(result["count"]);
        Assert.Equal(42, result["count"]);
    }

    [Fact]
    public void ParseToolArguments_NegativeInteger_ParsesAsInt()
    {
        var result = ArgumentParser.ParseToolArguments(new[] { "offset=-5" });

        Assert.Equal(-5, result["offset"]);
    }

    [Fact]
    public void ParseToolArguments_FloatValue_ParsesAsDouble()
    {
        var result = ArgumentParser.ParseToolArguments(new[] { "ratio=3.14" });

        Assert.IsType<double>(result["ratio"]);
        Assert.Equal(3.14, result["ratio"]);
    }

    [Fact]
    public void ParseToolArguments_BooleanTrue_ParsesAsBool()
    {
        var result = ArgumentParser.ParseToolArguments(new[] { "enabled=true" });

        Assert.True((bool)result["enabled"]!);
    }

    [Fact]
    public void ParseToolArguments_BooleanFalse_ParsesAsBool()
    {
        var result = ArgumentParser.ParseToolArguments(new[] { "enabled=false" });

        Assert.False((bool)result["enabled"]!);
    }

    [Fact]
    public void ParseToolArguments_PowerShellBooleans_ParsesCorrectly()
    {
        var result = ArgumentParser.ParseToolArguments(new[] { "a=$true", "b=$false" });

        Assert.True((bool)result["a"]!);
        Assert.False((bool)result["b"]!);
    }

    [Fact]
    public void ParseToolArguments_NullValue_ParsesAsNull()
    {
        var result = ArgumentParser.ParseToolArguments(new[] { "value=null" });

        Assert.Null(result["value"]);
    }

    [Fact]
    public void ParseToolArguments_PowerShellNull_ParsesAsNull()
    {
        var result = ArgumentParser.ParseToolArguments(new[] { "value=$null" });

        Assert.Null(result["value"]);
    }

    [Fact]
    public void ParseToolArguments_JsonArray_ParsesAsJsonElement()
    {
        var result = ArgumentParser.ParseToolArguments(new[] { "files=[\"a.cs\",\"b.cs\"]" });

        Assert.IsType<JsonElement>(result["files"]);
        var element = (JsonElement)result["files"]!;
        Assert.Equal(JsonValueKind.Array, element.ValueKind);
        Assert.Equal(2, element.GetArrayLength());
    }

    [Fact]
    public void ParseToolArguments_JsonObject_ParsesAsJsonElement()
    {
        var result = ArgumentParser.ParseToolArguments(new[] { "config={\"timeout\":30}" });

        Assert.IsType<JsonElement>(result["config"]);
        var element = (JsonElement)result["config"]!;
        Assert.Equal(JsonValueKind.Object, element.ValueKind);
    }

    [Fact]
    public void ParseToolArguments_PositionalArg_MapsToQuery()
    {
        var result = ArgumentParser.ParseToolArguments(new[] { "searchTerm" });

        Assert.Equal("searchTerm", result["query"]);
    }

    [Fact]
    public void ParseToolArguments_PositionalAndKeyValue_BothParsed()
    {
        var result = ArgumentParser.ParseToolArguments(new[] { "searchTerm", "limit=10" });

        Assert.Equal("searchTerm", result["query"]);
        Assert.Equal(10, result["limit"]);
    }

    [Fact]
    public void ParseToolArguments_MultipleKeyValues_AllParsed()
    {
        var result = ArgumentParser.ParseToolArguments(new[] { "a=1", "b=hello", "c=true" });

        Assert.Equal(3, result.Count);
        Assert.Equal(1, result["a"]);
        Assert.Equal("hello", result["b"]);
        Assert.True((bool)result["c"]!);
    }

    [Fact]
    public void ParseToolArguments_CaseInsensitiveKeys()
    {
        var result = ArgumentParser.ParseToolArguments(new[] { "Name=John" });

        Assert.True(result.ContainsKey("name"));
        Assert.True(result.ContainsKey("NAME"));
    }

    [Fact]
    public void ParseBatchCommand_SimpleCommand_ParsesCorrectly()
    {
        var (toolName, args) = ArgumentParser.ParseBatchCommand("get_status");

        Assert.Equal("get_status", toolName);
        Assert.Empty(args);
    }

    [Fact]
    public void ParseBatchCommand_CommandWithArgs_ParsesCorrectly()
    {
        var (toolName, args) = ArgumentParser.ParseBatchCommand("find_type pattern=*Service");

        Assert.Equal("find_type", toolName);
        Assert.Equal("*Service", args["pattern"]);
    }

    [Fact]
    public void ParseBatchCommand_QuotedString_ParsesCorrectly()
    {
        var (toolName, args) = ArgumentParser.ParseBatchCommand("search query=\"hello world\"");

        Assert.Equal("search", toolName);
        Assert.Equal("hello world", args["query"]);
    }

    [Fact]
    public void ParseBatchCommand_EmptyString_ReturnsNull()
    {
        var (toolName, _) = ArgumentParser.ParseBatchCommand("");

        Assert.Null(toolName);
    }

    [Fact]
    public void CoerceValue_StringWithNumbers_StaysString()
    {
        // A string starting with numbers but not a valid number should stay as string
        var result = ArgumentParser.CoerceValue("42abc");

        Assert.IsType<string>(result);
        Assert.Equal("42abc", result);
    }

    [Fact]
    public void CoerceValue_EmptyString_StaysString()
    {
        var result = ArgumentParser.CoerceValue("");

        Assert.IsType<string>(result);
        Assert.Equal("", result);
    }
}
