using CommunityToolkit.Mvvm.ComponentModel;
using Tyflocentrum.Windows.Domain.Catalog;
using Tyflocentrum.Windows.Domain.Models;

namespace Tyflocentrum.Windows.UI.ViewModels;

public partial class ShellViewModel : ObservableObject
{
    private AppSection _selectedSection = AppSections.News;

    public ShellViewModel()
    {
        Sections = AppSections.All;
        SelectSection(AppSections.News.Key);
    }

    public IReadOnlyList<AppSection> Sections { get; }

    [ObservableProperty]
    private string selectedSectionKey = AppSections.News.Key;

    [ObservableProperty]
    private string selectedSectionTitle = AppSections.News.Title;

    [ObservableProperty]
    private string selectedSectionDescription = AppSections.News.Description;

    [ObservableProperty]
    private string selectedSectionShortcutLabel = AppSections.News.ShortcutLabel;

    public string BootstrapStatusMessage =>
        $"Wersja testowa Windows. Aktualnie wybrana sekcja: {SelectedSectionTitle}. Skrót: {SelectedSectionShortcutLabel}.";

    public string KeyboardShortcutsDescription =>
        $"Skróty sekcji: {string.Join(", ", Sections.Select(section => $"{section.ShortcutLabel} {section.Title}"))}.";

    public void SelectSection(string? key)
    {
        _selectedSection = Sections.FirstOrDefault(
            candidate => string.Equals(candidate.Key, key, StringComparison.OrdinalIgnoreCase)
        ) ?? AppSections.News;

        SelectedSectionKey = _selectedSection.Key;
        SelectedSectionTitle = _selectedSection.Title;
        SelectedSectionDescription = _selectedSection.Description;
        SelectedSectionShortcutLabel = _selectedSection.ShortcutLabel;
        OnPropertyChanged(nameof(BootstrapStatusMessage));
    }

    public AppSection? GetSectionByShortcutNumber(int shortcutNumber)
    {
        return Sections.FirstOrDefault(section => section.ShortcutNumber == shortcutNumber);
    }
}
