using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Text;
using System.ComponentModel;
using TyfloCentrum.Windows.App.Services;
using TyfloCentrum.Windows.UI.ViewModels;
using Windows.System;

namespace TyfloCentrum.Windows.App.Views;

public sealed partial class FeedbackSectionView : UserControl
{
    private bool _isSynchronizingDescriptionEditor;

    public event EventHandler? ExitToSectionListRequested;

    public FeedbackSectionView(FeedbackSectionViewModel viewModel)
    {
        ViewModel = viewModel;
        InitializeComponent();
        DataContext = ViewModel;
        ViewModel.PropertyChanged += OnViewModelPropertyChanged;
        SyncDescriptionEditorFromViewModel();
        UpdateVisualState();
    }

    public FeedbackSectionViewModel ViewModel { get; }

    public void FocusPrimaryContent()
    {
        KindComboBox.Focus(FocusState.Programmatic);
    }

    private async void OnSubmitClick(object sender, RoutedEventArgs e)
    {
        await Task.Yield();
        await ViewModel.SubmitAsync();
        if (ViewModel.HasPublicIssueUrl)
        {
            OpenIssueButton.Focus(FocusState.Programmatic);
        }
        else if (ViewModel.HasError)
        {
            TitleTextBox.Focus(FocusState.Programmatic);
        }
    }

    private async void OnOpenIssueClick(object sender, RoutedEventArgs e)
    {
        await ViewModel.OpenPublicIssueAsync();
    }

    private void OnDescriptionEditorTextChanged(object sender, RoutedEventArgs e)
    {
        if (_isSynchronizingDescriptionEditor)
        {
            return;
        }

        var nextValue = GetDescriptionEditorText();
        if (string.Equals(ViewModel.Description, nextValue, StringComparison.Ordinal))
        {
            return;
        }

        ViewModel.Description = nextValue;
    }

    private void OnViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(FeedbackSectionViewModel.Description))
        {
            SyncDescriptionEditorFromViewModel();
        }

        UpdateVisualState();
    }

    private void UpdateVisualState()
    {
        StatusTextBlock.Text = ViewModel.StatusMessage ?? string.Empty;
        StatusTextBlock.Visibility = ViewModel.HasStatus ? Visibility.Visible : Visibility.Collapsed;

        ErrorBar.IsOpen = ViewModel.HasError;
        ErrorBar.Visibility = ViewModel.HasError ? Visibility.Visible : Visibility.Collapsed;
        ErrorBar.Message = ViewModel.ErrorMessage;

        SubmitButton.IsEnabled = ViewModel.CanSubmit;
        SubmitButton.Content = ViewModel.SubmitButtonText;
        OpenIssueButton.IsEnabled = ViewModel.CanOpenPublicIssue;
        OpenIssueButton.Visibility = ViewModel.HasPublicIssueUrl
            ? Visibility.Visible
            : Visibility.Collapsed;
        SubmittingIndicator.IsActive = ViewModel.IsSubmitting;
        SubmittingIndicator.Visibility = ViewModel.IsSubmitting
            ? Visibility.Visible
            : Visibility.Collapsed;
        KindComboBox.IsEnabled = !ViewModel.IsSubmitting;
        TitleTextBox.IsEnabled = !ViewModel.IsSubmitting;
        DescriptionEditor.IsReadOnly = ViewModel.IsSubmitting;
        ContactEmailTextBox.IsEnabled = !ViewModel.IsSubmitting;
        AllowPrivateContactByEmailCheckBox.IsEnabled = !ViewModel.IsSubmitting;
        IncludeDiagnosticsCheckBox.IsEnabled = !ViewModel.IsSubmitting;
        IncludeLogCheckBox.IsEnabled = !ViewModel.IsSubmitting;
    }

    private void SyncDescriptionEditorFromViewModel()
    {
        var currentValue = GetDescriptionEditorText();
        if (string.Equals(currentValue, ViewModel.Description, StringComparison.Ordinal))
        {
            return;
        }

        _isSynchronizingDescriptionEditor = true;
        try
        {
            DescriptionEditor.Document.SetText(TextSetOptions.None, ViewModel.Description ?? string.Empty);
        }
        finally
        {
            _isSynchronizingDescriptionEditor = false;
        }
    }

    private string GetDescriptionEditorText()
    {
        DescriptionEditor.Document.GetText(TextGetOptions.None, out var text);
        return NormalizeRichEditText(text);
    }

    private static string NormalizeRichEditText(string? text)
    {
        if (string.IsNullOrEmpty(text))
        {
            return string.Empty;
        }

        var normalized = text.Replace("\r\n", "\n", StringComparison.Ordinal).Replace('\r', '\n');
        if (normalized.EndsWith('\n'))
        {
            normalized = normalized[..^1];
        }

        return normalized;
    }

    private void OnPreviewKeyDown(object sender, KeyRoutedEventArgs e)
    {
        if (e.Key != VirtualKey.Escape || !FocusNavigationHelper.IsFocusWithin(this))
        {
            return;
        }

        e.Handled = true;
        ExitToSectionListRequested?.Invoke(this, EventArgs.Empty);
    }
}
