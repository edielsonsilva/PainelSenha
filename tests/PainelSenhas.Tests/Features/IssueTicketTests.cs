using PainelSenhas.Tests.Support;
using PainelSenhas.Web.Common;
using Shouldly;

namespace PainelSenhas.Tests.Features;

public sealed class IssueTicketTests(AppFixture app) : AppTestBase(app)
{
    [Fact]
    public async Task Issue_ConcurrentRequests_AssignsUniqueSequentialNumbers()
    {
        // Arrange
        const int requests = 20;

        // Act
        var results = await Task.WhenAll(Enumerable.Range(0, requests).Select(_ => App.IssueAsync(App.Reception, App.TreasuryId)));

        // Assert
        results.ShouldAllBe(x => x.IsSuccess);
        results.Select(x => x.Value.Code).Distinct().Count().ShouldBe(requests);
        results.Select(x => x.Value.Code).Order().ShouldBe(Enumerable.Range(1, requests).Select(n => $"T-{n:000}").Order());
    }

    [Fact]
    public async Task Issue_SameKeyTwice_ReturnsSameTicket()
    {
        // Arrange
        var key = Guid.NewGuid();
        var first = await App.IssueAsync(App.Reception, App.TreasuryId, key);

        // Act
        var second = await App.IssueAsync(App.Reception, App.TreasuryId, key);

        // Assert
        second.IsSuccess.ShouldBeTrue();
        second.Value.Id.ShouldBe(first.Value.Id);
        (await App.IssueAsync(App.Reception, App.TreasuryId)).Value.Code.ShouldBe("T-002");
    }

    [Fact]
    public async Task Issue_KeyUsedForAnotherDepartment_ReturnsConflict()
    {
        // Arrange
        var key = Guid.NewGuid();
        await App.IssueAsync(App.Reception, App.TreasuryId, key);

        // Act
        var result = await App.IssueAsync(App.Reception, App.PatrimonyId, key);

        // Assert
        result.IsFailure.ShouldBeTrue();
        result.Error.Type.ShouldBe(ErrorType.Conflict);
    }

    [Fact]
    public async Task Issue_EmptyKey_ReturnsValidationError()
    {
        // Act
        var result = await App.IssueAsync(App.Reception, App.TreasuryId, Guid.Empty);

        // Assert
        result.Error.Type.ShouldBe(ErrorType.Validation);
    }

    [Fact]
    public async Task Issue_OperatorRole_ReturnsUnauthorized()
    {
        // Act
        var result = await App.IssueAsync(App.TreasuryOperator, App.TreasuryId);

        // Assert
        result.IsFailure.ShouldBeTrue();
        result.Error.Type.ShouldBe(ErrorType.Unauthorized);
    }

    [Fact]
    public async Task Issue_AnonymousUser_ReturnsUnauthorized()
    {
        // Act
        var result = await App.IssueAsync(new System.Security.Claims.ClaimsPrincipal(), App.TreasuryId);

        // Assert
        result.Error.Type.ShouldBe(ErrorType.Unauthorized);
    }

    [Fact]
    public async Task Issue_UnknownDepartment_ReturnsNotFound()
    {
        // Act
        var result = await App.IssueAsync(App.Reception, departmentId: 9999);

        // Assert
        result.Error.Type.ShouldBe(ErrorType.NotFound);
    }

    [Fact]
    public async Task Issue_EachDepartmentHasItsOwnSequence()
    {
        // Act
        var treasury = await App.IssueAsync(App.Reception, App.TreasuryId);
        var patrimony = await App.IssueAsync(App.Reception, App.PatrimonyId);

        // Assert
        treasury.Value.Code.ShouldBe("T-001");
        patrimony.Value.Code.ShouldBe("P-001");
    }

    [Fact]
    public async Task Issue_Preferential_MarksTicketAndSharesTheDepartmentNumbering()
    {
        // Arrange
        await App.IssueAsync(App.Reception, App.TreasuryId);

        // Act
        var result = await App.IssueAsync(App.Reception, App.TreasuryId, preferential: true);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        result.Value.IsPreferential.ShouldBeTrue();
        result.Value.Code.ShouldBe("T-002");
    }

    [Fact]
    public async Task Issue_Default_IsNotPreferential()
    {
        // Act
        var result = await App.IssueAsync(App.Reception, App.TreasuryId);

        // Assert
        result.Value.IsPreferential.ShouldBeFalse();
    }

    [Fact]
    public async Task Issue_SameKeyWithDifferentPriority_ReturnsConflict()
    {
        // Arrange
        var key = Guid.NewGuid();
        await App.IssueAsync(App.Reception, App.TreasuryId, key);

        // Act
        var result = await App.IssueAsync(App.Reception, App.TreasuryId, key, preferential: true);

        // Assert
        result.Error.Type.ShouldBe(ErrorType.Conflict);
    }

    [Fact]
    public async Task Issue_NextBusinessDay_RestartsNumbering()
    {
        // Arrange
        await App.IssueAsync(App.Reception, App.TreasuryId);
        await App.IssueAsync(App.Reception, App.TreasuryId);
        App.AdvanceToNextBusinessDay();

        // Act
        var result = await App.IssueAsync(App.Reception, App.TreasuryId);

        // Assert
        result.Value.Code.ShouldBe("T-001");
    }
}
