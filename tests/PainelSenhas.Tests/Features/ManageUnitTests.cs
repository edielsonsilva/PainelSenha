using PainelSenhas.Tests.Support;
using PainelSenhas.Web.Common;
using PainelSenhas.Web.Data;
using PainelSenhas.Web.Features.Gestao;
using Shouldly;

namespace PainelSenhas.Tests.Features;

public sealed class ManageUnitTests(AppFixture app) : AppTestBase(app)
{
    [Fact]
    public async Task SaveDepartment_NewDepartment_CreatesItWithItsDesk()
    {
        // Act
        var result = await SaveAsync(App.Admin, new DepartmentInput(0, "  Secretaria ", "s", "Guichê 05", true));

        // Assert
        result.IsSuccess.ShouldBeTrue();
        await using var db = await App.CreateDbContextAsync();
        var department = await db.Departments.SingleAsync(x => x.Id == result.Value);
        (department.Name, department.Prefix).ShouldBe(("Secretaria", "S"));
        (await db.Desks.SingleAsync(x => x.DepartmentId == department.Id)).Name.ShouldBe("Guichê 05");

        await CleanUpAsync(department.Id);
    }

    [Fact]
    public async Task SaveDepartment_InvalidPrefix_ReturnsValidationError()
    {
        // Act
        var result = await SaveAsync(App.Admin, new DepartmentInput(0, "Secretaria", "123", "Guichê 05", true));

        // Assert
        result.Error.Type.ShouldBe(ErrorType.Validation);
        result.Error.Message.ShouldContain("prefixo");
    }

    [Fact]
    public async Task SaveDepartment_DuplicatePrefix_ReturnsConflict()
    {
        // Act
        var result = await SaveAsync(App.Admin, new DepartmentInput(0, "Outra Tesouraria", "T", "Guichê 09", true));

        // Assert
        result.Error.Type.ShouldBe(ErrorType.Conflict);
    }

    [Fact]
    public async Task SaveDepartment_NonAdmin_ReturnsUnauthorized()
    {
        // Act
        var result = await SaveAsync(App.Reception, new DepartmentInput(0, "Secretaria", "S", "Guichê 05", true));

        // Assert
        result.Error.Type.ShouldBe(ErrorType.Unauthorized);
    }

    [Fact]
    public async Task SaveDepartment_DeactivateWithPendingTickets_ReturnsConflict()
    {
        // Arrange
        await App.IssueAsync(App.Reception, App.PatrimonyId);

        // Act
        var result = await SaveAsync(App.Admin, new DepartmentInput(App.PatrimonyId, "Patrimônio", "P", "Guichê 02", false));

        // Assert
        result.Error.Type.ShouldBe(ErrorType.Conflict);
    }

    [Fact]
    public async Task SaveDepartment_UnknownId_ReturnsNotFound()
    {
        // Act
        var result = await SaveAsync(App.Admin, new DepartmentInput(9999, "Inexistente", "Q", "Guichê 99", true));

        // Assert
        result.Error.Type.ShouldBe(ErrorType.NotFound);
    }

    [Fact]
    public async Task CreateUser_OperatorWithoutDepartment_ReturnsValidationError()
    {
        // Act
        var result = await CreateUserAsync(App.Admin, new NewUserInput("novo.operador", AppFixture.TestPassword, Roles.Operator, []));

        // Assert
        result.Error.Type.ShouldBe(ErrorType.Validation);
    }

    [Fact]
    public async Task CreateUser_WeakPassword_ReturnsValidationErrorInPortuguese()
    {
        // Act
        var result = await CreateUserAsync(App.Admin, new NewUserInput("fraca", "abc", Roles.Reception, []));

        // Assert
        result.Error.Type.ShouldBe(ErrorType.Validation);
        result.Error.Message.ShouldContain("senha");
    }

    [Fact]
    public async Task CreateUser_AdminRoleByAdmin_CreatesAdministratorWithoutDepartments()
    {
        // Act
        var result = await CreateUserAsync(App.Admin, new NewUserInput("novo.admin", AppFixture.TestPassword, Roles.Admin, [App.TreasuryId]));

        // Assert
        result.IsSuccess.ShouldBeTrue();
        await using var db = await App.CreateDbContextAsync();
        var created = await db.Users.SingleAsync(x => x.Id == result.Value);
        (created.AccessRole, created.Active).ShouldBe((Roles.Admin, true));
        (await db.UserDepartments.AnyAsync(x => x.UserId == created.Id)).ShouldBeFalse();
    }

