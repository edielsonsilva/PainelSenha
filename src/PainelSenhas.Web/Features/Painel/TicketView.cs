using PainelSenhas.Web.Data;

namespace PainelSenhas.Web.Features.Painel;

/// <summary>Senha pronta para exibição, com horários em UTC (a tela converte para o fuso da unidade).</summary>
public sealed record TicketView(
    long Id,
    string Code,
    string Department,
    DateOnly Date,
    TicketStatus Status,
    DateTime IssuedUtc,
    DateTime? CalledUtc,
    DateTime? ClosedUtc,
    bool IsPreferential = false);
