using Supertext.OrchardCore.Translation.Services;
using Xunit;

namespace Supertext.OrchardCore.Translation.Tests;

public class ModuleVersionTests
{
    [Theory]
    [InlineData("0.1.0+3f2a1c9d", "0.1.0")]
    [InlineData("1.2.3", "1.2.3")]
    [InlineData("1.2.3-beta.1+abc", "1.2.3-beta.1")]
    [InlineData(null, "")]
    [InlineData("  ", "")]
    public void Normalize_strips_build_metadata(string input, string expected)
        => Assert.Equal(expected, ModuleVersion.Normalize(input));

    [Fact]
    public void Release_versions_link_to_the_github_release()
        => Assert.Equal("https://github.com/Supertext/OrchardCore-Supertext-Translation/releases/tag/v0.1.0", ModuleVersion.ReleaseUrl("0.1.0"));

    [Theory]
    [InlineData("1.2.3-beta.1")]
    [InlineData("1.2")]
    [InlineData("")]
    [InlineData(null)]
    public void Other_versions_have_no_link(string version)
        => Assert.Null(ModuleVersion.ReleaseUrl(version));

    [Fact]
    public void Current_matches_the_project_version()
        => Assert.Matches(@"^\d+\.\d+\.\d+", ModuleVersion.Current);
}
