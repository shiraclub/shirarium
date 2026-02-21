using Jellyfin.Plugin.Shirarium.Services;
using Xunit;

namespace Jellyfin.Plugin.Shirarium.Tests;

public sealed class HeuristicParserTests
{
    [Theory]
    [InlineData("NASA.Secrets.2024.mkv", "NASA Secrets")]
    [InlineData("FBI.Files.S01E01.mkv", "FBI Files")]
    [InlineData("S.H.I.E.L.D.S01E01.mkv", "S.H.I.E.L.D.")]
    [InlineData("the.matrix.1999.mkv", "The Matrix")]
    [InlineData("INCEPTION.2010.mkv", "INCEPTION")]
    public void Parse_PreservesAcronymsAndHandlesTitleCasing(string filename, string expectedTitle)
    {
        var parser = new HeuristicParser();
        var result = parser.Parse(filename);

        Assert.Equal(expectedTitle, result.Title);
    }

    [Fact]
    public void Parse_HandlesAnimeAbsoluteNumbering()
    {
        var parser = new HeuristicParser();
        // One Piece 1100
        var result = parser.Parse("One.Piece.1100.1080p.mkv");

        Assert.Equal("One Piece", result.Title);
        Assert.Equal("episode", result.MediaType);
        Assert.Equal(1100, result.Episode);
    }

    [Fact]
    public void Parse_UsesFolderContext()
    {
        var parser = new HeuristicParser();
        var path = Path.Combine("Movies", "Inception (2010)", "inception.mkv");
        var result = parser.Parse(path);

        Assert.Equal("Inception", result.Title);
        Assert.Equal(2010, result.Year);
        Assert.Equal("movie", result.MediaType);
    }
}
