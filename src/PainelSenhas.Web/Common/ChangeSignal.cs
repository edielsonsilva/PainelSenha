namespace PainelSenhas.Web.Common;

/// <summary>
/// Aviso em memória de que o banco mudou. As telas comparam a versão e consultam o banco de novo;
/// o sinal nunca é a fonte da fila. Vale para uma única instância da aplicação.
/// </summary>
internal sealed class ChangeSignal
{
    private long _version;

    public long Version => Interlocked.Read(ref _version);

    public void Publish() => Interlocked.Increment(ref _version);
}
