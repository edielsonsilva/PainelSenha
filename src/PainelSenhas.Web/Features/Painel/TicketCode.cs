namespace PainelSenhas.Web.Features.Painel;

/// <summary>Formato de exibição do código da senha: prefixo do departamento e número com três dígitos (T-001).</summary>
public static class TicketCode
{
    public static string Format(string prefix, int number) => $"{prefix}-{number:000}";
}
