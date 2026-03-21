using TyfloCentrum.Windows.Domain.Models;
using TyfloCentrum.Windows.Domain.Services;

namespace TyfloCentrum.Windows.UI.ViewModels;

public sealed class ArticleCatalogViewModel : ContentCatalogViewModelBase
{
    public ArticleCatalogViewModel(
        IWordPressCatalogService catalogService,
        IExternalLinkLauncher externalLinkLauncher
    )
        : base(ContentSource.Article, catalogService, externalLinkLauncher) { }

    public override string LeadText =>
        "Przeglądaj kategorie, listę artykułów i czasopismo TyfloŚwiat.";

    public override string ListAutomationName => "Lista artykułów";

    public override string CategoriesAutomationName => "Lista kategorii artykułów";

    protected override string EmptyStateMessage => "Brak artykułów do wyświetlenia w tej kategorii.";

    protected override string LoadErrorMessage => "Nie udało się pobrać listy artykułów. Spróbuj ponownie.";

    protected override string OpenErrorMessage => "Nie udało się otworzyć artykułu w przeglądarce.";

    protected override string AllCategoriesLabel => "Wszystkie kategorie";

    protected override string AllItemsHeading => "Wszystkie artykuły";

    protected override string CategoryItemsHeadingFormat => "Artykuły w kategorii: {0}";
}
