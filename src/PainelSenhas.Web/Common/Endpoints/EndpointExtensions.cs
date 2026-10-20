namespace PainelSenhas.Web.Common.Endpoints;

/// <summary>Registro automático dos grupos de endpoints do assembly.</summary>
internal static class EndpointExtensions
{
    /// <summary>Instancia e mapeia todas as implementações de <see cref="IEndpointGroup"/> do assembly.</summary>
    public static IEndpointRouteBuilder MapEndpoints(this IEndpointRouteBuilder app)
    {
        var groups = typeof(IEndpointGroup).Assembly.GetTypes()
            .Where(type => type is { IsAbstract: false, IsInterface: false } && type.IsAssignableTo(typeof(IEndpointGroup)));

        foreach (var type in groups)
        {
            // O filtro acima garante que o tipo implementa IEndpointGroup e possui construtor sem parâmetros
            ((IEndpointGroup)Activator.CreateInstance(type)!).Map(app);
        }

        return app;
    }
}
