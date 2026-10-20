# 🎫 Painel de Senhas

> Aplicativo **Blazor Server** para controle de senhas e filas de atendimento entre departamentos, construído em **.NET 10**, com emissão e impressão de senha, chamada em painel de TV com som, atendimento por guichê e gestão com histórico exportável para Excel.

---

## 📄 Descrição Geral

O **Painel de Senhas** organiza o atendimento presencial: a recepção emite a senha de cada departamento (comum ou preferencial) e imprime o comprovante, o operador do guichê chama, repete e encerra os atendimentos, e a TV da sala de espera exibe as chamadas com sinal sonoro. O administrador acompanha o dia ao vivo, consulta o histórico e cadastra departamentos e equipe.

A interface é **Blazor Web App (Interactive Server)**: as telas se atualizam em tempo real entre navegadores sem recarregar a página, com todo o código em C#. Um único processo atende a rede interna ou a hospedagem online, com a mesma base de código.

Há um guichê por departamento. As senhas preferenciais são sempre chamadas antes das comuns, e a numeração reinicia a cada dia, por departamento, no fuso da unidade.

### 🧭 Áreas do sistema

| Rota | Quem usa | Função |
|---|---|---|
| `/emissao` | Recepção | Um clique no departamento emite e imprime a senha (uma por vez; os botões travam 2 s contra clique duplo) |
| `/atendimento` (e `/atendimento/{prefixo}`, ex.: `/atendimento/T`) | Operador do guichê | Uma aba por departamento permitido (o operador de um só departamento não vê abas); chama, repete, encerra |
| `/painel` | TV (sem login) | Mostra as chamadas |
| `/gestao` (visão geral), `/gestao/historico`, `/gestao/departamentos`, `/gestao/equipe` | Administrador | Acompanhar o dia ao vivo, consultar e exportar o histórico, cadastrar departamentos e acessos |
| `/conta` | Qualquer usuário logado | Trocar a própria senha |
| `/health` | Monitoramento | Responde `Healthy` se o banco está acessível |

Decisões de arquitetura: [PLANO-MVP.md](PLANO-MVP.md). Histórico de versões: [CHANGELOG.md](CHANGELOG.md). Telas originais do protótipo: `docs/`.

---

## 🚀 Tecnologias Utilizadas

