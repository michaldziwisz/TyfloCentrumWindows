using CommunityToolkit.Mvvm.ComponentModel;
using System.Collections.ObjectModel;
using TyfloCentrum.Windows.Domain.Models;
using TyfloCentrum.Windows.Domain.Services;
using TyfloCentrum.Windows.Domain.Text;
using TyfloCentrum.Windows.UI.Formatting;

namespace TyfloCentrum.Windows.UI.ViewModels;

public partial class PostDetailViewModel : ObservableObject
{
    private readonly IAudioPlaybackRequestFactory _audioPlaybackRequestFactory;
    private readonly IExternalLinkLauncher _externalLinkLauncher;
    private readonly IFavoritesService _favoritesService;
    private readonly IShareService _shareService;
    private readonly IWordPressCommentsService _commentsService;
    private readonly IWordPressPostDetailsService _postDetailsService;
    private bool _hasRequestedInitialLoad;

    public PostDetailViewModel(
        IAudioPlaybackRequestFactory audioPlaybackRequestFactory,
        IWordPressPostDetailsService postDetailsService,
        IWordPressCommentsService commentsService,
        IExternalLinkLauncher externalLinkLauncher,
        IFavoritesService favoritesService,
        IShareService shareService
    )
    {
        _audioPlaybackRequestFactory = audioPlaybackRequestFactory;
        _postDetailsService = postDetailsService;
        _commentsService = commentsService;
        _externalLinkLauncher = externalLinkLauncher;
        _favoritesService = favoritesService;
        _shareService = shareService;
    }

    public ObservableCollection<CommentItemViewModel> Comments { get; } = [];

    public int PostId { get; private set; }

    public ContentSource Source { get; private set; }

    [ObservableProperty]
    private string title = string.Empty;

    [ObservableProperty]
    private string publishedDate = string.Empty;

    [ObservableProperty]
    private string contentText = string.Empty;

    [ObservableProperty]
    private string link = string.Empty;

    [ObservableProperty]
    private bool isLoading;

    [ObservableProperty]
    private bool hasLoaded;

    [ObservableProperty]
    private string? errorMessage;

    [ObservableProperty]
    private bool isCommentsLoading;

    [ObservableProperty]
    private string? commentsErrorMessage;

    [ObservableProperty]
    private bool isFavorite;

    public bool HasContent => !string.IsNullOrWhiteSpace(ContentText);

    public bool HasError => !string.IsNullOrWhiteSpace(ErrorMessage);

    public bool CanListen => Source == ContentSource.Podcast;

    public bool SupportsComments => Source == ContentSource.Podcast;

    public bool HasComments => Comments.Count > 0;

    public bool HasCommentsError => !string.IsNullOrWhiteSpace(CommentsErrorMessage);

    public bool ShowCommentsEmptyState =>
        SupportsComments && HasLoaded && !IsCommentsLoading && !HasComments && !HasCommentsError;

    public string CommentsHeaderText =>
        Comments.Count switch
        {
            1 => "Komentarze (1)",
            _ => $"Komentarze ({Comments.Count})",
        };

    public string ItemKindLabel => Source == ContentSource.Podcast ? "Podcast" : "Artykuł";

    public string ListenButtonLabel => $"Odtwórz podcast: {Title}";

    public string OpenActionButtonLabel =>
        Source == ContentSource.Article ? "Otwórz artykuł" : "Otwórz w przeglądarce";

    public string OpenInBrowserLabel => $"Otwórz {ItemKindLabel.ToLowerInvariant()} w przeglądarce";

    public string OpenActionAutomationLabel =>
        Source == ContentSource.Article
            ? $"Otwórz artykuł w aplikacji: {Title}"
            : OpenInBrowserLabel;

    public string ShareButtonLabel => $"Udostępnij {ItemKindLabel.ToLowerInvariant()}";

    public string ShareButtonAutomationLabel => $"{ShareButtonLabel}: {Title}";

    public string FavoriteButtonLabel =>
        IsFavorite ? "Usuń z ulubionych" : "Dodaj do ulubionych";

