namespace PainelSenhas.Web.Common.Endpoints;

/// <summary>
/// Contrato de um grupo de endpoints HTTP. Cada grupo fica em seu próprio arquivo e é descoberto
/// automaticamente por <see cref="EndpointExtensions.MapEndpoints"/>; o Program.cs nunca muda ao adicionar um.
/// </summary>
internal interface IEndpointGroup
{
    void Map(IEndpointRouteBuilder app);
}
