using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using TyfloCentrum.Windows.App.Views;
using TyfloCentrum.Windows.Domain.Models;
using TyfloCentrum.Windows.UI.ViewModels;

namespace TyfloCentrum.Windows.App.Services;

public sealed class PostDetailDialogService
{
    private readonly AudioPlayerDialogService _audioPlayerDialogService;
    private readonly CommentDetailDialogService _commentDetailDialogService;
    private readonly ContentEntryActionService _contentEntryActionService;
    private readonly IServiceProvider _serviceProvider;

    public PostDetailDialogService(
        IServiceProvider serviceProvider,
        ContentEntryActionService contentEntryActionService,
        AudioPlayerDialogService audioPlayerDialogService,
        CommentDetailDialogService commentDetailDialogService
    )
    {
        _serviceProvider = serviceProvider;
        _contentEntryActionService = contentEntryActionService;
        _audioPlayerDialogService = audioPlayerDialogService;
        _commentDetailDialogService = commentDetailDialogService;
    }

    public async Task ShowAsync(
        ContentSource source,
        int postId,
        string fallbackTitle,
        string fallbackDate,
        string fallbackLink,
        XamlRoot xamlRoot
    )
    {
        var view = _serviceProvider.GetRequiredService<PostDetailView>();
        view.ViewModel.Initialize(source, postId, fallbackTitle, fallbackDate, fallbackLink);
        ContentDialog? dialog = null;
        PendingPostDetailAction pendingAction = PendingPostDetailAction.None;
        AudioPlaybackRequest? pendingPlaybackRequest = null;
        CommentItemViewModel? pendingCommentItem = null;

        view.BrowserRequestHandler = () =>
        {
            pendingAction = PendingPostDetailAction.Browser;
            dialog?.Hide();
            return Task.CompletedTask;
        };

        view.ShareRequestHandler = () =>
        {
            pendingAction = PendingPostDetailAction.Share;
            dialog?.Hide();
            return Task.CompletedTask;
        };

        view.PlaybackRequestHandler = request =>
        {
            pendingAction = PendingPostDetailAction.Playback;
            pendingPlaybackRequest = request;
            dialog?.Hide();
            return Task.CompletedTask;
        };

        view.CommentDetailRequestHandler = item =>
        {
            pendingAction = PendingPostDetailAction.CommentDetails;
            pendingCommentItem = item;
            dialog?.Hide();
            return Task.CompletedTask;
        };

        dialog = new ContentDialog
        {
            XamlRoot = xamlRoot,
            Title = "Szczegóły wpisu",
            CloseButtonText = "Zamknij",
            DefaultButton = ContentDialogButton.Close,
            FullSizeDesired = true,
            Content = view,
        };

        await dialog.ShowAsync();

        switch (pendingAction)
        {
            case PendingPostDetailAction.Browser:
                var articleOpened = await _contentEntryActionService.OpenArticleAsync(
                    view.ViewModel.Source,
                    view.ViewModel.PostId,
                    view.ViewModel.Title,
                    view.ViewModel.PublishedDate,
                    view.ViewModel.Link,
                    xamlRoot
                );
                if (!articleOpened)
                {
                    await DialogHelpers.ShowErrorAsync(
                        xamlRoot,
                        "Nie udało się otworzyć artykułu w aplikacji."
                    );
                }

                break;
            case PendingPostDetailAction.Share:
                await view.ViewModel.ShareAsync();
                if (view.ViewModel.HasError)
                {
                    await DialogHelpers.ShowErrorAsync(
                        xamlRoot,
                        view.ViewModel.ErrorMessage ?? "Nie udało się udostępnić wpisu."
                    );
                }

                break;
            case PendingPostDetailAction.Playback when pendingPlaybackRequest is not null:
                var shown = await _audioPlayerDialogService.ShowAsync(pendingPlaybackRequest, xamlRoot);
                if (!shown)
                {
                    await DialogHelpers.ShowErrorAsync(
                        xamlRoot,
                        "Nie udało się uruchomić odtwarzacza podcastu."
                    );
                }

                break;
            case PendingPostDetailAction.CommentDetails when pendingCommentItem is not null:
                var commentShown = await _commentDetailDialogService.ShowAsync(
                    pendingCommentItem,
                    xamlRoot
                );
                if (!commentShown)
                {
                    await DialogHelpers.ShowErrorAsync(
                        xamlRoot,
                        "Nie udało się otworzyć szczegółów komentarza."
                    );
                }

                break;
        }
    }

    private enum PendingPostDetailAction
    {
        None,
        Browser,
        Share,
        Playback,
        CommentDetails,
    }
}
