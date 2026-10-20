using PainelSenhas.Tests.Support;
using PainelSenhas.Web.Common;
using PainelSenhas.Web.Data;
using PainelSenhas.Web.Features.Painel;
using Shouldly;

namespace PainelSenhas.Tests.Features;

public sealed class QueueQueriesTests(AppFixture app) : AppTestBase(app)
{
    [Fact]
    public async Task Departments_AfterActivity_CountsPendingCompletedAbsentAndCurrentTicket()
    {
        // Arrange
        for (var i = 0; i < 4; i++)
        {
            await App.IssueAsync(App.Reception, App.TreasuryId);
        }

        var first = (await App.CallNextAsync(App.TreasuryOperator, App.TreasuryId)).Value;
        await App.ActAsync(App.TreasuryOperator, first, EventKind.Completed);
        var second = (await App.CallNextAsync(App.TreasuryOperator, App.TreasuryId)).Value;
        await App.ActAsync(App.TreasuryOperator, second, EventKind.Absent);
        await App.CallNextAsync(App.TreasuryOperator, App.TreasuryId);

        // Act
        var departments = await App.InScopeAsync(sp => sp.GetRequiredService<QueueQueries>().DepartmentsAsync());

        // Assert
        var treasury = departments.Single(x => x.Id == App.TreasuryId);
        treasury.Pending.ShouldBe(1);
        treasury.Completed.ShouldBe(1);
        treasury.Absent.ShouldBe(1);
        treasury.CurrentCode.ShouldBe("T-003");
    }

    [Fact]
    public async Task Departments_PendingPreferential_IsCountedSeparately()
    {
        // Arrange
        await App.IssueAsync(App.Reception, App.TreasuryId);
        await App.IssueAsync(App.Reception, App.TreasuryId, preferential: true);

        // Act
        var departments = await App.InScopeAsync(sp => sp.GetRequiredService<QueueQueries>().DepartmentsAsync());

        // Assert
        var treasury = departments.Single(x => x.Id == App.TreasuryId);
        treasury.Pending.ShouldBe(2);
        treasury.PendingPreferential.ShouldBe(1);
    }

    [Fact]
    public async Task Attendance_PendingList_ShowsPreferentialsFirstInCallOrder()
    {
        // Arrange
        await App.IssueAsync(App.Reception, App.TreasuryId);
        await App.IssueAsync(App.Reception, App.TreasuryId, preferential: true);
        await App.IssueAsync(App.Reception, App.TreasuryId);
        await App.IssueAsync(App.Reception, App.TreasuryId, preferential: true);

        // Act
        var view = await App.InScopeAsync(sp =>
            sp.GetRequiredService<QueueQueries>().AttendanceAsync(App.TreasuryOperator, App.TreasuryId));

        // Assert
        view.Value.Pending.Select(x => x.Code).ShouldBe(["T-002", "T-004", "T-001", "T-003"]);
    }

    [Fact]
    public async Task Board_PreferentialCall_IsMarkedAsPreferential()
    {
        // Arrange
        await App.IssueAsync(App.Reception, App.TreasuryId, preferential: true);
        await App.CallNextAsync(App.TreasuryOperator, App.TreasuryId);

        // Act
        var board = await App.InScopeAsync(sp => sp.GetRequiredService<QueueQueries>().BoardAsync());

        // Assert
        board.Calls.Single().IsPreferential.ShouldBeTrue();
    }

    [Fact]
    public async Task Departments_NextBusinessDay_CountersCoverOnlyToday()
    {
        // Arrange
        await App.IssueAsync(App.Reception, App.TreasuryId);
        var called = (await App.CallNextAsync(App.TreasuryOperator, App.TreasuryId)).Value;
        await App.ActAsync(App.TreasuryOperator, called, EventKind.Completed);
        App.AdvanceToNextBusinessDay();

        // Act
        var departments = await App.InScopeAsync(sp => sp.GetRequiredService<QueueQueries>().DepartmentsAsync());

        // Assert
        departments.Single(x => x.Id == App.TreasuryId).Completed.ShouldBe(0);
    }

    [Fact]
    public async Task Board_RepeatedCall_AppearsOnceAndMarkedAsRepeat()
    {
        // Arrange
        await App.IssueAsync(App.Reception, App.TreasuryId);
        var called = (await App.CallNextAsync(App.TreasuryOperator, App.TreasuryId)).Value;
        await App.ActAsync(App.TreasuryOperator, called, EventKind.Repeated);
        await App.ActAsync(App.TreasuryOperator, called, EventKind.Repeated);

        // Act
        var board = await App.InScopeAsync(sp => sp.GetRequiredService<QueueQueries>().BoardAsync());

        // Assert
        board.Calls.Count.ShouldBe(1);
        board.Calls[0].Code.ShouldBe("T-001");
        board.Calls[0].Desk.ShouldBe("Guichê 01");
        board.Calls[0].IsRepeat.ShouldBeTrue();
    }

    [Fact]
    public async Task Board_TwoDepartments_ListsMostRecentCallFirst()
    {
        // Arrange
        await App.IssueAsync(App.Reception, App.TreasuryId);
        await App.IssueAsync(App.Reception, App.PatrimonyId);
        await App.CallNextAsync(App.TreasuryOperator, App.TreasuryId);
        await App.CallNextAsync(App.PatrimonyOperator, App.PatrimonyId);

        // Act
        var board = await App.InScopeAsync(sp => sp.GetRequiredService<QueueQueries>().BoardAsync());

        // Assert
        board.Calls.Select(x => x.Code).ShouldBe(["P-001", "T-001"]);
    }

    [Fact]
    public async Task Attendance_OperatorOfAnotherDepartment_ReturnsUnauthorized()
    {
        // Act
        var result = await App.InScopeAsync(sp => sp.GetRequiredService<QueueQueries>().AttendanceAsync(App.PatrimonyOperator, App.TreasuryId));

        // Assert
        result.Error.Type.ShouldBe(ErrorType.Unauthorized);
    }

    [Fact]
    public async Task Attendance_AfterCall_ShowsActiveTicketPendingAndRecent()
    {
        // Arrange
        for (var i = 0; i < 3; i++)
        {
            await App.IssueAsync(App.Reception, App.TreasuryId);
        }

        var first = (await App.CallNextAsync(App.TreasuryOperator, App.TreasuryId)).Value;
        await App.ActAsync(App.TreasuryOperator, first, EventKind.Completed);
        await App.CallNextAsync(App.TreasuryOperator, App.TreasuryId);

        // Act
        var result = await App.InScopeAsync(sp => sp.GetRequiredService<QueueQueries>().AttendanceAsync(App.TreasuryOperator, App.TreasuryId));

        // Assert
        result.Value.Active!.Code.ShouldBe("T-002");
        result.Value.Pending.Select(x => x.Code).ShouldBe(["T-003"]);
        result.Value.Recent.Select(x => x.Code).ShouldBe(["T-001"]);
    }
}
