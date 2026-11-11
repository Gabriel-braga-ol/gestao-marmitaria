# Progresso — Gestão Marmitaria

Atualizado em 11/11/2026 (Brasília). Base: código e histórico Git lidos em 07/11; build e testes executados em 11/11. Validação manual em PostgreSQL e testes de integração executados em 11/11.
O banco real não foi alterado; os testes de integração criam e apagam um banco próprio. Hashes só aparecem onde foram vistos no Git.

## Ponto de retomada

- **Último marco:** testes de integração HTTP/PostgreSQL (11/11): 45 casos novos cobrindo `401`/`403`, login e registro, filtros, datas, pagamento, reembolso, cardápio, exclusões e concorrência. A suíte também revelou e corrigiu o `500` quando o `40001` vinha envolvido pelo EF. Antes, validação manual em PostgreSQL (11/11) e robustez/seed (07/11).
- **Próxima tarefa:** auditoria de quem altera/cancela pedidos (o sistema ainda não registra o usuário) e, depois, rodar build + testes em CI com PostgreSQL.
- **Depois:** relatório financeiro, carga alta, deploy e frontend.
- Não repita os testes aprovados em 11/11: os 45 de integração (autenticação, pedidos/filtros, pagamento/reembolso, cardápio, produtos/clientes e concorrência) e os cenários manuais de seed, `409` de `tipo`/exclusão e cancelamento por Funcionário.
- Arquivos: `README.md` e `AGENTS.md` na raiz; `PROGRESSO.md` em `docs/`.

## Marcos concluídos

| Marco | Entrega |
| --- | --- |
| Base | API .NET 10, PostgreSQL, EF Core, migrations |
| Produtos e clientes | CRUD, validações, telefone normalizado, exclusão bloqueada se houver vínculo |
| Pedidos | Itens, preço histórico, total, entrega/retirada com taxa configurável, status por fluxo |
| Consulta | Filtros por status, cliente, período (Brasília → UTC), pago e reembolso pendente; paginação |
| Testes unitários | 51 testes: 28 de status/cancelamento + 23 de falhas de banco, JWT e seed do Admin (07/11); 52 com o caso do `40001` envolvido (11/11) |
| Testes de integração | 45 testes HTTP/PostgreSQL com `WebApplicationFactory`, banco criado/apagado por execução, cobrindo `401`/`403`, filtros/datas, pagamento, reembolso, cardápio, exclusões e concorrência (11/11) |
| Pagamentos | Pagamento integral único por pedido, valor calculado no backend |
| Cancelamento e reembolso | Cancelar em Novo/EmPreparo; reembolso integral manual, único por pagamento |
| Cardápio diário | Tipos de produto, itens por data, disponibilidade e validação ao criar pedido |
| Tipo vinculado | PUT retorna `409` ao mudar o `tipo` de produto no cardápio (`1d646d7`) |
| Transação em pedidos | Serializable (`e2f1b70`); `CommitAsync` adicionado em 06/11 |
| Autenticação JWT | `Usuario`, `PasswordHasher`, `TokenService`, registrar/login, `[Authorize]` (`35e9d48`) |
| Perfis | `Roles = "Admin"` em registrar, produtos (POST/PUT/DELETE), clientes (PUT/DELETE), POST do cardápio e POST de reembolso; `[AllowAnonymous]` no login |
| Limpeza de segurança | Removidos `ShowPII`, `Debug.WriteLine` e `RequireHttpsMetadata`; `TokenService` com UTF8; `WeatherForecast` do template removido; mensagens da autenticação em pt-BR (06/11) |
| Robustez e seed | JWT validado na subida, seed `AdminInicial` idempotente, transações em status/pagamento/disponibilidade e helper `FalhasBanco` (07/11) |
| Validação em PostgreSQL | Seed, `409` de `tipo`/exclusão, cancelamento por Funcionário e concorrência validados manualmente em banco separado (11/11) |

## Decisões que orientam a continuidade

