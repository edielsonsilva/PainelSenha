using PainelSenhas.Tests.Support;
using PainelSenhas.Web.Common;
using PainelSenhas.Web.Data;
using Shouldly;

namespace PainelSenhas.Tests.Features;

public sealed class AttendQueueTests(AppFixture app) : AppTestBase(app)
{
    [Fact]
    public async Task CallNext_PendingTickets_CallsOldestFirst()
    {
        // Arrange
        var first = await App.IssueAsync(App.Reception, App.TreasuryId);
        await App.IssueAsync(App.Reception, App.TreasuryId);

        // Act
        var result = await App.CallNextAsync(App.TreasuryOperator, App.TreasuryId);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        result.Value.ShouldBe(first.Value.Id);
    }

    [Fact]
    public async Task CallNext_PendingFromPreviousDay_IsCalledBeforeTodaysTickets()
    {
        // Arrange
        var yesterday = await App.IssueAsync(App.Reception, App.TreasuryId);
        App.AdvanceToNextBusinessDay();
        await App.IssueAsync(App.Reception, App.TreasuryId);

        // Act
        var result = await App.CallNextAsync(App.TreasuryOperator, App.TreasuryId);

        // Assert
        result.Value.ShouldBe(yesterday.Value.Id);
    }

    [Fact]
    public async Task CallNext_PreferentialIssuedLater_IsCalledBeforeOlderCommonTickets()
    {
        // Arrange
        await App.IssueAsync(App.Reception, App.TreasuryId);
        await App.IssueAsync(App.Reception, App.TreasuryId);
        var preferential = await App.IssueAsync(App.Reception, App.TreasuryId, preferential: true);

        // Act
        var result = await App.CallNextAsync(App.TreasuryOperator, App.TreasuryId);

        // Assert
        result.Value.ShouldBe(preferential.Value.Id);
    }

    [Fact]
    public async Task CallNext_SeveralPreferentials_AreCalledInIssueOrderBeforeAnyCommon()
    {
        // Arrange
        var common = await App.IssueAsync(App.Reception, App.TreasuryId);
        var firstPreferential = await App.IssueAsync(App.Reception, App.TreasuryId, preferential: true);
        var secondPreferential = await App.IssueAsync(App.Reception, App.TreasuryId, preferential: true);

        // Act
        var calls = new List<long>();
        for (var i = 0; i < 3; i++)
        {
            var called = (await App.CallNextAsync(App.TreasuryOperator, App.TreasuryId)).Value;
            calls.Add(called);
            await App.ActAsync(App.TreasuryOperator, called, EventKind.Completed);
        }

        // Assert
        calls.ShouldBe([firstPreferential.Value.Id, secondPreferential.Value.Id, common.Value.Id]);
    }

    [Fact]
    public async Task CallNext_PreferentialOfAnotherDepartment_DoesNotJumpTheQueue()
    {
        // Arrange
        var common = await App.IssueAsync(App.Reception, App.TreasuryId);
        await App.IssueAsync(App.Reception, App.PatrimonyId, preferential: true);

        // Act
        var result = await App.CallNextAsync(App.TreasuryOperator, App.TreasuryId);

        // Assert
        result.Value.ShouldBe(common.Value.Id);
    }

    [Fact]
    public async Task CallNext_PreferentialToday_IsCalledBeforeCommonFromPreviousDay()
    {
        // Arrange
        await App.IssueAsync(App.Reception, App.TreasuryId);
        App.AdvanceToNextBusinessDay();
        var preferential = await App.IssueAsync(App.Reception, App.TreasuryId, preferential: true);

        // Act
        var result = await App.CallNextAsync(App.TreasuryOperator, App.TreasuryId);

        // Assert
        result.Value.ShouldBe(preferential.Value.Id);
    }

    [Fact]
    public async Task CallNext_DeskAlreadyBusy_ReturnsConflict()
    {
        // Arrange
        await App.IssueAsync(App.Reception, App.TreasuryId);
        await App.IssueAsync(App.Reception, App.TreasuryId);
        await App.CallNextAsync(App.TreasuryOperator, App.TreasuryId);

        // Act
        var result = await App.CallNextAsync(App.TreasuryOperator, App.TreasuryId);

        // Assert
        result.IsFailure.ShouldBeTrue();
        result.Error.Type.ShouldBe(ErrorType.Conflict);
    }

