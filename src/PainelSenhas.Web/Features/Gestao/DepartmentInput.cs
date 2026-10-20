namespace PainelSenhas.Web.Features.Gestao;

/// <summary>Dados do formulário de departamento. <paramref name="Id"/> zero cria; outro valor edita. O guichê acompanha o departamento.</summary>
public sealed record DepartmentInput(int Id, string Name, string Prefix, string DeskName, bool Active);
