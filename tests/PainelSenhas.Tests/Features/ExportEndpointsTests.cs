using System.Net;
using ClosedXML.Excel;
using PainelSenhas.Tests.Support;
using PainelSenhas.Web.Common;
using PainelSenhas.Web.Data;
using Shouldly;

namespace PainelSenhas.Tests.Features;

public sealed class ExportEndpointsTests(AppFixture app) : AppTestBase(app)
{
    private const string XlsxContentType = "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet";

    [Fact]
    public async Task Export_Admin_ReturnsWorkbookWithSummaryAndTicketSheets()
    {
        // Arrange
        await App.IssueAsync(App.Reception, App.TreasuryId);
        App.Time.Advance(TimeSpan.FromMinutes(4));
        var called = (await App.CallNextAsync(App.TreasuryOperator, App.TreasuryId)).Value;
        App.Time.Advance(TimeSpan.FromMinutes(2));
        await App.ActAsync(App.TreasuryOperator, called, EventKind.Completed);
        await App.IssueAsync(App.Reception, App.PatrimonyId);
        var client = await App.LoginAsync("admin");

        // Act
        var response = await client.GetAsync(ExportUrl(), TestContext.Current.CancellationToken);

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        response.Content.Headers.ContentType!.MediaType.ShouldBe(XlsxContentType);
        response.Content.Headers.ContentDisposition!.FileName.ShouldBe($"historico-senhas-{Today():yyyy-MM-dd}.xlsx");

        using var workbook = await ReadAsync(response);
        workbook.Worksheets.Select(x => x.Name).ShouldBe(["Resumo", "Senhas"]);

        var tickets = workbook.Worksheet("Senhas");
        tickets.Row(1).Cells(1, 11).Select(x => x.GetString()).ShouldBe(
            ["Senha", "Departamento", "Data", "Status", "Emissão", "Chamada", "Encerramento", "Espera (min)", "Atendimento (min)", "Operador", "Guichê"]);
        tickets.LastRowUsed()!.RowNumber().ShouldBe(3);

        var first = tickets.Row(2);
        first.Cell(1).GetString().ShouldBe("T-001");
        first.Cell(2).GetString().ShouldBe("Tesouraria");
        first.Cell(4).GetString().ShouldBe("Concluída");
        first.Cell(8).GetDouble().ShouldBe(4);
        first.Cell(9).GetDouble().ShouldBe(2);
        first.Cell(10).GetString().ShouldBe("op.tesouraria");
        first.Cell(11).GetString().ShouldBe("Guichê 01");

        var second = tickets.Row(3);
        second.Cell(1).GetString().ShouldBe("P-001");
        second.Cell(4).GetString().ShouldBe("Na fila");
        second.Cell(6).IsEmpty().ShouldBeTrue();
        second.Cell(10).GetString().ShouldBe("");
    }

    [Fact]
    public async Task Export_IssuedTime_IsInTheUnitTimeZone()
    {
        // Arrange
        await App.IssueAsync(App.Reception, App.TreasuryId);
        var clock = App.Services.GetRequiredService<UnitClock>();
        var expected = clock.Local(clock.UtcNow);
        var client = await App.LoginAsync("admin");

        // Act
        using var workbook = await ReadAsync(await client.GetAsync(ExportUrl(), TestContext.Current.CancellationToken));

        // Assert
        workbook.Worksheet("Senhas").Cell(2, 5).GetDateTime().ShouldBe(expected, TimeSpan.FromSeconds(1));
    }

    [Fact]
    public async Task Export_SummarySheet_HasTotalsAndOneLinePerDepartment()
    {
        // Arrange
        await App.IssueAsync(App.Reception, App.TreasuryId);
        await App.IssueAsync(App.Reception, App.TreasuryId);
        await App.IssueAsync(App.Reception, App.PatrimonyId);
        var called = (await App.CallNextAsync(App.TreasuryOperator, App.TreasuryId)).Value;
        await App.ActAsync(App.TreasuryOperator, called, EventKind.Absent);
        var client = await App.LoginAsync("admin");

        // Act
        using var workbook = await ReadAsync(await client.GetAsync(ExportUrl(), TestContext.Current.CancellationToken));

        // Assert
        var summary = workbook.Worksheet("Resumo");
        var labels = summary.Column(1).CellsUsed().Select(x => x.GetString()).ToList();
        ValueOf(summary, "Total de senhas").ShouldBe(3);
        ValueOf(summary, "Ausentes").ShouldBe(1);
        ValueOf(summary, "Concluídas").ShouldBe(0);
        ValueOf(summary, "Na fila").ShouldBe(2);
        labels.ShouldContain("Tesouraria");
        labels.ShouldContain("Patrimônio");
    }

