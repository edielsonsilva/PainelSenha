using PainelSenhas.Web.Data;

namespace PainelSenhas.Web.Features.Painel;

/// <summary>
/// Filtros do histórico: período por data operacional (até 366 dias), departamento, status e busca pelo código da senha
/// (por exemplo <c>T-012</c>, <c>012</c> ou só o prefixo <c>T</c>).
/// </summary>
public sealed record HistoryFilter(DateOnly From, DateOnly To, int? DepartmentId = null, TicketStatus? Status = null, string? Search = null);
