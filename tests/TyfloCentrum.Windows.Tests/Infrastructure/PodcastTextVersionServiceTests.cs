using TyfloCentrum.Windows.Domain.Models;
using TyfloCentrum.Windows.Domain.Services;
using TyfloCentrum.Windows.Infrastructure.Http;
using Xunit;

namespace TyfloCentrum.Windows.Tests.Infrastructure;

public sealed class PodcastTextVersionServiceTests
{
    [Fact]
    public async Task GetAsync_resolves_page_id_links()
    {
        var pageService = new StubWordPressPageDetailsService
        {
            Page = CreatePage(11085, "Wersja tekstowa z identyfikatora"),
        };
        var service = new PodcastTextVersionService(pageService);

        var document = await service.GetAsync(
            new RelatedLink("Tekstowa wersja odcinka", new Uri("https://podcasts.example/?page_id=11085")),
            "Fallback",
            "2026-03-20"
        );

        Assert.NotNull(document);
        Assert.Equal(11085, pageService.RequestedPageId);
        Assert.Equal("Wersja tekstowa z identyfikatora", document!.Title);
        Assert.Contains("<article>", document.ReaderHtml);
    }

    [Fact]
    public async Task GetAsync_resolves_slug_links()
    {
        var pageService = new StubWordPressPageDetailsService
        {
            Page = CreatePage(123, "Wersja tekstowa ze sluga"),
        };
        var service = new PodcastTextVersionService(pageService);

        var document = await service.GetAsync(
            new RelatedLink(
                "Tekstowa wersja odcinka",
                new Uri("https://podcasts.example/tekstowe-wersje-audycji/test-wersja-tekstowa/")
            ),
            "Fallback",
            "2026-03-20"
        );

        Assert.NotNull(document);
        Assert.Equal("test-wersja-tekstowa", pageService.RequestedSlug);
        Assert.Equal("Wersja tekstowa ze sluga", document!.Title);
        Assert.Contains("Treść wersji tekstowej", document.ReaderHtml);
    }

    private static WpPostDetail CreatePage(int id, string title)
    {
        return new WpPostDetail
        {
            Id = id,
            Date = "2026-03-20T12:00:00",
            Link = $"https://podcasts.example/tekstowe-wersje-audycji/{id}/",
            Title = new RenderedText(title),
            Content = new RenderedText("<p>Treść wersji tekstowej</p>"),
        };
    }

    private sealed class StubWordPressPageDetailsService : IWordPressPageDetailsService
    {
        public WpPostDetail? Page { get; init; }

        public int? RequestedPageId { get; private set; }

        public string? RequestedSlug { get; private set; }

        public Task<WpPostDetail> GetPageAsync(
            ContentSource source,
            int pageId,
            CancellationToken cancellationToken = default
        )
        {
            RequestedPageId = pageId;
            return Task.FromResult(Page ?? throw new InvalidOperationException());
        }

        public Task<WpPostDetail?> GetPageBySlugAsync(
            ContentSource source,
            string slug,
            CancellationToken cancellationToken = default
        )
        {
            RequestedSlug = slug;
            return Task.FromResult(Page);
        }
    }
}
