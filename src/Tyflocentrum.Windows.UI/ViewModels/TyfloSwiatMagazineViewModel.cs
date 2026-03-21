using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using Tyflocentrum.Windows.Domain.Models;
using Tyflocentrum.Windows.Domain.Services;
using Tyflocentrum.Windows.Domain.Text;
using Tyflocentrum.Windows.UI.Formatting;

namespace Tyflocentrum.Windows.UI.ViewModels;

public partial class TyfloSwiatMagazineViewModel : ObservableObject
{
    private readonly IExternalLinkLauncher _externalLinkLauncher;
    private readonly IFavoritesService _favoritesService;
    private readonly ITyfloSwiatMagazineService _magazineService;
    private List<TyfloSwiatMagazineIssueItemViewModel> _allIssues = [];
    private CancellationTokenSource? _issueSelectionCancellationTokenSource;
    private bool _hasLoaded;

    public TyfloSwiatMagazineViewModel(
        ITyfloSwiatMagazineService magazineService,
        IExternalLinkLauncher externalLinkLauncher,
        IFavoritesService favoritesService
    )
    {
        _magazineService = magazineService;
        _externalLinkLauncher = externalLinkLauncher;
        _favoritesService = favoritesService;
    }

    public ObservableCollection<TyfloSwiatMagazineYearItemViewModel> Years { get; } = [];

    public ObservableCollection<TyfloSwiatMagazineIssueItemViewModel> Issues { get; } = [];

    public ObservableCollection<TyfloSwiatMagazineTocItemViewModel> TocItems { get; } = [];

    [ObservableProperty]
    private bool isLoading;

    [ObservableProperty]
    private bool isIssueLoading;

    [ObservableProperty]
    private bool hasLoadedOnce;

    [ObservableProperty]
    private string? errorMessage;

    [ObservableProperty]
    private string? issueErrorMessage;

    [ObservableProperty]
    private string? statusMessage;

    [ObservableProperty]
    private TyfloSwiatMagazineYearItemViewModel? selectedYear;

    [ObservableProperty]
    private TyfloSwiatMagazineYearItemViewModel? openedYear;

    [ObservableProperty]
    private TyfloSwiatMagazineIssueItemViewModel? selectedIssue;

    [ObservableProperty]
    private string selectedIssueTitle = string.Empty;

    [ObservableProperty]
    private string selectedIssuePublishedDate = string.Empty;

    [ObservableProperty]
    private string selectedIssueContentText = string.Empty;

    [ObservableProperty]
    private string selectedIssueLink = string.Empty;

    [ObservableProperty]
    private string? selectedIssuePdfUrl;

    public bool HasError => !string.IsNullOrWhiteSpace(ErrorMessage);

    public bool HasIssueError => !string.IsNullOrWhiteSpace(IssueErrorMessage);

    public bool HasYears => Years.Count > 0;

    public bool HasOpenedYear => OpenedYear is not null;

    public bool HasIssues => Issues.Count > 0;

    public bool HasIssueDetail => !string.IsNullOrWhiteSpace(SelectedIssueTitle);

    public bool HasTocItems => TocItems.Count > 0;

    public bool HasIssueContent => !string.IsNullOrWhiteSpace(SelectedIssueContentText);

    public bool CanOpenSelectedIssue => !string.IsNullOrWhiteSpace(SelectedIssueLink);

    public bool CanOpenSelectedIssuePdf => !string.IsNullOrWhiteSpace(SelectedIssuePdfUrl);

    public bool ShowEmptyState => HasLoadedOnce && !IsLoading && !HasYears && !HasError;

    public bool ShowIssuePlaceholder =>
        !IsIssueLoading && !HasIssueError && !HasIssueContent && !HasTocItems;

    public string IssuesPlaceholderText =>
        HasOpenedYear
            ? "Brak numerów czasopisma."
            : "Wybierz rocznik i naciśnij Enter, aby wyświetlić listę numerów.";

    public async Task LoadIfNeededAsync(CancellationToken cancellationToken = default)
    {
        if (_hasLoaded)
        {
            return;
        }

        _hasLoaded = true;
        await RefreshAsync(cancellationToken);
    }

