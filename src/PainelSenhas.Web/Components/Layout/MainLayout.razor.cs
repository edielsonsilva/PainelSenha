using System.Reflection;

namespace PainelSenhas.Web.Components.Layout;

/// <summary>Moldura das páginas: barra superior com a navegação por perfil e rodapé com a versão do sistema.</summary>
public partial class MainLayout
{
    // A versão vem do csproj (<Version>); o sufixo "+hash" que o SDK acrescenta ao informacional não interessa ao usuário
    private static readonly string Version =
        (typeof(MainLayout).Assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion ?? "")
        .Split('+')[0];
}
