using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Tyflocentrum.Windows.App.Services;
using Tyflocentrum.Windows.Domain.Models;
using Tyflocentrum.Windows.UI.Services;
using Tyflocentrum.Windows.UI.ViewModels;
using Windows.System;

namespace Tyflocentrum.Windows.App.Views;

public sealed partial class ShellPage : Page
{
    private readonly ShellViewModel _viewModel;
    private readonly ArticleSectionView _articleSectionView;
    private readonly FavoritesSectionView _favoritesSectionView;
    private readonly NewsSectionView _newsSectionView;
    private readonly PodcastSectionView _podcastSectionView;
    private readonly RadioSectionView _radioSectionView;
    private readonly SearchSectionView _searchSectionView;
    private readonly SettingsSectionView _settingsSectionView;
    private bool _synchronizingSectionSelection;

    public ShellPage(
        ShellViewModel viewModel,
        NewsSectionView newsSectionView,
        PodcastSectionView podcastSectionView,
        ArticleSectionView articleSectionView,
        SearchSectionView searchSectionView,
        FavoritesSectionView favoritesSectionView,
        RadioSectionView radioSectionView,
        SettingsSectionView settingsSectionView
    )
    {
        InitializeComponent();
        _viewModel = viewModel;
        _newsSectionView = newsSectionView;
        _podcastSectionView = podcastSectionView;
        _articleSectionView = articleSectionView;
        _searchSectionView = searchSectionView;
        _favoritesSectionView = favoritesSectionView;
        _radioSectionView = radioSectionView;
        _settingsSectionView = settingsSectionView;
        DataContext = _viewModel;
        _newsSectionView.ExitToSectionListRequested += OnExitToSectionListRequested;
        _podcastSectionView.ExitToSectionListRequested += OnExitToSectionListRequested;
        _articleSectionView.ExitToSectionListRequested += OnExitToSectionListRequested;
        _searchSectionView.ExitToSectionListRequested += OnExitToSectionListRequested;
        _favoritesSectionView.ExitToSectionListRequested += OnFavoritesExitToSectionListRequested;
        _radioSectionView.ExitToSectionListRequested += OnExitToSectionListRequested;
        _settingsSectionView.ExitToSectionListRequested += OnExitToSectionListRequested;
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        if (SectionList.SelectedItem is null && _viewModel.Sections.Count > 0)
        {
            ActivateSection(_viewModel.Sections[0], moveFocus: false);
        }

        UpdateSectionContent();
    }

