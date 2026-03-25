using CommunityToolkit.Mvvm.ComponentModel;
using TyfloCentrum.Windows.Domain.Models;
using TyfloCentrum.Windows.Domain.Services;
using TyfloCentrum.Windows.Domain.Text;
using TyfloCentrum.Windows.UI.Formatting;

namespace TyfloCentrum.Windows.UI.ViewModels;

public partial class TyfloSwiatPageDetailViewModel : ObservableObject
{
    private readonly IExternalLinkLauncher _externalLinkLauncher;
    private readonly IFavoritesService _favoritesService;
    private readonly ITyfloSwiatMagazineService _magazineService;
    private readonly IShareService _shareService;
    private bool _hasRequestedInitialLoad;

    public TyfloSwiatPageDetailViewModel(
        ITyfloSwiatMagazineService magazineService,
        IExternalLinkLauncher externalLinkLauncher,
        IFavoritesService favoritesService,
        IShareService shareService
    )
    {
        _magazineService = magazineService;
        _externalLinkLauncher = externalLinkLauncher;
        _favoritesService = favoritesService;
        _shareService = shareService;
    }

    public int PageId { get; private set; }

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
    private string? statusMessage;

    [ObservableProperty]
    private bool isFavorite;

    public bool HasContent => !string.IsNullOrWhiteSpace(ContentText);

    public bool HasError => !string.IsNullOrWhiteSpace(ErrorMessage);

    public bool HasStatus => !string.IsNullOrWhiteSpace(StatusMessage);

    public string FavoriteButtonLabel =>
        IsFavorite ? "Usuń z ulubionych" : "Dodaj do ulubionych";

    public string ShareButtonLabel => "Udostępnij stronę";

    public string ShareButtonAutomationLabel => $"{ShareButtonLabel}: {Title}";

    public string FavoriteButtonAutomationLabel => $"{FavoriteButtonLabel}: {Title}";

    public void Initialize(int pageId, string fallbackTitle, string fallbackDate, string fallbackLink)
    {
        PageId = pageId;
        Title = fallbackTitle;
        PublishedDate = fallbackDate;
        Link = fallbackLink;
        ContentText = string.Empty;
        ErrorMessage = null;
        StatusMessage = null;
        HasLoaded = false;
        IsFavorite = false;
        _hasRequestedInitialLoad = false;
        NotifyStateChanged();
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

    public Task RetryAsync(CancellationToken cancellationToken = default)
    {
        _hasRequestedInitialLoad = true;
        return LoadAsync(cancellationToken);
    }

    public async Task OpenInBrowserAsync(CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(Link))
        {
            ErrorMessage = "Ta strona nie ma poprawnego linku do otwarcia.";
            NotifyStateChanged();
            return;
        }

        var launched = await _externalLinkLauncher.LaunchAsync(Link, cancellationToken);
        if (!launched)
        {
            ErrorMessage = "Nie udało się otworzyć strony TyfloŚwiata w przeglądarce.";
            NotifyStateChanged();
        }
    }

    public async Task ToggleFavoriteAsync(CancellationToken cancellationToken = default)
    {
        if (IsFavorite)
        {
            await _favoritesService.RemoveAsync(
                ContentSource.Article,
                PageId,
                FavoriteArticleOrigin.Page,
                cancellationToken
            );
            IsFavorite = false;
            StatusMessage = $"Usunięto z ulubionych: {Title}.";
            return;
        }

        await _favoritesService.AddOrUpdateAsync(CreateFavoriteItem(), cancellationToken);
        IsFavorite = true;
        StatusMessage = $"Dodano do ulubionych: {Title}.";
    }

    public async Task ShareAsync(CancellationToken cancellationToken = default)
    {
        ErrorMessage = null;
        NotifyStateChanged();

        if (string.IsNullOrWhiteSpace(Link))
        {
            ErrorMessage = "Ta strona nie ma poprawnego linku do udostępnienia.";
            NotifyStateChanged();
            return;
        }

        var shared = await _shareService.ShareLinkAsync(Title, PublishedDate, Link, cancellationToken);
        if (!shared)
        {
            ErrorMessage = "Nie udało się udostępnić strony TyfloŚwiata.";
            NotifyStateChanged();
        }
    }

    partial void OnTitleChanged(string value)
    {
        OnPropertyChanged(nameof(ShareButtonAutomationLabel));
        OnPropertyChanged(nameof(FavoriteButtonAutomationLabel));
    }

    partial void OnIsFavoriteChanged(bool value)
    {
        OnPropertyChanged(nameof(FavoriteButtonLabel));
        OnPropertyChanged(nameof(FavoriteButtonAutomationLabel));
    }

    partial void OnStatusMessageChanged(string? value)
    {
        OnPropertyChanged(nameof(HasStatus));
    }

    private async Task LoadAsync(CancellationToken cancellationToken)
    {
        if (IsLoading)
        {
            return;
        }

        IsLoading = true;
        ErrorMessage = null;
        NotifyStateChanged();

        try
        {
            var pageTask = _magazineService.GetPageAsync(PageId, cancellationToken);
            var favoriteTask = _favoritesService.IsFavoriteAsync(
                ContentSource.Article,
                PageId,
                FavoriteArticleOrigin.Page,
                cancellationToken
            );
            var page = await pageTask;
            IsFavorite = await favoriteTask;
            Title = WordPressTextFormatter.NormalizeHtml(page.Title.Rendered);
            PublishedDate = WordPressTextFormatter.FormatDate(page.Date);
            Link = page.Link ?? page.Guid?.Rendered ?? Link;
            ContentText = WordPressContentText.ToReadablePlainText(page.Content.Rendered);
            HasLoaded = true;
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch
        {
            ContentText = string.Empty;
            ErrorMessage = "Nie udało się pobrać strony TyfloŚwiata. Spróbuj ponownie.";
            HasLoaded = true;
        }
        finally
        {
            IsLoading = false;
            NotifyStateChanged();
        }
    }

    private void NotifyStateChanged()
    {
        OnPropertyChanged(nameof(HasContent));
        OnPropertyChanged(nameof(HasError));
        OnPropertyChanged(nameof(ShareButtonLabel));
        OnPropertyChanged(nameof(ShareButtonAutomationLabel));
        OnPropertyChanged(nameof(FavoriteButtonLabel));
        OnPropertyChanged(nameof(FavoriteButtonAutomationLabel));
    }

    private FavoriteItem CreateFavoriteItem()
    {
        return new FavoriteItem
        {
            Id = FavoriteItem.CreateId(ContentSource.Article, PageId, FavoriteArticleOrigin.Page),
            Kind = FavoriteKind.Article,
            ArticleOrigin = FavoriteArticleOrigin.Page,
            Source = ContentSource.Article,
            PostId = PageId,
            Title = Title,
            PublishedDate = PublishedDate,
            Link = Link,
            SavedAtUtc = DateTimeOffset.UtcNow,
        };
    }
}
