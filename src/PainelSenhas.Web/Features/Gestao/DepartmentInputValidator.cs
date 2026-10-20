using FluentValidation;

namespace PainelSenhas.Web.Features.Gestao;

/// <summary>Valida o cadastro de departamento: nome, nome do guichê e prefixo de 1 a 5 letras maiúsculas.</summary>
internal sealed class DepartmentInputValidator : AbstractValidator<DepartmentInput>
{
    public DepartmentInputValidator()
    {
        RuleFor(x => x.Name).Length(2, 80).WithMessage("O nome do departamento deve ter de 2 a 80 caracteres.");
        RuleFor(x => x.DeskName).Length(1, 40).WithMessage("O nome do guichê deve ter de 1 a 40 caracteres.");
        RuleFor(x => x.Prefix).Matches("^[A-Z]{1,5}$").WithMessage("O prefixo deve ter de 1 a 5 letras.");
    }
}