    [Fact]
    public async Task Export_WithDepartmentAndSearchFilters_ExportsOnlyMatchingTickets()
    {
        // Arrange
        await App.IssueAsync(App.Reception, App.TreasuryId);
        await App.IssueAsync(App.Reception, App.TreasuryId);
        await App.IssueAsync(App.Reception, App.PatrimonyId);
        var client = await App.LoginAsync("admin");

        // Act
        using var byDepartment = await ReadAsync(await client.GetAsync(
            ExportUrl($"&department={App.PatrimonyId}"), TestContext.Current.CancellationToken));
        using var bySearch = await ReadAsync(await client.GetAsync(
            ExportUrl("&search=T-002"), TestContext.Current.CancellationToken));

        // Assert
        byDepartment.Worksheet("Senhas").LastRowUsed()!.RowNumber().ShouldBe(2);
        byDepartment.Worksheet("Senhas").Cell(2, 1).GetString().ShouldBe("P-001");
        bySearch.Worksheet("Senhas").LastRowUsed()!.RowNumber().ShouldBe(2);
        bySearch.Worksheet("Senhas").Cell(2, 1).GetString().ShouldBe("T-002");
    }

    [Fact]
    public async Task Export_NoTickets_ReturnsWorkbookWithOnlyTheHeader()
    {
        // Arrange
        var client = await App.LoginAsync("admin");

        // Act
        using var workbook = await ReadAsync(await client.GetAsync(ExportUrl(), TestContext.Current.CancellationToken));

        // Assert
        workbook.Worksheet("Senhas").Cell(1, 1).GetString().ShouldBe("Senha");
        workbook.Worksheet("Senhas").Cell(2, 1).IsEmpty().ShouldBeTrue();
    }

    [Theory]
    [InlineData("recepcao")]
    [InlineData("op.tesouraria")]
    public async Task Export_NonAdmin_IsDeniedWithoutAFile(string userName)
    {
        // Arrange
        var client = await App.LoginAsync(userName);

        // Act
        var response = await client.GetAsync(ExportUrl(), TestContext.Current.CancellationToken);

        // Assert
        response.StatusCode.ShouldNotBe(HttpStatusCode.OK);
        response.Content.Headers.ContentType?.MediaType.ShouldNotBe(XlsxContentType);
    }

    [Fact]
    public async Task Export_Anonymous_RedirectsToLogin()
    {
        // Act
        var response = await App.CreateClient().GetAsync(ExportUrl(), TestContext.Current.CancellationToken);

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.Redirect);
        var location = response.Headers.Location!;
        (location.IsAbsoluteUri ? location.AbsolutePath : location.OriginalString).ShouldStartWith("/entrar");
    }

    [Fact]
    public async Task Export_PeriodLongerThanAYear_ReturnsProblemDetails400()
    {
        // Arrange
        var client = await App.LoginAsync("admin");

        // Act
        var response = await client.GetAsync("/gestao/exportar?from=2025-01-01&to=2026-12-31", TestContext.Current.CancellationToken);

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        response.Content.Headers.ContentType!.MediaType.ShouldBe("application/problem+json");
    }

    [Fact]
    public async Task Export_InvalidSearch_ReturnsProblemDetails400()
    {
        // Arrange
        var client = await App.LoginAsync("admin");

        // Act
        var response = await client.GetAsync(ExportUrl("&search=---"), TestContext.Current.CancellationToken);

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Export_MissingPeriod_ReturnsBadRequest()
    {
        // Arrange
        var client = await App.LoginAsync("admin");

        // Act
        var response = await client.GetAsync("/gestao/exportar", TestContext.Current.CancellationToken);

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
    }

    private DateOnly Today() => App.Services.GetRequiredService<UnitClock>().Today;

    private string ExportUrl(string extra = "") => $"/gestao/exportar?from={Today():yyyy-MM-dd}&to={Today():yyyy-MM-dd}{extra}";

    private static async Task<XLWorkbook> ReadAsync(HttpResponseMessage response)
    {
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var bytes = await response.Content.ReadAsByteArrayAsync(TestContext.Current.CancellationToken);
        return new XLWorkbook(new MemoryStream(bytes));
    }

    private static int ValueOf(IXLWorksheet sheet, string label)
    {
        var cell = sheet.Column(1).CellsUsed().First(x => x.GetString() == label);
        return (int)cell.CellRight().GetDouble();
    }
}
