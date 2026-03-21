using TyfloCentrum.Windows.Domain.Catalog;
using TyfloCentrum.Windows.UI.ViewModels;
using Xunit;

namespace TyfloCentrum.Windows.Tests.UI;

public sealed class ShellViewModelTests
{
    [Fact]
    public void KeyboardShortcutsDescription_lists_all_primary_sections()
    {
        var viewModel = new ShellViewModel();

        Assert.Equal(
            "Skróty sekcji: Alt+1 Nowości, Alt+2 Podcasty, Alt+3 Artykuły, Alt+4 Szukaj, Alt+5 Ulubione, Alt+6 Tyfloradio, Alt+7 Ustawienia.",
            viewModel.KeyboardShortcutsDescription
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
    public void SelectSection_updates_bootstrap_message_with_shortcut()
    {
        var viewModel = new ShellViewModel();

        viewModel.SelectSection(AppSections.Favorites.Key);

        Assert.Equal("Ulubione", viewModel.SelectedSectionTitle);
        Assert.Equal("Alt+5", viewModel.SelectedSectionShortcutLabel);
        Assert.Equal(
            "Wersja testowa Windows. Aktualnie wybrana sekcja: Ulubione. Skrót: Alt+5.",
            viewModel.BootstrapStatusMessage
        );
    }
}