    [Fact]
    public async Task CreateUser_AdminRoleByAdmin_NewAdministratorCanManageTheTeam()
    {
        // Arrange
        await CreateUserAsync(App.Admin, new NewUserInput("admin.gestor", AppFixture.TestPassword, Roles.Admin, []));
        var newAdmin = await App.PrincipalOfAsync("admin.gestor");

        // Act
        var users = await App.InScopeAsync(sp => sp.GetRequiredService<ManageUnit>().UsersAsync(newAdmin));

        // Assert
        users.IsSuccess.ShouldBeTrue();
        users.Value.ShouldContain(x => x.UserName == "admin.gestor" && x.Role == Roles.Admin);
    }

    [Fact]
    public async Task CreateUser_AdminRoleWithWeakPassword_ReturnsValidationError()
    {
        // Act
        var result = await CreateUserAsync(App.Admin, new NewUserInput("admin.fraco", "curta", Roles.Admin, []));

        // Assert
        result.Error.Type.ShouldBe(ErrorType.Validation);
        await using var db = await App.CreateDbContextAsync();
        (await db.Users.AnyAsync(x => x.UserName == "admin.fraco")).ShouldBeFalse();
    }

    [Theory]
    [InlineData("reception")]
    [InlineData("operator")]
    public async Task CreateUser_AdminRoleByNonAdmin_ReturnsUnauthorized(string actor)
    {
        // Arrange
        var principal = actor == "reception" ? App.Reception : App.TreasuryOperator;

        // Act
        var result = await CreateUserAsync(principal, new NewUserInput($"admin.{actor}", AppFixture.TestPassword, Roles.Admin, []));

        // Assert
        result.Error.Type.ShouldBe(ErrorType.Unauthorized);
        await using var db = await App.CreateDbContextAsync();
        (await db.Users.AnyAsync(x => x.UserName == $"admin.{actor}")).ShouldBeFalse();
    }

    [Fact]
    public async Task CreateUser_UnknownRole_ReturnsValidationError()
    {
        // Act
        var result = await CreateUserAsync(App.Admin, new NewUserInput("perfil.invalido", AppFixture.TestPassword, "Superuser", []));

        // Assert
        result.Error.Type.ShouldBe(ErrorType.Validation);
    }

    [Fact]
    public async Task CreateUser_ByOperator_ReturnsUnauthorized()
    {
        // Act
        var result = await CreateUserAsync(App.TreasuryOperator, new NewUserInput("x.user", AppFixture.TestPassword, Roles.Reception, []));

        // Assert
        result.Error.Type.ShouldBe(ErrorType.Unauthorized);
    }

    [Fact]
    public async Task SetUserActive_Deactivate_RevokesAccessImmediately()
    {
        // Arrange
        var principal = await App.CreateUserAsync("temporario", Roles.Reception);
        var userId = principal.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)!.Value;

