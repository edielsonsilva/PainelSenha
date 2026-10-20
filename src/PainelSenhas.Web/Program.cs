using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Hosting.WindowsServices;
using FluentValidation;
using MudBlazor.Services;
using PainelSenhas.Web.Common;
using PainelSenhas.Web.Common.Endpoints;
using PainelSenhas.Web.Components;
using PainelSenhas.Web.Data;
using PainelSenhas.Web.Features.Atendimento;
using PainelSenhas.Web.Features.Emissao;
using PainelSenhas.Web.Features.Gestao;
using PainelSenhas.Web.Features.Painel;
using Serilog;

// Serviço do Windows e tarefa agendada (backup) rodam com a pasta de trabalho em System32; sem o appsettings.json
// no diretório atual, a raiz do conteúdo passa a ser a pasta do executável
var useBaseDirectory = WindowsServiceHelpers.IsWindowsService()
    || (!File.Exists("appsettings.json") && File.Exists(Path.Combine(AppContext.BaseDirectory, "appsettings.json")));
var builder = WebApplication.CreateBuilder(new WebApplicationOptions
{
    Args = args,
    ContentRootPath = useBaseDirectory ? AppContext.BaseDirectory : default
});
builder.Host.UseWindowsService();

// Rede interna sem certificado: Deployment:RequireHttps=false. Hospedagem online mantém o padrão (true).
var requireHttps = builder.Configuration.GetValue("Deployment:RequireHttps", !builder.Environment.IsDevelopment());
var dataDirectory = Path.GetFullPath(builder.Configuration["Storage:Directory"] ?? Path.Combine(builder.Environment.ContentRootPath, "App_Data"));
Directory.CreateDirectory(dataDirectory);
var database = DatabaseSettings.Resolve(builder.Configuration, dataDirectory);

builder.Host.UseSerilog((context, logger) => logger
    .ReadFrom.Configuration(context.Configuration)
    .Enrich.FromLogContext()
    .WriteTo.Console()
    .WriteTo.File(Path.Combine(dataDirectory, "logs", "painel-.log"), rollingInterval: RollingInterval.Day, retainedFileCountLimit: 14));

if (database.Provider == DatabaseProvider.SqlServer)
{
    builder.Services.AddDbContextFactory<SqlServerDbContext>(options => options.UseSqlServer(database.ConnectionString));
    builder.Services.AddSingleton<IDbContextFactory<AppDbContext>, DbContextFactoryAdapter<SqlServerDbContext>>();
    builder.Services.AddScoped(sp => sp.GetRequiredService<IDbContextFactory<AppDbContext>>().CreateDbContext());
}
else
{
    builder.Services.AddDbContextFactory<AppDbContext>(options => options.UseSqlite(database.ConnectionString));
}

builder.Services.AddDataProtection().PersistKeysToFileSystem(new DirectoryInfo(Path.Combine(dataDirectory, "keys"))).SetApplicationName("PainelSenhas");
builder.Services.AddAuthentication(IdentityConstants.ApplicationScheme).AddIdentityCookies();
builder.Services.AddIdentityCore<AppUser>(options =>
{
    options.Password.RequiredLength = 10;
    options.Lockout.MaxFailedAccessAttempts = 5;
    options.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(10);
})
    .AddEntityFrameworkStores<AppDbContext>()
    .AddSignInManager()
    .AddClaimsPrincipalFactory<AppClaimsFactory>()
    .AddErrorDescriber<PortugueseIdentityErrorDescriber>()
    .AddDefaultTokenProviders();
builder.Services.ConfigureApplicationCookie(options =>
{
    options.LoginPath = "/entrar";
    options.AccessDeniedPath = "/entrar";
    options.ExpireTimeSpan = TimeSpan.FromHours(8);
    options.SlidingExpiration = true;
    options.Cookie.HttpOnly = true;
    options.Cookie.SameSite = SameSiteMode.Lax;
    options.Cookie.SecurePolicy = requireHttps ? CookieSecurePolicy.Always : CookieSecurePolicy.SameAsRequest;
});
builder.Services.AddAuthorization();
builder.Services.AddCascadingAuthenticationState();
builder.Services.AddScoped<AuthenticationStateProvider, RevalidatingAuthState>();
builder.Services.AddRazorComponents().AddInteractiveServerComponents();
builder.Services.AddMudServices();
builder.Services.AddProblemDetails();
builder.Services.AddValidatorsFromAssemblyContaining<DepartmentInput>(includeInternalTypes: true);
var health = builder.Services.AddHealthChecks();
if (database.Provider == DatabaseProvider.SqlServer)
{
    health.AddSqlServer(database.ConnectionString, name: "banco");
}
else
{
    health.AddSqlite(database.ConnectionString, name: "banco");
}