    public string FavoriteButtonAutomationLabel =>
        $"{FavoriteButtonLabel}: {Title}";

    public void Initialize(
        ContentSource source,
        int postId,
        string fallbackTitle,
        string fallbackDate,
        string fallbackLink
    )
    {
        Source = source;
        PostId = postId;
        Title = fallbackTitle;
        PublishedDate = fallbackDate;
        Link = fallbackLink;
        ContentText = string.Empty;
        ErrorMessage = null;
        Comments.Clear();
        CommentsErrorMessage = null;
        IsCommentsLoading = false;
        HasLoaded = false;
        IsFavorite = false;
        _hasRequestedInitialLoad = false;
        OnPropertyChanged(nameof(ItemKindLabel));
        OnPropertyChanged(nameof(OpenActionButtonLabel));
        OnPropertyChanged(nameof(OpenActionAutomationLabel));
        OnPropertyChanged(nameof(OpenInBrowserLabel));
        OnPropertyChanged(nameof(ShareButtonLabel));
        OnPropertyChanged(nameof(ShareButtonAutomationLabel));
        OnPropertyChanged(nameof(HasContent));
        OnPropertyChanged(nameof(HasError));
        OnPropertyChanged(nameof(CanListen));
        OnPropertyChanged(nameof(SupportsComments));
        OnPropertyChanged(nameof(HasComments));
        OnPropertyChanged(nameof(HasCommentsError));
        OnPropertyChanged(nameof(ShowCommentsEmptyState));
        OnPropertyChanged(nameof(CommentsHeaderText));
        OnPropertyChanged(nameof(ListenButtonLabel));
        OnPropertyChanged(nameof(FavoriteButtonLabel));
        OnPropertyChanged(nameof(FavoriteButtonAutomationLabel));
    }

    public async Task LoadIfNeededAsync(CancellationToken cancellationToken = default)
    {
        if (_hasRequestedInitialLoad)
        {
            return;
        }

        _hasRequestedInitialLoad = true;
        await LoadAsync(cancellationToken);
    }

    public async Task RetryAsync(CancellationToken cancellationToken = default)
    {
        _hasRequestedInitialLoad = true;
        await LoadAsync(cancellationToken);
    }

    public async Task ToggleFavoriteAsync(CancellationToken cancellationToken = default)
    {
        if (IsFavorite)
        {
            await _favoritesService.RemoveAsync(
                Source,
                PostId,
                FavoriteArticleOrigin.Post,
                cancellationToken
            );
            IsFavorite = false;
            return;
        }

        await _favoritesService.AddOrUpdateAsync(CreateFavoriteItem(), cancellationToken);
        IsFavorite = true;
    }

    public async Task RetryCommentsAsync(CancellationToken cancellationToken = default)
    {
        if (!SupportsComments)
        {
            return;
        }

        await LoadCommentsAsync(cancellationToken);
    }

    public async Task OpenInBrowserAsync(CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(Link))
        {
            ErrorMessage = "Ten wpis nie ma poprawnego linku do otwarcia.";
            OnPropertyChanged(nameof(HasError));
            return;
        }

