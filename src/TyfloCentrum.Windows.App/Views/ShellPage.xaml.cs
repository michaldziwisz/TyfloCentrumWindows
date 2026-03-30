using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Automation;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Imaging;
using System.Runtime.InteropServices.WindowsRuntime;
using TyfloCentrum.Windows.App.Services;
using TyfloCentrum.Windows.Domain.Models;
using TyfloCentrum.Windows.UI.Services;
using TyfloCentrum.Windows.UI.ViewModels;
using Windows.System;
using Windows.Graphics.Imaging;
using Windows.Storage.Streams;

namespace TyfloCentrum.Windows.App.Views;

public sealed partial class ShellPage : Page
{
    private readonly ContentEntryActionService _contentEntryActionService;
    private readonly NotificationActivationService _notificationActivationService;
    private readonly IServiceProvider _serviceProvider;
    private readonly ShellViewModel _viewModel;
    private ArticleSectionView? _articleSectionView;
    private FavoritesSectionView? _favoritesSectionView;
    private NewsSectionView? _newsSectionView;
    private PodcastSectionView? _podcastSectionView;
    private RadioSectionView? _radioSectionView;
    private SearchSectionView? _searchSectionView;
    private SettingsSectionView? _settingsSectionView;
    private FeedbackSectionView? _feedbackSectionView;
    private bool _synchronizingSectionSelection;
    private readonly TaskCompletionSource _loadedCompletionSource = new();

    public ShellPage(
        ShellViewModel viewModel,
        ContentEntryActionService contentEntryActionService,
        NotificationActivationService notificationActivationService,
        IServiceProvider serviceProvider
    )
    {
        _contentEntryActionService = contentEntryActionService;
        _notificationActivationService = notificationActivationService;
        _serviceProvider = serviceProvider;
        _viewModel = viewModel;
        InitializeComponent();
        ConfigureKeyboardAccelerators();
        DataContext = _viewModel;
        _notificationActivationService.PendingRequestChanged += OnPendingNotificationRequestChanged;
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        if (SectionList.SelectedItem is null && _viewModel.Sections.Count > 0)
        {
            ActivateSection(_viewModel.Sections[0], moveFocus: false);
        }

        UpdateSectionContent();
        _loadedCompletionSource.TrySetResult();
        _ = ProcessPendingNotificationRequestAsync();
    }

    internal async Task CaptureInternalStoreScreenshotAsync(InternalStoreScreenshotRequest request)
    {
        await _loadedCompletionSource.Task;
        ApplyInternalStoreScreenshotLayout();

        var section =
            _viewModel.Sections.FirstOrDefault(candidate =>
                string.Equals(candidate.Key, request.SectionKey, StringComparison.OrdinalIgnoreCase)
            ) ?? _viewModel.Sections[0];

        ActivateSection(section, moveFocus: false);
        await PrepareSelectedSectionForScreenshotAsync(section.Key);
        RootLayout.UpdateLayout();
        await Task.Delay(300);
        await SaveRootLayoutAsPngAsync(request.OutputPath);
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
            VirtualKey.Number8 => 8,
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

    private void ConfigureKeyboardAccelerators()
    {
        KeyboardAcceleratorPlacementMode = KeyboardAcceleratorPlacementMode.Hidden;

        for (var shortcutNumber = 1; shortcutNumber <= 8; shortcutNumber++)
        {
            var key = shortcutNumber switch
            {
                1 => VirtualKey.Number1,
                2 => VirtualKey.Number2,
                3 => VirtualKey.Number3,
                4 => VirtualKey.Number4,
                5 => VirtualKey.Number5,
                6 => VirtualKey.Number6,
                7 => VirtualKey.Number7,
                8 => VirtualKey.Number8,
                _ => throw new ArgumentOutOfRangeException(nameof(shortcutNumber)),
            };

            KeyboardAccelerators.Add(
                new KeyboardAccelerator
                {
                    Key = key,
                    Modifiers = VirtualKeyModifiers.Menu,
                }
            );

            KeyboardAccelerators[^1].Invoked += OnSectionShortcutInvoked;
        }
    }

    private void UpdateSectionContent()
    {
        SectionContentHost.Content = GetSectionContent(_viewModel.SelectedSectionKey);

        if (_viewModel.SelectedSectionKey == "favorites")
        {
            _ = GetFavoritesSectionView().ReloadAsync();
        }
        else if (_viewModel.SelectedSectionKey == "settings")
        {
            _ = GetSettingsSectionView().ReloadAsync();
        }
    }

    private void ApplyInternalStoreScreenshotLayout()
    {
        RequestedTheme = ElementTheme.Light;
        RootLayout.Background = new SolidColorBrush(Microsoft.UI.Colors.White);
        SectionList.Background = new SolidColorBrush(Microsoft.UI.Colors.White);
    }

    private async Task PrepareSelectedSectionForScreenshotAsync(string sectionKey)
    {
        switch (sectionKey)
        {
            case "news":
                await GetNewsSectionView().PrepareForScreenshotAsync();
                break;
            case "podcasts":
                await GetPodcastSectionView().PrepareForScreenshotAsync();
                break;
            case "articles":
                await GetArticleSectionView().PrepareForScreenshotAsync();
                break;
            case "radio":
                await GetRadioSectionView().PrepareForScreenshotAsync();
                break;
            default:
                await Task.Delay(1200);
                break;
        }
    }

    private async Task SaveRootLayoutAsPngAsync(string outputPath)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(outputPath)!);