    public async Task RefreshAsync(CancellationToken cancellationToken = default)
    {
        if (IsLoading)
        {
            return;
        }

        CancelIssueSelection();
        IsLoading = true;
        ErrorMessage = null;
        StatusMessage = "Ładowanie numerów czasopisma TyfloŚwiat…";
        NotifyStateChanged();

        try
        {
            var issues = await _magazineService.GetIssuesAsync(cancellationToken);
            _allIssues = issues
                .Select(item => new TyfloSwiatMagazineIssueItemViewModel(item))
                .OrderByDescending(item => item.Year)
                .ThenByDescending(item => item.IssueNumber ?? -1)
                .ThenByDescending(item => item.PublishedDate, StringComparer.Ordinal)
                .ThenByDescending(item => item.IssueId)
                .ToList();

            ApplyYears();

            HasLoadedOnce = true;
            if (!HasIssueContent && !HasTocItems)
            {
                StatusMessage = _allIssues.Count switch
                {
                    0 => "Brak numerów czasopisma.",
                    1 => "Wczytano 1 numer czasopisma.",
                    >= 2 and <= 4 => $"Wczytano {_allIssues.Count} numery czasopisma.",
                    _ => $"Wczytano {_allIssues.Count} numerów czasopisma.",
                };
            }
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch
        {
            Years.Clear();
            Issues.Clear();
            TocItems.Clear();
            ClearSelectedIssueDetail();
            ErrorMessage = "Nie udało się pobrać numerów czasopisma. Spróbuj ponownie.";
            StatusMessage = ErrorMessage;
            HasLoadedOnce = true;
        }
        finally
        {
            IsLoading = false;
            NotifyStateChanged();
        }
    }

    public void SelectYearForNavigation(TyfloSwiatMagazineYearItemViewModel? year)
    {
        var nextYear = year ?? Years.FirstOrDefault();
        if (nextYear is null)
        {
            return;
        }

        if (ReferenceEquals(SelectedYear, nextYear))
        {
            return;
        }

        SelectedYear = nextYear;
        StatusMessage = null;
        NotifyStateChanged();
    }

    public Task OpenSelectedYearAsync(
        TyfloSwiatMagazineYearItemViewModel? year,
        CancellationToken cancellationToken = default
    )
    {
        var nextYear = year ?? Years.FirstOrDefault();
        if (nextYear is null)
        {
            return Task.CompletedTask;
        }

        SelectedYear = nextYear;
        if (OpenedYear?.Year == nextYear.Year && HasIssues)
        {
            return Task.CompletedTask;
        }

        OpenedYear = nextYear;
        ApplyIssuesForOpenedYear();
        SelectedIssue = Issues.FirstOrDefault();
        IssueErrorMessage = null;
        StatusMessage = null;
        ClearSelectedIssueDetail();
        NotifyStateChanged();

        return Task.CompletedTask;
    }

    public void SelectIssueForNavigation(TyfloSwiatMagazineIssueItemViewModel? issue)
    {
        if (issue is null)
        {
            SelectedIssue = null;
            IssueErrorMessage = null;
            ClearSelectedIssueDetail();
            NotifyStateChanged();
            return;
        }

        if (SelectedIssue?.IssueId == issue.IssueId && !HasIssueContent && !HasTocItems && !HasIssueError)
        {
            return;
        }

        CancelIssueSelection();
        SelectedIssue = issue;
        IsIssueLoading = false;
        IssueErrorMessage = null;
        StatusMessage = null;
        ClearSelectedIssueDetail();
        NotifyStateChanged();
    }

    public async Task SelectIssueAsync(
        TyfloSwiatMagazineIssueItemViewModel? issue,
        CancellationToken cancellationToken = default
    )
    {
        if (issue is null)
        {
            return;
        }

        if (
            SelectedIssue?.IssueId == issue.IssueId
            && !IsIssueLoading
            && (HasIssueContent || HasTocItems || HasIssueError)
        )
        {
            return;
        }

        CancelIssueSelection();
        using var linkedCancellationTokenSource = CancellationTokenSource.CreateLinkedTokenSource(
            cancellationToken
        );
        _issueSelectionCancellationTokenSource = linkedCancellationTokenSource;
        var selectionToken = linkedCancellationTokenSource.Token;

        SelectedIssue = issue;
        IsIssueLoading = true;
        IssueErrorMessage = null;
        StatusMessage = $"Ładowanie numeru: {issue.Title}.";
        ClearSelectedIssueDetail();
        NotifyStateChanged();

        try
        {
            var detail = await _magazineService.GetIssueAsync(issue.IssueId, selectionToken);
            if (!ReferenceEquals(_issueSelectionCancellationTokenSource, linkedCancellationTokenSource))
            {
                return;
            }

            SelectedIssueTitle = WordPressTextFormatter.NormalizeHtml(detail.Issue.Title.Rendered);
            SelectedIssuePublishedDate = WordPressTextFormatter.FormatDate(detail.Issue.Date);
            SelectedIssueLink = detail.Issue.Link ?? detail.Issue.Guid?.Rendered ?? issue.Link;
            SelectedIssuePdfUrl = detail.PdfUrl;
            SelectedIssueContentText = WordPressContentText.ToReadablePlainText(detail.Issue.Content.Rendered);

            var tocItems = detail.TocItems
                .Select(item => new TyfloSwiatMagazineTocItemViewModel(item))
                .ToArray();
            await PopulateFavoriteStateAsync(tocItems, cancellationToken);

            TocItems.Clear();
            foreach (var item in tocItems)
            {
                TocItems.Add(item);
            }

            StatusMessage = detail.TocItems.Count > 0
                ? "Wczytano spis treści numeru."
                : "Wczytano treść numeru.";
        }
        catch (OperationCanceledException)
        {
            if (selectionToken.IsCancellationRequested)
            {
                return;
            }

            throw;
        }
        catch
        {
            if (!ReferenceEquals(_issueSelectionCancellationTokenSource, linkedCancellationTokenSource))
            {
                return;
            }

            IssueErrorMessage = "Nie udało się pobrać danych numeru. Spróbuj ponownie.";
            StatusMessage = IssueErrorMessage;
            ClearSelectedIssueDetail();
        }
        finally
        {
            if (ReferenceEquals(_issueSelectionCancellationTokenSource, linkedCancellationTokenSource))
            {
                _issueSelectionCancellationTokenSource = null;
                IsIssueLoading = false;
                NotifyStateChanged();
            }
        }
    }

    public async Task RetrySelectedIssueAsync(CancellationToken cancellationToken = default)
    {
        await SelectIssueAsync(SelectedIssue, cancellationToken);
    }

    public async Task<bool> OpenSelectedIssueAsync(CancellationToken cancellationToken = default)
    {
        var target = !string.IsNullOrWhiteSpace(SelectedIssueLink)
            ? SelectedIssueLink
            : SelectedIssue?.Link;
        if (string.IsNullOrWhiteSpace(target))
        {
            IssueErrorMessage = "Ten numer nie ma poprawnego linku do otwarcia.";
            NotifyStateChanged();
            return false;
        }

        var launched = await _externalLinkLauncher.LaunchAsync(target, cancellationToken);
        if (!launched)
        {
            IssueErrorMessage = "Nie udało się otworzyć numeru w przeglądarce.";
            NotifyStateChanged();
            return false;
        }

        return true;
    }

    public async Task<bool> OpenPdfAsync(CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(SelectedIssuePdfUrl))
        {
            IssueErrorMessage = "Ten numer nie udostępnia pliku PDF.";
            NotifyStateChanged();
            return false;
        }

        var launched = await _externalLinkLauncher.LaunchAsync(
            SelectedIssuePdfUrl,
            cancellationToken
        );
        if (!launched)
        {
            IssueErrorMessage = "Nie udało się otworzyć pliku PDF.";
            NotifyStateChanged();
            return false;
        }

        return true;
    }

