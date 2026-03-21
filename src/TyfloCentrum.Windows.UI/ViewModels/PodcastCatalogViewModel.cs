using TyfloCentrum.Windows.Domain.Models;
using TyfloCentrum.Windows.Domain.Services;

namespace TyfloCentrum.Windows.UI.ViewModels;

public sealed class PodcastCatalogViewModel : ContentCatalogViewModelBase
{
    public PodcastCatalogViewModel(
        IWordPressCatalogService catalogService,
        IExternalLinkLauncher externalLinkLauncher
    )
        : base(ContentSource.Podcast, catalogService, externalLinkLauncher) { }

    public override string LeadText => "Przeglądaj kategorie i listę podcastów z Tyflopodcast.";

    public override string ListAutomationName => "Lista podcastów";

    public override string CategoriesAutomationName => "Lista kategorii podcastów";

    protected override string EmptyStateMessage => "Brak podcastów do wyświetlenia w tej kategorii.";

    protected override string LoadErrorMessage => "Nie udało się pobrać listy podcastów. Spróbuj ponownie.";

    protected override string OpenErrorMessage => "Nie udało się otworzyć podcastu w przeglądarce.";

    protected override string AllCategoriesLabel => "Wszystkie kategorie";

    protected override string AllItemsHeading => "Wszystkie podcasty";

    protected override string CategoryItemsHeadingFormat => "Podcasty w kategorii: {0}";
}
