# Histórico de versões

Versionamento semântico. A versão do sistema fica em `src/PainelSenhas.Web/PainelSenhas.Web.csproj` (`<Version>`) e aparece no rodapé das telas.

## Não lançado

**Favicon e som do painel**
- **Favicon:** ícone de cupom nas cores do tema (`favicon.svg` e `favicon.ico`), declarado em todas as páginas. Antes o navegador pedia `/favicon.ico` e recebia 404.
- **Som do painel:** o sinal de aeroporto (arquivo `chamada.ogg` e `LICENCA.md`) foi substituído por um sino sintetizado, mais próximo de banco e repartição: "ding-dong" descendente para senha comum e três notas ascendentes para preferencial. Sem arquivo de áudio e sem pendência de licença; o ganho foi ajustado para não estourar e o compressor evita estalos quando duas chamadas se sobrepõem.

**Senhas preferenciais**
- **Emissão:** cada departamento ganhou um botão **Preferencial** abaixo do botão comum; o comprovante sai com a marca PREFERENCIAL.
- **Regra de chamada (todos os departamentos):** preferenciais são sempre chamadas antes das comuns; dentro de cada grupo vale a ordem de emissão (data operacional e número). A numeração do departamento é compartilhada.
- **Telas:** etiqueta Preferencial na fila do atendimento, na senha em atendimento e no painel da TV; a lista "Próximas senhas" já mostra a ordem real de chamada.
- Migration `SenhasPreferenciais` (coluna `Tickets.IsPreferential`; as senhas existentes ficam como comuns).

**SQL Server como banco padrão**
- Nova conexão `ConnectionStrings:SqlServer` (LocalDB `(localdb)\MSSQLLocalDB` em desenvolvimento; produção define a própria por variável de ambiente). `Database:Provider` escolhe o provedor e o padrão é `SqlServer`; `Sqlite` continua disponível.
- Migrations do SQL Server em `Data/Migrations/SqlServer` (contexto `SqlServerDbContext`), separadas das do SQLite. `scripts/gerar-migrations.ps1` ganhou `-Provedor`.
- As escritas continuam serializadas: `BEGIN IMMEDIATE` no SQLite e `sp_getapplock` no SQL Server, mantendo numeração e chamadas sem duplicidade.
- Health check, `--backup` e `--initialize` funcionam nos dois provedores. Testes podem rodar contra o SQL Server com `PAINEL_TESTS_SQLSERVER`.

**Senha de exemplo**
- Ao iniciar em Development (ou com `Seed:Ticket=true`), a fila abre com uma senha de exemplo no primeiro departamento.

## 0.0.1 · 12/10/2026

Primeira versão utilizável do controle de senhas entre departamentos (um guichê por departamento).

**Telas**
- **Emissão:** um clique no departamento emite e imprime a senha (cupom térmico 80mm ou A4), com trava de 2 s contra clique duplo e reimpressão da última senha.
- **Atendimento:** uma aba por departamento permitido (`/atendimento/T`); chamar a próxima, repetir, concluir e registrar ausência. Um operador pode atender vários departamentos.
- **Painel da TV:** última chamada em destaque, resumo por departamento, som de chamada (CC0) e destaque pulsante de 4 s a cada chamada.
- **Gestão:** visão geral ao vivo, histórico com filtros, busca e ordenação, exportação para Excel e cadastro de departamentos e equipe por diálogos.
- **Conta:** login, troca da própria senha e recuperação do administrador por linha de comando (`--reset-admin`).

**Arquitetura e operação**
- Blazor Web App .NET 10 (Interactive Server), MudBlazor, EF Core 10 com SQLite e ASP.NET Identity.
- Numeração diária e chamadas em transação curta do SQLite, com chaves de idempotência (sem senhas duplicadas por clique duplo ou reenvio).
- Mesma base de código para rede interna (sem HTTPS) e hospedagem online (`Deployment:RequireHttps`).
- Scripts de publicação, serviço do Windows e backup em `scripts/`; `/health` para monitoramento.
- 126 testes de integração com SQLite real.
