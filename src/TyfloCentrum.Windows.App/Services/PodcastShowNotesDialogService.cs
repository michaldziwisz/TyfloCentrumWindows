using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using TyfloCentrum.Windows.App.Views;
using TyfloCentrum.Windows.Domain.Models;
using TyfloCentrum.Windows.Domain.Services;
using TyfloCentrum.Windows.UI.ViewModels;

namespace TyfloCentrum.Windows.App.Services;

public sealed class PodcastShowNotesDialogService
{
    private readonly AudioPlayerDialogService _audioPlayerDialogService;
    private readonly IAudioPlaybackRequestFactory _audioPlaybackRequestFactory;
    private readonly IClipboardService _clipboardService;
    private readonly IExternalLinkLauncher _externalLinkLauncher;
    private readonly IFavoritesService _favoritesService;
    private readonly IShareService _shareService;
    private readonly IServiceProvider _serviceProvider;
    private readonly IWordPressCommentsService _wordPressCommentsService;

    public PodcastShowNotesDialogService(
        IServiceProvider serviceProvider,
        AudioPlayerDialogService audioPlayerDialogService,
        IAudioPlaybackRequestFactory audioPlaybackRequestFactory,
        IClipboardService clipboardService,
        IExternalLinkLauncher externalLinkLauncher,
        IFavoritesService favoritesService,
        IShareService shareService,
        IWordPressCommentsService wordPressCommentsService
    )
    {
        _serviceProvider = serviceProvider;
        _audioPlayerDialogService = audioPlayerDialogService;
        _audioPlaybackRequestFactory = audioPlaybackRequestFactory;
        _clipboardService = clipboardService;
        _externalLinkLauncher = externalLinkLauncher;
        _favoritesService = favoritesService;
        _shareService = shareService;
        _wordPressCommentsService = wordPressCommentsService;
    }

