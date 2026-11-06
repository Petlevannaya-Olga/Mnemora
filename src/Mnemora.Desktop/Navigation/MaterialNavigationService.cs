using Mnemora.Desktop.ViewModels.Library;

namespace Mnemora.Desktop.Navigation;

public sealed class MaterialNavigationService(
    IPageNavigationService pageNavigationService)
    : IMaterialNavigationService
{
    public void OpenArticle(
        Guid articleId,
        LibraryArticleOpenOrigin origin)
    {
        if (articleId == Guid.Empty)
        {
            return;
        }

        pageNavigationService.NavigateTo<ArticleViewModel>(viewModel =>
            viewModel.Initialize(articleId, origin));
    }
}
