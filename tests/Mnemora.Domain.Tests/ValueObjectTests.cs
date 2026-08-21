using Mnemora.Domain.Materials;
using Mnemora.Domain.Sections;
using Mnemora.Domain.Topics;
using Xunit;

namespace Mnemora.Domain.Tests;

public sealed class ValueObjectTests
{
    [Fact]
    public void SectionName_Create_TrimsValidValue()
    {
        var result = SectionName.Create("  Backend  ");

        Assert.True(result.IsSuccess);
        Assert.Equal("Backend", result.Value.Value);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("A")]
    public void SectionName_Create_RejectsMissingOrTooShortValue(string? value)
    {
        var result = SectionName.Create(value!);

        Assert.True(result.IsFailure);
    }

    [Fact]
    public void TopicName_Create_RejectsValueLongerThanMaximum()
    {
        var result = TopicName.Create(new string('T', TopicName.MAXLENGTH + 1));

        Assert.True(result.IsFailure);
        Assert.Equal("name.length.is.wrong", result.Error.Code);
    }

    [Fact]
    public void MaterialTitle_Create_DistinguishesMissingAndEmptyValues()
    {
        var missing = MaterialTitle.Create(null);
        var empty = MaterialTitle.Create("   ");

        Assert.Equal("title.is.required", missing.Error.Code);
        Assert.Equal("title.is.empty", empty.Error.Code);
    }

    [Fact]
    public void MaterialTag_Equality_IsTrimmedAndCaseInsensitive()
    {
        var first = MaterialTag.Create("  EF-Core ");
        var second = MaterialTag.Create("ef-core");

        Assert.True(first.IsSuccess);
        Assert.True(second.IsSuccess);
        Assert.Equal("EF-Core", first.Value.Value);
        Assert.Equal(first.Value, second.Value);
    }

    [Theory]
    [InlineData("1article")]
    [InlineData("article_icon")]
    [InlineData("статья")]
    public void MaterialIcon_Create_RejectsUnsupportedKeyFormat(string key)
    {
        var result = MaterialIcon.Create(key);

        Assert.True(result.IsFailure);
        Assert.Equal("material.icon.key.is.invalid", result.Error.Code);
    }

    [Fact]
    public void MaterialIcon_Create_NormalizesValidKey()
    {
        var result = MaterialIcon.Create("  ARTICLE-2  ");

        Assert.True(result.IsSuccess);
        Assert.Equal("article-2", result.Value.Key);
    }

    [Theory]
    [InlineData(4, 3)]
    [InlineData(101, 20)]
    [InlineData(50, 4)]
    [InlineData(50, 101)]
    [InlineData(50, 50)]
    [InlineData(50, 60)]
    public void MaterialExperienceRewards_Create_RejectsInvalidCombination(
        int studyPoints,
        int reviewPoints)
    {
        var result = MaterialExperienceRewards.Create(studyPoints, reviewPoints);

        Assert.True(result.IsFailure);
    }

    [Fact]
    public void MaterialExperienceRewards_Create_AcceptsBoundaryValues()
    {
        var result = MaterialExperienceRewards.Create(
            MaterialExperienceRewards.MaxPoints,
            MaterialExperienceRewards.MinPoints);

        Assert.True(result.IsSuccess);
        Assert.Equal(100, result.Value.StudyPoints);
        Assert.Equal(5, result.Value.ReviewPoints);
    }

    [Fact]
    public void ContentValueObjects_RejectWhitespace()
    {
        Assert.True(ArticleContent.Create("   ").IsFailure);
        Assert.True(QuestionContent.Create("Question", "   ").IsFailure);
        Assert.True(QuestionContent.Create("   ", "Answer").IsFailure);
    }
}
