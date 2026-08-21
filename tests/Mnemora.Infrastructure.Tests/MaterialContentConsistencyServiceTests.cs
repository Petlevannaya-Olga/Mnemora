using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Mnemora.Application.Materials.Content;
using Mnemora.Domain.Materials;
using Mnemora.Domain.Sections;
using Mnemora.Domain.Topics;
using Mnemora.Infrastructure.Persistence;
using Xunit;

namespace Mnemora.Infrastructure.Tests;

public sealed class MaterialContentConsistencyServiceTests
{
    [Fact]
    public async Task CheckAndRepair_ReportsMissingContentAndQuarantinesOnlyRecoverableDirectories()
    {
        using var temporaryDirectory = new TemporaryDirectory();
        await using ServiceProvider provider = TestServiceProviderFactory.Create(temporaryDirectory.Path);
        var factory = provider.GetRequiredService<IDbContextFactory<MnemoraDbContext>>();

        await using (MnemoraDbContext dbContext = await factory.CreateDbContextAsync())
        {
            await dbContext.Database.EnsureCreatedAsync();
            Section section = Section.Create(
                SectionName.Create("Section").Value,
                SectionColor.Teal,
                SectionIcon.Folder);
            Topic topic = Topic.Create(
                section.Id,
                TopicName.Create("Topic").Value,
                TopicColor.Teal,
                TopicIcon.Bookmark);
            Article article = Article.Create(
                topic.Id,
                MaterialTitle.Create("Article").Value,
                MaterialDifficulty.Medium,
                MaterialIcon.DefaultArticle,
                MaterialExperienceRewards.Create(50, 20).Value).Value;
            dbContext.AddRange(section, topic, article);
            await dbContext.SaveChangesAsync();
        }

        string orphanDirectory = System.IO.Path.Combine(
            temporaryDirectory.Path,
            "materials",
            "articles",
            Guid.NewGuid().ToString("N"));
        string temporaryOperationDirectory = System.IO.Path.Combine(
            temporaryDirectory.Path,
            "materials",
            "questions",
            ".unfinished.tmp");
        string invalidDirectory = System.IO.Path.Combine(
            temporaryDirectory.Path,
            "materials",
            "questions",
            "invalid-name");
        Directory.CreateDirectory(orphanDirectory);
        Directory.CreateDirectory(temporaryOperationDirectory);
        Directory.CreateDirectory(invalidDirectory);

        IMaterialContentConsistencyService service =
            provider.GetRequiredService<IMaterialContentConsistencyService>();
        var result = await service.CheckAndRepairAsync(CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(1, result.Value.MissingContentCount);
        Assert.Equal(2, result.Value.QuarantinedDirectoryCount);
        Assert.Equal(1, result.Value.InvalidDirectoryCount);
        Assert.False(Directory.Exists(orphanDirectory));
        Assert.False(Directory.Exists(temporaryOperationDirectory));
        Assert.True(Directory.Exists(invalidDirectory));

        string recoveryDirectory = System.IO.Path.Combine(
            temporaryDirectory.Path,
            ".mnemora-data",
            "recovery",
            "material-content");
        Assert.Equal(2, Directory.EnumerateDirectories(recoveryDirectory).Count());
    }
}
