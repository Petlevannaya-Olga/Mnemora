using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Mnemora.Domain.LibraryContainers;
using Mnemora.Domain.Materials;
using Mnemora.Domain.Sections;
using Mnemora.Infrastructure.Persistence;
using Xunit;

namespace Mnemora.Infrastructure.Tests;

public sealed class MaterialContainerPersistenceTests
{
    [Fact]
    public async Task Material_PersistsRootAndNestedFolder()
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
        LibraryContainer root = LibraryContainer.CreateRoot(section.Id).Value;
        LibraryContainer folder = LibraryContainer.CreateFolder(
            root,
            FolderName.Create("Memory").Value,
            FolderColor.Teal,
            FolderIcon.Folder).Value;

        Article article = Article.CreateInContainer(
            root.Id,
            MaterialTitle.Create("Garbage Collector").Value,
            MaterialDifficulty.Medium,
            null,
            MaterialExperienceRewards.Create(50, 20).Value).Value;

        Assert.True(article.MoveToContainer(folder.Id).IsSuccess);

        Question question = Question.CreateStandaloneInContainer(
            root.Id,
            MaterialTitle.Create("Independent question").Value,
            MaterialDifficulty.Medium,
            null,
            MaterialExperienceRewards.Create(50, 20).Value).Value;

        dbContext.Sections.Add(section);
        dbContext.LibraryContainers.AddRange(root, folder);
        dbContext.Materials.AddRange(article, question);

        await dbContext.SaveChangesAsync(ct);
        dbContext.ChangeTracker.Clear();

        Material persistedArticle = await dbContext.Materials
            .AsNoTracking()
            .SingleAsync(material => material.Id == article.Id, ct);
        Material persistedQuestion = await dbContext.Materials
            .AsNoTracking()
            .SingleAsync(material => material.Id == question.Id, ct);

        Assert.Equal(folder.Id, persistedArticle.ContainerId);
        Assert.Equal(root.Id, persistedQuestion.ContainerId);
    }

    private static Section CreateSection(string name) =>
        Section.Create(
            SectionName.Create(name).Value,
            Enum.GetValues<SectionColor>()[0],
            Enum.GetValues<SectionIcon>()[0]);
}