- `RegrasStatusPedido` é uma classe estática chamada pelo PATCH; os controllers usam o `AppDbContext` direto, sem camada de serviços.
- `AplicarFiltros` compõe o `IQueryable` antes de `CountAsync`/`ToListAsync`; `BuscarPaginaAsync` ordena por `DataCriacao` e `Id` decrescentes e retorna lista vazia em página excedente.
- `CarregarProdutosDoPedidoAsync` valida existência, `Ativo` e cardápio de hoje; `MontarPedido` grava os dados históricos e salva pedido e itens de uma vez.
- Período de datas: início inclusivo e dia seguinte exclusivo, convertidos de Brasília para UTC; data final `9999-12-31` retorna `400`.
- `Entrega:Taxa` (5,00) é lido no construtor de `PedidosController`; sem ele a API falha ao atender pedidos.
- Pagamento e reembolso: índices únicos (`IX_Pagamentos_PedidoId`, `IX_Reembolsos_PagamentoId`) com FK Restrict; só a violação do índice esperado vira `409`. Pendência de reembolso é inferida (cancelado + pagamento + sem reembolso), sem coluna própria.
- `FalhasBanco` centraliza a detecção de `40001` (percorrendo a cadeia de exceções, inclusive o `InvalidOperationException` de falha transiente do EF) e da violação do índice único esperado, usada por pedidos e cardápio.
- Cardápio: `IX_ItensCardapio_Data_ProdutoId` único; nome e preço vêm do catálogo atual, enquanto os do pedido são históricos. Migrations `AdicionarTipoProduto` e `CriarTabelaItensCardapio` já aplicadas; não reescrever, versionar designer e snapshot.
- Validações manuais usam `mensagem`; Data Annotations mantêm o `400` com `errors`.
- Permissões: Funcionário também cancela pedido; só o Admin confirma reembolso e gerencia produtos, usuários, cardápio e edição/exclusão de clientes. Relatório financeiro, previsto para o Admin, não existe ainda.
- JWT: `JwtSettings.Validar` exige chave com pelo menos 32 caracteres, emissor, audiência e expiração maior que zero; `appsettings.json` deixa a chave vazia e o valor real fica em User Secrets.
- Seed: `AdminInicialSeeder` só cria o Admin quando não existe nenhum usuário Admin; se o e-mail já estiver cadastrado, apenas registra aviso e não altera o usuário. Não roda em design-time (`EF.IsDesignTime`), então não interfere em `dotnet ef`.
- Transações: criação de pedido, atualização de status, registro de pagamento e PATCH de disponibilidade usam Serializable com `CommitAsync`; conflito `40001` retorna `409`, inclusive quando o EF o envolve (coberto por teste de integração).
- `UseHttpsRedirection` comentado: o `307` fazia o cliente descartar `Authorization` (`401`). Reativar com HTTPS no deploy.
- Dependências: `Microsoft.EntityFrameworkCore.Relational` 10.0.13 adicionado para alinhar com o EFCore.Design; `Microsoft.EntityFrameworkCore.InMemory` 10.0.13 foi adicionado ao projeto de testes para o seed; `Microsoft.AspNetCore.Mvc.Testing` 10.0.13 sustenta os testes de integração. O build de 11/11 terminou com 0 avisos.

## Histórico por sessão

**Até 23/10:** base da API, CRUD, pedidos, entrega/retirada, status, paginação, testes unitários, filtro de período e refatoração em auxiliares.

**30/10, pagamentos:** modelo `Pagamento` (numeric(18,2), data UTC), migration aplicada, GET/POST em `/pedidos/{id}/pagamento`. Verificado manualmente: `201`, `200`, repetição `409`, enum inválido `400`, inexistente `404`, persistência após reinício. A proteção contra pagamentos simultâneos existe no banco, mas não foi exercitada.

**31/10, filtros, cancelamento e reembolso:** `pago` e `reembolsoPendente` (`bool?`), `Cancelado=6` na regra de status (suíte de 15 para 28 casos), modelo `Reembolso`, migration aplicada, GET/POST de reembolso e correção de uma `NullReferenceException` no GET sem reembolso. Pedidos 1 e 5 foram usados para provar a pendência e o reembolso; 11 e 12, para os bloqueios.

**01/11, cardápio diário:** `TipoProduto` (migration com DEFAULT 1 para produtos antigos), `ItemCardapio` (migration aplicada), `CardapioController` (GET por data e por ID, POST, PATCH de disponibilidade) e validação em `CarregarProdutosDoPedidoAsync`. Confirmado manualmente: cadastro, duplicidade, entradas inválidas, isolamento entre dias, e pedidos com marmita disponível, indisponível ou fora do cardápio, bebida e pedido misto. Saída de 01/11: 28 testes aprovados (4,8 s) e 3 avisos MSB3277.

**05/11, tipo, transação e JWT:** bloqueio de troca de `tipo` (`1d646d7`); `CriarPedidoAsync` em transação Serializable com `DbUpdateException` → `409` (`e2f1b70`); autenticação JWT (`35e9d48`) com migration `20261106005946_CriarTabelaUsuarios`. O resumo da sessão (conduzida com o Gemini) citava `[AllowAnonymous]`, que não existia no código; foi adicionado em 06/11. O `401` do redirecionamento HTTPS foi diagnosticado como descrito acima.

