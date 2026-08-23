using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Mnemora.Domain.LibraryContainers;
using Mnemora.Domain.Materials;
using Mnemora.Domain.Sections;
using Mnemora.Domain.Topics;
using Mnemora.Infrastructure.Persistence;
using Xunit;

namespace Mnemora.Infrastructure.Tests;

public sealed class MaterialContainerPersistenceTests
{
    [Fact]
    public async Task Material_PersistsContainerIndependentlyFromLegacyTopicBridge()
    {
        CancellationToken ct = CancellationToken.None;

        using var temporaryDirectory = new TemporaryDirectory();
        await using ServiceProvider provider = TestServiceProviderFactory.Create(
            temporaryDirectory.Path);
        await using var scope = provider.CreateAsyncScope();

        var factory = scope.ServiceProvider
            .GetRequiredService<IDbContextFactory<MnemoraDbContext>>();

        await using MnemoraDbContext dbContext =
            await factory.CreateDbContextAsync(ct);

        await dbContext.Database.EnsureCreatedAsync(ct);

        Section section = CreateSection("C#");
        LibraryContainer root =
            LibraryContainer.CreateRoot(section.Id).Value;

        Topic topic = Topic.Create(
            section.Id,
            TopicName.Create("CLR").Value,
            TopicColor.Purple,
            TopicIcon.DotNet);

        LibraryContainer topicFolder =
            LibraryContainer.CreateFolderWithId(
                LibraryContainerId.Create(topic.Id.Value).Value,
                root,
                FolderName.Create("CLR").Value,
                FolderColor.Purple,
                FolderIcon.DotNet).Value;

        LibraryContainer nestedFolder =
            LibraryContainer.CreateFolder(
                topicFolder,
                FolderName.Create("Memory").Value,
                FolderColor.Teal,
                FolderIcon.Folder).Value;

        Article article = Article.Create(
            topic.Id,
            MaterialTitle.Create("Garbage Collector").Value,
            MaterialDifficulty.Medium,
            null,
            MaterialExperienceRewards.Create(50, 20).Value).Value;

        var moveResult = article.MoveToContainer(nestedFolder.Id);

        Assert.True(moveResult.IsSuccess);
        Assert.Equal(topic.Id, article.TopicId);
        Assert.Equal(nestedFolder.Id, article.ContainerId);

        dbContext.Sections.Add(section);
        dbContext.Topics.Add(topic);
        dbContext.LibraryContainers.AddRange(
            root,
            topicFolder,
            nestedFolder);
        dbContext.Materials.Add(article);

        await dbContext.SaveChangesAsync(ct);
        dbContext.ChangeTracker.Clear();

        Material persistedMaterial = await dbContext.Materials
            .AsNoTracking()
            .SingleAsync(material => material.Id == article.Id, ct);

        Assert.Equal(topic.Id, persistedMaterial.TopicId);
        Assert.Equal(nestedFolder.Id, persistedMaterial.ContainerId);
    }

    private static Section CreateSection(string name) =>
        Section.Create(
            SectionName.Create(name).Value,
            Enum.GetValues<SectionColor>()[0],
            Enum.GetValues<SectionIcon>()[0]);
}