    [Fact]
    public async Task CallNext_ConcurrentCallsOnSameDesk_OnlyOneSucceeds()
    {
        // Arrange
        await App.IssueAsync(App.Reception, App.TreasuryId);
        await App.IssueAsync(App.Reception, App.TreasuryId);
        await App.IssueAsync(App.Reception, App.TreasuryId);

        // Act
        var results = await Task.WhenAll(Enumerable.Range(0, 5).Select(_ => App.CallNextAsync(App.TreasuryOperator, App.TreasuryId)));

        // Assert
        results.Count(x => x.IsSuccess).ShouldBe(1);
        await using var db = await App.CreateDbContextAsync();
        (await db.Tickets.CountAsync(x => x.Status == TicketStatus.Called)).ShouldBe(1);
    }

    [Fact]
    public async Task CallNext_SameKeyTwice_ReturnsSameTicketWithoutCallingAnother()
    {
        // Arrange
        await App.IssueAsync(App.Reception, App.TreasuryId);
        await App.IssueAsync(App.Reception, App.TreasuryId);
        var key = Guid.NewGuid();
        var first = await App.CallNextAsync(App.TreasuryOperator, App.TreasuryId, key);

        // Act
        var second = await App.CallNextAsync(App.TreasuryOperator, App.TreasuryId, key);

        // Assert
        second.IsSuccess.ShouldBeTrue();
        second.Value.ShouldBe(first.Value);
        await using var db = await App.CreateDbContextAsync();
        (await db.Tickets.CountAsync(x => x.Status == TicketStatus.Called)).ShouldBe(1);
    }

    [Fact]
    public async Task CallNext_EmptyQueue_ReturnsNotFound()
    {
        // Act
        var result = await App.CallNextAsync(App.TreasuryOperator, App.TreasuryId);

        // Assert
        result.Error.Type.ShouldBe(ErrorType.NotFound);
    }

    [Fact]
    public async Task CallNext_OperatorOfAnotherDepartment_ReturnsUnauthorized()
    {
        // Arrange
        await App.IssueAsync(App.Reception, App.TreasuryId);

        // Act
        var result = await App.CallNextAsync(App.PatrimonyOperator, App.TreasuryId);

        // Assert
        result.Error.Type.ShouldBe(ErrorType.Unauthorized);
    }

    [Fact]
    public async Task CallNext_ReceptionRole_ReturnsUnauthorized()
    {
        // Arrange
        await App.IssueAsync(App.Reception, App.TreasuryId);

        // Act
        var result = await App.CallNextAsync(App.Reception, App.TreasuryId);

        // Assert
        result.Error.Type.ShouldBe(ErrorType.Unauthorized);
    }

    [Fact]
    public async Task CallNext_AdminCanAttendAnyDepartment()
    {
        // Arrange
        await App.IssueAsync(App.Reception, App.PatrimonyId);

        // Act
        var result = await App.CallNextAsync(App.Admin, App.PatrimonyId);

        // Assert
        result.IsSuccess.ShouldBeTrue();
    }

    [Fact]
    public async Task CallNext_OtherDepartmentsKeepWorkingWhileOneDeskIsBusy()
    {
        // Arrange
        await App.IssueAsync(App.Reception, App.TreasuryId);
        await App.IssueAsync(App.Reception, App.PatrimonyId);
        await App.CallNextAsync(App.TreasuryOperator, App.TreasuryId);

        // Act
        var result = await App.CallNextAsync(App.PatrimonyOperator, App.PatrimonyId);

        // Assert
        result.IsSuccess.ShouldBeTrue();
    }

    [Fact]
    public async Task Act_Complete_ClosesTicketAndFreesTheDesk()
    {
        // Arrange
        await App.IssueAsync(App.Reception, App.TreasuryId);
        await App.IssueAsync(App.Reception, App.TreasuryId);
        var called = (await App.CallNextAsync(App.TreasuryOperator, App.TreasuryId)).Value;

        // Act
        var completed = await App.ActAsync(App.TreasuryOperator, called, EventKind.Completed);
        var next = await App.CallNextAsync(App.TreasuryOperator, App.TreasuryId);

        // Assert
        completed.IsSuccess.ShouldBeTrue();
        next.IsSuccess.ShouldBeTrue();
        next.Value.ShouldNotBe(called);
        await using var db = await App.CreateDbContextAsync();
        var ticket = await db.Tickets.SingleAsync(x => x.Id == called);
        ticket.Status.ShouldBe(TicketStatus.Completed);
        ticket.ClosedAtUtc.ShouldNotBeNull();
    }

