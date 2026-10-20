<#
.SYNOPSIS
    Publica o Painel de Senhas (Release) para uma pasta, pronto para o servidor local ou para a hospedagem.
.PARAMETER Output
    Pasta de destino. Padrão: .\publish (ao lado deste script, um nível acima).
.PARAMETER Runtime
    Identificador de runtime do servidor. Padrão: win-x64. Use linux-x64 para hospedagem Linux.
.NOTES
    Requer o runtime ASP.NET Core 10 no servidor (publicação dependente do framework, bem menor).
    O banco NÃO fica na pasta publicada: defina ConnectionStrings__SqlServer (ou, no SQLite, Storage:Directory) e veja o README.
#>
param(
    [string]$Output = (Join-Path $PSScriptRoot "..\publish"),
    [string]$Runtime = "win-x64"
)

$ErrorActionPreference = "Stop"
$project = Join-Path $PSScriptRoot "..\src\PainelSenhas.Web\PainelSenhas.Web.csproj"

dotnet publish $project -c Release -r $Runtime --self-contained false -o $Output
if ($LASTEXITCODE -ne 0) { throw "Falha na publicação." }

Write-Host "Publicado em $((Resolve-Path $Output).Path)"
