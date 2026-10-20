using System.Security.Claims;
using PainelSenhas.Tests.Support;
using PainelSenhas.Web.Common;
using PainelSenhas.Web.Data;
using PainelSenhas.Web.Features.Gestao;
using PainelSenhas.Web.Features.Painel;
using Shouldly;

namespace PainelSenhas.Tests.Features;

public sealed class HistoryQueryTests(AppFixture app) : AppTestBase(app)
{
    [Fact]
    public async Task Page_NonAdmin_ReturnsUnauthorized()
    {
        // Act
        var result = await PageAsync(App.Reception, TodayFilter());

        // Assert
        result.Error.Type.ShouldBe(ErrorType.Unauthorized);
    }

    [Fact]
    public async Task Page_PeriodLongerThanAYear_ReturnsValidationError()
    {
        // Arrange
        var from = new DateOnly(2025, 1, 1);

        // Act
        var result = await PageAsync(App.Admin, new HistoryFilter(from, from.AddDays(400)));

        // Assert
        result.Error.Type.ShouldBe(ErrorType.Validation);
    }

    [Fact]
    public async Task Page_PeriodEndingBeforeStart_ReturnsValidationError()
    {
        // Arrange
        var today = Today();

        // Act
        var result = await PageAsync(App.Admin, new HistoryFilter(today, today.AddDays(-1)));

        // Assert
        result.Error.Type.ShouldBe(ErrorType.Validation);
    }

    [Fact]
    public async Task Page_Admin_ComputesTotalsAndAverages()
    {
        // Arrange
        await App.IssueAsync(App.Reception, App.TreasuryId);
        await App.IssueAsync(App.Reception, App.TreasuryId);
        App.Time.Advance(TimeSpan.FromMinutes(10));
        var first = (await App.CallNextAsync(App.TreasuryOperator, App.TreasuryId)).Value;
        App.Time.Advance(TimeSpan.FromMinutes(6));
        await App.ActAsync(App.TreasuryOperator, first, EventKind.Completed);

        // Act
        var result = await PageAsync(App.Admin, TodayFilter());

        // Assert
        var history = result.Value;
        history.Total.ShouldBe(2);
        history.Completed.ShouldBe(1);
        history.Pending.ShouldBe(1);
        history.WaitMinutes.ShouldBe(10);
        history.ServiceMinutes.ShouldBe(6);
    }

    [Fact]
    public async Task Page_RowOfAttendedTicket_CarriesOperatorDeskAndTimes()
    {
        // Arrange
        await App.IssueAsync(App.Reception, App.TreasuryId);
        App.Time.Advance(TimeSpan.FromMinutes(4));
        var called = (await App.CallNextAsync(App.TreasuryOperator, App.TreasuryId)).Value;
        App.Time.Advance(TimeSpan.FromMinutes(2));
        await App.ActAsync(App.TreasuryOperator, called, EventKind.Completed);

        // Act
        var row = (await PageAsync(App.Admin, TodayFilter())).Value.Rows.Single();

        // Assert
        row.Ticket.Code.ShouldBe("T-001");
        row.Operator.ShouldBe("op.tesouraria");
        row.Desk.ShouldBe("Guichê 01");
        row.WaitMinutes.ShouldBe(4);
        row.ServiceMinutes.ShouldBe(2);
    }

    [Fact]
    public async Task Page_RowOfPendingTicket_HasNoOperatorOrTimes()
    {
        // Arrange
        await App.IssueAsync(App.Reception, App.TreasuryId);

        // Act
        var row = (await PageAsync(App.Admin, TodayFilter())).Value.Rows.Single();

        // Assert
        row.Operator.ShouldBeNull();
        row.Desk.ShouldBeNull();
        row.WaitMinutes.ShouldBeNull();
        row.ServiceMinutes.ShouldBeNull();
    }

    [Theory]
    [InlineData("T-002", new[] { "T-002" })]
    [InlineData("t-002", new[] { "T-002" })]
    [InlineData("002", new[] { "T-002", "P-002" })]
    [InlineData("2", new[] { "T-002", "P-002" })]
    [InlineData("P", new[] { "P-001", "P-002" })]
    [InlineData("P2", new[] { "P-002" })]
    public async Task Page_SearchByCode_FindsMatchingTickets(string search, string[] expected)
    {
        // Arrange
        await IssueManyAsync(App.TreasuryId, 3);
        await IssueManyAsync(App.PatrimonyId, 2);

        // Act
        var result = await PageAsync(App.Admin, TodayFilter() with { Search = search });

        // Assert
        result.Value.Rows.Select(x => x.Ticket.Code).ShouldBe(expected, ignoreOrder: true);
    }

