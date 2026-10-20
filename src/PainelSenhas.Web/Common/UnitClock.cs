namespace PainelSenhas.Web.Common;

/// <summary>
/// Relógio da unidade: guarda instantes em UTC e calcula a data operacional no fuso configurado
/// (<c>Unit:TimeZone</c>), que define quando a numeração das senhas reinicia.
/// </summary>
internal sealed class UnitClock(TimeProvider time, IConfiguration configuration)
{
    private readonly TimeZoneInfo _zone = TimeZoneInfo.FindSystemTimeZoneById(configuration["Unit:TimeZone"] ?? "America/Cuiaba");

    public DateTime UtcNow => time.GetUtcNow().UtcDateTime;

    public DateOnly Today => DateOnly.FromDateTime(Local(UtcNow));

    public DateTime Local(DateTime utc) => TimeZoneInfo.ConvertTimeFromUtc(DateTime.SpecifyKind(utc, DateTimeKind.Utc), _zone);
}
