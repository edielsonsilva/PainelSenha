namespace PainelSenhas.Web.Common;

/// <summary>
/// Resultado de uma operação que devolve <typeparamref name="T"/> em caso de sucesso.
/// Falhas esperadas (validação, não encontrado, conflito, acesso) viajam como <see cref="Common.Error"/>, não como exceção.
/// </summary>
public sealed class Result<T>
{
    private readonly T? _value;
    private readonly Error? _error;

    private Result(T value)
    {
        _value = value;
    }

    private Result(Error error)
    {
        _error = error;
    }

    public bool IsSuccess => _error is null;

    public bool IsFailure => _error is not null;

    public Error Error => _error ?? Error.None;

    /// <summary>Valor do sucesso. Ler o valor de um resultado com falha é erro de programação.</summary>
    public T Value => _error is null
        // Sem erro, o construtor de sucesso sempre atribuiu o valor
        ? _value!
        : throw new InvalidOperationException("Resultado com falha não possui valor.");

    /// <summary>Cria um resultado de sucesso explicitamente; útil quando <typeparamref name="T"/> é uma interface e a conversão implícita não se aplica.</summary>
    public static Result<T> Success(T value) => new(value);

    public static implicit operator Result<T>(T value) => new(value);

    public static implicit operator Result<T>(Error error) => new(error);

    public Result ToResult() => _error is null ? Result.Success : _error;
}
