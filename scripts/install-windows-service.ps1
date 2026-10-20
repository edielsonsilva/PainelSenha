<#
.SYNOPSIS
    Instala o Painel de Senhas como serviço do Windows na rede interna (executar como Administrador).
.DESCRIPTION
    1. Cria a pasta de dados fora da pasta publicada.
    2. Inicializa o banco (migrations, departamentos iniciais e administrador).
    3. Registra o serviço com início automático e reinício em caso de falha.
    O serviço grava logs e banco em DataDir. Para atualizar, pare o serviço, publique por cima e inicie de novo.
.PARAMETER PublishDir
    Pasta gerada por scripts\publish.ps1.
.PARAMETER DataDir
    Pasta do banco, das chaves de login e dos logs. Padrão: C:\ProgramData\PainelSenhas.
.PARAMETER Port
    Porta HTTP em que o serviço escuta. Padrão: 5000.
.PARAMETER RequireHttps
    "false" para rede interna sem certificado (padrão); "true" quando houver certificado configurado.
.PARAMETER AdminUser
    Usuário do administrador criado na inicialização. Padrão: admin.
#>
param(
    [Parameter(Mandatory)] [string]$PublishDir,
    [string]$DataDir = "C:\ProgramData\PainelSenhas",
    [int]$Port = 5000,
    [ValidateSet("true", "false")] [string]$RequireHttps = "false",
    [string]$AdminUser = "admin",
    [string]$ServiceName = "PainelSenhas"
)

$ErrorActionPreference = "Stop"

$isAdmin = ([Security.Principal.WindowsPrincipal][Security.Principal.WindowsIdentity]::GetCurrent()).IsInRole("Administrator")
if (-not $isAdmin) { throw "Execute este script como Administrador." }

$exe = Join-Path (Resolve-Path $PublishDir) "PainelSenhas.Web.exe"
if (-not (Test-Path $exe)) { throw "PainelSenhas.Web.exe não encontrado em $PublishDir. Rode scripts\publish.ps1 antes." }

New-Item -ItemType Directory -Force $DataDir | Out-Null

$secure = Read-Host "Senha do administrador (10+ caracteres, maiúscula, minúscula, número e símbolo)" -AsSecureString
$password = [Runtime.InteropServices.Marshal]::PtrToStringBSTR([Runtime.InteropServices.Marshal]::SecureStringToBSTR($secure))

# Variáveis só para este processo de inicialização; a senha não é gravada no serviço
$env:Storage__Directory = $DataDir
$env:Setup__AdminUser = $AdminUser
$env:Setup__AdminPassword = $password
$env:ASPNETCORE_ENVIRONMENT = "Production"
& $exe --initialize
if ($LASTEXITCODE -ne 0) { throw "Falha ao inicializar o banco." }
Remove-Item Env:Setup__AdminPassword, Env:Setup__AdminUser

if (Get-Service $ServiceName -ErrorAction SilentlyContinue) { throw "O serviço $ServiceName já existe. Remova-o antes (sc.exe delete $ServiceName)." }

New-Service -Name $ServiceName -BinaryPathName "`"$exe`"" -DisplayName "Painel de Senhas" -StartupType Automatic `
    -Description "Controle de senhas entre departamentos" | Out-Null

# Configuração do serviço (variáveis de ambiente do próprio serviço)
$environment = @(
    "ASPNETCORE_ENVIRONMENT=Production",
    "ASPNETCORE_URLS=http://0.0.0.0:$Port",
    "Storage__Directory=$DataDir",
    "Deployment__RequireHttps=$RequireHttps"
)
Set-ItemProperty -Path "HKLM:\SYSTEM\CurrentControlSet\Services\$ServiceName" -Name Environment -Value $environment -Type MultiString

sc.exe failure $ServiceName reset= 86400 actions= restart/5000/restart/15000/restart/60000 | Out-Null
Start-Service $ServiceName

Write-Host "Serviço $ServiceName iniciado em http://localhost:$Port (dados em $DataDir)."
Write-Host "Libere a porta $Port no firewall do Windows para os terminais da rede. Acompanhe: http://localhost:$Port/health"
