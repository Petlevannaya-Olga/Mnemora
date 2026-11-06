namespace Mnemora.Desktop.Navigation;

public interface IMaterialNavigationService
{
    void OpenArticle(
        Guid articleId,
        LibraryArticleOpenOrigin origin);
}
