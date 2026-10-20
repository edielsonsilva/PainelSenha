using MudBlazor;

namespace PainelSenhas.Web.Components.Shared;

/// <summary>Tema visual único do sistema (verde institucional com destaque dourado).</summary>
public static class AppTheme
{
    public static MudTheme Default { get; } = new()
    {
        PaletteLight = new PaletteLight
        {
            Primary = "#146b60",
            Secondary = "#d8a544",
            Background = "#f5f6f2",
            Surface = "#ffffff",
            AppbarBackground = "#146b60"
        },
        Typography = new Typography
        {
            Default = new DefaultTypography { FontFamily = ["Segoe UI", "Arial", "sans-serif"] }
        }
    };
}
