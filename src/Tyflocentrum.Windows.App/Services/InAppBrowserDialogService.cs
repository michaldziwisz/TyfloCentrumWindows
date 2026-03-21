using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Tyflocentrum.Windows.App.Views;
using Tyflocentrum.Windows.Domain.Models;
using Tyflocentrum.Windows.Domain.Services;
using Tyflocentrum.Windows.Domain.Text;

namespace Tyflocentrum.Windows.App.Services;

public sealed class InAppBrowserDialogService
{
    private readonly ITyfloSwiatMagazineService _magazineService;
    private readonly IWordPressPostDetailsService _postDetailsService;
    private readonly IServiceProvider _serviceProvider;

    public InAppBrowserDialogService(
        IServiceProvider serviceProvider,
        IWordPressPostDetailsService postDetailsService,
        ITyfloSwiatMagazineService magazineService
    )
    {
        _serviceProvider = serviceProvider;
        _postDetailsService = postDetailsService;
        _magazineService = magazineService;
    }

    public async Task<bool> ShowAsync(
        ContentSource source,
        int postId,
        string fallbackTitle,
        string fallbackDate,
        string fallbackLink,
        XamlRoot? xamlRoot,
        CancellationToken cancellationToken = default
    )
    {
        if (xamlRoot is null)
        {
            return false;
        }

        if (source != ContentSource.Article)
        {
            return false;
        }

        try
        {
            var post = await _postDetailsService.GetPostAsync(source, postId, cancellationToken);
            var title = WordPressContentText.NormalizeHtml(post.Title.Rendered);
            var link = string.IsNullOrWhiteSpace(post.Link) ? fallbackLink : post.Link;
            var readerHtml = ArticleReaderDocumentBuilder.Build(
                title,
                string.IsNullOrWhiteSpace(post.Date) ? fallbackDate : post.Date,
                link,
                post.Content.Rendered
            );
            return await ShowDocumentAsync(title, link, readerHtml, xamlRoot, cancellationToken);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch
        {
            return false;
        }
    }

    public async Task<bool> ShowTyfloSwiatPageAsync(
        int pageId,
        string fallbackTitle,
        string fallbackDate,
        string fallbackLink,
        XamlRoot? xamlRoot,
        CancellationToken cancellationToken = default
    )
    {
        if (xamlRoot is null)
        {
            return false;
        }

        try
        {
            var page = await _magazineService.GetPageAsync(pageId, cancellationToken);
            var title = WordPressContentText.NormalizeHtml(page.Title.Rendered);
            var link = string.IsNullOrWhiteSpace(page.Link) ? fallbackLink : page.Link;
            var readerHtml = ArticleReaderDocumentBuilder.Build(
                title,
                string.IsNullOrWhiteSpace(page.Date) ? fallbackDate : page.Date,
                link,
                page.Content.Rendered
            );
            return await ShowDocumentAsync(title, link, readerHtml, xamlRoot, cancellationToken);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch
        {
            return false;
        }
    }

    private async Task<bool> ShowDocumentAsync(
        string title,
        string link,
        string readerHtml,
        XamlRoot xamlRoot,
        CancellationToken cancellationToken
    )
    {
        var view = _serviceProvider.GetRequiredService<InAppBrowserView>();
        if (!view.Initialize(title, link, readerHtml))
        {
            return false;
        }

        ContentDialog? dialog = null;
        dialog = new ContentDialog
        {
            XamlRoot = xamlRoot,
            Title = "Czytanie artykułu",
            CloseButtonText = "Zamknij",
            DefaultButton = ContentDialogButton.Close,
            FullSizeDesired = true,
            Content = view,
        };
        view.CloseRequested = () => dialog?.Hide();

        try
        {
            cancellationToken.ThrowIfCancellationRequested();
            await dialog.ShowAsync();
            return true;
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch
        {
            return false;
        }
        finally
        {
            view.Cleanup();
        }
    }
}
