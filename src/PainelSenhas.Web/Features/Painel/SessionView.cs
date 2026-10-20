namespace PainelSenhas.Web.Features.Painel;

/// <summary>Dados do usuário logado que as telas precisam: identificação e perfil.</summary>
public sealed record SessionView(string Id, string UserName, string Role);
