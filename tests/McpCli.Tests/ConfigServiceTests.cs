using McpCli.Services;

namespace McpCli.Tests;

/// <summary>
/// Tests for ConfigService - the keeper of the sacred aliases.
/// </summary>
public class ConfigServiceTests : IDisposable
{
    private readonly string _originalConfigDir;
    private readonly string _testConfigDir;

    public ConfigServiceTests()
    {
        // We'll test the public API behavior, not the file storage directly
        // since the config directory is fixed. Save initial state.
        _originalConfigDir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
            ".mcp-cli");
        _testConfigDir = Path.Combine(Path.GetTempPath(), $"mcp-cli-test-{Guid.NewGuid()}");
    }

    public void Dispose()
    {
        // Cleanup is handled by individual tests
    }

    [Fact]
    public void ResolveServer_UnknownAlias_ReturnsOriginal()
    {
        // An unknown alias should pass through unchanged
        var result = ConfigService.ResolveServer("some-unknown-server-xyz");

        Assert.Equal("some-unknown-server-xyz", result);
    }

    [Fact]
    public void ResolveServer_KnownAlias_ReturnsCommand()
    {
        // Create an alias for testing
        ConfigService.SetAlias("test-resolve-alias", "test-server-command");

        try
        {
            var result = ConfigService.ResolveServer("test-resolve-alias");
            Assert.Equal("test-server-command", result);
        }
        finally
        {
            // Cleanup
            ConfigService.RemoveAlias("test-resolve-alias");
        }
    }

    [Fact]
    public void ResolveServer_CaseInsensitive()
    {
        ConfigService.SetAlias("TestCaseAlias", "test-server");

        try
        {
            Assert.Equal("test-server", ConfigService.ResolveServer("testcasealias"));
            Assert.Equal("test-server", ConfigService.ResolveServer("TESTCASEALIAS"));
            Assert.Equal("test-server", ConfigService.ResolveServer("TestCaseAlias"));
        }
        finally
        {
            ConfigService.RemoveAlias("TestCaseAlias");
        }
    }

    [Fact]
    public void SetAlias_CreatesEntry()
    {
        ConfigService.SetAlias("test-set-alias", "my-server");

        try
        {
            var aliases = ConfigService.GetAliases();
            Assert.True(aliases.ContainsKey("test-set-alias"));
            Assert.Equal("my-server", aliases["test-set-alias"]);
        }
        finally
        {
            ConfigService.RemoveAlias("test-set-alias");
        }
    }

    [Fact]
    public void SetAlias_UpdatesExisting()
    {
        ConfigService.SetAlias("test-update-alias", "original-server");
        ConfigService.SetAlias("test-update-alias", "updated-server");

        try
        {
            var aliases = ConfigService.GetAliases();
            Assert.Equal("updated-server", aliases["test-update-alias"]);
        }
        finally
        {
            ConfigService.RemoveAlias("test-update-alias");
        }
    }

    [Fact]
    public void RemoveAlias_ExistingAlias_ReturnsTrue()
    {
        ConfigService.SetAlias("test-remove-alias", "server");

        var result = ConfigService.RemoveAlias("test-remove-alias");

        Assert.True(result);
    }

    [Fact]
    public void RemoveAlias_NonExistingAlias_ReturnsFalse()
    {
        var result = ConfigService.RemoveAlias("non-existent-alias-xyz");

        Assert.False(result);
    }

    [Fact]
    public void GetAliases_ReturnsAllAliases()
    {
        ConfigService.SetAlias("test-list-1", "server-1");
        ConfigService.SetAlias("test-list-2", "server-2");

        try
        {
            var aliases = ConfigService.GetAliases();
            Assert.True(aliases.ContainsKey("test-list-1"));
            Assert.True(aliases.ContainsKey("test-list-2"));
        }
        finally
        {
            ConfigService.RemoveAlias("test-list-1");
            ConfigService.RemoveAlias("test-list-2");
        }
    }

    [Fact]
    public void GetConfigPath_ReturnsValidPath()
    {
        var path = ConfigService.GetConfigPath();

        Assert.Contains(".mcp-cli", path);
        Assert.Contains("config.json", path);
    }
}
