using System.Data.Common;
using System.Reflection;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Mnemora.Domain.LibraryContainers;
using Mnemora.Domain.Materials;
using Mnemora.Domain.Sections;
using Mnemora.Domain.Topics;
using Mnemora.Infrastructure.Persistence;

namespace Mnemora.Application.IntegrationTests.Library;

internal sealed class SqliteLibraryTestDatabase : IAsyncDisposable
{
    private const string UnicodeContainsFunctionName =
        "MNEMORA_UNICODE_CONTAINS";

    private SqliteLibraryTestDatabase(
        SqliteConnection connection,
        MnemoraDbContext context,
        CountingDbCommandInterceptor commandCounter)
    {
        Connection = connection;
        Context = context;
        CommandCounter = commandCounter;
    }

    public SqliteConnection Connection { get; }
    public MnemoraDbContext Context { get; }
    public CountingDbCommandInterceptor CommandCounter { get; }

    public static async Task<SqliteLibraryTestDatabase> CreateAsync(
        CancellationToken cancellationToken = default)
    {
        var connection =
            new SqliteConnection(
                "Data Source=:memory:;Cache=Shared");

        await connection.OpenAsync(cancellationToken);

        // Register the same SQLite runtime extensions that production queries
        // rely on. The function/collation are connection-scoped in SQLite.
        RegisterSqliteExtensions(connection);

        var commandCounter =
            new CountingDbCommandInterceptor();

        var options =
            new DbContextOptionsBuilder<MnemoraDbContext>()
                .UseSqlite(connection)
                .AddInterceptors(commandCounter)
                .EnableDetailedErrors()
                .Options;

        var context = new MnemoraDbContext(options);

        await context.Database.EnsureCreatedAsync(cancellationToken);
        commandCounter.Reset();

        return new SqliteLibraryTestDatabase(
            connection,
            context,
            commandCounter);
    }

    public LibraryContainer AddSectionWithRoot(Section section)
    {
        ArgumentNullException.ThrowIfNull(section);

        LibraryContainer root =
            LibraryContainer.CreateRoot(section.Id).Value;

        Context.Sections.Add(section);
        Context.LibraryContainers.Add(root);

        return root;
    }

    public async Task<(Section Section, Topic Topic)> CreateSectionAndTopicAsync(
        string sectionName = "Section",
        string topicName = "Topic",
        CancellationToken cancellationToken = default)
    {
        Section section = Section.Create(
            SectionName.Create(sectionName).Value,
            Enum.GetValues<SectionColor>()[0],
            Enum.GetValues<SectionIcon>()[0]);

        LibraryContainer root =
            LibraryContainer.CreateRoot(section.Id).Value;

        Topic topic = Topic.Create(
            section.Id,
            TopicName.Create(topicName).Value,
            Enum.GetValues<TopicColor>()[0],
            Enum.GetValues<TopicIcon>()[0]);

        LibraryContainer folder =
            LibraryContainer.CreateFolderWithId(
                LibraryContainerId.Create(topic.Id.Value).Value,
                root,
                FolderName.Create(topic.Name.Value).Value,
                Enum.Parse<FolderColor>(topic.Color.ToString()),
                Enum.Parse<FolderIcon>(topic.Icon.ToString())).Value;

        Context.Sections.Add(section);
        Context.LibraryContainers.AddRange(root, folder);
        Context.Topics.Add(topic);

        await Context.SaveChangesAsync(cancellationToken);
        Context.ChangeTracker.Clear();
        CommandCounter.Reset();

        return (section, topic);
    }

    public Article CreateArticle(
        TopicId topicId,
        string title)
    {
        return Article.Create(
            topicId,
            MaterialTitle.Create(title).Value,
            MaterialDifficulty.Medium,
            MaterialIcon.DefaultArticle,
            MaterialExperienceRewards.Create(50, 20).Value,
            Array.Empty<MaterialTag>()).Value;
    }

    public Question CreateStandaloneQuestion(
        TopicId topicId,
        string title)
    {
        return Question.CreateStandalone(
            topicId,
            MaterialTitle.Create(title).Value,
            MaterialDifficulty.Medium,
            MaterialIcon.DefaultQuestion,
            MaterialExperienceRewards.Create(50, 20).Value,
            Array.Empty<MaterialTag>()).Value;
    }

