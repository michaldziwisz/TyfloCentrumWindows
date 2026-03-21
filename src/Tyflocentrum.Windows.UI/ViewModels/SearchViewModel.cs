using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using Tyflocentrum.Windows.Domain.Models;
using Tyflocentrum.Windows.Domain.Services;

namespace Tyflocentrum.Windows.UI.ViewModels;

public partial class SearchViewModel : ObservableObject
{
    private readonly IExternalLinkLauncher _externalLinkLauncher;
    private readonly IWordPressSearchService _searchService;

    public SearchViewModel(
        IWordPressSearchService searchService,
        IExternalLinkLauncher externalLinkLauncher
    )
    {
        _searchService = searchService;
        _externalLinkLauncher = externalLinkLauncher;
        ScopeOptions = SearchScopeOptionViewModel.All;
        SelectedScope = ScopeOptions[0];
    }

    public ObservableCollection<ContentPostItemViewModel> Results { get; } = [];

    public IReadOnlyList<SearchScopeOptionViewModel> ScopeOptions { get; }

    [ObservableProperty]
    private string searchText = string.Empty;

    [ObservableProperty]
    private SearchScopeOptionViewModel selectedScope = SearchScopeOptionViewModel.All[0];

    [ObservableProperty]
    private bool isLoading;

    [ObservableProperty]
    private bool hasLoaded;

    [ObservableProperty]
    private string? errorMessage;

    [ObservableProperty]
    private string? lastSearchQuery;

    [ObservableProperty]
    private string? statusMessage;

    public bool CanSearch => !IsLoading && !string.IsNullOrWhiteSpace(SearchText);

    public bool CanRefresh =>
        !IsLoading
        && (!string.IsNullOrWhiteSpace(LastSearchQuery) || !string.IsNullOrWhiteSpace(SearchText));

    public bool HasResults => Results.Count > 0;

    public bool HasError => !string.IsNullOrWhiteSpace(ErrorMessage);

    public bool ShowEmptyState =>
        HasLoaded && !IsLoading && !HasResults && !HasError && !string.IsNullOrWhiteSpace(LastSearchQuery);

    public string EmptyStateMessage =>
        "Brak wyników wyszukiwania dla podanej frazy. Spróbuj użyć innych słów kluczowych.";

    public async Task SearchAsync(CancellationToken cancellationToken = default)
    {
        var trimmedQuery = SearchText.Trim();
        if (string.IsNullOrWhiteSpace(trimmedQuery))
        {
            return;
        }

        await ExecuteSearchAsync(trimmedQuery, cancellationToken);
    }

    public async Task RetryAsync(CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(LastSearchQuery))
        {
            return;
        }

        await ExecuteSearchAsync(LastSearchQuery, cancellationToken);
    }

    public async Task RefreshAsync(CancellationToken cancellationToken = default)
    {
        var query = string.IsNullOrWhiteSpace(LastSearchQuery) ? SearchText.Trim() : LastSearchQuery;
        if (string.IsNullOrWhiteSpace(query))
        {
            return;
        }

        await ExecuteSearchAsync(query, cancellationToken);
    }

    public async Task OpenResultAsync(
        ContentPostItemViewModel item,
        CancellationToken cancellationToken = default
    )
    {
        if (string.IsNullOrWhiteSpace(item.Link))
        {
            ErrorMessage = "Ten wynik nie ma poprawnego linku do otwarcia.";
            StatusMessage = ErrorMessage;
            NotifyStateChanged();
            return;
        }

        var launched = await _externalLinkLauncher.LaunchAsync(item.Link, cancellationToken);
        if (!launched)
        {
            ErrorMessage = "Nie udało się otworzyć wyniku w przeglądarce.";
            StatusMessage = ErrorMessage;
            NotifyStateChanged();
        }
    }

    partial void OnSearchTextChanged(string value)
    {
        NotifyStateChanged();
    }

    private async Task ExecuteSearchAsync(string query, CancellationToken cancellationToken)
    {
        if (IsLoading)
        {
            return;
        }

        IsLoading = true;
        ErrorMessage = null;
        LastSearchQuery = query;
        StatusMessage = "Wyszukiwanie…";
        NotifyStateChanged();

        try
        {
            var items = await _searchService.SearchAsync(
                SelectedScope?.Value ?? SearchScope.All,
                query,
                100,
                cancellationToken
            );

            Results.Clear();
            foreach (var item in items)
            {
                Results.Add(new ContentPostItemViewModel(item.Source, item.Post));
            }

            HasLoaded = true;
            StatusMessage = Results.Count == 0
                ? "Brak wyników wyszukiwania."
                : BuildResultsStatusMessage(Results.Count);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch
        {
            Results.Clear();
            ErrorMessage = "Nie udało się wyszukać treści. Spróbuj ponownie.";
            HasLoaded = true;
            StatusMessage = ErrorMessage;
        }
        finally
        {
            IsLoading = false;
            NotifyStateChanged();
        }
    }

    private void NotifyStateChanged()
    {
        OnPropertyChanged(nameof(CanSearch));
        OnPropertyChanged(nameof(CanRefresh));
        OnPropertyChanged(nameof(HasResults));
        OnPropertyChanged(nameof(HasError));
        OnPropertyChanged(nameof(ShowEmptyState));
        OnPropertyChanged(nameof(EmptyStateMessage));
    }

    private static string BuildResultsStatusMessage(int count)
    {
        return count switch
        {
            1 => "Znaleziono 1 wynik.",
            > 1 and < 5 => $"Znaleziono {count} wyniki.",
            _ => $"Znaleziono {count} wyników.",
        };
    }
}
