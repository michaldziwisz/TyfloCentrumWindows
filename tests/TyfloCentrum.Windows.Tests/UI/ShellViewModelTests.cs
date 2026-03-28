using TyfloCentrum.Windows.Domain.Catalog;
using TyfloCentrum.Windows.UI.ViewModels;
using Xunit;

namespace TyfloCentrum.Windows.Tests.UI;

public sealed class ShellViewModelTests
{
    [Fact]
    public void Sections_expose_shortcuts_in_visible_display_title()
    {
        var viewModel = new ShellViewModel();

        Assert.Equal(
            [
                "Nowości (Alt+1)",
                "Podcasty (Alt+2)",
                "Artykuły (Alt+3)",
                "Szukaj (Alt+4)",
                "Ulubione (Alt+5)",
                "Tyfloradio (Alt+6)",
                "Ustawienia (Alt+7)",
                "Zgłoś błąd lub sugestię (Alt+8)",
            ],
            viewModel.Sections.Select(section => section.DisplayTitle)
        );
    }

    [Fact]
    public void GetSectionByShortcutNumber_returns_matching_section()
    {
        var viewModel = new ShellViewModel();

        var section = viewModel.GetSectionByShortcutNumber(6);

        Assert.Equal(AppSections.Radio, section);
    }

    [Fact]
    public void SelectSection_updates_selected_section_title_and_description()
    {
        var viewModel = new ShellViewModel();

        viewModel.SelectSection(AppSections.Favorites.Key);

        Assert.Equal("Ulubione", viewModel.SelectedSectionTitle);
        Assert.Equal("Lokalna lista zapisanych podcastów i artykułów.", viewModel.SelectedSectionDescription);
    }

    [Fact]
    public void GetSectionByShortcutNumber_returns_feedback_section_for_alt_8()
    {
        var viewModel = new ShellViewModel();

        var section = viewModel.GetSectionByShortcutNumber(8);

        Assert.Equal(AppSections.Feedback, section);
    }
}
