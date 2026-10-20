using ClosedXML.Excel;
using PainelSenhas.Web.Common;
using PainelSenhas.Web.Data;
using PainelSenhas.Web.Features.Painel;

namespace PainelSenhas.Web.Features.Gestao;

/// <summary>
/// Monta a planilha do histórico: a aba "Resumo" com filtros, totais e quadro por departamento, e a aba "Senhas" com
/// uma linha por senha. Horários saem no fuso da unidade e tempos em minutos numéricos, para o Excel filtrar e somar.
/// </summary>
internal static class HistoryWorkbook
{
    private const string DateTimeFormat = "dd/MM/yyyy HH:mm:ss";
    private const string MinutesFormat = "0.0";

    public static byte[] Build(
        IReadOnlyList<HistoryRow> rows, HistoryFilter filter, string departmentLabel, string generatedBy, UnitClock clock)
    {
        using var workbook = new XLWorkbook();
        BuildSummary(workbook.Worksheets.Add("Resumo"), rows, filter, departmentLabel, generatedBy, clock);
        BuildTickets(workbook.Worksheets.Add("Senhas"), rows, clock);

        using var stream = new MemoryStream();
        workbook.SaveAs(stream);
        return stream.ToArray();
    }

    private static void BuildSummary(
        IXLWorksheet sheet, IReadOnlyList<HistoryRow> rows, HistoryFilter filter, string departmentLabel, string generatedBy, UnitClock clock)
    {
        sheet.Cell("A1").Value = "Painel de Senhas · Histórico de atendimento";
        sheet.Cell("A1").Style.Font.Bold = true;
        sheet.Cell("A1").Style.Font.FontSize = 14;

        var period = filter.From == filter.To
            ? filter.From.ToString("dd/MM/yyyy")
            : $"{filter.From:dd/MM/yyyy} a {filter.To:dd/MM/yyyy}";
        string[][] info =
        [
            ["Período", period],
            ["Departamento", departmentLabel],
            ["Status", filter.Status is { } status ? StatusText.Label(status) : "Todos"],
            ["Busca", string.IsNullOrWhiteSpace(filter.Search) ? "—" : filter.Search.Trim()],
            ["Gerado em", clock.Local(clock.UtcNow).ToString(DateTimeFormat)],
            ["Gerado por", generatedBy]
        ];
        for (var i = 0; i < info.Length; i++)
        {
            sheet.Cell(3 + i, 1).Value = info[i][0];
            sheet.Cell(3 + i, 1).Style.Font.Bold = true;
            sheet.Cell(3 + i, 2).Value = info[i][1];
        }

        var totalsRow = 3 + info.Length + 1;
        sheet.Cell(totalsRow, 1).Value = "Totais do período";
        sheet.Cell(totalsRow, 1).Style.Font.Bold = true;
        (string Label, XLCellValue Value, string? Format)[] totals =
        [
            ("Total de senhas", rows.Count, null),
            ("Concluídas", Count(rows, TicketStatus.Completed), null),
            ("Ausentes", Count(rows, TicketStatus.Absent), null),
            ("Em atendimento", Count(rows, TicketStatus.Called), null),
            ("Na fila", Count(rows, TicketStatus.Pending), null),
            ("Espera média (min)", Average(rows.Select(x => x.WaitMinutes)), MinutesFormat),
            ("Atendimento médio (min)", Average(rows.Select(x => x.ServiceMinutes)), MinutesFormat)
        ];
        for (var i = 0; i < totals.Length; i++)
        {
            sheet.Cell(totalsRow + 1 + i, 1).Value = totals[i].Label;
            var cell = sheet.Cell(totalsRow + 1 + i, 2);
            cell.Value = totals[i].Value;
            if (totals[i].Format is { } format)
            {
                cell.Style.NumberFormat.Format = format;
            }
        }

        var tableRow = totalsRow + totals.Length + 3;
        string[] headers = ["Departamento", "Total", "Concluídas", "Ausentes", "Em atendimento", "Na fila", "Espera média (min)", "Atendimento médio (min)"];
        for (var i = 0; i < headers.Length; i++)
        {
            sheet.Cell(tableRow, i + 1).Value = headers[i];
        }

        StyleHeader(sheet.Range(tableRow, 1, tableRow, headers.Length));
        var line = tableRow + 1;
        foreach (var group in rows.GroupBy(x => x.Ticket.Department).OrderBy(x => x.Key))
        {
            var list = group.ToList();
            sheet.Cell(line, 1).Value = group.Key;
            sheet.Cell(line, 2).Value = list.Count;
            sheet.Cell(line, 3).Value = Count(list, TicketStatus.Completed);
            sheet.Cell(line, 4).Value = Count(list, TicketStatus.Absent);
            sheet.Cell(line, 5).Value = Count(list, TicketStatus.Called);
            sheet.Cell(line, 6).Value = Count(list, TicketStatus.Pending);
            sheet.Cell(line, 7).Value = Average(list.Select(x => x.WaitMinutes));
            sheet.Cell(line, 8).Value = Average(list.Select(x => x.ServiceMinutes));
            sheet.Range(line, 7, line, 8).Style.NumberFormat.Format = MinutesFormat;
            line++;
        }

        sheet.Column(1).Width = 30;
        for (var column = 2; column <= headers.Length; column++)
        {
            sheet.Column(column).Width = 18;
        }
    }

