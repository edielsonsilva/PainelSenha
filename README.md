# Painel de Senhas

Controle de senhas entre departamentos (um guichê por departamento). Quatro áreas:

| Rota | Quem usa | Função |
|---|---|---|
| `/emissao` | Recepção | Um clique no departamento emite e imprime a senha (uma por vez; os botões travam 2 s contra clique duplo) |
| `/atendimento` (e `/atendimento/{prefixo}`, ex.: `/atendimento/T`) | Operador do guichê | Uma aba por departamento permitido (o operador de um só departamento não vê abas); chama, repete, encerra |
| `/painel` | TV (sem login) | Mostra as chamadas |
| `/gestao` (visão geral), `/gestao/historico`, `/gestao/departamentos`, `/gestao/equipe` | Administrador | Acompanhar o dia ao vivo, consultar e exportar o histórico, cadastrar departamentos e acessos |
| `/conta` | Qualquer usuário logado | Trocar a própria senha |
| `/health` | Monitoramento | Responde `Healthy` se o banco está acessível |

Decisões de arquitetura: [PLANO-MVP.md](PLANO-MVP.md). Telas originais do protótipo: `docs/`.

Blazor Web App .NET 10 (Interactive Server) + MudBlazor + EF Core 10 + SQL Server (padrão; SQLite opcional) + ASP.NET Identity. Um único processo atende rede interna ou hospedagem, com a mesma base de código.

## Primeira execução (desenvolvimento)

Requer o SDK .NET 10 e o SQL Server LocalDB (instalado com o Visual Studio ou o SQL Server Express). O banco de desenvolvimento é `PainelSenhas` em `(localdb)\MSSQLLocalDB`, definido em `ConnectionStrings:SqlServer` no `appsettings.Development.json`.

```bash
dotnet tool restore
dotnet run --project src/PainelSenhas.Web -- --initialize
dotnet run --project src/PainelSenhas.Web
```

`--initialize` cria o banco (se preciso), aplica as migrations, cria os departamentos iniciais (Tesouraria, Patrimônio, Livro Voluntário, Manutenção Preventiva, um guichê cada) e o administrador.

**Não existe senha padrão.** Se `Setup:AdminPassword` não estiver definido, `--initialize` gera uma senha aleatória de 16 caracteres e a mostra no console uma única vez (nunca no log). Anote-a e troque-a em **Minha conta** depois de entrar. Para escolher usuário e senha, defina antes (em produção, use as variáveis `Setup__AdminUser` e `Setup__AdminPassword`):

```bash
dotnet user-secrets init --project src/PainelSenhas.Web
dotnet user-secrets set "Setup:AdminUser" "admin" --project src/PainelSenhas.Web
dotnet user-secrets set "Setup:AdminPassword" "<senha com 10+ caracteres, maiúscula, minúscula, número e símbolo>" --project src/PainelSenhas.Web
```

Depois do login, o administrador cria o acesso da recepção, dos operadores e de outros administradores em **Gestão → Equipe**. Só administrador cria acessos. O administrador não é vinculado a departamentos (atende qualquer um), pode inativar outro administrador, mas nunca a própria conta, então sempre sobra ao menos um administrador ativo. A senha de administrador é trocada em *Minha conta*.

### Esqueci a senha do administrador

```bash
dotnet run --project src/PainelSenhas.Web -- --reset-admin
```

Gera uma nova senha aleatória e a mostra uma única vez. Também destrava a conta e encerra as sessões abertas. Com `Setup:AdminPassword` definido, usa essa senha em vez de gerar uma; com mais de um administrador, informe `Setup:AdminUser`. No servidor publicado: `PainelSenhas.Web.exe --reset-admin` com `Storage__Directory` apontando para a pasta de dados. Não apaga senhas emitidas.

## Testes

```bash
dotnet test --solution PainelSenhas.slnx
```

Os testes sobem a aplicação real em memória com SQLite em arquivo temporário (sem banco em memória do EF). Para rodá-los contra o SQL Server, aponte `PAINEL_TESTS_SQLSERVER` para uma conexão (cada execução cria e apaga o próprio banco): `$env:PAINEL_TESTS_SQLSERVER = "Server=(localdb)\MSSQLLocalDB;Database=master;Trusted_Connection=True;TrustServerCertificate=True"`. Cobrem emissões e chamadas simultâneas, repetição da mesma solicitação, guichê ocupado, encerramento duplicado, virada do dia, permissões por perfil, login por HTTP e troca de senha. Não passe `--nologo` ao `dotnet test`: o Microsoft Testing Platform rejeita a opção.

## Configuração