    private void OnSectionListSelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_synchronizingSectionSelection)
        {
            return;
        }

        if (sender is ListView { SelectedItem: AppSection section })
        {
            ActivateSection(section, moveFocus: false);
        }
    }

    private void OnSectionListKeyDown(object sender, KeyRoutedEventArgs e)
    {
        if (sender is not ListView listView)
        {
            return;
        }

        if (TryGetLatinLetter(e.Key, out var input))
        {
            var sectionItems = _viewModel.Sections;
            var match = InitialListNavigationHelper.FindNextByInitial(
                sectionItems,
                GetCurrentSectionIndex(sectionItems),
                section => section.Title,
                input
            );

            if (match is not null)
            {
                e.Handled = true;
                ActivateSection(match, moveFocus: true);
            }

            return;
        }

        if (e.Key is not (VirtualKey.Up or VirtualKey.Down))
        {
            return;
        }

        var sections = _viewModel.Sections;
        if (sections.Count == 0)
        {
            return;
        }

        var currentIndex = GetCurrentSectionIndex(sections);
        var nextIndex = e.Key == VirtualKey.Down ? currentIndex + 1 : currentIndex - 1;
        if (nextIndex < 0 || nextIndex >= sections.Count)
        {
            return;
        }

        e.Handled = true;
        ActivateSection(sections[nextIndex], moveFocus: true);
    }

    private void OnPreviewKeyDown(object sender, KeyRoutedEventArgs e)
    {
        if (e.Key != VirtualKey.Enter || !FocusNavigationHelper.IsFocusWithin(SectionList))
        {
            return;
        }

        e.Handled = true;
        FocusSelectedSectionContent();
    }

    private void OnSectionShortcutInvoked(
        KeyboardAccelerator sender,
        KeyboardAcceleratorInvokedEventArgs args
    )
    {
        var shortcutNumber = sender.Key switch
        {
            VirtualKey.Number1 => 1,
            VirtualKey.Number2 => 2,
            VirtualKey.Number3 => 3,
            VirtualKey.Number4 => 4,
            VirtualKey.Number5 => 5,
            VirtualKey.Number6 => 6,
            VirtualKey.Number7 => 7,
            _ => 0,
        };

        if (shortcutNumber == 0)
        {
            return;
        }

        var section = _viewModel.GetSectionByShortcutNumber(shortcutNumber);
        if (section is null)
        {
            return;
        }

        ActivateSection(section, moveFocus: true);
        args.Handled = true;
    }

    private void UpdateSectionContent()
    {
        SectionContentHost.Content = _viewModel.SelectedSectionKey switch
        {
            "news" => _newsSectionView,
            "podcasts" => _podcastSectionView,
            "articles" => _articleSectionView,
            "search" => _searchSectionView,
            "favorites" => _favoritesSectionView,
            "radio" => _radioSectionView,
            "settings" => _settingsSectionView,
            _ => CreatePlaceholderContent(),
        };

        if (_viewModel.SelectedSectionKey == "favorites")
        {
            _ = _favoritesSectionView.ReloadAsync();
        }
        else if (_viewModel.SelectedSectionKey == "settings")
        {
            _ = _settingsSectionView.ReloadAsync();
        }
    }

    private void ActivateSection(AppSection? section, bool moveFocus)
    {
        if (section is null)
        {
            return;
        }

        var shouldRefreshContent = !string.Equals(
            _viewModel.SelectedSectionKey,
            section.Key,
            StringComparison.OrdinalIgnoreCase
        );
        if (!ReferenceEquals(SectionList.SelectedItem, section))
        {
            _synchronizingSectionSelection = true;
            SectionList.SelectedItem = section;
            _synchronizingSectionSelection = false;
        }

        _viewModel.SelectSection(section.Key);

        if (shouldRefreshContent)
        {
            UpdateSectionContent();
        }

        if (!moveFocus)
        {
            return;
        }

        ListViewFocusHelper.RestoreFocus(SectionList, section);
    }

    private int GetCurrentSectionIndex(IReadOnlyList<AppSection> sections)
    {
        for (var index = 0; index < sections.Count; index++)
        {
            if (
                string.Equals(
                    sections[index].Key,
                    _viewModel.SelectedSectionKey,
                    StringComparison.OrdinalIgnoreCase
                )
            )
            {
                return index;
            }
        }

        return 0;
    }

    private void OnFavoritesExitToSectionListRequested(object? sender, EventArgs e)
    {
        OnExitToSectionListRequested(sender, e);
    }

    private void OnExitToSectionListRequested(object? sender, EventArgs e)
    {
        var currentSection = SectionList.SelectedItem as AppSection
            ?? _viewModel.Sections.FirstOrDefault(section =>
                string.Equals(
                    section.Key,
                    _viewModel.SelectedSectionKey,
                    StringComparison.OrdinalIgnoreCase
                )
            );

        if (currentSection is not null)
        {
            ListViewFocusHelper.RestoreFocus(SectionList, currentSection);
            return;
        }

        SectionList.Focus(FocusState.Programmatic);
    }

    private void FocusSelectedSectionContent()
    {
        switch (_viewModel.SelectedSectionKey)
        {
            case "news":
                _newsSectionView.FocusPrimaryContent();
                break;
            case "podcasts":
                _podcastSectionView.FocusPrimaryContent();
                break;
            case "articles":
                _articleSectionView.FocusPrimaryContent();
                break;
            case "search":
                _searchSectionView.FocusPrimaryContent();
                break;
            case "favorites":
                _favoritesSectionView.FocusPrimaryContent();
                break;
            case "radio":
                _radioSectionView.FocusPrimaryContent();
                break;
            case "settings":
                _settingsSectionView.FocusPrimaryContent();
                break;
        }
    }

    private static bool TryGetLatinLetter(VirtualKey key, out char value)
    {
        if (key is >= VirtualKey.A and <= VirtualKey.Z)
        {
            value = (char)('A' + (key - VirtualKey.A));
            return true;
        }

        value = '\0';
        return false;
    }

    private UIElement CreatePlaceholderContent()
    {
        return new StackPanel
        {
            Spacing = 8,
            Children =
            {
                new TextBlock
                {
                    FontWeight = Microsoft.UI.Text.FontWeights.SemiBold,
                    Text = "Sekcja w przygotowaniu",
                },
                new TextBlock
                {
                    Text = $"Scaffold dla sekcji \"{_viewModel.SelectedSectionTitle}\" jest gotowy. Implementacja funkcjonalna będzie dodawana w kolejnych iteracjach.",
                    TextWrapping = TextWrapping.WrapWholeWords,
                },
            },
        };
    }
}