- [.NET 10](https://dotnet.microsoft.com/)
- [Blazor Web App (Interactive Server)](https://dotnet.microsoft.com/en-us/apps/aspnet/web-apps/blazor)
- [MudBlazor](https://mudblazor.com/) – *Componentes de interface*
- [Entity Framework Core 10](https://learn.microsoft.com/ef/core)
- [SQL Server](https://www.microsoft.com/sql-server/) – *Banco padrão*
- [SQLite](https://www.sqlite.org/) – *Banco opcional*
- [ASP.NET Core Identity](https://learn.microsoft.com/aspnet/core/security/authentication/identity) – *Usuários e senhas*
- [FluentValidation](https://docs.fluentvalidation.net/)
- [ClosedXML](https://github.com/ClosedXML/ClosedXML) – *Exportação do histórico para Excel*
- [Serilog](https://serilog.net/) – *Logs em console e arquivo*
- [AspNetCore.HealthChecks](https://github.com/Xabaril/AspNetCore.Diagnostics.HealthChecks) – *Endpoint `/health`*
- [xUnit v3](https://xunit.net/), [Shouldly](https://docs.shouldly.org/) e [Respawn](https://github.com/jbogard/Respawn) – *Testes de integração*
- **Web Audio API** – *Sino de chamada gerado no navegador, sem arquivos de áudio*

---

## 🏗️ Arquitetura do Projeto

Um projeto web organizado por funcionalidades (*vertical slices*), mais um projeto de testes de integração. Para este tamanho não há camadas em projetos separados, mediator, repositório genérico nem mensageria externa.

```text
PainelSenha/
├── src/PainelSenhas.Web/
│   ├── Features/        # Emissao, Atendimento, Painel (consultas), Gestao, Conta, Health
│   ├── Common/          # Result, regras de acesso, escrita transacional, relógio, descoberta de endpoints
│   ├── Data/            # AppDbContext (SQLite), SqlServerDbContext, entidades e migrations
│   │                    #   Migrations = SQLite, Migrations/SqlServer = SQL Server
│   ├── Components/      # Pages (razor + code-behind + css + js), Global (reutilizáveis), Layout, Shared
│   └── wwwroot/         # CSS global e favicon
├── tests/PainelSenhas.Tests/   # Testes de integração
├── scripts/                    # Publicação, serviço do Windows, backup e migrations
├── docs/                       # Protótipo original e script SQL completo (docs/migrations)
└── PainelSenhas.slnx
```

---

## 🧠 Padrões e Design de Projeto

- **Vertical Slice / feature folders**: cada operação (`IssueTicket`, chamar, encerrar…) é uma classe injetada diretamente, sem mediator.
- **Result pattern**: falhas esperadas (não encontrado, conflito, validação, acesso negado) viajam como `Result<T>` com erros tipados; exceção só para falhas inesperadas.
- **Idempotência**: a emissão usa chave por departamento e tipo, então um reenvio após falha de conexão devolve a mesma senha em vez de gerar outra.
- **Escritas serializadas e transacionais**: `BEGIN IMMEDIATE` no SQLite e `sp_getapplock` no SQL Server mantêm a numeração e as chamadas sem duplicidade. Um índice único garante um atendimento ativo por guichê.
- **`DbContext` por operação**: `IDbContextFactory`, sem contexto compartilhado durante a sessão Blazor.
- **Atualização em tempo real**: depois do commit, um sinal interno (`ChangeSignal`) avisa as telas inscritas, que consultam o banco de novo; uma reconciliação a cada 5 s recupera avisos perdidos. O sinal é por processo, então use **uma única instância** do aplicativo.
- **Defesa em profundidade**: toda operação reconfere no banco se o usuário está ativo e qual o perfil; o circuito Blazor é revalidado a cada minuto.
- **Perfis de acesso**: Administrador, Operador (só os departamentos vinculados) e Recepção.
- **Code-behind em quatro arquivos** nas páginas (`.razor`, `.razor.cs`, `.razor.css`, `.razor.js`) e componentes reutilizáveis em `Components/Global`.

---

## ⚙️ Como Executar o Projeto

Requer o **SDK .NET 10** e o **SQL Server LocalDB** (instalado com o Visual Studio ou o SQL Server Express). O banco de desenvolvimento é `PainelSenhas` em `(localdb)\MSSQLLocalDB`, definido em `ConnectionStrings:SqlServer` no `appsettings.Development.json`.

1. **Clone o repositório:**

   ```bash
   git clone https://dev.azure.com/especialistanetcba/CCB_ADM/_git/PainelSenha
   cd PainelSenha
   ```

2. **Restaure as ferramentas locais (dotnet-ef):**

   ```bash
   dotnet tool restore
   ```

3. **Crie o banco, aplique as migrations e crie os dados iniciais:**

   ```bash
   dotnet run --project src/PainelSenhas.Web -- --initialize
   ```

   `--initialize` cria o banco (se preciso), aplica as migrations, cria os departamentos iniciais (Tesouraria, Patrimônio, Livro Voluntário, Manutenção Preventiva, um guichê cada) e o administrador.

4. **Execute o projeto:**

   ```bash
   dotnet run --project src/PainelSenhas.Web
   ```

5. **Acesse a aplicação** no endereço mostrado no console (perfil `launchSettings`).

### 🔑 Administrador inicial

**Não existe senha padrão.** Se `Setup:AdminPassword` não estiver definido, `--initialize` gera uma senha aleatória de 16 caracteres e a mostra no console uma única vez (nunca no log). Anote-a e troque-a em **Minha conta** depois de entrar. Para escolher usuário e senha, defina antes (em produção, use as variáveis `Setup__AdminUser` e `Setup__AdminPassword`):

```bash
dotnet user-secrets init --project src/PainelSenhas.Web
dotnet user-secrets set "Setup:AdminUser" "admin" --project src/PainelSenhas.Web
dotnet user-secrets set "Setup:AdminPassword" "<senha com 10+ caracteres, maiúscula, minúscula, número e símbolo>" --project src/PainelSenhas.Web
```

Depois do login, o administrador cria o acesso da recepção, dos operadores e de outros administradores em **Gestão → Equipe**. Só administrador cria acessos. O administrador não é vinculado a departamentos (atende qualquer um), pode inativar outro administrador, mas nunca a própria conta, então sempre sobra ao menos um administrador ativo.

#### Esqueci a senha do administrador

```bash
dotnet run --project src/PainelSenhas.Web -- --reset-admin
```

Gera uma nova senha aleatória e a mostra uma única vez. Também destrava a conta e encerra as sessões abertas. Com `Setup:AdminPassword` definido, usa essa senha em vez de gerar uma; com mais de um administrador, informe `Setup:AdminUser`. No servidor publicado: `PainelSenhas.Web.exe --reset-admin` com `Storage__Directory` apontando para a pasta de dados. Não apaga senhas emitidas.

---

## 🧪 Testes

```bash
dotnet test --solution PainelSenhas.slnx
```

Os testes sobem a aplicação real em memória com SQLite em arquivo temporário (sem banco em memória do EF). Para rodá-los contra o SQL Server, aponte `PAINEL_TESTS_SQLSERVER` para uma conexão (cada execução cria e apaga o próprio banco):

```powershell
$env:PAINEL_TESTS_SQLSERVER = "Server=(localdb)\MSSQLLocalDB;Database=master;Trusted_Connection=True;TrustServerCertificate=True"
```

Cobrem emissões e chamadas simultâneas, repetição da mesma solicitação, guichê ocupado, encerramento duplicado, virada do dia, permissões por perfil, login por HTTP e troca de senha. Não passe `--nologo` ao `dotnet test`: o Microsoft Testing Platform rejeita a opção.

---

## 🗃️ Bancos de Dados Suportados

- **Microsoft SQL Server** → `"SqlServer"` (padrão)
- **SQLite** → `"Sqlite"` (arquivo `painel.db` em `Storage:Directory`)

### Como trocar o banco

1. Defina `Database:Provider` (`appsettings.json` ou variável `Database__Provider`).
2. Para o SQL Server, informe `ConnectionStrings:SqlServer` (em produção, por variável de ambiente ou cofre de segredos, nunca no repositório).
3. Rode `--initialize` para aplicar as migrations do provedor escolhido.

### Migrations

Cada provedor tem o próprio conjunto de migrations. Ao mudar o modelo, gere nos dois (o SQL Server é o padrão):

```powershell
.\scripts\gerar-migrations.ps1 -Nome AdicionaObservacao -Executar
.\scripts\gerar-migrations.ps1 -Provedor Sqlite -Nome AdicionaObservacao
```

O script aplica no banco local (`-Executar`) e gera scripts SQL em `docs/migrations/` (`-Sql`). O script SQL completo do SQL Server está em `docs/migrations/sqlserver-full.sql`. Faça backup antes de aplicar.

---

## 🔐 Configuração e Variáveis de Ambiente

Variáveis de ambiente usam `__` no lugar de `:` (ex.: `Deployment__RequireHttps=false`).

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
| `Setup:AdminUser` / `Setup:AdminPassword` | — | Usuário e senha do administrador criado por `--initialize` (use só na primeira execução). |
| `Serilog:MinimumLevel` | `Information` | Nível de log. Os logs vão para o console e para `Storage:Directory/logs`, com 14 dias de retenção. |

---

## 📦 Implantação

Os scripts ficam em `scripts/` (PowerShell):

| Script | Função |
|---|---|
| `publish.ps1` | Publica em Release (dependente do framework, cerca de 20 MB). Requer o runtime ASP.NET Core 10 no servidor. |
| `install-windows-service.ps1` | Como Administrador: cria a pasta de dados, inicializa o banco, registra o serviço com início automático e reinício em falha. |
| `backup.ps1` | Backup consistente com o serviço rodando, com retenção em dias. |
| `gerar-migrations.ps1` | Gera e aplica migrations por provedor. |

### Rede interna

```powershell
.\scripts\publish.ps1 -Output C:\PainelSenhas\publish
.\scripts\install-windows-service.ps1 -PublishDir C:\PainelSenhas\publish -Port 5000
```

O serviço escuta em HTTP na porta informada, com `Deployment__RequireHttps=false`. Libere a porta no firewall e acesse `http://<servidor>:5000` nos terminais. Com certificado interno, use `-RequireHttps true` e configure o HTTPS do Kestrel. Impeça a suspensão do computador servidor.

> ⚠️ Sem HTTPS o login e a sessão trafegam sem criptografia; use apenas em rede confiável.

### Hospedagem online

Publique com `scripts\publish.ps1 -Runtime linux-x64` (ou `win-x64`) e rode atrás de HTTPS (proxy reverso ou Kestrel com certificado). Mantenha `Deployment:RequireHttps=true`, `Storage:Directory` em volume persistente (chaves de login e logs) e `ConnectionStrings__SqlServer` apontando para o SQL Server de produção. As escritas são serializadas por um bloqueio de aplicação no banco, então o SQL Server aceita mais de uma instância, mas o sinal de atualização das telas é por processo; para respostas imediatas entre telas prefira uma única instância. Com SQLite, use uma só e não coloque o `.db` em compartilhamento de rede. Defina `AllowedHosts` com o domínio. Monitore `/health`.

### Atualizar

Pare o serviço, publique por cima da pasta, inicie o serviço. As migrations pendentes são aplicadas com `PainelSenhas.Web.exe --initialize` (com `ConnectionStrings__SqlServer` e `Storage__Directory` definidos); ele não recria nada que já exista.

### Backup e restauração

```powershell
.\scripts\backup.ps1 -PublishDir C:\PainelSenhas\publish -DataDir C:\ProgramData\PainelSenhas -BackupDir D:\Backups\PainelSenhas
```

Com SQL Server, prefira o backup nativo do servidor (job do SQL Server Agent ou `BACKUP DATABASE`); `--backup` também funciona, mas o arquivo é escrito pelo próprio servidor, então o destino precisa ser acessível a ele. O exemplo de agendamento diário (Agendador de Tarefas) está no cabeçalho do script. Para restaurar o SQLite: pare o serviço, substitua `painel.db` por uma cópia (apague `painel.db-wal` e `painel.db-shm`) e inicie o serviço. As chaves de login (`keys/`) não fazem parte do backup: se forem perdidas, os usuários só precisam entrar de novo. Copie os backups para outro dispositivo e teste a restauração.

---

## 🖨️ Impressão da senha

A impressão é feita pelo navegador do computador da recepção, igual na rede interna e na hospedagem: a impressora fica ao lado de quem emite.

- **Térmica de cupom 80mm:** deixe-a como impressora padrão do Windows (driver do fabricante, papel de 80mm). Para imprimir sem diálogo, abra o navegador da recepção em modo de impressão silenciosa:
  `chrome.exe --kiosk-printing http://<servidor>:5000/emissao` (no Edge: `msedge.exe --kiosk-printing ...`). Com `Printing:AutoPrint=true` (padrão) a senha imprime sozinha logo após o clique no departamento; o botão **Reimprimir** (última senha emitida) cobre falta de papel. Sem `--kiosk-printing`, o diálogo de impressão abre a cada emissão.
- **Impressora comum:** use `Printing:Layout=A4`. O diálogo de impressão do navegador aparece normalmente; o comprovante sai em um cartão centralizado.

O layout do comprovante foi validado apenas na tela; confira a primeira impressão no equipamento real (largura útil, corte e margens do driver). Navegadores de celular e tablet não imprimem em silêncio: a emissão deve ficar em um computador Windows com a impressora instalada.

---

## 📊 Gestão

Quatro áreas, cada uma com endereço próprio (dá para favoritar), só para o administrador:

- **Visão geral** (`/gestao`, tela inicial): acompanha a fila em tempo real. Indicadores de hoje (emitidas, na fila, em atendimento, concluídas, ausentes, espera e atendimento médios, maior espera agora) e um cartão por departamento com a senha em atendimento e há quanto tempo, o movimento do dia e a espera da senha mais antiga da fila. Senhas de dias anteriores que ainda estão na fila são sinalizadas. A maior espera fica em alerta a partir de `Management:LongWaitMinutes`.
- **Histórico** (`/gestao/historico`): atalhos de período (hoje, ontem, 7 e 30 dias, este mês, personalizado), departamento, status e busca pelo código (`T-012`, `012` ou só o prefixo `T`). Qualquer mudança consulta na hora; as colunas principais ordenam; a tabela mostra espera, atendimento e quem atendeu (operador e guichê). O período é limitado a 366 dias.
- **Departamentos** (`/gestao/departamentos`) e **Equipe** (`/gestao/equipe`): cadastro por diálogos. Departamentos com histórico são inativados, nunca excluídos. Ao criar um acesso ou redefinir uma senha, **Gerar senha segura** cria uma senha de 16 caracteres, que fica visível para o administrador repassá-la.

**Exportar para Excel:** o botão do histórico baixa uma planilha com os mesmos filtros da tela, com a aba *Resumo* (período, filtros, totais e quadro por departamento) e a aba *Senhas* (uma linha por senha). Horários no fuso da unidade e tempos em minutos numéricos. O endereço é `GET /gestao/exportar?from=AAAA-MM-DD&to=AAAA-MM-DD[&department=][&status=][&search=]`, restrito ao administrador, com limite de 366 dias e 100.000 linhas.

---

## 📺 Painel da TV

Abra `http://<servidor>:5000/painel` em um navegador atualizado na TV, clique em **Ativar som** (o navegador só libera áudio depois de um clique) e em **Tela cheia**. Se a conexão cair, o painel tenta reconectar sozinho e recarrega a página quando a sessão expira. Depois de recarregar, o som precisa ser ativado de novo.

### Som e destaque da chamada

- **Som:** a cada chamada nova ou repetida o painel toca um sinal de sino (cerca de 2,2 s) gerado no próprio navegador com Web Audio, em `Components/Pages/Board.razor.js`. Não há arquivo de áudio nem licença de terceiros. A senha **comum** toca um "ding-dong" descendente (mi-dó) e a **preferencial** toca três notas ascendentes (dó-mi-sol), para que a equipe distinga uma da outra sem olhar a TV. Ajuste o volume na própria TV. Para mudar o som, edite as notas `COMMON_CHIME` e `PREFERENTIAL_CHIME` (frequências em Hz e atraso em segundos) e o ganho `MASTER_LEVEL`.
- **Ícone da aba:** `wwwroot/favicon.svg` (navegadores atuais) e `wwwroot/favicon.ico` (16, 32 e 48 px), declarados em `Components/App.razor`.
- **Destaque:** o card **Senha chamada** pulsa por **4 segundos** a cada chamada nova (também na repetição), mesmo com o som desligado. São 4 pulsos de 1 s: menos de 3 piscadas por segundo (WCAG 2.3.1, segurança para fotossensíveis) e menos de 5 s (WCAG 2.2.2), e o destaque dura mais que o som. Ao abrir ou reconectar o painel o card aparece parado. Quem usa o sistema com "reduzir movimento" ativado recebe um halo fixo pelos mesmos 4 s. Para mudar a duração, altere `animation` em `Components/Pages/Board.razor.css` (`1s ... 4` = 4 pulsos de 1 s).

---

## 📁 Estrutura de Pastas

| Pasta | Descrição |
|---|---|
| `src/PainelSenhas.Web/Features` | Operações por funcionalidade: emissão, atendimento, painel, gestão, conta e health |
| `src/PainelSenhas.Web/Common` | `Result`, regras de acesso, escrita transacional, relógio e descoberta de endpoints |
| `src/PainelSenhas.Web/Data` | `DbContext`s, entidades e migrations (uma pasta por provedor) |
| `src/PainelSenhas.Web/Components` | Páginas, componentes globais, layout e componentes compartilhados |
| `tests/PainelSenhas.Tests` | Testes de integração com xUnit v3, Shouldly e Respawn |
| `scripts` | Publicação, serviço do Windows, backup e migrations |
| `docs` | Protótipo original e script SQL completo do SQL Server |

---

## 🚀💻 Tecnologias e Ferramentas

<p align="left">

  <img src="https://img.shields.io/badge/Visual_Studio-5C2D91?style=for-the-badge&logo=visual%20studio&logoColor=white" />
  <img src="https://img.shields.io/badge/Visual_Studio_Code-0078D4?style=for-the-badge&logo=visual%20studio%20code&logoColor=white" />

  <img src="https://img.shields.io/badge/C%23-239120?style=for-the-badge&logo=csharp&logoColor=white" />
  <img src="https://img.shields.io/badge/HTML5-E34F26?style=for-the-badge&logo=html5&logoColor=white" />
  <img src="https://img.shields.io/badge/CSS3-1572B6?style=for-the-badge&logo=css3&logoColor=white" />
  <img src="https://img.shields.io/badge/JavaScript-323330?style=for-the-badge&logo=javascript&logoColor=F7DF1E" />
  <img src="https://img.shields.io/badge/json-5E5C5C?style=for-the-badge&logo=json&logoColor=white" />
  <img src="https://img.shields.io/badge/PowerShell-5391FE?style=for-the-badge&logo=powershell&logoColor=white" />

  <img src="https://img.shields.io/badge/.NET_10-512BD4?style=for-the-badge&logo=dotnet&logoColor=white" />
  <img src="https://img.shields.io/badge/Blazor-512BD4?style=for-the-badge&logo=blazor&logoColor=white" />
  <img src="https://img.shields.io/badge/MudBlazor-594AE2?style=for-the-badge&logo=blazor&logoColor=white" />
  <img src="https://img.shields.io/badge/Entity_Framework_Core-68217A?style=for-the-badge&logo=.net&logoColor=white" />
  <img src="https://img.shields.io/badge/ASP.NET_Identity-512BD4?style=for-the-badge&logo=dotnet&logoColor=white" />
  <img src="https://img.shields.io/badge/Serilog-0E0E0E?style=for-the-badge&logo=serilog&logoColor=white" />
  <img src="https://img.shields.io/badge/FluentValidation-3E7FC1?style=for-the-badge&logo=fluentvalidation&logoColor=white" />
  <img src="https://img.shields.io/badge/ClosedXML-217346?style=for-the-badge&logo=microsoft%20excel&logoColor=white" />
  <img src="https://img.shields.io/badge/xUnit-512BD4?style=for-the-badge&logo=dotnet&logoColor=white" />
  <img src="https://img.shields.io/badge/Shouldly-2088FF?style=for-the-badge&logo=checkmarx&logoColor=white" />
  <img src="https://img.shields.io/badge/Microsoft%20SQL%20Server-CC2927?style=for-the-badge&logo=microsoft%20sql%20server&logoColor=white" />
  <img src="https://img.shields.io/badge/Sqlite-003B57?style=for-the-badge&logo=sqlite&logoColor=white" />
  <img src="https://img.shields.io/badge/Azure_DevOps-0078D7?style=for-the-badge&logo=azure-devops&logoColor=white" />
  <img src="https://img.shields.io/badge/Git-F05032?style=for-the-badge&logo=git&logoColor=white" />
</p>
