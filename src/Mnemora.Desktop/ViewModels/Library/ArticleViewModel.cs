using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MaterialDesignThemes.Wpf;
using Microsoft.Extensions.Logging;
using Mnemora.Application.Library.GetContainerContents;
using Mnemora.Application.Materials.GetDetails;
using Mnemora.Application.Queries;
using Mnemora.Contracts;
using Mnemora.Contracts.Library;
using Mnemora.Desktop.Editors;
using Mnemora.Desktop.Navigation;
using Mnemora.Desktop.ViewModels.Common;

namespace Mnemora.Desktop.ViewModels.Library;

public sealed partial class ArticleViewModel(
    IQueryDispatcher queryDispatcher,
    IPageNavigationService pageNavigationService,
    IMaterialEditorService materialEditorService,
    ILogger<ArticleViewModel> logger)
    : ViewModelBase
{
    private Guid _articleId;
    private CancellationToken _viewCancellationToken;

    public LibraryArticleOpenOrigin Origin { get; private set; }

    public ObservableCollection<LibraryBreadcrumbItemViewModel> Breadcrumbs { get; } = [];

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasArticle))]
    [NotifyPropertyChangedFor(nameof(Title))]
    [NotifyPropertyChangedFor(nameof(SectionName))]
    [NotifyPropertyChangedFor(nameof(FolderPath))]
    [NotifyPropertyChangedFor(nameof(DifficultyText))]
    [NotifyPropertyChangedFor(nameof(IconKind))]
    [NotifyPropertyChangedFor(nameof(Tags))]
    [NotifyPropertyChangedFor(nameof(BodyMarkdown))]
    [NotifyCanExecuteChangedFor(nameof(EditCommand))]
    private ArticleDetailsDto? _article;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasArticle))]
    private bool _isLoading;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasError))]
    [NotifyPropertyChangedFor(nameof(HasArticle))]
    private string? _errorMessage;

    public bool HasError => !string.IsNullOrWhiteSpace(ErrorMessage);

    public bool HasArticle => Article is not null && !IsLoading && !HasError;

    public string Title => Article?.Metadata.Title ?? string.Empty;

    public string SectionName => Article?.Metadata.SectionName ?? string.Empty;

    public string FolderPath => Article?.Metadata.ContainerPath ?? string.Empty;

    public string DifficultyText => Article?.Metadata.Difficulty switch
    {
        "Easy" => "Лёгкий",
        "Medium" => "Средний",
        "Hard" => "Сложный",
        _ => Article?.Metadata.Difficulty ?? string.Empty,
    };

    public PackIconKind IconKind
    {
        get
        {
            string? icon = Article?.Metadata.Icon;

            return !string.IsNullOrWhiteSpace(icon) &&
                   Enum.TryParse(icon, true, out PackIconKind parsedIcon)
                ? parsedIcon
                : PackIconKind.FileDocumentOutline;
        }
    }

    public IReadOnlyList<string> Tags =>
        Article?.Metadata.Tags ?? Array.Empty<string>();

    public string BodyMarkdown => Article?.BodyMarkdown ?? string.Empty;

    public void Initialize(
        Guid articleId,
        LibraryArticleOpenOrigin origin)
    {
        if (articleId == Guid.Empty)
        {
            throw new ArgumentException(
                "Article id cannot be empty.",
                nameof(articleId));
        }

        _articleId = articleId;
        Origin = origin;
    }

    public async Task LoadAsync(CancellationToken cancellationToken = default)
    {
        if (_articleId == Guid.Empty)
        {
            throw new InvalidOperationException(
                "Статья не была инициализирована перед загрузкой.");
        }

        _viewCancellationToken = cancellationToken;
        await LoadCoreAsync(cancellationToken);
    }

    [RelayCommand]
    private void NavigateLibrary()
    {
        pageNavigationService.NavigateTo<LibraryOverviewViewModel>();
    }

    [RelayCommand]
    private void NavigateBreadcrumb(LibraryBreadcrumbItemViewModel? item)
    {
        if (item is null || item.IsCurrent) return;

        pageNavigationService.NavigateTo<LibraryContainerViewModel>(
            viewModel => viewModel.Initialize(item.ContainerId));
    }

    [RelayCommand(CanExecute = nameof(CanEdit))]
    private async Task EditAsync(CancellationToken cancellationToken)
    {
        using var linkedCancellationTokenSource =
            CancellationTokenSource.CreateLinkedTokenSource(
                _viewCancellationToken,
                cancellationToken);

        await materialEditorService.OpenArticleInEditorAsync(
            _articleId,
            linkedCancellationTokenSource.Token);
    }

    private bool CanEdit() => Article is not null && _articleId != Guid.Empty;

    [RelayCommand]
    private async Task RetryAsync(CancellationToken cancellationToken)
    {
        using var linkedCancellationTokenSource =
            CancellationTokenSource.CreateLinkedTokenSource(
                _viewCancellationToken,
                cancellationToken);

        await LoadCoreAsync(linkedCancellationTokenSource.Token);
    }

    private async Task BuildBreadcrumbsAsync(Guid containerId, CancellationToken cancellationToken)
    {
        try
        {
            var path = new List<LibraryContainerContentsDto>();
            var visited = new HashSet<Guid>();
            Guid? currentId = containerId;

            while (currentId is { } id && visited.Add(id))
            {
                var result = await queryDispatcher.SendAsync<
                    GetLibraryContainerContentsQuery,
                    LibraryContainerContentsDto>(
                    new GetLibraryContainerContentsQuery(id),
                    cancellationToken);

                if (result.IsFailure)
                {
                    logger.LogWarning(
                        "Не удалось построить breadcrumbs статьи {ArticleId}: контейнер {ContainerId} не найден",
                        _articleId,
                        id);
                    break;
                }

                LibraryContainerContentsDto current = result.Value;
                path.Add(current);
                currentId = current.Container.ParentId;
            }

            path.Reverse();
            Breadcrumbs.Clear();

            if (path.Count == 0) return;

            for (int index = 0; index < path.Count; index++)
            {
                LibraryContainerHeaderDto container = path[index].Container;
                Breadcrumbs.Add(new LibraryBreadcrumbItemViewModel(
                    container.Id,
                    container.Name,
                    container.Depth,
                    index == path.Count - 1));
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception)
        {
            Breadcrumbs.Clear();
            logger.LogWarning(
                exception,
                "Не удалось построить breadcrumbs статьи {ArticleId}",
                _articleId);
        }
    }

    private async Task LoadCoreAsync(CancellationToken cancellationToken)
    {
        IsLoading = true;
        ErrorMessage = null;

        try
        {
            var result = await queryDispatcher.SendAsync<
                GetMaterialDetailsQuery,
                MaterialDetailsDto>(
                new GetMaterialDetailsQuery(_articleId),
                cancellationToken);

            if (cancellationToken.IsCancellationRequested)
            {
                return;
            }

            if (result.IsFailure)
            {
                Article = null;
                ErrorMessage = result.Error.FirstOrDefault()?.Message
                    ?? "Не удалось загрузить статью.";
                return;
            }

            if (result.Value is not ArticleDetailsDto article)
            {
                Article = null;
                ErrorMessage = "Выбранный материал не является статьёй.";
                return;
            }

            Article = article;
            await BuildBreadcrumbsAsync(article.Metadata.ContainerId, cancellationToken);
        }
        catch (OperationCanceledException)
            when (cancellationToken.IsCancellationRequested)
        {
            // Уход со страницы отменяет чтение статьи.
        }
        catch (Exception exception)
        {
            logger.LogError(
                exception,
                "Не удалось открыть статью {ArticleId} внутри Mnemora",
                _articleId);

            Article = null;
            ErrorMessage = "Не удалось загрузить статью.";
        }
        finally
        {
            IsLoading = false;
        }
    }
}