**06/11, perfis e correção da transação:** o código lido não tinha `CommitAsync`, então a transação era descartada sem confirmar. Com a linha adicionada, o pedido 17 (19:41) apareceu no pgAdmin sem buraco na sequência. Cenários de Admin, Funcionário e sem token (registrar, clientes, produtos, cardápio e reembolso) rodados no `.http`, todos conforme o esperado. Não houve teste de cancelamento por Funcionário, de concorrência nem de falha de serialização.

**06/11, noite, limpeza:** Limpeza do `Program.cs` e do `TokenService`, remoção do `WeatherForecastController` e padronização das mensagens ("registrado", "Usuário"). Testes L1 a L9 do `.http` executados (login, `200`/`401` em produtos, token adulterado, senha errada, `404` no weatherforecast, registrar repetido e novo); resultados individuais não registrados e código não relido depois da mudança. Teste de concorrência adiado.

**07/11, robustez e seed:** `JwtSettings.Validar` exige chave com pelo menos 32 caracteres e falha na subida; `appsettings.json` passou a deixar a chave vazia. `AdminInicialSeeder` cria o primeiro Admin de forma idempotente via User Secrets. `FalhasBanco` centraliza `40001` e violação de índice único. O PATCH de status, o registro de pagamento e o PATCH de disponibilidade passaram a usar transação Serializable com `409` em conflito. Build com 0 avisos e 51 testes aprovados. Não testado em HTTP/PostgreSQL: concorrência real, seed em banco novo e corrida pagamento × cancelamento.

**07/11, limpeza do `.http`:** removidas as requisições sem `Authorization` (bloco legado, `weatherforecast` e as variáveis `@baseUrl` e `@pedidoPagamentoId`, sem uso). Mantidos o `login`, os testes intencionais de `401` e as declarações de variáveis. O arquivo caiu de 1300 para 224 linhas; os cenários antigos de filtros, datas, pagamento, cancelamento e status precisam ser recriados quando forem testados.

**11/11, validação manual em PostgreSQL:** banco separado `gestao_marmitaria_val_20261111a` criado pelas migrations; API em `http://localhost:5150` com `--no-build`. O seed criou o Admin e o login retornou `200`; reiniciar a API não duplicou o Admin. Confirmados `409` na troca de `tipo` de produto vinculado ao cardápio e na exclusão de produto vinculado só ao cardápio, além do cancelamento por Funcionário (`204`, status `6`). Concorrência: dois pagamentos simultâneos → `201`+`409` com um único registro; duas mudanças de status → `204`+`409` com status final `2`; pagamento × cancelamento → `201`+`204` (cancelado com pagamento, reembolso pendente) quando o pagamento venceu e `204`+`409` quando o cancelamento venceu (10/10); pedido × disponibilidade → `201`+`204` sem `500`; 5 pedidos simultâneos → 5×`201`. Nenhum `fail`/exceção no log. O conflito de serialização (`40001`) não foi observado; os `409` vieram de regra de negócio ou índice único.

**11/11, testes de integração:** `WebApplicationFactory<Program>` contra PostgreSQL local. A fixture lê a conexão dos User Secrets, cria um banco `gm_test_*` pelas migrations, injeta `ConnectionStrings`, `Jwt:ChaveSecreta` e `AdminInicial` por variáveis de ambiente e apaga o banco com `DROP DATABASE ... WITH (FORCE)` no fim. São 45 casos: `401`/`403`, login/registro, pedidos (retirada, entrega, produto repetido, marmita fora do cardápio, bebida, dados históricos), filtros (status, cliente, pago, reembolso pendente, data Brasília → UTC, paginação, `dataFinal` 9999), pagamento (valor, duplicidade, cancelado, status), reembolso (regras, valor, unicidade, perfil), cardápio (bebida, inativa, duplicidade, consulta, disponibilidade) e concorrência (pagamentos, status, pagamento × cancelamento, 5 pedidos). O caso de pagamentos simultâneos expôs um `500`: o EF envolve o `40001` em `InvalidOperationException` ("transient failure") e `FalhasBanco` só olhava um nível. `EhFalhaDeSerializacao` passou a percorrer a cadeia de exceções, com teste unitário novo. Suíte completa: 97 aprovados (52 unitários + 45 de integração), build com 0 avisos.

