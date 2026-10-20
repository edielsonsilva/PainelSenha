namespace PainelSenhas.Web.Common;

/// <summary>Formato de exibição de durações em minutos nas telas de gestão.</summary>
public static class DurationFormat
{
    /// <summary>"—" sem valor, "menos de 1 min", "7 min" ou "1h 05min" para uma hora ou mais.</summary>
    public static string Minutes(double? minutes)
    {
        if (minutes is not { } value)
        {
            return "—";
        }

        var rounded = (int)Math.Round(value, MidpointRounding.AwayFromZero);
        return rounded switch
        {
            < 1 => "menos de 1 min",
            < 60 => $"{rounded} min",
            _ => $"{rounded / 60}h {rounded % 60:00}min"
        };
    }
}
