using System.Net;
using System.Net.Http;
using System.Text;
using TyfloCentrum.Windows.Domain.Models;
using TyfloCentrum.Windows.Domain.Services;
using TyfloCentrum.Windows.Infrastructure.Http;
using TyfloCentrum.Windows.Infrastructure.Storage;
using Xunit;

namespace TyfloCentrum.Windows.Tests.Infrastructure;

public sealed class ContentDownloadServiceTests
{
    [Fact]
    public async Task DownloadPodcastAsync_saves_mp3_in_selected_directory()
    {
        var tempDirectory = CreateTempDirectory();
        try
        {
            var handler = new StubHttpMessageHandler(request =>
            {
                Assert.Contains("pobierz.php", request.RequestUri?.AbsoluteUri);
                return new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new ByteArrayContent([0x01, 0x02, 0x03, 0x04]),
                };
            });

            var service = CreateService(
                tempDirectory,
                handler,
                new WpPostDetail
                {
                    Id = 10,
                    Date = "2026-03-21T08:00:00",
                    Title = new RenderedText("Podcast testowy"),
                    Content = new RenderedText("<p>Treść</p>"),
                    Link = "https://example.com/post",
                }
            );

            var filePath = await service.DownloadPodcastAsync(123, "Podcast: test/odcinek?");

            Assert.True(File.Exists(filePath));
            Assert.EndsWith(".mp3", filePath);
            Assert.Equal(new byte[] { 0x01, 0x02, 0x03, 0x04 }, await File.ReadAllBytesAsync(filePath));
        }
        finally
        {
            Directory.Delete(tempDirectory, recursive: true);
        }
    }

    [Fact]
    public async Task DownloadArticleAsync_saves_single_html_file_with_embedded_images()
    {
        var tempDirectory = CreateTempDirectory();
        try
        {
            var postDetail = new WpPostDetail
            {
                Id = 22,
                Date = "2026-03-21T10:00:00",
                Title = new RenderedText("Artykuł: test/1?"),
                Content = new RenderedText(
                    "<p>Treść artykułu.</p><p><img src=\"https://example.com/media/test.png\" alt=\"Grafika\"></p>"
                ),
                Link = "https://example.com/artykul",
            };

            var handler = new StubHttpMessageHandler(request =>
            {
                if (request.RequestUri?.AbsoluteUri == "https://example.com/media/test.png")
                {
                    var response = new HttpResponseMessage(HttpStatusCode.OK)
                    {
                        Content = new ByteArrayContent([0x89, 0x50, 0x4E, 0x47]),
                    };
                    response.Content.Headers.ContentType =
                        new System.Net.Http.Headers.MediaTypeHeaderValue("image/png");
                    return response;
                }

                throw new InvalidOperationException($"Unexpected URL: {request.RequestUri}");
            });

            var service = CreateService(tempDirectory, handler, postDetail);

            var filePath = await service.DownloadArticleAsync(
                ContentSource.Article,
                22,
                "Artykuł: test/1?",
                "21 marca 2026",
                "https://example.com/artykul"
            );

            var html = await File.ReadAllTextAsync(filePath, Encoding.UTF8);

            Assert.True(File.Exists(filePath));
            Assert.EndsWith(".html", filePath);
            Assert.DoesNotContain(':', Path.GetFileName(filePath));
            Assert.DoesNotContain('/', Path.GetFileName(filePath));
            Assert.Contains("data:image/png;base64,", html);
            Assert.Contains("Treść artykułu.", html);
        }
        finally
        {
            Directory.Delete(tempDirectory, recursive: true);
        }
    }

    private static ContentDownloadService CreateService(
        string downloadDirectoryPath,
        HttpMessageHandler handler,
        WpPostDetail postDetail
    )
    {
        return new ContentDownloadService(
            new HttpClient(handler),
            new FakeAppSettingsService(downloadDirectoryPath),
            new FakeDownloadDirectoryService(downloadDirectoryPath),
            new FakePostDetailsService(postDetail),
            new FakeMagazineService(postDetail),
            new TyfloCentrumEndpointsOptions()
        );
    }

    private static string CreateTempDirectory()
    {
        var path = Path.Combine(Path.GetTempPath(), "TyfloCentrum.Tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(path);
        return path;
    }

    private sealed class FakeAppSettingsService : IAppSettingsService
    {
        private readonly string _downloadDirectoryPath;

        public FakeAppSettingsService(string downloadDirectoryPath)
        {
            _downloadDirectoryPath = downloadDirectoryPath;
        }

        public Task<AppSettingsSnapshot> GetAsync(CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return Task.FromResult(
                new AppSettingsSnapshot(
                    null,
                    null,
                    _downloadDirectoryPath,
                    PlaybackRateCatalog.DefaultValue,
                    false,
                    null,
                    true,
                    true
                )
            );
        }

        public Task SaveAsync(
            AppSettingsSnapshot settings,
            CancellationToken cancellationToken = default
        )
        {
            cancellationToken.ThrowIfCancellationRequested();
            return Task.CompletedTask;
        }
    }

    private sealed class FakeDownloadDirectoryService : IDownloadDirectoryService
    {
        private readonly string _downloadDirectoryPath;

        public FakeDownloadDirectoryService(string downloadDirectoryPath)
        {
            _downloadDirectoryPath = downloadDirectoryPath;
        }

        public string GetDefaultDownloadDirectoryPath()
        {
            return _downloadDirectoryPath;
        }

        public string GetEffectiveDownloadDirectoryPath(string? configuredPath)
        {
            return string.IsNullOrWhiteSpace(configuredPath) ? _downloadDirectoryPath : configuredPath;
        }
    }

    private sealed class FakePostDetailsService : IWordPressPostDetailsService
    {
        private readonly WpPostDetail _postDetail;

        public FakePostDetailsService(WpPostDetail postDetail)
        {
            _postDetail = postDetail;
        }

        public Task<WpPostDetail> GetPostAsync(
            ContentSource source,
            int postId,
            CancellationToken cancellationToken = default
        )
        {
            cancellationToken.ThrowIfCancellationRequested();
            return Task.FromResult(_postDetail);
        }
    }

    private sealed class FakeMagazineService : ITyfloSwiatMagazineService
    {
        private readonly WpPostDetail _page;

        public FakeMagazineService(WpPostDetail page)
        {
            _page = page;
        }

        public Task<IReadOnlyList<WpPostSummary>> GetIssuesAsync(CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return Task.FromResult<IReadOnlyList<WpPostSummary>>([]);
        }

        public Task<TyfloSwiatIssueDetail> GetIssueAsync(
            int issueId,
            CancellationToken cancellationToken = default
        )
        {
            throw new NotSupportedException();
        }

        public Task<WpPostDetail> GetPageAsync(int pageId, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return Task.FromResult(_page);
        }
    }

    private sealed class StubHttpMessageHandler : HttpMessageHandler
    {
        private readonly Func<HttpRequestMessage, HttpResponseMessage> _responseFactory;

        public StubHttpMessageHandler(Func<HttpRequestMessage, HttpResponseMessage> responseFactory)
        {
            _responseFactory = responseFactory;
        }

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken
        )
        {
            cancellationToken.ThrowIfCancellationRequested();
            return Task.FromResult(_responseFactory(request));
        }
    }
}