| Chave | Padrão | Uso |
|---|---|---|
| `Database:Provider` | `SqlServer` | `SqlServer` (padrão) ou `Sqlite` (arquivo `painel.db` em `Storage:Directory`). |
| `ConnectionStrings:SqlServer` | LocalDB, só em Development | Conexão do SQL Server. Em produção, defina por variável de ambiente (`ConnectionStrings__SqlServer`) ou cofre de segredos, nunca no repositório. |
| `Storage:Directory` | `<pasta do app>/App_Data` | Pasta das chaves de login e dos logs (e do banco, no SQLite). Aponte para fora da pasta publicada. |
| `Deployment:RequireHttps` | `true` (`false` em Development) | `true`: redireciona para HTTPS, HSTS e cookie Secure. `false`: rede interna sem certificado. |
| `AllowedHosts` | `*` | Restrinja ao nome/IP do servidor (ex.: `painel.empresa.local`). |
| `Unit:TimeZone` | `America/Cuiaba` | Fuso que define a data operacional (a numeração reinicia por dia). |
| `Seed:Ticket` | `true` em Development, `false` nas demais | Ao iniciar, emite uma senha de exemplo (primeiro departamento, em nome do administrador) se o banco já está inicializado e ainda não tem nenhuma senha. Roda uma única vez por banco. |
| `Printing:Layout` | `Thermal80` | `Thermal80` (cupom 80mm) ou `A4` (impressora comum). |
| `Printing:AutoPrint` | `true` | Imprime logo após emitir. Com `false`, só imprime pelo botão **Reimprimir**. |
| `Management:LongWaitMinutes` | `15` | Espera (em minutos) a partir da qual a "maior espera" da visão geral entra em alerta. |
| `Serilog:MinimumLevel` | `Information` | Nível de log. Os logs vão para o console e para `Storage:Directory/logs`, com 14 dias de retenção. |

Variáveis de ambiente usam `__` no lugar de `:` (ex.: `Deployment__RequireHttps=false`).

## Implantação

Os três scripts ficam em `scripts/` (PowerShell):

| Script | Função |
|---|---|
| `publish.ps1` | Publica em Release (dependente do framework, cerca de 20 MB). Requer o runtime ASP.NET Core 10 no servidor. |
| `install-windows-service.ps1` | Como Administrador: cria a pasta de dados, inicializa o banco, registra o serviço com início automático e reinício em falha. |
| `backup.ps1` | Backup consistente com o serviço rodando, com retenção em dias. |

### Rede interna

```powershell
.\scripts\publish.ps1 -Output C:\PainelSenhas\publish
.\scripts\install-windows-service.ps1 -PublishDir C:\PainelSenhas\publish -Port 5000
```

O serviço escuta em HTTP na porta informada, com `Deployment__RequireHttps=false`. Libere a porta no firewall e acesse `http://<servidor>:5000` nos terminais. Com certificado interno, use `-RequireHttps true` e configure o HTTPS do Kestrel. Impeça a suspensão do computador servidor.

Sem HTTPS o login e a sessão trafegam sem criptografia; use apenas em rede confiável.

### Hospedagem online

Publique com `scripts\publish.ps1 -Runtime linux-x64` (ou `win-x64`) e rode atrás de HTTPS (proxy reverso ou Kestrel com certificado). Mantenha `Deployment:RequireHttps=true`, `Storage:Directory` em volume persistente (chaves de login e logs) e `ConnectionStrings__SqlServer` apontando para o SQL Server de produção. As escritas são serializadas por um bloqueio de aplicação no banco, então o SQL Server aceita mais de uma instância; com SQLite, use uma só e não coloque o `.db` em compartilhamento de rede. Defina `AllowedHosts` com o domínio. Monitore `/health`.

### Atualizar

Pare o serviço, publique por cima da pasta, inicie o serviço. As migrations pendentes são aplicadas com `PainelSenhas.Web.exe --initialize` (com `ConnectionStrings__SqlServer` e `Storage__Directory` definidos); ele não recria nada que já exista.

## Migrations

Cada provedor tem o próprio conjunto de migrations. Ao mudar o modelo, gere nos dois (o SQL Server é o padrão):

```powershell
.\scripts\gerar-migrations.ps1 -Nome AdicionaObservacao -Executar
.\scripts\gerar-migrations.ps1 -Provedor Sqlite -Nome AdicionaObservacao
```

O script aplica no banco local (`-Executar`) e gera scripts SQL em `docs/migrations/` (`-Sql`). Faça backup antes de aplicar.

## Backup e restauração

```powershell
.\scripts\backup.ps1 -PublishDir C:\PainelSenhas\publish -DataDir C:\ProgramData\PainelSenhas -BackupDir D:\Backups\PainelSenhas
```

Com SQL Server, prefira o backup nativo do servidor (job do SQL Server Agent ou `BACKUP DATABASE`); `--backup` também funciona, mas o arquivo é escrito pelo próprio servidor, então o destino precisa ser acessível a ele. O exemplo de agendamento diário (Agendador de Tarefas) está no cabeçalho do script. Para restaurar o SQLite: pare o serviço, substitua `painel.db` por uma cópia (apague `painel.db-wal` e `painel.db-shm`) e inicie o serviço. As chaves de login (`keys/`) não fazem parte do backup: se forem perdidas, os usuários só precisam entrar de novo. Copie os backups para outro dispositivo e teste a restauração.

## Impressão da senha

A impressão é feita pelo navegador do computador da recepção, igual na rede interna e na hospedagem: a impressora fica ao lado de quem emite.

