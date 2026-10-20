using PainelSenhas.Tests.Support;
using PainelSenhas.Web.Common;
using PainelSenhas.Web.Data;
using PainelSenhas.Web.Features.Gestao;
using Shouldly;

namespace PainelSenhas.Tests.Features;

public sealed class OverviewQueryTests(AppFixture app) : AppTestBase(app)
{
    [Fact]
    public async Task Overview_NonAdmin_ReturnsUnauthorized()
    {
        // Act
        var reception = await OverviewAsync(App.Reception);
        var operatorUser = await OverviewAsync(App.TreasuryOperator);

        // Assert
        reception.Error.Type.ShouldBe(ErrorType.Unauthorized);
        operatorUser.Error.Type.ShouldBe(ErrorType.Unauthorized);
    }

    [Fact]
    public async Task Overview_NoActivity_ListsActiveDepartmentsWithEmptyCounters()
    {
        // Act
        var result = await OverviewAsync(App.Admin);

        // Assert
        var overview = result.Value;
        overview.Departments.Select(x => x.Prefix).ShouldBe(["T", "P", "V", "M"], ignoreOrder: true);
        overview.Totals.Issued.ShouldBe(0);
        overview.Totals.Pending.ShouldBe(0);
        overview.Totals.InService.ShouldBe(0);
        overview.Totals.AverageWaitMinutes.ShouldBeNull();
        overview.Totals.LongestWaitMinutes.ShouldBeNull();
    }

    [Fact]
    public async Task Overview_AfterActivity_ComputesTotalsAndDepartmentFigures()
    {
        // Arrange
        for (var i = 0; i < 4; i++)
        {
            await App.IssueAsync(App.Reception, App.TreasuryId);
        }

        await App.IssueAsync(App.Reception, App.PatrimonyId);
        App.Time.Advance(TimeSpan.FromMinutes(10));
        var first = (await App.CallNextAsync(App.TreasuryOperator, App.TreasuryId)).Value;
        App.Time.Advance(TimeSpan.FromMinutes(6));
        await App.ActAsync(App.TreasuryOperator, first, EventKind.Completed);
        await App.CallNextAsync(App.TreasuryOperator, App.TreasuryId);
        App.Time.Advance(TimeSpan.FromMinutes(3));

        // Act
        var result = await OverviewAsync(App.Admin);

        // Assert
        var totals = result.Value.Totals;
        totals.Issued.ShouldBe(5);
        totals.Pending.ShouldBe(3);
        totals.InService.ShouldBe(1);
        totals.Completed.ShouldBe(1);
        totals.Absent.ShouldBe(0);
        totals.AverageWaitMinutes.ShouldBe(13);
        totals.AverageServiceMinutes.ShouldBe(6);
        totals.LongestWaitMinutes.ShouldBe(19);

        var treasury = result.Value.Departments.Single(x => x.Prefix == "T");
        treasury.CurrentCode.ShouldBe("T-002");
        treasury.CurrentMinutes.ShouldBe(3);
        treasury.Pending.ShouldBe(2);
        treasury.OldestWaitMinutes.ShouldBe(19);
        treasury.Issued.ShouldBe(4);
        treasury.Completed.ShouldBe(1);
        result.Value.Departments.Single(x => x.Prefix == "P").CurrentCode.ShouldBeNull();
    }

    [Fact]
    public async Task Overview_PendingFromPreviousDay_StaysInQueueButNotInTodaysIssued()
    {
        // Arrange
        await App.IssueAsync(App.Reception, App.TreasuryId);
        App.AdvanceToNextBusinessDay();
        await App.IssueAsync(App.Reception, App.TreasuryId);

        // Act
        var result = await OverviewAsync(App.Admin);

        // Assert
        var treasury = result.Value.Departments.Single(x => x.Prefix == "T");
        treasury.Pending.ShouldBe(2);
        treasury.PendingFromPreviousDays.ShouldBe(1);
        treasury.Issued.ShouldBe(1);
    }

    [Fact]
    public async Task Overview_AbsentTicket_CountsAsAbsentOnly()
    {
        // Arrange
        await App.IssueAsync(App.Reception, App.PatrimonyId);
        var called = (await App.CallNextAsync(App.PatrimonyOperator, App.PatrimonyId)).Value;
        await App.ActAsync(App.PatrimonyOperator, called, EventKind.Absent);

        // Act
        var result = await OverviewAsync(App.Admin);

        // Assert
        result.Value.Totals.Absent.ShouldBe(1);
        result.Value.Totals.Completed.ShouldBe(0);
        result.Value.Totals.InService.ShouldBe(0);
        result.Value.Totals.AverageServiceMinutes.ShouldBeNull();
    }

    private Task<Result<Web.Features.Painel.OverviewView>> OverviewAsync(System.Security.Claims.ClaimsPrincipal who) =>
        App.InScopeAsync(sp => sp.GetRequiredService<OverviewQuery>().ExecuteAsync(who));
}
