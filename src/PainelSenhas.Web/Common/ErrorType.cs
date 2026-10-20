namespace PainelSenhas.Web.Common;

/// <summary>Categoria de uma falha esperada, usada para mapear o erro para mensagens e status HTTP.</summary>
public enum ErrorType
{
    Validation,
    NotFound,
    Conflict,
    Unauthorized
}
