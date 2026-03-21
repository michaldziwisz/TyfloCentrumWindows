using Microsoft.UI.Xaml;
using Tyflocentrum.Windows.Domain.Models;
using Tyflocentrum.Windows.Domain.Services;
using Tyflocentrum.Windows.UI.ViewModels;

namespace Tyflocentrum.Windows.App.Services;

public sealed class ContentEntryActionService
{
    private readonly AudioPlayerDialogService _audioPlayerDialogService;
    private readonly IAudioPlaybackRequestFactory _audioPlaybackRequestFactory;
    private readonly InAppBrowserDialogService _inAppBrowserDialogService;

    public ContentEntryActionService(
        AudioPlayerDialogService audioPlayerDialogService,
        IAudioPlaybackRequestFactory audioPlaybackRequestFactory,
        InAppBrowserDialogService inAppBrowserDialogService
    )
    {
        _audioPlayerDialogService = audioPlayerDialogService;
        _audioPlaybackRequestFactory = audioPlaybackRequestFactory;
        _inAppBrowserDialogService = inAppBrowserDialogService;
    }

    public Task<bool> OpenDefaultAsync(
        ContentPostItemViewModel item,
        XamlRoot? xamlRoot,
        CancellationToken cancellationToken = default
    )
    {
        ArgumentNullException.ThrowIfNull(item);

        return OpenDefaultAsync(
            item.Source,
            item.PostId,
            item.Title,
            item.PublishedDate,
            item.Link,
            xamlRoot,
            cancellationToken
        );
    }

    public Task<bool> OpenDefaultAsync(
        NewsFeedItemViewModel item,
        XamlRoot? xamlRoot,
        CancellationToken cancellationToken = default
    )
    {
        ArgumentNullException.ThrowIfNull(item);

        return OpenDefaultAsync(
            item.Source,
            item.PostId,
            item.Title,
            item.PublishedDate,
            item.Link,
            xamlRoot,
            cancellationToken
        );
    }

    public Task<bool> OpenDefaultAsync(
        ContentSource source,
        int postId,
        string title,
        string publishedDate,
        string link,
        XamlRoot? xamlRoot,
        CancellationToken cancellationToken = default
    )
    {
        return source == ContentSource.Podcast
            ? OpenPodcastAsync(postId, title, publishedDate, xamlRoot, cancellationToken)
            : OpenArticleAsync(source, postId, title, publishedDate, link, xamlRoot, cancellationToken);
    }

    public Task<bool> OpenArticleAsync(
        ContentSource source,
        int postId,
        string title,
        string publishedDate,
        string link,
        XamlRoot? xamlRoot,
        CancellationToken cancellationToken = default
    )
    {
        return _inAppBrowserDialogService.ShowAsync(
            source,
            postId,
            title,
            publishedDate,
            link,
            xamlRoot,
            cancellationToken
        );
    }

    public Task<bool> OpenPodcastAsync(
        int postId,
        string title,
        string subtitle,
        XamlRoot? xamlRoot,
        CancellationToken cancellationToken = default
    )
    {
        var request = _audioPlaybackRequestFactory.CreatePodcast(postId, title, subtitle);
        return _audioPlayerDialogService.ShowAsync(request, xamlRoot, cancellationToken);
    }
}