builder.Services.AddSingleton(TimeProvider.System);
builder.Services.AddSingleton<UnitClock>();
builder.Services.AddSingleton<ChangeSignal>();
builder.Services.AddScoped<DatabaseWriter>();
builder.Services.AddScoped<IssueTicket>();
builder.Services.AddScoped<AttendQueue>();
builder.Services.AddScoped<QueueQueries>();
builder.Services.AddScoped<ManageUnit>();
builder.Services.AddScoped<OverviewQuery>();
builder.Services.AddScoped<HistoryQuery>();

var app = builder.Build();

if (args.Contains("--initialize"))
{
    var created = await DatabaseSetup.InitializeAsync(app.Services, app.Configuration);
    app.Logger.LogInformation("Banco inicializado. Inicie a aplicação sem --initialize.");
    ShowGeneratedPassword(created);
    return;
}

if (args.Contains("--reset-admin"))
{
    var reset = await DatabaseSetup.ResetAdminAsync(app.Services, app.Configuration);
    if (reset.IsFailure)
    {
        Console.Error.WriteLine($"Não foi possível redefinir a senha do administrador: {reset.Error.Message}");
        Environment.ExitCode = 1;
        return;
    }

    app.Logger.LogInformation("Senha do administrador redefinida; sessões abertas foram encerradas.");
    if (reset.Value.WasGenerated)
    {
        ShowGeneratedPassword(reset.Value);
    }
    else
    {
        Console.WriteLine($"Senha do administrador {reset.Value.UserName} redefinida.");
    }

    return;
}

var backupIndex = Array.IndexOf(args, "--backup");
if (backupIndex >= 0)
{
    if (backupIndex + 1 >= args.Length)
    {
        throw new ArgumentException("Informe o arquivo de destino após --backup.");
    }

    await DatabaseSetup.BackupAsync(database, args[backupIndex + 1]);
    app.Logger.LogInformation("Backup concluído.");
    return;
}

// Em desenvolvimento a fila já abre com uma senha de exemplo; em produção só com Seed:Ticket=true
if (app.Configuration.GetValue("Seed:Ticket", app.Environment.IsDevelopment()))
{
    var seeded = await DatabaseSetup.SeedTicketAsync(app.Services);
    if (seeded.IsFailure)
    {
        app.Logger.LogWarning("Senha de exemplo não criada: {Message}", seeded.Error.Message);
    }
    else if (seeded.Value is { } ticket)
    {
        app.Logger.LogInformation("Senha de exemplo {Code} criada para {Department}.", ticket.Code, ticket.Department);
    }
}

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
}

if (requireHttps)
{
    if (!app.Environment.IsDevelopment())
    {
        app.UseHsts();
    }

    app.UseHttpsRedirection();
}

app.UseAuthentication();
app.UseAuthorization();
app.UseAntiforgery();
app.MapStaticAssets();
app.MapEndpoints();
app.MapRazorComponents<App>().AddInteractiveServerRenderMode();
app.Run();

// A senha gerada vai só para o console (nunca para o log em arquivo) e não é exibida de novo
static void ShowGeneratedPassword(AdminCredentials? credentials)
{
    if (credentials is not { WasGenerated: true })
    {
        return;
    }

    Console.WriteLine($"Administrador: {credentials.UserName}");
    Console.WriteLine($"Senha gerada: {credentials.Password}");
    Console.WriteLine("Anote a senha agora: ela não será exibida de novo. Troque-a em Minha conta depois de entrar.");
}

/// <summary>Ponto de entrada exposto para os testes de integração (WebApplicationFactory).</summary>
public partial class Program;