    public async Task<bool> OpenTocItemInBrowserAsync(
        TyfloSwiatMagazineTocItemViewModel item,
        CancellationToken cancellationToken = default
    )
    {
        if (string.IsNullOrWhiteSpace(item.Link))
        {
            IssueErrorMessage = "Ta pozycja spisu treści nie ma poprawnego linku do otwarcia.";
            NotifyStateChanged();
            return false;
        }

        var launched = await _externalLinkLauncher.LaunchAsync(item.Link, cancellationToken);
        if (!launched)
        {
            IssueErrorMessage = "Nie udało się otworzyć pozycji spisu treści w przeglądarce.";
            NotifyStateChanged();
            return false;
        }

        return true;
    }

    public async Task<bool> ToggleTocFavoriteAsync(
        TyfloSwiatMagazineTocItemViewModel item,
        CancellationToken cancellationToken = default
    )
    {
        try
        {
            if (item.IsFavorite)
            {
                await _favoritesService.RemoveAsync(
                    ContentSource.Article,
                    item.PageId,
                    FavoriteArticleOrigin.Page,
                    cancellationToken
                );
                item.IsFavorite = false;
                StatusMessage = $"Usunięto stronę TyfloŚwiata z ulubionych: {item.Title}.";
            }
            else
            {
                await _favoritesService.AddOrUpdateAsync(CreatePageFavoriteItem(item), cancellationToken);
                item.IsFavorite = true;
                StatusMessage = $"Dodano stronę TyfloŚwiata do ulubionych: {item.Title}.";
            }

            IssueErrorMessage = null;
            NotifyStateChanged();
            return true;
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch
        {
            IssueErrorMessage = "Nie udało się zaktualizować ulubionej strony TyfloŚwiata.";
            StatusMessage = IssueErrorMessage;
            NotifyStateChanged();
            return false;
        }
    }

    public async Task RefreshTocFavoriteAsync(
        TyfloSwiatMagazineTocItemViewModel item,
        CancellationToken cancellationToken = default
    )
    {
        try
        {
            item.IsFavorite = await _favoritesService.IsFavoriteAsync(
                ContentSource.Article,
                item.PageId,
                FavoriteArticleOrigin.Page,
                cancellationToken
            );
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch
        {
            item.IsFavorite = false;
        }
    }

    private void ApplyYears()
    {
        var previousOpenedYear = OpenedYear?.Year;
        var previousYear = SelectedYear?.Year;
        var years = _allIssues
            .GroupBy(issue => issue.Year)
            .OrderByDescending(group => group.Key)
            .Select(group => new TyfloSwiatMagazineYearItemViewModel(group.Key, group.Count()))
            .ToArray();

        Years.Clear();
        foreach (var year in years)
        {
            Years.Add(year);
        }

        var nextYear = Years.FirstOrDefault(item => item.Year == previousYear) ?? Years.FirstOrDefault();
        SelectedYear = nextYear;

        OpenedYear = previousOpenedYear is null
            ? null
            : Years.FirstOrDefault(item => item.Year == previousOpenedYear.Value);

        if (OpenedYear is not null)
        {
            ApplyIssuesForOpenedYear();
        }
        else
        {
            Issues.Clear();
            SelectedIssue = null;
            ClearSelectedIssueDetail();
        }
    }

    private void ApplyIssuesForOpenedYear()
    {
        var selectedYearValue = OpenedYear?.Year;
        var items = selectedYearValue is null
            ? []
            : _allIssues.Where(item => item.Year == selectedYearValue.Value).ToArray();

        Issues.Clear();
        foreach (var item in items)
        {
            Issues.Add(item);
        }

        SelectedIssue = Issues.FirstOrDefault(item => item.IssueId == SelectedIssue?.IssueId);

        if (SelectedIssue is null)
        {
            ClearSelectedIssueDetail();
        }
    }

    private void ClearSelectedIssueDetail()
    {
        SelectedIssueTitle = string.Empty;
        SelectedIssuePublishedDate = string.Empty;
        SelectedIssueContentText = string.Empty;
        SelectedIssueLink = string.Empty;
        SelectedIssuePdfUrl = null;
        TocItems.Clear();
    }

    private void CancelIssueSelection()
    {
        var cancellationTokenSource = _issueSelectionCancellationTokenSource;
        if (cancellationTokenSource is null)
        {
            return;
        }

        _issueSelectionCancellationTokenSource = null;
        cancellationTokenSource.Cancel();
        cancellationTokenSource.Dispose();
    }

    private async Task PopulateFavoriteStateAsync(
        IReadOnlyList<TyfloSwiatMagazineTocItemViewModel> items,
        CancellationToken cancellationToken
    )
    {
        try
        {
            var favoriteTasks = items.Select(async item =>
            {
                var isFavorite = await _favoritesService.IsFavoriteAsync(
                    ContentSource.Article,
                    item.PageId,
                    FavoriteArticleOrigin.Page,
                    cancellationToken
                );
                return (Item: item, IsFavorite: isFavorite);
            });

            foreach (var result in await Task.WhenAll(favoriteTasks))
            {
                result.Item.IsFavorite = result.IsFavorite;
            }
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch
        {
            foreach (var item in items)
            {
                item.IsFavorite = false;
            }
        }
    }

    private static FavoriteItem CreatePageFavoriteItem(TyfloSwiatMagazineTocItemViewModel item)
    {
        return new FavoriteItem
        {
            Id = FavoriteItem.CreateId(ContentSource.Article, item.PageId, FavoriteArticleOrigin.Page),
            Kind = FavoriteKind.Article,
            ArticleOrigin = FavoriteArticleOrigin.Page,
            Source = ContentSource.Article,
            PostId = item.PageId,
            Title = item.Title,
            PublishedDate = item.PublishedDate,
            Link = item.Link,
            SavedAtUtc = DateTimeOffset.UtcNow,
        };
    }

    private void NotifyStateChanged()
    {
        OnPropertyChanged(nameof(HasError));
        OnPropertyChanged(nameof(HasIssueError));
        OnPropertyChanged(nameof(HasYears));
        OnPropertyChanged(nameof(HasOpenedYear));
        OnPropertyChanged(nameof(HasIssues));
        OnPropertyChanged(nameof(HasIssueDetail));
        OnPropertyChanged(nameof(HasTocItems));
        OnPropertyChanged(nameof(HasIssueContent));
        OnPropertyChanged(nameof(CanOpenSelectedIssue));
        OnPropertyChanged(nameof(CanOpenSelectedIssuePdf));
        OnPropertyChanged(nameof(ShowEmptyState));
        OnPropertyChanged(nameof(ShowIssuePlaceholder));
        OnPropertyChanged(nameof(IssuesPlaceholderText));
    }
}