        var launched = await _externalLinkLauncher.LaunchAsync(Link, cancellationToken);
        if (!launched)
        {
            ErrorMessage = "Nie udało się otworzyć wpisu w przeglądarce.";
            OnPropertyChanged(nameof(HasError));
        }
    }

    public async Task ShareAsync(CancellationToken cancellationToken = default)
    {
        ErrorMessage = null;
        OnPropertyChanged(nameof(HasError));

        if (string.IsNullOrWhiteSpace(Link))
        {
            ErrorMessage = "Ten wpis nie ma poprawnego linku do udostępnienia.";
            OnPropertyChanged(nameof(HasError));
            return;
        }

        var shared = await _shareService.ShareLinkAsync(Title, PublishedDate, Link, cancellationToken);
        if (!shared)
        {
            ErrorMessage = "Nie udało się udostępnić wpisu.";
            OnPropertyChanged(nameof(HasError));
        }
    }

    public AudioPlaybackRequest? CreatePlaybackRequest()
    {
        return CanListen
            ? _audioPlaybackRequestFactory.CreatePodcast(PostId, Title, PublishedDate)
            : null;
    }

    public void ReportPlaybackError()
    {
        ErrorMessage = "Nie udało się uruchomić odtwarzacza podcastu.";
        OnPropertyChanged(nameof(HasError));
    }

    partial void OnTitleChanged(string value)
    {
        OnPropertyChanged(nameof(ListenButtonLabel));
        OnPropertyChanged(nameof(OpenActionAutomationLabel));
        OnPropertyChanged(nameof(ShareButtonAutomationLabel));
        OnPropertyChanged(nameof(FavoriteButtonAutomationLabel));
    }

    partial void OnIsFavoriteChanged(bool value)
    {
        OnPropertyChanged(nameof(FavoriteButtonLabel));
        OnPropertyChanged(nameof(FavoriteButtonAutomationLabel));
    }

    private async Task LoadAsync(CancellationToken cancellationToken)
    {
        if (IsLoading)
        {
            return;
        }

        IsLoading = true;
        ErrorMessage = null;
        OnPropertyChanged(nameof(HasError));

        try
        {
            var detailTask = _postDetailsService.GetPostAsync(Source, PostId, cancellationToken);
            var favoriteTask = _favoritesService.IsFavoriteAsync(
                Source,
                PostId,
                FavoriteArticleOrigin.Post,
                cancellationToken
            );
            Task? commentsTask = SupportsComments ? LoadCommentsAsync(cancellationToken) : null;
            var item = await detailTask;
            IsFavorite = await favoriteTask;
            Title = WordPressContentText.NormalizeHtml(item.Title.Rendered);
            PublishedDate = WordPressTextFormatter.FormatDate(item.Date);
            Link = item.Link ?? item.Guid?.Rendered ?? Link;
            ContentText = WordPressContentText.ToReadablePlainText(item.Content.Rendered);
            if (commentsTask is not null)
            {
                await commentsTask;
            }
            HasLoaded = true;
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch
        {
            ContentText = string.Empty;
            ErrorMessage = "Nie udało się pobrać szczegółów wpisu. Spróbuj ponownie.";
            HasLoaded = true;
        }
        finally
        {
            IsLoading = false;
            OnPropertyChanged(nameof(HasContent));
            OnPropertyChanged(nameof(HasError));
            OnPropertyChanged(nameof(HasComments));
            OnPropertyChanged(nameof(HasCommentsError));
            OnPropertyChanged(nameof(ShowCommentsEmptyState));
            OnPropertyChanged(nameof(CommentsHeaderText));
        }
    }

    private FavoriteItem CreateFavoriteItem()
    {
        return new FavoriteItem
        {
            Id = FavoriteItem.CreateId(Source, PostId),
            Kind = Source == ContentSource.Podcast ? FavoriteKind.Podcast : FavoriteKind.Article,
            ArticleOrigin = FavoriteArticleOrigin.Post,
            Source = Source,
            PostId = PostId,
            Title = Title,
            PublishedDate = PublishedDate,
            Link = Link,
            SavedAtUtc = DateTimeOffset.UtcNow,
        };
    }

    private async Task LoadCommentsAsync(CancellationToken cancellationToken)
    {
        if (IsCommentsLoading)
        {
            return;
        }

        IsCommentsLoading = true;
        CommentsErrorMessage = null;
        OnPropertyChanged(nameof(HasCommentsError));

        try
        {
            var items = await _commentsService.GetCommentsAsync(PostId, cancellationToken);
            Comments.Clear();

            foreach (var item in items)
            {
                Comments.Add(new CommentItemViewModel(item));
            }
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch
        {
            Comments.Clear();
            CommentsErrorMessage = "Nie udało się pobrać komentarzy. Spróbuj ponownie.";
        }
        finally
        {
            IsCommentsLoading = false;
            OnPropertyChanged(nameof(HasComments));
            OnPropertyChanged(nameof(HasCommentsError));
            OnPropertyChanged(nameof(ShowCommentsEmptyState));
            OnPropertyChanged(nameof(CommentsHeaderText));
        }
    }
}
