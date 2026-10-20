namespace PainelSenhas.Web.Common;

/// <summary>Resultado de uma operação sem valor de retorno: sucesso ou um <see cref="Common.Error"/>.</summary>
public sealed class Result
{
    private readonly Error? _error;

    private Result(Error? error)
    {
        _error = error;
    }

    public static Result Success { get; } = new(null);

    public bool IsSuccess => _error is null;

    public bool IsFailure => _error is not null;

    public Error Error => _error ?? Error.None;

    public static implicit operator Result(Error error) => new(error);
}
