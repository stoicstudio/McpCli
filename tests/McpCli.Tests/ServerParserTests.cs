using McpCli.Commands;

namespace McpCli.Tests;

/// <summary>
/// Tests for ServerParser - parsing server command strings into executable commands.
/// High ROI: Currently 0% coverage, pure logic, no mocking needed.
/// </summary>
public class ServerParserTests
{
    [Fact]
    public void ParseServerCommand_SimpleCommand_ReturnsCommandOnly()
    {
        var (command, args) = ServerParser.ParseServerCommand("roslyn-codex");

        Assert.Equal("roslyn-codex", command);
        Assert.Empty(args);
    }

    [Fact]
    public void ParseServerCommand_CommandWithArgs_SplitsCorrectly()
    {
        var (command, args) = ServerParser.ParseServerCommand("roslyn-codex --solution C:/path/to/sln");

        Assert.Equal("roslyn-codex", command);
        Assert.Equal(2, args.Length);
        Assert.Equal("--solution", args[0]);
        Assert.Equal("C:/path/to/sln", args[1]);
    }

    [Fact]
    public void ParseServerCommand_DotnetRun_ParsesCorrectly()
    {
        var (command, args) = ServerParser.ParseServerCommand("dotnet run --project /path/to/project");

        Assert.Equal("dotnet", command);
        Assert.Equal(3, args.Length);
        Assert.Equal("run", args[0]);
        Assert.Equal("--project", args[1]);
        Assert.Equal("/path/to/project", args[2]);
    }

    [Fact]
    public void ParseServerCommand_QuotedPath_PreservesPath()
    {
        var (command, args) = ServerParser.ParseServerCommand("dotnet run --project \"C:/path with spaces/project\"");

        Assert.Equal("dotnet", command);
        Assert.Equal("C:/path with spaces/project", args[2]);
    }

    [Fact]
    public void ParseServerCommand_SingleQuotes_PreservesContent()
    {
        var (command, args) = ServerParser.ParseServerCommand("cmd 'arg with spaces'");

        Assert.Equal("cmd", command);
        Assert.Single(args);
        Assert.Equal("arg with spaces", args[0]);
    }

    [Fact]
    public void ParseServerCommand_EmptyString_ReturnsEmptyCommand()
    {
        var (command, args) = ServerParser.ParseServerCommand("");

        Assert.Equal("", command);
        Assert.Empty(args);
    }

    [Fact]
    public void ParseServerCommand_MultipleSpaces_IgnoresExtraSpaces()
    {
        var (command, args) = ServerParser.ParseServerCommand("cmd   arg1    arg2");

        Assert.Equal("cmd", command);
        Assert.Equal(2, args.Length);
        Assert.Equal("arg1", args[0]);
        Assert.Equal("arg2", args[1]);
    }

    [Fact]
    public void ParseServerCommand_MixedQuotes_HandlesCorrectly()
    {
        var (command, args) = ServerParser.ParseServerCommand("cmd \"double quoted\" 'single quoted'");

        Assert.Equal("cmd", command);
        Assert.Equal(2, args.Length);
        Assert.Equal("double quoted", args[0]);
        Assert.Equal("single quoted", args[1]);
    }

    [Fact]
    public void ParseServerCommand_NestedQuotes_PreservesInnerQuote()
    {
        // Double quote inside single quotes
        var (command, args) = ServerParser.ParseServerCommand("cmd 'say \"hello\"'");

        Assert.Equal("cmd", command);
        Assert.Single(args);
        Assert.Equal("say \"hello\"", args[0]);
    }

    [Fact]
    public void ParseServerCommand_WindowsPath_ParsesCorrectly()
    {
        var (command, args) = ServerParser.ParseServerCommand(@"dotnet run --project C:\stoic\gh\MyMcp\MyMcp.csproj");

        Assert.Equal("dotnet", command);
        Assert.Equal(3, args.Length);
        Assert.Equal(@"C:\stoic\gh\MyMcp\MyMcp.csproj", args[2]);
    }

    [Fact]
    public void ParseServerCommand_ComplexDotnetRun_ParsesAllArgs()
    {
        var (command, args) = ServerParser.ParseServerCommand("dotnet run --project C:/path -- --solution C:/sln");

        Assert.Equal("dotnet", command);
        Assert.Equal(6, args.Length);
        Assert.Equal("run", args[0]);
        Assert.Equal("--project", args[1]);
        Assert.Equal("C:/path", args[2]);
        Assert.Equal("--", args[3]);
        Assert.Equal("--solution", args[4]);
        Assert.Equal("C:/sln", args[5]);
    }
}