    public Question CreateLinkedQuestion(
        Article article,
        string title)
    {
        return Question.CreateForArticle(
            article,
            MaterialTitle.Create(title).Value,
            MaterialDifficulty.Medium,
            MaterialIcon.DefaultQuestion,
            MaterialExperienceRewards.Create(50, 20).Value,
            Array.Empty<MaterialTag>()).Value;
    }

    public async Task AddMaterialsAsync(
        params Material[] materials)
    {
        Context.Materials.AddRange(materials);

        await Context.SaveChangesAsync();
        Context.ChangeTracker.Clear();
        CommandCounter.Reset();
    }

    public async Task AddMaterialsInBatchesAsync(
        IEnumerable<Material> materials,
        int batchSize = 2_000)
    {
        var batch = new List<Material>(batchSize);

        foreach (Material material in materials)
        {
            batch.Add(material);

            if (batch.Count < batchSize)
            {
                continue;
            }

            Context.Materials.AddRange(batch);
            await Context.SaveChangesAsync();
            Context.ChangeTracker.Clear();
            batch.Clear();
        }

        if (batch.Count > 0)
        {
            Context.Materials.AddRange(batch);
            await Context.SaveChangesAsync();
            Context.ChangeTracker.Clear();
        }

        CommandCounter.Reset();
    }

    public async ValueTask DisposeAsync()
    {
        await Context.DisposeAsync();
        await Connection.DisposeAsync();
    }

    private static void RegisterSqliteExtensions(
        SqliteConnection connection)
    {
        Assembly infrastructureAssembly =
            typeof(MnemoraDbContext).Assembly;

        Type? collationsType =
            infrastructureAssembly.GetType(
                "Mnemora.Infrastructure.Persistence.SqliteCollations",
                throwOnError: false);

        const BindingFlags flags =
            BindingFlags.Public |
            BindingFlags.NonPublic |
            BindingFlags.Static;

        FieldInfo? field =
            collationsType?.GetField(
                "UnicodeNoCase",
                flags);

        PropertyInfo? property =
            collationsType?.GetProperty(
                "UnicodeNoCase",
                flags);

        string collationName =
            field?.GetValue(null) as string
            ?? property?.GetValue(null) as string
            ?? throw new InvalidOperationException(
                "Не найдено имя Unicode collation Mnemora. " +
                "Обновите тестовый bootstrap вместе с SqliteCollations.");

        connection.CreateCollation(
            collationName,
            StringComparer.OrdinalIgnoreCase.Compare);

        connection.CreateFunction<string?, string?, bool>(
            UnicodeContainsFunctionName,
            static (source, search) =>
                source is not null &&
                search is not null &&
                source.Contains(
                    search,
                    StringComparison.OrdinalIgnoreCase));
    }
}

internal sealed class CountingDbCommandInterceptor
    : DbCommandInterceptor
{
    private int _count;

    public int Count => Volatile.Read(ref _count);

    public void Reset() =>
        Interlocked.Exchange(ref _count, 0);

    public override InterceptionResult<DbDataReader> ReaderExecuting(
        DbCommand command,
        CommandEventData eventData,
        InterceptionResult<DbDataReader> result)
    {
        Interlocked.Increment(ref _count);
        return result;
    }

    public override ValueTask<InterceptionResult<DbDataReader>> ReaderExecutingAsync(
        DbCommand command,
        CommandEventData eventData,
        InterceptionResult<DbDataReader> result,
        CancellationToken cancellationToken = default)
    {
        Interlocked.Increment(ref _count);
        return ValueTask.FromResult(result);
    }

    public override InterceptionResult<object> ScalarExecuting(
        DbCommand command,
        CommandEventData eventData,
        InterceptionResult<object> result)
    {
        Interlocked.Increment(ref _count);
        return result;
    }

    public override ValueTask<InterceptionResult<object>> ScalarExecutingAsync(
        DbCommand command,
        CommandEventData eventData,
        InterceptionResult<object> result,
        CancellationToken cancellationToken = default)
    {
        Interlocked.Increment(ref _count);
        return ValueTask.FromResult(result);
    }
}
