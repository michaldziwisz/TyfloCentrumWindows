using TyfloCentrum.Windows.Domain.Models;
using TyfloCentrum.Windows.Domain.Services;
using TyfloCentrum.Windows.UI.ViewModels;
using Xunit;

namespace TyfloCentrum.Windows.Tests.UI;

public sealed class PostDetailViewModelTests
{
    [Fact]
    public async Task LoadIfNeededAsync_formats_post_detail_content_for_reading()
    {
        var service = new FakePostDetailsService
        {
            Item = new WpPostDetail
            {
                Id = 44,
                Date = "2026-03-20T12:00:00",
                Link = "https://example.invalid/post/44",
                Title = new RenderedText("Test &amp; wpis"),
                Excerpt = new RenderedText("<p>Opis</p>"),
                Content = new RenderedText("<h2>Nagłówek</h2><p>Pierwszy akapit</p><ul><li>Punkt 1</li><li>Punkt 2</li></ul>"),
                Guid = new RenderedText("https://example.invalid/?p=44"),
            },
        };
        var commentsService = new FakeCommentsService();
        var viewModel = new PostDetailViewModel(
            new FakeAudioPlaybackRequestFactory(),
            service,
            commentsService,
            new FakeExternalLinkLauncher(),
            new FakeFavoritesService(),
            new FakeShareService()
        );
        viewModel.Initialize(ContentSource.Article, 44, "Fallback", "20.03.2026", "https://fallback.invalid");

        await viewModel.LoadIfNeededAsync();

        Assert.Equal(ContentSource.Article, service.RequestedSource);
        Assert.Equal(44, service.RequestedPostId);
        Assert.Equal("Test & wpis", viewModel.Title);
        Assert.Equal("20.03.2026", viewModel.PublishedDate);
        Assert.Contains("Nagłówek", viewModel.ContentText, StringComparison.Ordinal);
        Assert.Contains("Pierwszy akapit", viewModel.ContentText, StringComparison.Ordinal);
        Assert.Contains("- Punkt 1", viewModel.ContentText, StringComparison.Ordinal);
        Assert.True(viewModel.HasContent);
        Assert.Empty(viewModel.Comments);
    }

    [Fact]
    public async Task OpenInBrowserAsync_sets_error_when_launcher_fails()
    {
        var launcher = new FakeExternalLinkLauncher { Result = false };
        var viewModel = new PostDetailViewModel(
            new FakeAudioPlaybackRequestFactory(),
            new FakePostDetailsService(),
            new FakeCommentsService(),
            launcher,
            new FakeFavoritesService(),
            new FakeShareService()
        );
        viewModel.Initialize(ContentSource.Podcast, 11, "Tytuł", "19.03.2026", "https://example.invalid/post/11");

        await viewModel.OpenInBrowserAsync();

        Assert.Equal("https://example.invalid/post/11", launcher.LastTarget);
        Assert.Equal("Nie udało się otworzyć wpisu w przeglądarce.", viewModel.ErrorMessage);
    }

    [Fact]
    public async Task LoadIfNeededAsync_loads_comments_for_podcast_details()
    {
        var commentsService = new FakeCommentsService
        {
            Items =
            [
                new WordPressComment
                {
                    Id = 1001,
                    PostId = 11,
                    ParentId = 0,
                    AuthorName = "Słuchacz",
                    Content = new RenderedText("<p>Świetny odcinek!</p>"),
                },
            ],
        };
        var viewModel = new PostDetailViewModel(
            new FakeAudioPlaybackRequestFactory(),
            new FakePostDetailsService(),
            commentsService,
            new FakeExternalLinkLauncher(),
            new FakeFavoritesService(),
            new FakeShareService()
        );
        viewModel.Initialize(ContentSource.Podcast, 11, "Tytuł", "19.03.2026", "https://example.invalid/post/11");

        await viewModel.LoadIfNeededAsync();

        Assert.Equal(11, commentsService.RequestedPostId);
        var comment = Assert.Single(viewModel.Comments);
        Assert.Equal("Słuchacz", comment.AuthorName);
        Assert.Contains("Świetny odcinek!", comment.BodyText, StringComparison.Ordinal);
        Assert.Equal("Komentarze (1)", viewModel.CommentsHeaderText);
    }

    [Fact]
    public async Task RetryCommentsAsync_sets_error_when_comments_fail()
    {
        var commentsService = new FakeCommentsService
        {
            ExceptionToThrow = new HttpRequestException("boom"),
        };
        var viewModel = new PostDetailViewModel(
            new FakeAudioPlaybackRequestFactory(),
            new FakePostDetailsService(),
            commentsService,
            new FakeExternalLinkLauncher(),
            new FakeFavoritesService(),
            new FakeShareService()
        );
        viewModel.Initialize(ContentSource.Podcast, 11, "Tytuł", "19.03.2026", "https://example.invalid/post/11");

        await viewModel.RetryCommentsAsync();

        Assert.Equal("Nie udało się pobrać komentarzy. Spróbuj ponownie.", viewModel.CommentsErrorMessage);
        Assert.True(viewModel.HasCommentsError);
    }

