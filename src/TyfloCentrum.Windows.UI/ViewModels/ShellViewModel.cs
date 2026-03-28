using CommunityToolkit.Mvvm.ComponentModel;
using TyfloCentrum.Windows.Domain.Catalog;
using TyfloCentrum.Windows.Domain.Models;

namespace TyfloCentrum.Windows.UI.ViewModels;

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

    public void SelectSection(string? key)
    {
        _selectedSection = Sections.FirstOrDefault(
            candidate => string.Equals(candidate.Key, key, StringComparison.OrdinalIgnoreCase)
        ) ?? AppSections.News;

        SelectedSectionKey = _selectedSection.Key;
        SelectedSectionTitle = _selectedSection.Title;
        SelectedSectionDescription = _selectedSection.Description;
    }

    public AppSection? GetSectionByShortcutNumber(int shortcutNumber)
    {
        return Sections.FirstOrDefault(section => section.ShortcutNumber == shortcutNumber);
    }
}
