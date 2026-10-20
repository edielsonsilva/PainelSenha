using Microsoft.AspNetCore.Identity;

namespace PainelSenhas.Web.Common;

/// <summary>Mensagens do ASP.NET Identity em português para os erros que o usuário pode ver (senha e nome de usuário).</summary>
internal sealed class PortugueseIdentityErrorDescriber : IdentityErrorDescriber
{
    public override IdentityError PasswordTooShort(int length) =>
        new() { Code = nameof(PasswordTooShort), Description = $"A senha deve ter pelo menos {length} caracteres." };

    public override IdentityError PasswordRequiresUpper() =>
        new() { Code = nameof(PasswordRequiresUpper), Description = "A senha deve ter ao menos uma letra maiúscula." };

    public override IdentityError PasswordRequiresLower() =>
        new() { Code = nameof(PasswordRequiresLower), Description = "A senha deve ter ao menos uma letra minúscula." };

    public override IdentityError PasswordRequiresDigit() =>
        new() { Code = nameof(PasswordRequiresDigit), Description = "A senha deve ter ao menos um número." };

    public override IdentityError PasswordRequiresNonAlphanumeric() =>
        new() { Code = nameof(PasswordRequiresNonAlphanumeric), Description = "A senha deve ter ao menos um símbolo." };

    public override IdentityError PasswordMismatch() =>
        new() { Code = nameof(PasswordMismatch), Description = "A senha atual está incorreta." };

    public override IdentityError DuplicateUserName(string userName) =>
        new() { Code = nameof(DuplicateUserName), Description = "Este usuário já existe." };

    public override IdentityError InvalidUserName(string? userName) =>
        new() { Code = nameof(InvalidUserName), Description = "O usuário contém caracteres não permitidos." };
}