    [Fact]
    public void CreatePlaybackRequest_returns_podcast_request_for_podcast_details()
    {
        var factory = new FakeAudioPlaybackRequestFactory();
        var viewModel = new PostDetailViewModel(
            factory,
            new FakePostDetailsService(),
            new FakeCommentsService(),
            new FakeExternalLinkLauncher(),
            new FakeFavoritesService(),
            new FakeShareService()
        );
        viewModel.Initialize(ContentSource.Podcast, 11, "Tytuł", "19.03.2026", "https://example.invalid/post/11");

        var request = viewModel.CreatePlaybackRequest();

        Assert.NotNull(request);
        Assert.Equal(11, factory.LastPodcastPostId);
        Assert.Equal("Tytuł", factory.LastPodcastTitle);
        Assert.Equal("19.03.2026", factory.LastPodcastSubtitle);
    }

    [Fact]
    public async Task ToggleFavoriteAsync_adds_and_removes_current_post()
    {
        var favoritesService = new FakeFavoritesService();
        var viewModel = new PostDetailViewModel(
            new FakeAudioPlaybackRequestFactory(),
            new FakePostDetailsService(),
            new FakeCommentsService(),
            new FakeExternalLinkLauncher(),
            favoritesService,
            new FakeShareService()
        );
        viewModel.Initialize(ContentSource.Podcast, 11, "Tytuł", "19.03.2026", "https://example.invalid/post/11");

        await viewModel.ToggleFavoriteAsync();

        Assert.True(viewModel.IsFavorite);
        Assert.NotNull(favoritesService.LastAddedItem);
        Assert.Equal("Tytuł", favoritesService.LastAddedItem!.Title);

        await viewModel.ToggleFavoriteAsync();

        Assert.False(viewModel.IsFavorite);
        Assert.Equal(ContentSource.Podcast, favoritesService.LastRemovedSource);
        Assert.Equal(11, favoritesService.LastRemovedPostId);
    }

    [Fact]
    public async Task LoadIfNeededAsync_updates_comments_count_header_for_multiple_comments()
    {
        var commentsService = new FakeCommentsService
        {
            Items =
            [
                new WordPressComment
                {
                    Id = 1001,
                    PostId = 11,
                    ParentId = 0,
                    AuthorName = "Słuchacz 1",
                    Content = new RenderedText("<p>Pierwszy komentarz</p>"),
                },
                new WordPressComment
                {
                    Id = 1002,
                    PostId = 11,
                    ParentId = 0,
                    AuthorName = "Słuchacz 2",
                    Content = new RenderedText("<p>Drugi komentarz</p>"),
                },
            ],
        };
        var viewModel = new PostDetailViewModel(
            new FakeAudioPlaybackRequestFactory(),
            new FakePostDetailsService(),
            commentsService,
            new FakeExternalLinkLauncher(),
            new FakeFavoritesService(),
            new FakeShareService()
        );
        viewModel.Initialize(ContentSource.Podcast, 11, "Tytuł", "19.03.2026", "https://example.invalid/post/11");

        await viewModel.LoadIfNeededAsync();

        Assert.Equal("Komentarze (2)", viewModel.CommentsHeaderText);
    }

    [Fact]
    public async Task ShareAsync_invokes_system_share_for_valid_link()
    {
        var shareService = new FakeShareService();
        var viewModel = new PostDetailViewModel(
            new FakeAudioPlaybackRequestFactory(),
            new FakePostDetailsService(),
            new FakeCommentsService(),
            new FakeExternalLinkLauncher(),
            new FakeFavoritesService(),
            shareService
        );
        viewModel.Initialize(ContentSource.Article, 44, "Tytuł wpisu", "20.03.2026", "https://example.invalid/post/44");

        await viewModel.ShareAsync();

        Assert.Equal("Tytuł wpisu", shareService.LastTitle);
        Assert.Equal("20.03.2026", shareService.LastDescription);
        Assert.Equal("https://example.invalid/post/44", shareService.LastUrl);
        Assert.Null(viewModel.ErrorMessage);
    }

    private sealed class FakePostDetailsService : IWordPressPostDetailsService
    {
        public WpPostDetail Item { get; init; } = new()
        {
            Id = 1,
            Date = "2026-03-19T10:00:00",
            Link = "https://example.invalid/post/1",
            Title = new RenderedText("Test"),
            Excerpt = new RenderedText("Opis"),
            Content = new RenderedText("<p>Treść</p>"),
            Guid = new RenderedText("https://example.invalid/?p=1"),
        };

