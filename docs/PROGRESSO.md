# Progresso — Gestão Marmitaria

Atualizado em 07/11/2026 (Brasília). Base: código e histórico Git lidos em 07/11; build e testes executados em 07/11. Testes manuais de 06/11.
API e banco não foram reexecutados nesta revisão. Hashes só aparecem onde foram vistos no Git.

## Ponto de retomada

- **Último marco:** robustez e seed do Admin (07/11): validação do JWT na subida, seed `AdminInicial` idempotente, transações Serializable em status, pagamento e disponibilidade, helper `FalhasBanco` e 51 testes aprovados.
- **Próxima tarefa:** testar a concorrência real (pedidos, pagamentos, disponibilidade e cancelamento × pagamento) e o seed do Admin em banco novo.
- **Depois:** testes pendentes (cancelamento por Funcionário, troca de `tipo`, exclusão de produto vinculado só ao cardápio) e testes de integração HTTP/PostgreSQL.
- Pendente de teste: DELETE de produto vinculado **somente** ao cardápio e a troca de `tipo` (recrie as requisições no `.http`, com `Authorization`, usando produto de teste, nunca um com pedidos).
- Não repita testes aprovados nem recrie como pendentes os filtros, o cancelamento, o reembolso ou o cardápio.
- Arquivos: `README.md` e `AGENTS.md` na raiz; `PROGRESSO.md` em `docs/`.

## Marcos concluídos

| Marco | Entrega |
| --- | --- |
| Base | API .NET 10, PostgreSQL, EF Core, migrations |
| Produtos e clientes | CRUD, validações, telefone normalizado, exclusão bloqueada se houver vínculo |
| Pedidos | Itens, preço histórico, total, entrega/retirada com taxa configurável, status por fluxo |
| Consulta | Filtros por status, cliente, período (Brasília → UTC), pago e reembolso pendente; paginação |
| Testes unitários | 51 testes: 28 de status/cancelamento + 23 de falhas de banco, JWT e seed do Admin (07/11) |
| Pagamentos | Pagamento integral único por pedido, valor calculado no backend |
| Cancelamento e reembolso | Cancelar em Novo/EmPreparo; reembolso integral manual, único por pagamento |
| Cardápio diário | Tipos de produto, itens por data, disponibilidade e validação ao criar pedido |
| Tipo vinculado | PUT retorna `409` ao mudar o `tipo` de produto no cardápio (`1d646d7`) |
| Transação em pedidos | Serializable (`e2f1b70`); `CommitAsync` adicionado em 06/11 |
| Autenticação JWT | `Usuario`, `PasswordHasher`, `TokenService`, registrar/login, `[Authorize]` (`35e9d48`) |
| Perfis | `Roles = "Admin"` em registrar, produtos (POST/PUT/DELETE), clientes (PUT/DELETE), POST do cardápio e POST de reembolso; `[AllowAnonymous]` no login |
| Limpeza de segurança | Removidos `ShowPII`, `Debug.WriteLine` e `RequireHttpsMetadata`; `TokenService` com UTF8; `WeatherForecast` do template removido; mensagens da autenticação em pt-BR (06/11) |
| Robustez e seed | JWT validado na subida, seed `AdminInicial` idempotente, transações em status/pagamento/disponibilidade e helper `FalhasBanco` (07/11) |

## Decisões que orientam a continuidade