    private static void BuildTickets(IXLWorksheet sheet, IReadOnlyList<HistoryRow> rows, UnitClock clock)
    {
        string[] headers =
            ["Senha", "Departamento", "Data", "Status", "Emissão", "Chamada", "Encerramento", "Espera (min)", "Atendimento (min)", "Operador", "Guichê"];
        for (var i = 0; i < headers.Length; i++)
        {
            sheet.Cell(1, i + 1).Value = headers[i];
        }

        StyleHeader(sheet.Range(1, 1, 1, headers.Length));

        for (var i = 0; i < rows.Count; i++)
        {
            var row = rows[i];
            var ticket = row.Ticket;
            var line = i + 2;
            sheet.Cell(line, 1).Value = ticket.Code;
            sheet.Cell(line, 2).Value = ticket.Department;
            sheet.Cell(line, 3).Value = ticket.Date.ToDateTime(TimeOnly.MinValue);
            sheet.Cell(line, 4).Value = StatusText.Label(ticket.Status);
            sheet.Cell(line, 5).Value = clock.Local(ticket.IssuedUtc);
            SetDate(sheet.Cell(line, 6), ticket.CalledUtc is { } called ? clock.Local(called) : null);
            SetDate(sheet.Cell(line, 7), ticket.ClosedUtc is { } closed ? clock.Local(closed) : null);
            SetNumber(sheet.Cell(line, 8), row.WaitMinutes);
            SetNumber(sheet.Cell(line, 9), row.ServiceMinutes);
            sheet.Cell(line, 10).Value = row.Operator ?? "";
            sheet.Cell(line, 11).Value = row.Desk ?? "";
        }

        if (rows.Count > 0)
        {
            sheet.Range(2, 3, rows.Count + 1, 3).Style.NumberFormat.Format = "dd/MM/yyyy";
            sheet.Range(2, 5, rows.Count + 1, 7).Style.NumberFormat.Format = DateTimeFormat;
            sheet.Range(2, 8, rows.Count + 1, 9).Style.NumberFormat.Format = MinutesFormat;
        }

        sheet.Range(1, 1, Math.Max(2, rows.Count + 1), headers.Length).SetAutoFilter();
        sheet.SheetView.FreezeRows(1);

        double[] widths = [10, 26, 12, 16, 20, 20, 20, 14, 18, 20, 14];
        for (var column = 0; column < widths.Length; column++)
        {
            sheet.Column(column + 1).Width = widths[column];
        }
    }

    private static void StyleHeader(IXLRange range)
    {
        range.Style.Font.Bold = true;
        range.Style.Font.FontColor = XLColor.White;
        range.Style.Fill.BackgroundColor = XLColor.FromHtml("#146B60");
    }

    private static int Count(IEnumerable<HistoryRow> rows, TicketStatus status) => rows.Count(x => x.Ticket.Status == status);

    private static XLCellValue Average(IEnumerable<double?> values)
    {
        var list = values.Where(x => x.HasValue).Select(x => x.GetValueOrDefault()).ToList();
        return list.Count == 0 ? Blank.Value : list.Average();
    }

    private static void SetDate(IXLCell cell, DateTime? value)
    {
        if (value is { } present)
        {
            cell.Value = present;
        }
    }

    private static void SetNumber(IXLCell cell, double? value)
    {
        if (value is { } present)
        {
            cell.Value = present;
        }
    }
}
