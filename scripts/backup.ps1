<#
.SYNOPSIS
    Backup consistente do banco SQLite (API de backup do SQLite), seguro com o serviço em execução.
.DESCRIPTION
    Gera painel-AAAAMMDD-HHmmss.db em BackupDir e remove cópias mais antigas que KeepDays.
    Copie BackupDir para outro dispositivo/local e teste a restauração periodicamente.
.PARAMETER PublishDir
    Pasta publicada (onde está PainelSenhas.Web.exe).
.PARAMETER DataDir
    A mesma Storage:Directory usada pelo serviço.
.PARAMETER BackupDir
    Destino dos backups.
.PARAMETER KeepDays
    Quantos dias de backups manter. Padrão: 30.
.EXAMPLE
    Agendamento diário às 02:00 (Administrador):
    $action  = New-ScheduledTaskAction -Execute "powershell.exe" -Argument '-NoProfile -File "C:\PainelSenhas\scripts\backup.ps1" -PublishDir "C:\PainelSenhas\publish" -DataDir "C:\ProgramData\PainelSenhas" -BackupDir "D:\Backups\PainelSenhas"'
    Register-ScheduledTask -TaskName "Backup Painel de Senhas" -Action $action -Trigger (New-ScheduledTaskTrigger -Daily -At 2am) -RunLevel Highest
#>
param(
    [Parameter(Mandatory)] [string]$PublishDir,
    [Parameter(Mandatory)] [string]$DataDir,
    [Parameter(Mandatory)] [string]$BackupDir,
    [int]$KeepDays = 30
)

$ErrorActionPreference = "Stop"
$exe = Join-Path (Resolve-Path $PublishDir) "PainelSenhas.Web.exe"
if (-not (Test-Path $exe)) { throw "PainelSenhas.Web.exe não encontrado em $PublishDir." }

New-Item -ItemType Directory -Force $BackupDir | Out-Null
$target = Join-Path $BackupDir ("painel-{0:yyyyMMdd-HHmmss}.db" -f (Get-Date))

$env:Storage__Directory = $DataDir
$env:ASPNETCORE_ENVIRONMENT = "Production"
& $exe --backup $target
if ($LASTEXITCODE -ne 0 -or -not (Test-Path $target)) { throw "Falha ao gerar o backup." }

Get-ChildItem $BackupDir -Filter "painel-*.db" | Where-Object { $_.LastWriteTime -lt (Get-Date).AddDays(-$KeepDays) } | Remove-Item -Force
Write-Host "Backup gerado: $target"
