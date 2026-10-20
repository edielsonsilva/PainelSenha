using System.Security.Claims;
using PainelSenhas.Tests.Support;
using PainelSenhas.Web.Common;
using PainelSenhas.Web.Data;
using PainelSenhas.Web.Features.Gestao;
using PainelSenhas.Web.Features.Painel;
using Shouldly;

namespace PainelSenhas.Tests.Features;

public sealed class UserDepartmentsTests(AppFixture app) : AppTestBase(app)
{
    [Fact]
    public async Task CallNext_OperatorWithTwoDepartments_AttendsBothButNotAThird()
    {
        // Arrange
        var operatorOfTwo = await App.CreateUserAsync("op.dois", Roles.Operator, App.TreasuryId, App.PatrimonyId);
        var thirdId = await DepartmentIdAsync("V");
        await App.IssueAsync(App.Reception, App.TreasuryId);
        await App.IssueAsync(App.Reception, App.PatrimonyId);
        await App.IssueAsync(App.Reception, thirdId);

        // Act
        var treasury = await App.CallNextAsync(operatorOfTwo, App.TreasuryId);
        var patrimony = await App.CallNextAsync(operatorOfTwo, App.PatrimonyId);
        var third = await App.CallNextAsync(operatorOfTwo, thirdId);

        // Assert
        treasury.IsSuccess.ShouldBeTrue();
        patrimony.IsSuccess.ShouldBeTrue();
        third.Error.Type.ShouldBe(ErrorType.Unauthorized);
    }

    [Fact]
    public async Task PermittedDepartments_Admin_ListsEveryActiveDepartment()
    {
        // Act
        var result = await PermittedAsync(App.Admin);

        // Assert
        result.Value.Select(x => x.Prefix).ShouldBe(["T", "P", "V", "M"], ignoreOrder: true);
    }

    [Fact]
    public async Task PermittedDepartments_Operator_ListsOnlyLinkedDepartmentsInRegistrationOrder()
    {
        // Arrange
        var operatorOfTwo = await App.CreateUserAsync("op.lista", Roles.Operator, App.PatrimonyId, App.TreasuryId);

        // Act
        var two = await PermittedAsync(operatorOfTwo);
        var one = await PermittedAsync(App.TreasuryOperator);

        // Assert
        two.Value.Select(x => x.Prefix).ShouldBe(["T", "P"]);
        one.Value.Select(x => x.Prefix).ShouldBe(["T"]);
    }

    [Fact]
    public async Task PermittedDepartments_Reception_ReturnsUnauthorized()
    {
        // Act
        var result = await PermittedAsync(App.Reception);

        // Assert
        result.Error.Type.ShouldBe(ErrorType.Unauthorized);
    }

    [Fact]
    public async Task SetUserDepartments_GrantsAndRevokesAccess()
    {
        // Arrange
        var principal = await App.CreateUserAsync("op.muda", Roles.Operator, App.TreasuryId);
        var userId = principal.FindFirst(ClaimTypes.NameIdentifier)!.Value;
        await App.IssueAsync(App.Reception, App.PatrimonyId);

        // Act
        var grant = await SetAsync(App.Admin, userId, [App.TreasuryId, App.PatrimonyId]);
        var canAttendNow = await App.CallNextAsync(principal, App.PatrimonyId);
        var revoke = await SetAsync(App.Admin, userId, [App.TreasuryId]);
        var cannotAnymore = await App.CallNextAsync(principal, App.PatrimonyId);

        // Assert
        grant.IsSuccess.ShouldBeTrue();
        canAttendNow.IsSuccess.ShouldBeTrue();
        revoke.IsSuccess.ShouldBeTrue();
        cannotAnymore.Error.Type.ShouldBe(ErrorType.Unauthorized);
    }

    [Fact]
    public async Task SetUserDepartments_EmptyList_ReturnsValidationError()
    {
        // Arrange
        var principal = await App.CreateUserAsync("op.vazio", Roles.Operator, App.TreasuryId);
        var userId = principal.FindFirst(ClaimTypes.NameIdentifier)!.Value;

        // Act
        var result = await SetAsync(App.Admin, userId, []);

        // Assert
        result.Error.Type.ShouldBe(ErrorType.Validation);
    }

    [Fact]
    public async Task SetUserDepartments_ReceptionUser_ReturnsConflict()
    {
        // Act
        var result = await SetAsync(App.Admin, UserId(App.Reception), [App.TreasuryId]);

        // Assert
        result.Error.Type.ShouldBe(ErrorType.Conflict);
    }

    [Fact]
    public async Task SetUserDepartments_NonAdmin_ReturnsUnauthorized()
    {
        // Act
        var result = await SetAsync(App.TreasuryOperator, UserId(App.TreasuryOperator), [App.TreasuryId, App.PatrimonyId]);

        // Assert
        result.Error.Type.ShouldBe(ErrorType.Unauthorized);
    }

    [Fact]
    public async Task SetUserDepartments_UnknownDepartment_ReturnsValidationError()
    {
        // Arrange
        var principal = await App.CreateUserAsync("op.desconhecido", Roles.Operator, App.TreasuryId);

        // Act
        var result = await SetAsync(App.Admin, UserId(principal), [9999]);

        // Assert
        result.Error.Type.ShouldBe(ErrorType.Validation);
    }

    [Fact]
    public async Task CreateUser_OperatorWithDepartments_LinksThemAndListsThem()
    {
        // Act
        var created = await App.InScopeAsync(sp => sp.GetRequiredService<ManageUnit>().CreateUserAsync(
            App.Admin, new NewUserInput("novo.op", AppFixture.TestPassword, Roles.Operator, [App.TreasuryId, App.PatrimonyId])));
        var users = await App.InScopeAsync(sp => sp.GetRequiredService<ManageUnit>().UsersAsync(App.Admin));

        // Assert
        created.IsSuccess.ShouldBeTrue();
        users.Value.Single(x => x.UserName == "novo.op").DepartmentIds.ShouldBe([App.TreasuryId, App.PatrimonyId], ignoreOrder: true);
    }

    [Fact]
    public async Task CreateUser_OperatorWithUnknownDepartment_ReturnsValidationErrorAndCreatesNothing()
    {
        // Act
        var result = await App.InScopeAsync(sp => sp.GetRequiredService<ManageUnit>().CreateUserAsync(
            App.Admin, new NewUserInput("op.invalido", AppFixture.TestPassword, Roles.Operator, [9999])));

        // Assert
        result.Error.Type.ShouldBe(ErrorType.Validation);
        await using var db = await App.CreateDbContextAsync();
        (await db.Users.AnyAsync(x => x.UserName == "op.invalido")).ShouldBeFalse();
    }

    private Task<Result<IReadOnlyList<DepartmentView>>> PermittedAsync(ClaimsPrincipal who) =>
        App.InScopeAsync(sp => sp.GetRequiredService<QueueQueries>().PermittedDepartmentsAsync(who));

    private Task<Result<bool>> SetAsync(ClaimsPrincipal who, string userId, IReadOnlyList<int> departmentIds) =>
        App.InScopeAsync(sp => sp.GetRequiredService<ManageUnit>().SetUserDepartmentsAsync(who, userId, departmentIds));

    private static string UserId(ClaimsPrincipal principal) => principal.FindFirst(ClaimTypes.NameIdentifier)!.Value;

    private async Task<int> DepartmentIdAsync(string prefix)
    {
        await using var db = await App.CreateDbContextAsync();
        return await db.Departments.Where(x => x.Prefix == prefix).Select(x => x.Id).SingleAsync();
    }
}