        var renderTargetBitmap = new RenderTargetBitmap();
        var width = Math.Max(1, (int)Math.Ceiling(RootLayout.ActualWidth));
        var height = Math.Max(1, (int)Math.Ceiling(RootLayout.ActualHeight));

        await renderTargetBitmap.RenderAsync(RootLayout, width, height);
        var pixels = await renderTargetBitmap.GetPixelsAsync();
        var pixelBytes = pixels.ToArray();

        using var stream = new InMemoryRandomAccessStream();
        var encoder = await BitmapEncoder.CreateAsync(BitmapEncoder.PngEncoderId, stream);
        encoder.SetPixelData(
            BitmapPixelFormat.Bgra8,
            BitmapAlphaMode.Premultiplied,
            (uint)renderTargetBitmap.PixelWidth,
            (uint)renderTargetBitmap.PixelHeight,
            96,
            96,
            pixelBytes
        );
        await encoder.FlushAsync();

        stream.Seek(0);
        using var inputStream = stream.GetInputStreamAt(0);
        using var dataReader = new DataReader(inputStream);
        await dataReader.LoadAsync((uint)stream.Size);

        var bytes = new byte[(int)stream.Size];
        dataReader.ReadBytes(bytes);
        await File.WriteAllBytesAsync(outputPath, bytes);
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

    private void OnPendingNotificationRequestChanged(object? sender, EventArgs e)
    {
        _ = ProcessPendingNotificationRequestAsync();
    }

    private async Task ProcessPendingNotificationRequestAsync()
    {
        if (!IsLoaded || XamlRoot is null)
        {
            return;
        }

        var request = _notificationActivationService.TakePendingRequest();
        if (request is null)
        {
            return;
        }

        var sectionKey = request.Source == ContentSource.Podcast ? "podcasts" : "articles";
        var section = _viewModel.Sections.FirstOrDefault(item =>
            string.Equals(item.Key, sectionKey, StringComparison.OrdinalIgnoreCase)
        );
        ActivateSection(section, moveFocus: false);

        if (request.Source == ContentSource.Podcast)
        {
            await _contentEntryActionService.OpenPodcastAsync(
                request.PostId,
                request.Title,
                request.PublishedDate ?? string.Empty,
                XamlRoot
            );
            return;
        }

        await _contentEntryActionService.OpenArticleAsync(
            request.Source,
            request.PostId,
            request.Title,
            request.PublishedDate ?? string.Empty,
            request.Link ?? string.Empty,
            XamlRoot
        );
    }

