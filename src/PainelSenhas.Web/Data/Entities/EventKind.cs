namespace PainelSenhas.Web.Data;

/// <summary>Tipos de evento gravados no histórico de uma senha.</summary>
public enum EventKind
{
    Issued,
    Called,
    Repeated,
    Completed,
    Absent
}
