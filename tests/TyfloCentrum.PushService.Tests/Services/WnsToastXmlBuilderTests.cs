using TyfloCentrum.PushService.Models;
using TyfloCentrum.PushService.Services;
using Xunit;

namespace TyfloCentrum.PushService.Tests.Services;

public sealed class WnsToastXmlBuilderTests
{
    [Fact]
    public void Build_includes_launch_arguments_and_escaped_text()
    {
        var payload = new PushDispatchPayload(
            PushCategories.Article,
            "Tytuł & test",
            "Treść <czytelna>",
            42,
            "2026-03-21T20:00:00Z",
            "https://example.test/post?id=42"
        );

        var xml = WnsToastXmlBuilder.Build(payload);

        Assert.Contains("kind=article", xml);
        Assert.Contains("id=42", xml);
        Assert.Contains("title=Tytu%C5%82%20%26%20test", xml);
        Assert.Contains("Tytuł &amp; test", xml);
        Assert.Contains("Treść &lt;czytelna&gt;", xml);
    }
}
