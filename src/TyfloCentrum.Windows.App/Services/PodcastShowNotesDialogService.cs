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
    private readonly IExternalLinkLauncher _externalLinkLauncher;
    private readonly IServiceProvider _serviceProvider;

    public PodcastShowNotesDialogService(
        IServiceProvider serviceProvider,
        AudioPlayerDialogService audioPlayerDialogService,
        IAudioPlaybackRequestFactory audioPlaybackRequestFactory,
        IExternalLinkLauncher externalLinkLauncher
    )
    {
        _serviceProvider = serviceProvider;
        _audioPlayerDialogService = audioPlayerDialogService;
        _audioPlaybackRequestFactory = audioPlaybackRequestFactory;
        _externalLinkLauncher = externalLinkLauncher;
    }

    public async Task<bool> ShowAsync(
        PodcastShowNotesSection section,
        int postId,
        string title,
        string subtitle,
        PodcastShowNotesSnapshot snapshot,
        XamlRoot? xamlRoot,
        CancellationToken cancellationToken = default
    )
    {
        if (xamlRoot is null || !HasSectionContent(snapshot, section))
        {
            return false;
        }

        var view = _serviceProvider.GetRequiredService<PodcastShowNotesDialogView>();
        ContentDialog? dialog = null;

        switch (section)
        {
            case PodcastShowNotesSection.Comments:
                view.InitializeComments(title, PodcastCommentThreadBuilder.Build(snapshot.Comments));
                break;
            case PodcastShowNotesSection.ChapterMarkers:
                view.InitializeChapterMarkers(
                    title,
                    snapshot.Markers.Select(CreateChapterMarkerItem).ToArray(),
                    async item =>
                    {
                        dialog?.Hide();
                        var request = _audioPlaybackRequestFactory.CreatePodcast(
                            postId,
                            title,
                            subtitle,
                            item.Seconds
                        );
                        return await _audioPlayerDialogService.ShowAsync(request, xamlRoot, cancellationToken);
                    }
                );
                break;
            case PodcastShowNotesSection.RelatedLinks:
                view.InitializeRelatedLinks(
                    title,
                    snapshot.Links.Select(CreateRelatedLinkItem).ToArray(),
                    item => _externalLinkLauncher.LaunchAsync(item.Url.AbsoluteUri, cancellationToken)
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
    }

    private static bool HasSectionContent(PodcastShowNotesSnapshot snapshot, PodcastShowNotesSection section)
    {
        return section switch
        {
            PodcastShowNotesSection.Comments => snapshot.HasComments,
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

    private static PodcastChapterMarkerItemViewModel CreateChapterMarkerItem(ChapterMarker marker)
    {
        return new PodcastChapterMarkerItemViewModel(
            marker.Title,
            marker.Seconds,
            FormatTime(marker.Seconds)
        );
    }

    private static PodcastRelatedLinkItemViewModel CreateRelatedLinkItem(RelatedLink link)
    {
        return new PodcastRelatedLinkItemViewModel(link.Title, link.Url, GetHostLabel(link.Url));
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