- `RegrasStatusPedido` é uma classe estática chamada pelo PATCH; os controllers usam o `AppDbContext` direto, sem camada de serviços.
- `AplicarFiltros` compõe o `IQueryable` antes de `CountAsync`/`ToListAsync`; `BuscarPaginaAsync` ordena por `DataCriacao` e `Id` decrescentes e retorna lista vazia em página excedente.
- `CarregarProdutosDoPedidoAsync` valida existência, `Ativo` e cardápio de hoje; `MontarPedido` grava os dados históricos e salva pedido e itens de uma vez.
- Período de datas: início inclusivo e dia seguinte exclusivo, convertidos de Brasília para UTC; data final `9999-12-31` retorna `400`.
- `Entrega:Taxa` (5,00) é lido no construtor de `PedidosController`; sem ele a API falha ao atender pedidos.
- Pagamento e reembolso: índices únicos (`IX_Pagamentos_PedidoId`, `IX_Reembolsos_PagamentoId`) com FK Restrict; só a violação do índice esperado vira `409`. Pendência de reembolso é inferida (cancelado + pagamento + sem reembolso), sem coluna própria.
- `FalhasBanco` centraliza a detecção de `40001` (solta ou dentro de `DbUpdateException`) e da violação do índice único esperado, usada por pedidos e cardápio.
- Cardápio: `IX_ItensCardapio_Data_ProdutoId` único; nome e preço vêm do catálogo atual, enquanto os do pedido são históricos. Migrations `AdicionarTipoProduto` e `CriarTabelaItensCardapio` já aplicadas; não reescrever, versionar designer e snapshot.
- Validações manuais usam `mensagem`; Data Annotations mantêm o `400` com `errors`.
- Permissões: Funcionário também cancela pedido; só o Admin confirma reembolso e gerencia produtos, usuários, cardápio e edição/exclusão de clientes. Relatório financeiro, previsto para o Admin, não existe ainda.
- JWT: `JwtSettings.Validar` exige chave com pelo menos 32 caracteres, emissor, audiência e expiração maior que zero; `appsettings.json` deixa a chave vazia e o valor real fica em User Secrets.
- Seed: `AdminInicialSeeder` só cria o Admin quando não existe nenhum usuário Admin; se o e-mail já estiver cadastrado, apenas registra aviso e não altera o usuário. Não roda em design-time (`EF.IsDesignTime`), então não interfere em `dotnet ef`.
- Transações: criação de pedido, atualização de status, registro de pagamento e PATCH de disponibilidade usam Serializable com `CommitAsync`; conflito `40001` retorna `409`.
- `UseHttpsRedirection` comentado: o `307` fazia o cliente descartar `Authorization` (`401`). Reativar com HTTPS no deploy.
- Dependências: `Microsoft.EntityFrameworkCore.Relational` 10.0.13 adicionado para alinhar com o EFCore.Design; `Microsoft.EntityFrameworkCore.InMemory` 10.0.13 foi adicionado ao projeto de testes para o seed. O build de 07/11 terminou com 0 avisos.

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

## Git

- Commits vistos no clone de 06/11 (recentes primeiro): `35e9d48` autenticação JWT, `e2f1b70` transação, `1d646d7` tipo vinculado, `f7a42d2` docs do cardápio, `6f7ff3a` cardápio diário. Estão no remoto.
- Commits de sessões anteriores constam só por mensagem, sem hash; não inventar hashes.
- Alterações de 06/11 (perfis, `CommitAsync`, limpeza, documentação): `abde12f` (docs, 06/11); `c310de8` (perfis, `CommitAsync` e limpeza do JWT), `46efadf` e `7adf32b` (docs, 07/11). O `main` local está sem *ahead/behind* em relação a `origin/main`; confirmar o envio ao remoto com `git fetch` antes de afirmar.
- Alterações de 07/11: `2c8bf27` (JWT, seed e `FalhasBanco`), `f63405b` (transações), `c8d51e9` (testes), `b3226ea` (documentação) e a limpeza do `.http` neste commit. Build e testes de 07/11: 0 avisos e 51 testes aprovados.

## Pendências

- Testar a concorrência real em `CriarPedidoAsync`, no PATCH de status, no pagamento e no PATCH de disponibilidade; as transações Serializable e o retorno `409` para `40001` estão aplicados, mas não exercitados.
- Corrida entre registrar pagamento e cancelar: mitigada com transações Serializable nos dois fluxos; falta exercitar.
- Avaliar auditoria de quem cancela ou altera pedidos (o sistema ainda não registra o usuário).
- Testar exclusão de produto vinculado só ao cardápio e a troca de `tipo`; testar o cancelamento por Funcionário.
- Testes de integração (filtros, pagamentos, reembolsos, cardápio, autenticação `401`/`403`) e de concorrência.
- Frontend React/TypeScript, relatórios (financeiro), Swagger UI, CI e deploy; avaliar pesquisa e filtros de produtos e clientes.