- **Térmica de cupom 80mm:** deixe-a como impressora padrão do Windows (driver do fabricante, papel de 80mm). Para imprimir sem diálogo, abra o navegador da recepção em modo de impressão silenciosa:
  `chrome.exe --kiosk-printing http://<servidor>:5000/emissao` (no Edge: `msedge.exe --kiosk-printing ...`). Com `Printing:AutoPrint=true` (padrão) a senha imprime sozinha logo após o clique no departamento; o botão **Reimprimir** (última senha emitida) cobre falta de papel. Sem `--kiosk-printing`, o diálogo de impressão abre a cada emissão.
- **Impressora comum:** use `Printing:Layout=A4`. O diálogo de impressão do navegador aparece normalmente; o comprovante sai em um cartão centralizado.

O layout do comprovante foi validado apenas na tela; confira a primeira impressão no equipamento real (largura útil, corte e margens do driver).

## Gestão

Quatro áreas, cada uma com endereço próprio (dá para favoritar), só para o administrador:

- **Visão geral** (`/gestao`, tela inicial): acompanha a fila em tempo real. Indicadores de hoje (emitidas, na fila, em atendimento, concluídas, ausentes, espera e atendimento médios, maior espera agora) e um cartão por departamento com a senha em atendimento e há quanto tempo, o movimento do dia e a espera da senha mais antiga da fila. Senhas de dias anteriores que ainda estão na fila são sinalizadas. A maior espera fica em alerta a partir de `Management:LongWaitMinutes`.
- **Histórico** (`/gestao/historico`): atalhos de período (hoje, ontem, 7 e 30 dias, este mês, personalizado), departamento, status e busca pelo código (`T-012`, `012` ou só o prefixo `T`). Qualquer mudança consulta na hora; as colunas principais ordenam; a tabela mostra espera, atendimento e quem atendeu (operador e guichê). O período é limitado a 366 dias.
- **Departamentos** (`/gestao/departamentos`) e **Equipe** (`/gestao/equipe`): cadastro por diálogos. Departamentos com histórico são inativados, nunca excluídos. Ao criar um acesso ou redefinir uma senha, **Gerar senha segura** cria uma senha de 16 caracteres, que fica visível para o administrador repassá-la.

**Exportar para Excel:** o botão do histórico baixa uma planilha com os mesmos filtros da tela, com a aba *Resumo* (período, filtros, totais e quadro por departamento) e a aba *Senhas* (uma linha por senha). Horários no fuso da unidade e tempos em minutos numéricos. O endereço é `GET /gestao/exportar?from=AAAA-MM-DD&to=AAAA-MM-DD[&department=][&status=][&search=]`, restrito ao administrador, com limite de 366 dias e 100.000 linhas. Gerado com ClosedXML.

## Painel da TV

Abra `http://<servidor>:5000/painel` em um navegador atualizado na TV, clique em **Ativar som** (o navegador só libera áudio depois de um clique) e em **Tela cheia**. Se a conexão cair, o painel tenta reconectar sozinho e recarrega a página quando a sessão expira. Depois de recarregar, o som precisa ser ativado de novo.

### Som e destaque da chamada

- **Som:** a cada chamada nova ou repetida o painel toca um sinal de sino (cerca de 2,2 s) gerado no próprio navegador com Web Audio, em `Components/Pages/Board.razor.js`. Não há arquivo de áudio nem licença de terceiros. A senha **comum** toca um "ding-dong" descendente (mi-dó) e a **preferencial** toca três notas ascendentes (dó-mi-sol), para que a equipe distinga uma da outra sem olhar a TV. Ajuste o volume na própria TV. Para mudar o som, edite as notas `COMMON_CHIME` e `PREFERENTIAL_CHIME` (frequências em Hz e atraso em segundos) e o ganho `MASTER_LEVEL`.
- **Ícone da aba:** `wwwroot/favicon.svg` (navegadores atuais) e `wwwroot/favicon.ico` (16, 32 e 48 px), declarados em `Components/App.razor`.
- **Destaque:** o card **Senha chamada** pulsa por **4 segundos** a cada chamada nova (também na repetição), mesmo com o som desligado. São 4 pulsos de 1 s: menos de 3 piscadas por segundo (WCAG 2.3.1, segurança para fotossensíveis) e menos de 5 s (WCAG 2.2.2), e o destaque dura mais que o som. Ao abrir ou reconectar o painel o card aparece parado. Quem usa o sistema com "reduzir movimento" ativado recebe um halo fixo pelos mesmos 4 s. Para mudar a duração, altere `animation` em `Components/Pages/Board.razor.css` (`1s ... 4` = 4 pulsos de 1 s).

## Organização do código

```text
src/PainelSenhas.Web/
  Features/        Emissao, Atendimento, Painel (consultas), Gestao, Conta, Health
  Common/          Result, regras de acesso, escrita transacional, relógio, descoberta de endpoints
  Data/            AppDbContext (SQLite), SqlServerDbContext, entidades e migrations (Migrations = SQLite, Migrations/SqlServer = SQL Server)
  Components/      Pages (razor + code-behind + css + js), Global (componentes reutilizáveis), Layout, Shared
tests/PainelSenhas.Tests/   testes de integração
scripts/                    publicação, serviço do Windows e backup
```
