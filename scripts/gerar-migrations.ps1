<#
.SYNOPSIS
    Gera e executa migrations do EF Core (SQL Server por padrao, ou SQLite) e gera scripts SQL delas.

.DESCRIPTION
    -Nome     cria uma nova migration (dotnet ef migrations add) a partir das mudancas do modelo.
    -Executar aplica as migrations pendentes no banco (dotnet ef database update); com -Ate, ate a migration indicada.
    -Sql      gera o script SQL em docs/migrations/ (e o padrao quando nem -Nome nem -Executar sao informados).
              Sem -De gera o script completo; com -De gera so o incremento, de -De ate -Ate (ou ate a ultima).
    As acoes podem ser combinadas e rodam nesta ordem: -Nome, -Executar, -Sql.

    -Provedor escolhe o banco: SqlServer (padrao; contexto SqlServerDbContext, migrations em Data\Migrations\SqlServer)
    ou Sqlite (contexto AppDbContext, migrations em Data\Migrations). Cada provedor tem o proprio conjunto de migrations:
    ao mudar o modelo, gere a migration nos dois provedores que voce mantem.

    SQL Server: a conexao vem de ConnectionStrings:SqlServer (appsettings.Development.json, ja apontando para o LocalDB
    (localdb)\MSSQLLocalDB, ou a variavel de ambiente ConnectionStrings__SqlServer). Sem ASPNETCORE_ENVIRONMENT, assume Development.
    No SQL Server o esquema padrao e sempre dbo, declarado em SqlServerDbContext (HasDefaultSchema): as migrations
    novas e os scripts gerados qualificam as tabelas como [dbo].[Tabela].
    SQLite: o banco e <Storage:Directory>\painel.db, resolvido como na aplicacao (padrao: src\PainelSenhas.Web\App_Data).
    Use -DataDir para apontar outra pasta. Em instalacao publicada, prefira PainelSenhas.Web.exe --initialize.

    -Executar pede confirmacao (use -Confirm:$false para pular ou -WhatIf para simular): faca backup antes.
    O EF Core nao suporta scripts idempotentes em SQLite: cada script SQL so deve ser aplicado uma vez, sobre um banco
    no estado informado em -De (ou vazio, no script completo).

.PARAMETER Nome
    Nome da nova migration (ex.: AdicionaCampoObservacao).

.PARAMETER Provedor
    SqlServer (padrao) ou Sqlite.

.PARAMETER Executar
    Aplica as migrations pendentes no banco.

.PARAMETER Sql
    Gera o script SQL das migrations.

.PARAMETER De
    Ultima migration ja aplicada no banco (nome ou id). Omitido = banco vazio. Usado com -Sql.

.PARAMETER Ate
    Ultima migration a incluir (em -Sql) ou a alcancar (em -Executar). Omitido = a mais recente.

.PARAMETER Saida
    Arquivo do script SQL. Padrao: docs/migrations/<provedor>-full.sql, ou <provedor>-<De>-<Ate>.sql no modo incremental.

.PARAMETER DataDir
    Pasta do banco SQLite (Storage:Directory). Omitido = a mesma da aplicacao em desenvolvimento. Ignorado no SQL Server.

.EXAMPLE
    .\scripts\gerar-migrations.ps1 -Nome AdicionaObservacao -Executar

.EXAMPLE
    .\scripts\gerar-migrations.ps1 -Provedor Sqlite -Nome AdicionaObservacao

.EXAMPLE
    .\scripts\gerar-migrations.ps1 -Sql -De SenhasPreferenciais
#>
[CmdletBinding(SupportsShouldProcess, ConfirmImpact = "High")]
param(
    [string]$Nome,
    [ValidateSet("SqlServer", "Sqlite")]
    [string]$Provedor = "SqlServer",
    [switch]$Executar,
    [switch]$Sql,
    [string]$De,
    [string]$Ate,
    [string]$Saida,
    [string]$DataDir
)

$ErrorActionPreference = "Stop"
$root = Split-Path $PSScriptRoot -Parent
$project = Join-Path $root "src\PainelSenhas.Web"
$sqlServer = $Provedor -eq "SqlServer"
$context = if ($sqlServer) { "SqlServerDbContext" } else { "AppDbContext" }
$pastaMigrations = if ($sqlServer) { "Data\Migrations\SqlServer" } else { "Data\Migrations" }

function Invoke-Ef {
    param([string[]]$Argumentos)

    dotnet ef @Argumentos --project $project --context $context
    if ($LASTEXITCODE -ne 0) {
        throw "Falha em 'dotnet ef $($Argumentos[0..1] -join ' ')' (exit code $LASTEXITCODE)."
    }
}

if (-not ($Nome -or $Executar -or $Sql)) { $Sql = [switch]::new($true) }

if ($DataDir -and -not $sqlServer) {
    $env:Storage__Directory = [System.IO.Path]::GetFullPath($DataDir)
}

if ($Nome) {
    Write-Host ""
    Write-Host "Criando migration '$Nome' ($Provedor)..." -ForegroundColor Yellow
    Invoke-Ef @("migrations", "add", $Nome, "--output-dir", ($pastaMigrations -replace "\\", "/"))
    Write-Host "  -> src\PainelSenhas.Web\$pastaMigrations (revise antes de aplicar)" -ForegroundColor Green
}

if ($Executar) {
    $alvoBanco = if ($sqlServer) {
        "banco SQL Server de ConnectionStrings:SqlServer"
    }
    elseif ($env:Storage__Directory) {
        Join-Path $env:Storage__Directory "painel.db"
    }
    else {
        Join-Path $project "App_Data\painel.db"
    }
    Write-Host ""
    Write-Host "Aplicando migrations em $alvoBanco ..." -ForegroundColor Yellow
    if ($PSCmdlet.ShouldProcess($alvoBanco, "Aplicar migrations pendentes")) {
        $alvo = if ($Ate) { @($Ate) } else { @() }
        Invoke-Ef (@("database", "update") + $alvo)
        Write-Host "  -> banco atualizado" -ForegroundColor Green
    }
}

if ($Sql) {
    if (-not $Saida) {
        $prefixo = $Provedor.ToLowerInvariant()
        $arquivo = if ($De -or $Ate) {
            "$prefixo-" + $(if ($De) { $De } else { "inicio" }) + "-" + $(if ($Ate) { $Ate } else { "ultima" }) + ".sql"
        }
        else {
            "$prefixo-full.sql"
        }
        $Saida = Join-Path $root "docs\migrations\$arquivo"
    }
    elseif (-not [System.IO.Path]::IsPathRooted($Saida)) {
        $Saida = Join-Path (Get-Location) $Saida
    }

    New-Item -ItemType Directory -Force -Path (Split-Path $Saida -Parent) | Out-Null

    $argumentos = @("migrations", "script", "--output", $Saida)
    if ($De) { $argumentos += $De }
    if ($Ate) {
        if (-not $De) { $argumentos += "0" }
        $argumentos += $Ate
    }

    Write-Host ""
    Write-Host "Gerando script SQL ($Provedor)..." -ForegroundColor Yellow
    Invoke-Ef $argumentos
    Write-Host "  -> $Saida" -ForegroundColor Green
    if ($sqlServer) {
        Write-Host "     Aplicar com backup feito: sqlcmd -S <servidor> -d <banco> -i `"$Saida`""
    }
    else {
        Write-Host "     Aplicar com o servico parado e backup feito: sqlite3 painel.db < `"$Saida`""
    }
}

Write-Host ""