    private void FocusSelectedSectionContent()
    {
        switch (_viewModel.SelectedSectionKey)
        {
            case "news":
                GetNewsSectionView().FocusPrimaryContent();
                break;
            case "podcasts":
                GetPodcastSectionView().FocusPrimaryContent();
                break;
            case "articles":
                GetArticleSectionView().FocusPrimaryContent();
                break;
            case "search":
                GetSearchSectionView().FocusPrimaryContent();
                break;
            case "favorites":
                GetFavoritesSectionView().FocusPrimaryContent();
                break;
            case "radio":
                GetRadioSectionView().FocusPrimaryContent();
                break;
            case "settings":
                GetSettingsSectionView().FocusPrimaryContent();
                break;
            case "feedback":
                GetFeedbackSectionView().FocusPrimaryContent();
                break;
        }
    }

    private object GetSectionContent(string sectionKey)
    {
        return sectionKey switch
        {
            "news" => GetNewsSectionView(),
            "podcasts" => GetPodcastSectionView(),
            "articles" => GetArticleSectionView(),
            "search" => GetSearchSectionView(),
            "favorites" => GetFavoritesSectionView(),
            "radio" => GetRadioSectionView(),
            "settings" => GetSettingsSectionView(),
            "feedback" => GetFeedbackSectionView(),
            _ => CreatePlaceholderContent(),
        };
    }

    private NewsSectionView GetNewsSectionView()
    {
        if (_newsSectionView is not null)
        {
            return _newsSectionView;
        }

        _newsSectionView = _serviceProvider.GetRequiredService<NewsSectionView>();
        _newsSectionView.ExitToSectionListRequested += OnExitToSectionListRequested;
        return _newsSectionView;
    }

    private PodcastSectionView GetPodcastSectionView()
    {
        if (_podcastSectionView is not null)
        {
            return _podcastSectionView;
        }

        _podcastSectionView = _serviceProvider.GetRequiredService<PodcastSectionView>();
        _podcastSectionView.ExitToSectionListRequested += OnExitToSectionListRequested;
        return _podcastSectionView;
    }

    private ArticleSectionView GetArticleSectionView()
    {
        if (_articleSectionView is not null)
        {
            return _articleSectionView;
        }

        _articleSectionView = _serviceProvider.GetRequiredService<ArticleSectionView>();
        _articleSectionView.ExitToSectionListRequested += OnExitToSectionListRequested;
        return _articleSectionView;
    }

    private SearchSectionView GetSearchSectionView()
    {
        if (_searchSectionView is not null)
        {
            return _searchSectionView;
        }

        _searchSectionView = _serviceProvider.GetRequiredService<SearchSectionView>();
        _searchSectionView.ExitToSectionListRequested += OnExitToSectionListRequested;
        return _searchSectionView;
    }

    private FavoritesSectionView GetFavoritesSectionView()
    {
        if (_favoritesSectionView is not null)
        {
            return _favoritesSectionView;
        }

        _favoritesSectionView = _serviceProvider.GetRequiredService<FavoritesSectionView>();
        _favoritesSectionView.ExitToSectionListRequested += OnFavoritesExitToSectionListRequested;
        return _favoritesSectionView;
    }

    private RadioSectionView GetRadioSectionView()
    {
        if (_radioSectionView is not null)
        {
            return _radioSectionView;
        }

        _radioSectionView = _serviceProvider.GetRequiredService<RadioSectionView>();
        _radioSectionView.ExitToSectionListRequested += OnExitToSectionListRequested;
        return _radioSectionView;
    }

    private SettingsSectionView GetSettingsSectionView()
    {
        if (_settingsSectionView is not null)
        {
            return _settingsSectionView;
        }

        _settingsSectionView = _serviceProvider.GetRequiredService<SettingsSectionView>();
        _settingsSectionView.ExitToSectionListRequested += OnExitToSectionListRequested;
        return _settingsSectionView;
    }

    private FeedbackSectionView GetFeedbackSectionView()
    {
        if (_feedbackSectionView is not null)
        {
            return _feedbackSectionView;
        }

        _feedbackSectionView = _serviceProvider.GetRequiredService<FeedbackSectionView>();
        _feedbackSectionView.ExitToSectionListRequested += OnExitToSectionListRequested;
        return _feedbackSectionView;
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