**11/11, revisão de segurança — tarefa 1 (segredos no `.http`):** removidos os JWTs fixos (`@token`, `@tokenFuncionario`) e as senhas reais do `GestaoMarmitaria.Api.http`. O arquivo passou a usar `http-client.env.json` (ignorado pelo Git) com valores fictícios no `http-client.env.json.example`, e os tokens são capturados das respostas `loginAdmin` e `loginFuncionario` (`# @name` + `{{login.response.body.token}}`). README documenta o fluxo. Nenhum código de aplicação mudou; build com 0 avisos e 97 testes aprovados em 11/11. Não verificado: execução do `.http` nos clientes HTTP nem rotação da chave/senhas (ação manual pendente).

**11/11, revisão de segurança — tarefa 2 (força bruta e política de senha):** rate limiting por IP em `POST /api/autenticacao/login` via `AddRateLimiter`/`UseRateLimiter`, política `login` com janela fixa e limites em `RateLimiting:Login` (`LimiteTentativas` 5, `JanelaEmSegundos` 60 no `appsettings.json`); o excedente retorna `429` com `Retry-After` e mensagem JSON. `[EnableRateLimiting("login")]` fica só na ação de login. Senha passou a exigir 10 a 128 caracteres (`PoliticaSenha`, `RegistrarUsuarioDto` e `AdminInicialSettings`), com o seed ajustado. `ILogger` registra tentativas inválidas com o e-mail, sem senha, token ou hash. Testes: 13 casos novos (limites de senha no cadastro e no seed, validação das configurações de rate limit e `429` por uma instância isolada com limite 3); suíte com 110 aprovados (61 unitários + 49 de integração) e build com 0 avisos. Não verificado: rate limiting distribuído (o estado é em memória por processo) nem execução real do `.http`.

**11/11, revisão de segurança — tarefa 3 (tempo de resposta e e-mail normalizado):** o login passou a usar `AsNoTracking()`, normaliza o e-mail (Trim + minúsculas) e, quando o e-mail não existe, executa `VerifyHashedPassword` contra um hash fictício estático para gastar o mesmo tempo do caminho com usuário real, mantendo o `400` com mensagem única. A normalização vale no cadastro (inclusive no setter do DTO, antes das Data Annotations), no login e no seed (`NormalizacaoEmail`). Migration `NormalizarEmailsUsuarios`: `Up()` normaliza `Usuarios.Email` com `lower(btrim(...))` e falha com `RAISE EXCEPTION` se houver e-mails equivalentes que colidam; `Down()` é no-op documentado, pois a caixa e os espaços originais não são armazenados. Testes: 8 casos novos (normalização unitária, seed, login com caixa/espaços, registro normalizado e duplicidade por caixa); suíte com 118 aprovados (66 unitários + 52 de integração) e build com 0 avisos. Não verificado: igualdade estatística de tempo sob carga e aplicação da migration em banco real com dados (a suíte exercita o `Up()` em banco vazio).

## Git

- Commits vistos no clone de 06/11 (recentes primeiro): `35e9d48` autenticação JWT, `e2f1b70` transação, `1d646d7` tipo vinculado, `f7a42d2` docs do cardápio, `6f7ff3a` cardápio diário. Estão no remoto.
- Commits de sessões anteriores constam só por mensagem, sem hash; não inventar hashes.
- Alterações de 06/11 (perfis, `CommitAsync`, limpeza, documentação): `abde12f` (docs, 06/11); `c310de8` (perfis, `CommitAsync` e limpeza do JWT), `46efadf` e `7adf32b` (docs, 07/11). O `main` local está sem *ahead/behind* em relação a `origin/main`; confirmar o envio ao remoto com `git fetch` antes de afirmar.
- Alterações de 07/11: `2c8bf27` (JWT, seed e `FalhasBanco`), `f63405b` (transações), `c8d51e9` (testes), `b3226ea` (documentação) e a limpeza do `.http` neste commit. Build e testes de 07/11: 0 avisos e 51 testes aprovados.
- Alterações de 11/11: validação manual em banco separado (`3e8918a`, `9fd1420`); testes de integração e correção do `40001` em `34748ed` (fix) e `fc9af8c` (test), com esta documentação no commit seguinte. O banco real não foi alterado e o banco de validação foi mantido.

## Pendências

- Avaliar auditoria de quem cancela ou altera pedidos (o sistema ainda não registra o usuário).
- Rodar build e testes em CI com PostgreSQL como serviço; avaliar carga alta e concorrência com mais clientes e produtos.
- Frontend React/TypeScript, relatórios (financeiro), Swagger UI e deploy; avaliar pesquisa e filtros de produtos e clientes.
