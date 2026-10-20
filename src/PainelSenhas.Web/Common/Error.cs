namespace PainelSenhas.Web.Common;

/// <summary>Falha esperada de uma operação, com categoria tipada e mensagem pronta para o usuário.</summary>
public sealed record Error(ErrorType Type, string Message)
{
    /// <summary>Valor sentinela de um resultado bem-sucedido.</summary>
    public static readonly Error None = new(ErrorType.Validation, string.Empty);

    public static Error Validation(string message) => new(ErrorType.Validation, message);

    public static Error NotFound(string message) => new(ErrorType.NotFound, message);

    public static Error Conflict(string message) => new(ErrorType.Conflict, message);

    public static Error Unauthorized(string message) => new(ErrorType.Unauthorized, message);
}
