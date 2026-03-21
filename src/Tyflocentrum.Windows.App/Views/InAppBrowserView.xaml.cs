using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.Web.WebView2.Core;
using Tyflocentrum.Windows.Domain.Services;

namespace Tyflocentrum.Windows.App.Views;

public sealed partial class InAppBrowserView : UserControl
{
    private readonly IExternalLinkLauncher _externalLinkLauncher;
    private Uri? _currentUri;
    private string _currentHtml = string.Empty;
    private bool _isInitialized;
    private bool _messageHandlerAttached;
    private Uri? _pendingUri;

    public InAppBrowserView(IExternalLinkLauncher externalLinkLauncher)
    {
        _externalLinkLauncher = externalLinkLauncher;
        InitializeComponent();
    }

    public Action? CloseRequested { get; set; }

    public bool Initialize(string title, string link, string readerHtml)
    {
        if (!Uri.TryCreate(link, UriKind.Absolute, out var uri) || string.IsNullOrWhiteSpace(readerHtml))
        {
            return false;
        }

        _pendingUri = uri;
        _currentUri = uri;
        _currentHtml = readerHtml;
        _isInitialized = false;
        ViewTitleTextBlock.Text = string.IsNullOrWhiteSpace(title) ? "Artykuł" : title.Trim();
        ErrorBar.IsOpen = false;
        ErrorBar.Message = string.Empty;
        LoadingIndicator.IsActive = false;
        LoadingIndicator.Visibility = Visibility.Collapsed;
        return true;
    }

    public void Cleanup()
    {
        try
        {
            if (BrowserView.CoreWebView2 is not null && _messageHandlerAttached)
            {
                BrowserView.CoreWebView2.WebMessageReceived -= OnWebMessageReceived;
                _messageHandlerAttached = false;
            }

            BrowserView.CoreWebView2?.Stop();
            BrowserView.Source = null;
        }
        catch
        {
            // Ignore cleanup errors when the dialog is already closing.
        }
    }

    private async void OnLoaded(object sender, RoutedEventArgs e)
    {
        if (_pendingUri is null || _isInitialized)
        {
            return;
        }

        try
        {
            SetLoadingState(true);
            await BrowserView.EnsureCoreWebView2Async();

            if (BrowserView.CoreWebView2 is not null && !_messageHandlerAttached)
            {
                BrowserView.CoreWebView2.WebMessageReceived += OnWebMessageReceived;
                BrowserView.CoreWebView2.Settings.IsStatusBarEnabled = false;
                BrowserView.CoreWebView2.Settings.AreDevToolsEnabled = false;
                _messageHandlerAttached = true;
            }

            BrowserView.NavigateToString(_currentHtml);
            _isInitialized = true;
        }
        catch
        {
            ShowError("Nie udało się otworzyć artykułu w aplikacji.");
        }
        finally
        {
            SetLoadingState(false);
        }
    }

    private void OnUnloaded(object sender, RoutedEventArgs e)
    {
        Cleanup();
    }

    private void OnNavigationStarting(WebView2 sender, CoreWebView2NavigationStartingEventArgs args)
    {
        if (Uri.TryCreate(args.Uri, UriKind.Absolute, out var uri))
        {
            _currentUri = uri;
        }

        ErrorBar.IsOpen = false;
        ErrorBar.Message = string.Empty;
        SetLoadingState(true);
    }

    private async void OnNavigationCompleted(WebView2 sender, CoreWebView2NavigationCompletedEventArgs args)
    {
        SetLoadingState(false);

        if (!args.IsSuccess)
        {
            ShowError("Nie udało się załadować artykułu.");
            return;
        }

        await FocusReaderAsync();
    }

    private void OnRefreshClick(object sender, RoutedEventArgs e)
    {
        try
        {
            ErrorBar.IsOpen = false;
            ErrorBar.Message = string.Empty;
            BrowserView.NavigateToString(_currentHtml);
        }
        catch
        {
            ShowError("Nie udało się odświeżyć artykułu.");
        }
    }

    private async void OnOpenExternalClick(object sender, RoutedEventArgs e)
    {
        if (_currentUri is null)
        {
            ShowError("Ten artykuł nie ma poprawnego linku do otwarcia.");
            return;
        }

        var launched = await _externalLinkLauncher.LaunchAsync(_currentUri.AbsoluteUri);
        if (!launched)
        {
            ShowError("Nie udało się otworzyć artykułu w zewnętrznej przeglądarce.");
        }
    }

    private void SetLoadingState(bool isLoading)
    {
        LoadingIndicator.IsActive = isLoading;
        LoadingIndicator.Visibility = isLoading ? Visibility.Visible : Visibility.Collapsed;
    }

    private void ShowError(string message)
    {
        ErrorBar.Message = message;
        ErrorBar.IsOpen = true;
    }

    private async void OnWebMessageReceived(CoreWebView2 sender, CoreWebView2WebMessageReceivedEventArgs args)
    {
        var message = args.TryGetWebMessageAsString();

        if (string.Equals(message, "close", StringComparison.Ordinal))
        {
            CloseRequested?.Invoke();
            return;
        }

        const string prefix = "openExternal:";
        if (!message.StartsWith(prefix, StringComparison.Ordinal))
        {
            return;
        }

        var link = message[prefix.Length..];
        if (string.IsNullOrWhiteSpace(link))
        {
            return;
        }

        var launched = await _externalLinkLauncher.LaunchAsync(link);
        if (!launched)
        {
            ShowError("Nie udało się otworzyć artykułu w zewnętrznej przeglądarce.");
        }
    }

    private async Task FocusReaderAsync()
    {
        BrowserView.Focus(FocusState.Programmatic);

        if (BrowserView.CoreWebView2 is null)
        {
            return;
        }

        try
        {
            await BrowserView.CoreWebView2.ExecuteScriptAsync(
                "const root = document.getElementById('article-root'); if (root) { root.focus(); }"
            );
        }
        catch
        {
            // Ignore focus script failures. The control already has focus.
        }
    }
}