    [Theory]
    [InlineData("---")]
    [InlineData("T-12-3")]
    [InlineData("abc def")]
    public async Task Page_InvalidSearch_ReturnsValidationError(string search)
    {
        // Act
        var result = await PageAsync(App.Admin, TodayFilter() with { Search = search });

        // Assert
        result.Error.Type.ShouldBe(ErrorType.Validation);
    }

    [Fact]
    public async Task Page_FilterByDepartmentAndStatus_NarrowsRowsAndTotals()
    {
        // Arrange
        await IssueManyAsync(App.TreasuryId, 2);
        await IssueManyAsync(App.PatrimonyId, 1);
        var called = (await App.CallNextAsync(App.TreasuryOperator, App.TreasuryId)).Value;
        await App.ActAsync(App.TreasuryOperator, called, EventKind.Completed);

        // Act
        var byDepartment = await PageAsync(App.Admin, TodayFilter() with { DepartmentId = App.TreasuryId });
        var byStatus = await PageAsync(App.Admin, TodayFilter() with { Status = TicketStatus.Pending });

        // Assert
        byDepartment.Value.Total.ShouldBe(2);
        byStatus.Value.Total.ShouldBe(2);
        byStatus.Value.Rows.ShouldAllBe(x => x.Ticket.Status == TicketStatus.Pending);
    }

    [Fact]
    public async Task Page_SortByCode_OrdersByPrefixThenNumberInBothDirections()
    {
        // Arrange
        await IssueManyAsync(App.TreasuryId, 2);
        await IssueManyAsync(App.PatrimonyId, 2);

        // Act
        var ascending = await PageAsync(App.Admin, TodayFilter(), HistorySort.Code, descending: false);
        var descending = await PageAsync(App.Admin, TodayFilter(), HistorySort.Code, descending: true);

        // Assert
        ascending.Value.Rows.Select(x => x.Ticket.Code).ShouldBe(["P-001", "P-002", "T-001", "T-002"]);
        descending.Value.Rows.Select(x => x.Ticket.Code).ShouldBe(["T-002", "T-001", "P-002", "P-001"]);
    }

    [Fact]
    public async Task Page_DefaultSortByIssued_ShowsNewestFirstWhenDescending()
    {
        // Arrange
        await IssueManyAsync(App.TreasuryId, 3);

        // Act
        var result = await PageAsync(App.Admin, TodayFilter(), HistorySort.Issued, descending: true);

        // Assert
        result.Value.Rows.Select(x => x.Ticket.Code).ShouldBe(["T-003", "T-002", "T-001"]);
    }

    [Fact]
    public async Task Page_Paging_ReturnsRequestedSliceAndKeepsTheTotal()
    {
        // Arrange
        await IssueManyAsync(App.TreasuryId, 5);

        // Act
        var second = await PageAsync(App.Admin, TodayFilter(), HistorySort.Code, descending: false, page: 2, pageSize: 2);

        // Assert
        second.Value.Total.ShouldBe(5);
        second.Value.Rows.Select(x => x.Ticket.Code).ShouldBe(["T-003", "T-004"]);
    }

    [Fact]
    public async Task All_ReturnsEverySenhaInChronologicalOrder()
    {
        // Arrange
        await IssueManyAsync(App.TreasuryId, 3);

        // Act
        var result = await App.InScopeAsync(sp => sp.GetRequiredService<HistoryQuery>().AllAsync(App.Admin, TodayFilter()));

        // Assert
        result.Value.Select(x => x.Ticket.Code).ShouldBe(["T-001", "T-002", "T-003"]);
    }

    [Fact]
    public async Task All_NonAdmin_ReturnsUnauthorized()
    {
        // Act
        var result = await App.InScopeAsync(sp => sp.GetRequiredService<HistoryQuery>().AllAsync(App.TreasuryOperator, TodayFilter()));

        // Assert
        result.Error.Type.ShouldBe(ErrorType.Unauthorized);
    }

    private DateOnly Today() => App.Services.GetRequiredService<UnitClock>().Today;

    private HistoryFilter TodayFilter() => new(Today(), Today());

    private async Task IssueManyAsync(int departmentId, int count)
    {
        for (var i = 0; i < count; i++)
        {
            await App.IssueAsync(App.Reception, departmentId);
        }
    }

    private Task<Result<HistoryView>> PageAsync(
        ClaimsPrincipal who, HistoryFilter filter, HistorySort sort = HistorySort.Issued, bool descending = true, int page = 1, int pageSize = 25) =>
        App.InScopeAsync(sp => sp.GetRequiredService<HistoryQuery>().PageAsync(who, filter, sort, descending, page, pageSize));
}
