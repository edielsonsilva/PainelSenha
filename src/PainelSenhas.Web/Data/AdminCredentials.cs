namespace PainelSenhas.Web.Data;

/// <summary>Usuário e senha do administrador definidos por uma operação de linha de comando. <paramref name="WasGenerated"/> indica senha aleatória que precisa ser exibida uma única vez.</summary>
internal sealed record AdminCredentials(string UserName, string Password, bool WasGenerated);
