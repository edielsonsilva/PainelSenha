using PainelSenhas.Web.Common.Endpoints;

namespace PainelSenhas.Web.Features.Health;

/// <summary>Endpoint <c>/health</c> para monitoramento da hospedagem e do serviço: verifica se o banco responde. Sem dados sensíveis.</summary>
internal sealed class HealthEndpoints : IEndpointGroup
{
    public void Map(IEndpointRouteBuilder app) => app.MapHealthChecks("/health").AllowAnonymous();
}