        // Act
        var result = await App.InScopeAsync(sp => sp.GetRequiredService<ManageUnit>().SetUserActiveAsync(App.Admin, userId, false));
        var issue = await App.IssueAsync(principal, App.TreasuryId);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        issue.Error.Type.ShouldBe(ErrorType.Unauthorized);
    }

    [Fact]
    public async Task SetUserActive_OwnAdminAccount_ReturnsConflictAndKeepsLastAdminActive()
    {
        // Arrange
        var adminId = App.Admin.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)!.Value;

        // Act
        var result = await App.InScopeAsync(sp => sp.GetRequiredService<ManageUnit>().SetUserActiveAsync(App.Admin, adminId, false));

        // Assert
        result.Error.Type.ShouldBe(ErrorType.Conflict);
        await using var db = await App.CreateDbContextAsync();
        (await db.Users.CountAsync(x => x.AccessRole == Roles.Admin && x.Active)).ShouldBeGreaterThanOrEqualTo(1);
        (await db.Users.SingleAsync(x => x.Id == adminId)).Active.ShouldBeTrue();
    }

    [Fact]
    public async Task SetUserActive_AnotherAdmin_DeactivatesItAndRevokesAccess()
    {
        // Arrange
        var created = await CreateUserAsync(App.Admin, new NewUserInput("admin.saindo", AppFixture.TestPassword, Roles.Admin, []));
        var leaving = await App.PrincipalOfAsync("admin.saindo");

        // Act
        var result = await App.InScopeAsync(sp => sp.GetRequiredService<ManageUnit>().SetUserActiveAsync(App.Admin, created.Value, false));
        var afterwards = await App.InScopeAsync(sp => sp.GetRequiredService<ManageUnit>().UsersAsync(leaving));

        // Assert
        result.IsSuccess.ShouldBeTrue();
        afterwards.Error.Type.ShouldBe(ErrorType.Unauthorized);
    }

    [Fact]
    public async Task SetUserActive_DeactivatedAdminTriesToRemoveTheOtherAdmin_ReturnsUnauthorized()
    {
        // Arrange
        var created = await CreateUserAsync(App.Admin, new NewUserInput("admin.rival", AppFixture.TestPassword, Roles.Admin, []));
        var rival = await App.PrincipalOfAsync("admin.rival");
        var adminId = App.Admin.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)!.Value;
        await App.InScopeAsync(sp => sp.GetRequiredService<ManageUnit>().SetUserActiveAsync(App.Admin, created.Value, false));

        // Act
        var result = await App.InScopeAsync(sp => sp.GetRequiredService<ManageUnit>().SetUserActiveAsync(rival, adminId, false));

        // Assert
        result.Error.Type.ShouldBe(ErrorType.Unauthorized);
        await using var db = await App.CreateDbContextAsync();
        (await db.Users.SingleAsync(x => x.Id == adminId)).Active.ShouldBeTrue();
    }

    [Fact]
    public async Task ResetPassword_ForReceptionUser_ReplacesPassword()
    {
        // Arrange
        await App.CreateUserAsync("trocar.senha", Roles.Reception);
        await using var db = await App.CreateDbContextAsync();
        var userId = await db.Users.Where(x => x.UserName == "trocar.senha").Select(x => x.Id).SingleAsync();

        // Act
        var result = await App.InScopeAsync(sp =>
            sp.GetRequiredService<ManageUnit>().ResetPasswordAsync(App.Admin, userId, "NovaSenha#2026y"));

        // Assert
        result.IsSuccess.ShouldBeTrue();
        using var scope = App.Services.CreateScope();
        var users = scope.ServiceProvider.GetRequiredService<Microsoft.AspNetCore.Identity.UserManager<AppUser>>();
        var user = await users.FindByIdAsync(userId);
        (await users.CheckPasswordAsync(user!, "NovaSenha#2026y")).ShouldBeTrue();
    }

    [Fact]
    public async Task ResetPassword_ForAdminAccount_ReturnsConflict()
    {
        // Arrange
        var adminId = App.Admin.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)!.Value;

        // Act
        var result = await App.InScopeAsync(sp =>
            sp.GetRequiredService<ManageUnit>().ResetPasswordAsync(App.Admin, adminId, "NovaSenha#2026y"));

        // Assert
        result.Error.Type.ShouldBe(ErrorType.Conflict);
    }

    private Task<Result<int>> SaveAsync(System.Security.Claims.ClaimsPrincipal who, DepartmentInput input) =>
        App.InScopeAsync(sp => sp.GetRequiredService<ManageUnit>().SaveDepartmentAsync(who, input));

    private Task<Result<string>> CreateUserAsync(System.Security.Claims.ClaimsPrincipal who, NewUserInput input) =>
        App.InScopeAsync(sp => sp.GetRequiredService<ManageUnit>().CreateUserAsync(who, input));

    // Departamentos criados por um teste são desativados para não alterar a lista dos demais
    private async Task CleanUpAsync(int departmentId)
    {
        await SaveAsync(App.Admin, new DepartmentInput(departmentId, "Secretaria", "S", "Guichê 05", false));
    }
}