        public ContentSource RequestedSource { get; private set; }

        public int RequestedPostId { get; private set; }

        public Task<WpPostDetail> GetPostAsync(
            ContentSource source,
            int postId,
            CancellationToken cancellationToken = default
        )
        {
            RequestedSource = source;
            RequestedPostId = postId;
            return Task.FromResult(Item);
        }
    }

    private sealed class FakeCommentsService : IWordPressCommentsService
    {
        public IReadOnlyList<WordPressComment> Items { get; init; } = [];

        public Exception? ExceptionToThrow { get; init; }

        public int RequestedPostId { get; private set; }

        public Task<IReadOnlyList<WordPressComment>> GetCommentsAsync(
            int postId,
            CancellationToken cancellationToken = default
        )
        {
            RequestedPostId = postId;

            if (ExceptionToThrow is not null)
            {
                throw ExceptionToThrow;
            }

            return Task.FromResult(Items);
        }
    }

    private sealed class FakeAudioPlaybackRequestFactory : IAudioPlaybackRequestFactory
    {
        public int LastPodcastPostId { get; private set; }

        public string? LastPodcastTitle { get; private set; }

        public string? LastPodcastSubtitle { get; private set; }

        public AudioPlaybackRequest CreatePodcast(
            int postId,
            string title,
            string? subtitle = null,
            double? initialSeekSeconds = null
        )
        {
            LastPodcastPostId = postId;
            LastPodcastTitle = title;
            LastPodcastSubtitle = subtitle;

            return new AudioPlaybackRequest(
                "Podcast",
                title,
                subtitle,
                new Uri($"https://audio.example/podcast/{postId}.mp3"),
                false,
                true,
                true,
                InitialSeekSeconds: initialSeekSeconds
            );
        }

        public AudioPlaybackRequest CreateRadio(string? subtitle = null)
        {
            return new AudioPlaybackRequest(
                "Tyfloradio",
                "Tyfloradio",
                subtitle,
                new Uri("https://audio.example/live.m3u8"),
                true,
                false,
                false
            );
        }
    }

    private sealed class FakeExternalLinkLauncher : IExternalLinkLauncher
    {
        public bool Result { get; init; } = true;

        public string? LastTarget { get; private set; }

        public Task<bool> LaunchAsync(string target, CancellationToken cancellationToken = default)
        {
            LastTarget = target;
            return Task.FromResult(Result);
        }
    }

    private sealed class FakeFavoritesService : IFavoritesService
    {
        public FavoriteItem? LastAddedItem { get; private set; }

        public string? LastRemovedId { get; private set; }

        public ContentSource? LastRemovedSource { get; private set; }

        public int LastRemovedPostId { get; private set; }

        public Task<IReadOnlyList<FavoriteItem>> GetItemsAsync(
            CancellationToken cancellationToken = default
        )
        {
            cancellationToken.ThrowIfCancellationRequested();
            return Task.FromResult<IReadOnlyList<FavoriteItem>>([]);
        }

        public Task<bool> IsFavoriteAsync(
            string favoriteId,
            CancellationToken cancellationToken = default
        )
        {
            cancellationToken.ThrowIfCancellationRequested();
            return Task.FromResult(false);
        }

        public Task<bool> IsFavoriteAsync(
            ContentSource source,
            int postId,
            FavoriteArticleOrigin articleOrigin = FavoriteArticleOrigin.Post,
            CancellationToken cancellationToken = default
        )
        {
            cancellationToken.ThrowIfCancellationRequested();
            return Task.FromResult(false);
        }

        public Task AddOrUpdateAsync(FavoriteItem item, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            LastAddedItem = item;
            return Task.CompletedTask;
        }

        public Task RemoveAsync(string favoriteId, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            LastRemovedId = favoriteId;
            return Task.CompletedTask;
        }

        public Task RemoveAsync(
            ContentSource source,
            int postId,
            FavoriteArticleOrigin articleOrigin = FavoriteArticleOrigin.Post,
            CancellationToken cancellationToken = default
        )
        {
            cancellationToken.ThrowIfCancellationRequested();
            LastRemovedSource = source;
            LastRemovedPostId = postId;
            return Task.CompletedTask;
        }
    }

    private sealed class FakeShareService : IShareService
    {
        public string? LastTitle { get; private set; }

        public string? LastDescription { get; private set; }

        public string? LastUrl { get; private set; }

        public bool Result { get; init; } = true;

        public Task<bool> ShareLinkAsync(
            string title,
            string? description,
            string url,
            CancellationToken cancellationToken = default
        )
        {
            cancellationToken.ThrowIfCancellationRequested();
            LastTitle = title;
            LastDescription = description;
            LastUrl = url;
            return Task.FromResult(Result);
        }
    }
}