    public async Task<bool> ShowAsync(
        PodcastShowNotesSection section,
        int postId,
        string title,
        string subtitle,
        PodcastShowNotesSnapshot snapshot,
        XamlRoot? xamlRoot,
        CancellationToken cancellationToken = default,
        int? replyToCommentId = null
    )
    {
        if (xamlRoot is null || !HasSectionContent(snapshot, section))
        {
            return false;
        }
        ContentDialog? dialog = null;

        try
        {
            var view = _serviceProvider.GetRequiredService<PodcastShowNotesDialogView>();

            switch (section)
            {
                case PodcastShowNotesSection.Comments:
                    view.InitializeComments(
                        postId,
                        title,
                        subtitle,
                        PodcastCommentThreadBuilder.Build(snapshot.Comments),
                        async cancellationTokenInner =>
                            PodcastCommentThreadBuilder.Build(
                                (await _wordPressCommentsService.GetCommentsAsync(
                                    postId,
                                    cancellationTokenInner,
                                    forceRefresh: true
                                )).ToArray()
                            )
                    );
                    if (replyToCommentId is int initialReplyCommentId && initialReplyCommentId > 0)
                    {
                        view.BeginReplyToComment(initialReplyCommentId);
                    }
                    break;
                case PodcastShowNotesSection.ChapterMarkers:
                    var chapterMarkers = await LoadChapterMarkerFavoritesAsync(
                        snapshot.Markers,
                        postId,
                        cancellationToken
                    );
                    view.InitializeChapterMarkers(
                        postId,
                        title,
                        subtitle,
                        chapterMarkers,
                        async item =>
                        {
                            dialog?.Hide();
                            var request = _audioPlaybackRequestFactory.CreatePodcast(
                                postId,
                                title,
                                subtitle,
                                item.Seconds
                            );
                            return await _audioPlayerDialogService.ShowAsync(
                                request,
                                xamlRoot,
                                cancellationToken
                            );
                        },
                        item => ToggleChapterMarkerFavoriteAsync(item, postId, title, subtitle)
                    );
                    break;
                case PodcastShowNotesSection.RelatedLinks:
                    var relatedLinks = await LoadRelatedLinkFavoritesAsync(
                        snapshot.Links,
                        postId,
                        cancellationToken
                    );
                    view.InitializeRelatedLinks(
                        postId,
                        title,
                        subtitle,
                        relatedLinks,
                        item => _externalLinkLauncher.LaunchAsync(item.Url.AbsoluteUri),
                        item => _clipboardService.SetTextAsync(item.Url.AbsoluteUri),
                        item => _shareService.ShareLinkAsync(item.Title, title, item.Url.AbsoluteUri),
                        item => ToggleRelatedLinkFavoriteAsync(item, postId, title, subtitle)
                    );
                    break;
                default:
                    return false;
            }

            dialog = new ContentDialog
            {
                XamlRoot = xamlRoot,
                Title = GetDialogTitle(section),
                CloseButtonText = "Zamknij",
                DefaultButton = ContentDialogButton.Close,
                FullSizeDesired = true,
                Content = view,
            };

            cancellationToken.ThrowIfCancellationRequested();
            await Task.Yield();
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
    }

    private static bool HasSectionContent(PodcastShowNotesSnapshot snapshot, PodcastShowNotesSection section)
    {
        return section switch
        {
            PodcastShowNotesSection.Comments => true,
            PodcastShowNotesSection.ChapterMarkers => snapshot.HasChapterMarkers,
            PodcastShowNotesSection.RelatedLinks => snapshot.HasRelatedLinks,
            _ => false,
        };
    }

    private static string GetDialogTitle(PodcastShowNotesSection section)
    {
        return section switch
        {
            PodcastShowNotesSection.Comments => "Komentarze podcastu",
            PodcastShowNotesSection.ChapterMarkers => "Znaczniki czasu",
            PodcastShowNotesSection.RelatedLinks => "Odnośniki",
            _ => "Dodatki podcastu",
        };
    }

    private async Task<PodcastChapterMarkerItemViewModel[]> LoadChapterMarkerFavoritesAsync(
        IReadOnlyList<ChapterMarker> markers,
        int podcastPostId,
        CancellationToken cancellationToken
    )
    {
        var tasks = markers.Select(async marker =>
        {
            var favoriteId = FavoriteItem.CreateTopicId(podcastPostId, marker.Title, marker.Seconds);
            var isFavorite = await _favoritesService.IsFavoriteAsync(favoriteId, cancellationToken);
            return new PodcastChapterMarkerItemViewModel(
                marker.Title,
                marker.Seconds,
                FormatTime(marker.Seconds),
                isFavorite
            );
        });

        return await Task.WhenAll(tasks);
    }

    private async Task<PodcastRelatedLinkItemViewModel[]> LoadRelatedLinkFavoritesAsync(
        IReadOnlyList<RelatedLink> links,
        int podcastPostId,
        CancellationToken cancellationToken
    )
    {
        var tasks = links.Select(async link =>
        {
            var favoriteId = FavoriteItem.CreateLinkId(podcastPostId, link.Url.AbsoluteUri);
            var isFavorite = await _favoritesService.IsFavoriteAsync(favoriteId, cancellationToken);
            return new PodcastRelatedLinkItemViewModel(
                link.Title,
                link.Url,
                GetHostLabel(link.Url),
                isFavorite
            );
        });

        return await Task.WhenAll(tasks);
    }

    private async Task<bool> ToggleChapterMarkerFavoriteAsync(
        PodcastChapterMarkerItemViewModel item,
        int podcastPostId,
        string title,
        string subtitle
    )
    {
        var favoriteItem = new FavoriteItem
        {
            Id = FavoriteItem.CreateTopicId(podcastPostId, item.Title, item.Seconds),
            Kind = FavoriteKind.Topic,
            Source = ContentSource.Podcast,
            PostId = podcastPostId,
            Title = item.Title,
            Subtitle = title,
            PublishedDate = subtitle,
            ContextTitle = title,
            ContextSubtitle = subtitle,
            StartPositionSeconds = item.Seconds,
            SavedAtUtc = DateTimeOffset.UtcNow,
        };

        if (item.IsFavorite)
        {
            await _favoritesService.RemoveAsync(favoriteItem.Id);
            return true;
        }

        await _favoritesService.AddOrUpdateAsync(favoriteItem);
        return true;
    }

    private async Task<bool> ToggleRelatedLinkFavoriteAsync(
        PodcastRelatedLinkItemViewModel item,
        int podcastPostId,
        string title,
        string subtitle
    )
    {
        var favoriteItem = new FavoriteItem
        {
            Id = FavoriteItem.CreateLinkId(podcastPostId, item.Url.AbsoluteUri),
            Kind = FavoriteKind.Link,
            Source = ContentSource.Podcast,
            PostId = podcastPostId,
            Title = item.Title,
            Subtitle = item.HostLabel,
            PublishedDate = subtitle,
            Link = item.Url.AbsoluteUri,
            ContextTitle = title,
            ContextSubtitle = subtitle,
            SavedAtUtc = DateTimeOffset.UtcNow,
        };

        if (item.IsFavorite)
        {
            await _favoritesService.RemoveAsync(favoriteItem.Id);
            return true;
        }

        await _favoritesService.AddOrUpdateAsync(favoriteItem);
        return true;
    }

    private static string FormatTime(double totalSeconds)
    {
        var roundedSeconds = Math.Max(0, (int)Math.Round(totalSeconds, MidpointRounding.AwayFromZero));
        var time = TimeSpan.FromSeconds(roundedSeconds);
        return roundedSeconds >= 3600
            ? $"{(int)time.TotalHours:00}:{time.Minutes:00}:{time.Seconds:00}"
            : $"{time.Minutes:00}:{time.Seconds:00}";
    }

    private static string GetHostLabel(Uri url)
    {
        if (!string.IsNullOrWhiteSpace(url.Host))
        {
            return url.Host;
        }

        return string.Equals(url.Scheme, "mailto", StringComparison.OrdinalIgnoreCase)
            ? "e-mail"
            : url.Scheme;
    }
}
