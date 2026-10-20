using FluentValidation;
using PainelSenhas.Web.Data;

namespace PainelSenhas.Web.Features.Gestao;

/// <summary>Valida a criação de acesso: usuário com caracteres permitidos, perfil administrador/operador/recepção e departamentos para o operador.</summary>
internal sealed class NewUserInputValidator : AbstractValidator<NewUserInput>
{
    public NewUserInputValidator()
    {
        RuleFor(x => x.UserName).Matches("^[A-Za-z0-9._@+-]{3,100}$")
            .WithMessage("O usuário deve ter de 3 a 100 caracteres (letras, números e . _ @ + -).");
        RuleFor(x => x.Password).NotEmpty().WithMessage("Informe a senha de acesso.");
        RuleFor(x => x.Role).Must(role => role is Roles.Admin or Roles.Operator or Roles.Reception)
            .WithMessage("Selecione administrador, operador ou recepção.");
        RuleFor(x => x.DepartmentIds).NotEmpty()
            .When(x => x.Role == Roles.Operator)
            .WithMessage("Selecione ao menos um departamento para o operador.");
    }
}
