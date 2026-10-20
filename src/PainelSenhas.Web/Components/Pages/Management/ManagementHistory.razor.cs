using Microsoft.AspNetCore.Components;
using MudBlazor;
using PainelSenhas.Web.Common;
using PainelSenhas.Web.Components.Shared;
using PainelSenhas.Web.Data;
using PainelSenhas.Web.Features.Gestao;
using PainelSenhas.Web.Features.Painel;

namespace PainelSenhas.Web.Components.Pages.Management;

/// <summary>
/// Histórico de senhas da gestão. Qualquer mudança de filtro (atalho de período, datas, departamento, status, busca) consulta
/// na hora; as colunas principais ordenam no servidor. Não acompanha a fila em tempo real: só recarrega nas ações do administrador.
/// </summary>
public partial class ManagementHistory : LivePage
{
    private HistoryPreset _preset = HistoryPreset.Today;
    private DateTime? _from;
    private DateTime? _to;
    private int _filterDepartment;
    private int _filterStatus = -1;
    private string _search = "";
    private HistorySort _sort = HistorySort.Issued;
    private bool _descending = true;
    private int _page = 1;
    private int _pageSize = 25;
    private bool _initialized;
    private IReadOnlyList<DepartmentView> _departments = [];
    private HistoryView? _history;

    [Inject]
    private QueueQueries Queries { get; set; } = default!;

    [Inject]
    private HistoryQuery History { get; set; } = default!;

    [Inject]
    private UnitClock Clock { get; set; } = default!;

    protected override bool PollingEnabled => false;

    private int PageCount => Math.Max(1, (int)Math.Ceiling((_history?.Total ?? 0) / (double)_pageSize));

    private string PeriodText => _from == _to
        ? $"{_from:dd/MM/yyyy}"
        : $"{_from:dd/MM/yyyy} a {_to:dd/MM/yyyy}";

    // Os mesmos filtros da tela; só dados de consulta (período, departamento, status e código), nenhum dado pessoal
    private string ExportUrl
    {
        get
        {
            var filter = CurrentFilter();
            var url = $"/gestao/exportar?from={filter.From:yyyy-MM-dd}&to={filter.To:yyyy-MM-dd}";
            if (filter.DepartmentId is { } department)
            {
                url += $"&department={department}";
            }

            if (filter.Status is { } status)
            {
                url += $"&status={status}";
            }

            if (filter.Search is { } search)
            {
                url += $"&search={Uri.EscapeDataString(search)}";
            }

            return url;
        }
    }

    private string RangeText
    {
        get
        {
            var total = _history?.Total ?? 0;
            if (total == 0)
            {
                return "Nenhuma senha";
            }

            var first = ((_page - 1) * _pageSize) + 1;
            return $"Mostrando {first}–{Math.Min(total, _page * _pageSize)} de {total}";
        }
    }

    protected override async Task<Result> RefreshAsync()
    {
        if (!_initialized)
        {
            ApplyPreset(HistoryPreset.Today);
            _initialized = true;
        }

        _departments = await Queries.DepartmentsAsync(includeInactive: true, ct: StopToken);
        var history = await History.PageAsync(await UserAsync(), CurrentFilter(), _sort, _descending, _page, _pageSize, StopToken);
        if (history.IsFailure)
        {
            return history.Error;
        }

        _history = history.Value;
        return Result.Success;
    }

    private HistoryFilter CurrentFilter() => new(
        DateOnly.FromDateTime(_from ?? DateTime.Today),
        DateOnly.FromDateTime(_to ?? DateTime.Today),
        _filterDepartment == 0 ? null : _filterDepartment,
        _filterStatus < 0 ? null : (TicketStatus)_filterStatus,
        string.IsNullOrWhiteSpace(_search) ? null : _search.Trim());

    private string Time(DateTime? utc) => utc.HasValue ? Clock.Local(utc.Value).ToString("HH:mm:ss") : "—";

    private static Color StatusColor(TicketStatus status) => status switch
    {
        TicketStatus.Completed => Color.Success,
        TicketStatus.Absent => Color.Warning,
        TicketStatus.Called => Color.Info,
        _ => Color.Default
    };

    private void ApplyPreset(HistoryPreset preset)
    {
        _preset = preset;
        var today = Clock.Today;
        (DateOnly from, DateOnly to)? range = preset switch
        {
            HistoryPreset.Today => (today, today),
            HistoryPreset.Yesterday => (today.AddDays(-1), today.AddDays(-1)),
            HistoryPreset.Last7Days => (today.AddDays(-6), today),
            HistoryPreset.Last30Days => (today.AddDays(-29), today),
            HistoryPreset.ThisMonth => (new DateOnly(today.Year, today.Month, 1), today),
            _ => null
        };

        if (range is { } value)
        {
            _from = value.from.ToDateTime(TimeOnly.MinValue);
            _to = value.to.ToDateTime(TimeOnly.MinValue);
        }
    }

    private Task ReloadFromFirstPageAsync()
    {
        _page = 1;
        return RunAsync(() => Task.FromResult(Result.Success));
    }

    private Task SelectPresetAsync(HistoryPreset preset)
    {
        ApplyPreset(preset);
        return preset == HistoryPreset.Custom ? Task.CompletedTask : ReloadFromFirstPageAsync();
    }

    private Task FromChangedAsync(DateTime? date)
    {
        _from = date;
        return DatesChangedAsync();
    }

    private Task ToChangedAsync(DateTime? date)
    {
        _to = date;
        return DatesChangedAsync();
    }

    private Task DatesChangedAsync() => _from is null || _to is null ? Task.CompletedTask : ReloadFromFirstPageAsync();

    private Task DepartmentChangedAsync(int departmentId)
    {
        _filterDepartment = departmentId;
        return ReloadFromFirstPageAsync();
    }

    private Task StatusChangedAsync(int status)
    {
        _filterStatus = status;
        return ReloadFromFirstPageAsync();
    }

    private Task SearchChangedAsync(string? text)
    {
        _search = text ?? "";
        return ReloadFromFirstPageAsync();
    }

    private Task SortAsync(HistorySort sort)
    {
        _descending = _sort == sort ? !_descending : sort == HistorySort.Issued;
        _sort = sort;
        return ReloadFromFirstPageAsync();
    }

    private Task PageSizeChangedAsync(int size)
    {
        _pageSize = size;
        return ReloadFromFirstPageAsync();
    }

    private Task PageChangedAsync(int page)
    {
        _page = page;
        return RunAsync(() => Task.FromResult(Result.Success));
    }
}
