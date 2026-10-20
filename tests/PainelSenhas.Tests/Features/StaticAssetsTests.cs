using System.Net;
using PainelSenhas.Tests.Support;
using Shouldly;

namespace PainelSenhas.Tests.Features;

public sealed class StaticAssetsTests(AppFixture app) : AppTestBase(app)
{
    [Fact]
    public async Task Favicon_Svg_IsServedAsSvg()
    {
        // Act
        var response = await App.CreateClient().GetAsync("/favicon.svg", TestContext.Current.CancellationToken);
        var text = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        response.Content.Headers.ContentType?.MediaType.ShouldBe("image/svg+xml");
        text.ShouldContain("<svg");
    }

    [Fact]
    public async Task Favicon_Ico_IsServedWithIconHeader()
    {
        // Act
        var response = await App.CreateClient().GetAsync("/favicon.ico", TestContext.Current.CancellationToken);
        var bytes = await response.Content.ReadAsByteArrayAsync(TestContext.Current.CancellationToken);

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        bytes.Take(4).ShouldBe(new byte[] { 0, 0, 1, 0 });
    }

    [Fact]
    public async Task LoginPage_DeclaresTheFavicon()
    {
        // Act
        var html = await App.CreateClient().GetStringAsync("/entrar", TestContext.Current.CancellationToken);

        // Assert
        // O @Assets acrescenta um hash ao nome do arquivo (favicon.<hash>.svg)
        html.ShouldContain("rel=\"icon\"");
        html.ShouldMatch(@"href=""favicon(\.[\w-]+)?\.svg""");
        html.ShouldMatch(@"href=""favicon(\.[\w-]+)?\.ico""");
    }
}