    [Fact]
    public async Task Act_Absent_MarksTicketAsAbsent()
    {
        // Arrange
        await App.IssueAsync(App.Reception, App.TreasuryId);
        var called = (await App.CallNextAsync(App.TreasuryOperator, App.TreasuryId)).Value;

        // Act
        await App.ActAsync(App.TreasuryOperator, called, EventKind.Absent);

        // Assert
        await using var db = await App.CreateDbContextAsync();
        (await db.Tickets.SingleAsync(x => x.Id == called)).Status.ShouldBe(TicketStatus.Absent);
    }

    [Fact]
    public async Task Act_CompleteTwiceWithDifferentKeys_ReturnsConflict()
    {
        // Arrange
        await App.IssueAsync(App.Reception, App.TreasuryId);
        var called = (await App.CallNextAsync(App.TreasuryOperator, App.TreasuryId)).Value;
        await App.ActAsync(App.TreasuryOperator, called, EventKind.Completed);

        // Act
        var second = await App.ActAsync(App.TreasuryOperator, called, EventKind.Completed);

        // Assert
        second.IsFailure.ShouldBeTrue();
        second.Error.Type.ShouldBe(ErrorType.Conflict);
    }

    [Fact]
    public async Task Act_CompleteTwiceWithSameKey_IsIdempotentAndRecordsOneEvent()
    {
        // Arrange
        await App.IssueAsync(App.Reception, App.TreasuryId);
        var called = (await App.CallNextAsync(App.TreasuryOperator, App.TreasuryId)).Value;
        var key = Guid.NewGuid();
        await App.ActAsync(App.TreasuryOperator, called, EventKind.Completed, key);

        // Act
        var second = await App.ActAsync(App.TreasuryOperator, called, EventKind.Completed, key);

        // Assert
        second.IsSuccess.ShouldBeTrue();
        await using var db = await App.CreateDbContextAsync();
        (await db.Events.CountAsync(x => x.TicketId == called && x.Kind == EventKind.Completed)).ShouldBe(1);
    }

    [Fact]
    public async Task Act_Repeat_KeepsTicketCalledAndRecordsAnotherEvent()
    {
        // Arrange
        await App.IssueAsync(App.Reception, App.TreasuryId);
        var called = (await App.CallNextAsync(App.TreasuryOperator, App.TreasuryId)).Value;

        // Act
        var result = await App.ActAsync(App.TreasuryOperator, called, EventKind.Repeated);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        await using var db = await App.CreateDbContextAsync();
        (await db.Tickets.SingleAsync(x => x.Id == called)).Status.ShouldBe(TicketStatus.Called);
        (await db.Events.CountAsync(x => x.TicketId == called && x.Kind == EventKind.Repeated)).ShouldBe(1);
    }

    [Fact]
    public async Task Act_OperatorOfAnotherDepartment_ReturnsUnauthorized()
    {
        // Arrange
        await App.IssueAsync(App.Reception, App.TreasuryId);
        var called = (await App.CallNextAsync(App.TreasuryOperator, App.TreasuryId)).Value;

        // Act
        var result = await App.ActAsync(App.PatrimonyOperator, called, EventKind.Completed);

        // Assert
        result.Error.Type.ShouldBe(ErrorType.Unauthorized);
    }

    [Fact]
    public async Task Act_InvalidKind_ReturnsValidationError()
    {
        // Arrange
        await App.IssueAsync(App.Reception, App.TreasuryId);
        var called = (await App.CallNextAsync(App.TreasuryOperator, App.TreasuryId)).Value;

        // Act
        var result = await App.ActAsync(App.TreasuryOperator, called, EventKind.Issued);

        // Assert
        result.Error.Type.ShouldBe(ErrorType.Validation);
    }

    [Fact]
    public async Task Act_UnknownTicket_ReturnsNotFound()
    {
        // Act
        var result = await App.ActAsync(App.TreasuryOperator, ticketId: 424242, EventKind.Completed);

        // Assert
        result.Error.Type.ShouldBe(ErrorType.NotFound);
    }
}
