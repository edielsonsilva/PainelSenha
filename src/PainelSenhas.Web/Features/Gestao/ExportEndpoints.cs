using System.Security.Claims;
using PainelSenhas.Web.Common;
using PainelSenhas.Web.Common.Endpoints;
using PainelSenhas.Web.Data;
using PainelSenhas.Web.Features.Painel;

namespace PainelSenhas.Web.Features.Gestao;

/// <summary>
/// Exportação do histórico para Excel (<c>GET /gestao/exportar</c>), somente administrador. Recebe os mesmos filtros da tela
/// e devolve a planilha como download; é um endpoint comum (e não JavaScript) para funcionar igual na rede interna e na hospedagem.
/// </summary>
internal sealed class ExportEndpoints : IEndpointGroup
{
    private const string XlsxContentType = "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet";

    public void Map(IEndpointRouteBuilder app)
    {
        app.MapGet("/gestao/exportar", ExportAsync)
            .RequireAuthorization(policy => policy.RequireRole(Roles.Admin))
            .WithTags("Gestão");
    }

    private static async Task<IResult> ExportAsync(
        ClaimsPrincipal user,
        HistoryQuery history,
        QueueQueries queries,
        UnitClock clock,
        DateOnly from,
        DateOnly to,
        int? department,
        TicketStatus? status,
        string? search,
        CancellationToken ct)
    {
        var filter = new HistoryFilter(from, to, department, status, string.IsNullOrWhiteSpace(search) ? null : search.Trim());
        var rows = await history.AllAsync(user, filter, ct);
        if (rows.IsFailure)
        {
            return Failure(rows.Error);
        }

        var departmentLabel = "Todos os departamentos";
        if (department is { } id)
        {
            departmentLabel = (await queries.DepartmentsAsync(includeInactive: true, ct: ct)).FirstOrDefault(x => x.Id == id)?.Name
                ?? $"Departamento {id}";
        }

        var session = await queries.CurrentUserAsync(user, ct);
        var bytes = HistoryWorkbook.Build(rows.Value, filter, departmentLabel, session.IsSuccess ? session.Value.UserName : "", clock);
        var name = from == to ? $"historico-senhas-{from:yyyy-MM-dd}.xlsx" : $"historico-senhas-{from:yyyy-MM-dd}_a_{to:yyyy-MM-dd}.xlsx";
        return Results.File(bytes, XlsxContentType, name);
    }

    private static IResult Failure(Error error) => error.Type switch
    {
        ErrorType.Unauthorized => Results.Problem(statusCode: StatusCodes.Status403Forbidden, title: "Acesso negado", detail: error.Message),
        ErrorType.NotFound => Results.Problem(statusCode: StatusCodes.Status404NotFound, title: "Não encontrado", detail: error.Message),
        _ => Results.Problem(statusCode: StatusCodes.Status400BadRequest, title: "Filtro inválido", detail: error.Message)
    };
}
