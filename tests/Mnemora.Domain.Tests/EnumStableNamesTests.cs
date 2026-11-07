using Mnemora.Domain.LibraryContainers;
using Mnemora.Domain.Materials;
using Mnemora.Domain.Sections;
using Mnemora.Shared;
using Xunit;

namespace Mnemora.Domain.Tests;

public sealed class EnumStableNamesTests
{
    [Theory]
    [InlineData(MaterialType.ARTICLE, "Article")]
    [InlineData(MaterialType.QUESTION, "Question")]
    public void MaterialTypes_KeepContractStrings(MaterialType value, string expected) =>
        Assert.Equal(expected, value.ToStableName());

    [Theory]
    [InlineData(MaterialDifficulty.EASY, "Easy")]
    [InlineData(MaterialDifficulty.MEDIUM, "Medium")]
    [InlineData(MaterialDifficulty.HARD, "Hard")]
    public void Difficulty_KeepContractStrings(MaterialDifficulty value, string expected) =>
        Assert.Equal(expected, value.ToStableName());

    [Fact]
    public void RenamedSymbols_KeepOldExternalStrings()
    {
        Assert.Equal("DotNet", SectionIcon.DOT_NET.ToStableName());
        Assert.Equal("CSharp", FolderIcon.C_SHARP.ToStableName());
        Assert.Equal("Teal", SectionColor.TEAL.ToStableName());
        Assert.Equal("Blue", FolderColor.BLUE.ToStableName());
    }

    [Theory]
    [InlineData("DotNet", SectionIcon.DOT_NET)]
    [InlineData("DOT_NET", SectionIcon.DOT_NET)]
    [InlineData("CSharp", SectionIcon.C_SHARP)]
    public void SectionIcons_ReadLegacyAndCurrentNames(string text, SectionIcon expected)
    {
        Assert.True(EnumStableNames.TryParseStableName<SectionIcon>(text, out var actual));
        Assert.Equal(expected, actual);
    }

    [Fact]
    public void FolderIcon_ReadsLegacyNameAndRejectsUnknownName()
    {
        Assert.Equal(FolderIcon.DOT_NET, EnumStableNames.ParseStableName<FolderIcon>("DotNet"));
        Assert.False(EnumStableNames.TryParseStableName<FolderIcon>("unknown-icon", out _));
        Assert.False(EnumStableNames.TryParseStableName<FolderIcon>(null, out _));
    }
}
