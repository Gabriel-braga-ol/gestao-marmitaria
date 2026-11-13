# AGENTS.md

API REST de marmitaria (projeto de portfólio): C# / .NET 10, ASP.NET Core, EF Core + PostgreSQL, JWT.
Conduza como mentoria prática: explique uma tarefa por vez e deixe o usuário implementar, salvo pedido explícito de edição direta. Responda em português do Brasil.

## Comece por aqui

- Leia `docs/PROGRESSO.md` (estado e próxima tarefa) e o código relacionado antes de propor algo. Contrato e execução estão no `README.md`.
- Testes manuais: `GestaoMarmitaria.Api/GestaoMarmitaria.Api.http`.

## Comandos (na raiz)

```bash
dotnet build GestaoMarmitaria.slnx
dotnet test GestaoMarmitaria.slnx
dotnet run --project GestaoMarmitaria.Api --launch-profile http   # http://localhost:5149
dotnet ef migrations add [Nome] --project GestaoMarmitaria.Api
dotnet ef database update --project GestaoMarmitaria.Api
dotnet user-secrets set "Jwt:ChaveSecreta" "[CHAVE]" --project GestaoMarmitaria.Api
```

Conexão (`ConnectionStrings:DefaultConnection`) e `Jwt:ChaveSecreta` ficam em User Secrets; use o ambiente Development.

## Convenções

- Nomes de domínio em português. Métodos assíncronos novos ou alterados terminam em `Async`; ao renomear ações, confira rotas e `CreatedAtAction` (os GETs de pagamento, reembolso e item do cardápio mantêm `ActionName` explícito).
- `AsNoTracking()` em leituras; `decimal` para dinheiro. Métodos assíncronos recebem `CancellationToken` e o repassam ao EF.
- Datas de negócio usam o `TimeProvider` injetado (não `DateTime.UtcNow`) e o fuso de Brasília fica em `FusoBrasilia` (não `DateTime.Today`). `Entrega:Taxa` é validada por `EntregaSettings` com `ValidateOnStart`.
- Erros manuais: `BadRequest(new { mensagem = "..." })`. Preserve o `400` automático com `errors` das Data Annotations.
- Trate como `409` de duplicidade só a violação do índice esperado, nunca qualquer falha do banco.
- Divida métodos que misturem validação, consulta, montagem e persistência; extraia regras independentes de HTTP/banco para componentes testáveis. Sem novas camadas sem necessidade concreta. Controllers orquestram; serviços devolvem resultados tipados (sem `IActionResult`) e usam `ExecucaoTransacional.ExecutarSerializavelAsync` quando a operação exigir transação Serializable.
- Respostas usam DTOs próprios (não exponha entidade nem navegação). Normalize telefone só com `NormalizacaoTelefone` (dígitos), como e-mail usa `NormalizacaoEmail`.
- Commits em pt-BR com prefixo (`feat:`, `fix:`, `docs:`). Limite cada alteração à tarefa acordada.
- A `main` é protegida por ruleset: exige o check `Build e testes` e PR. Trabalhe em branch e abra PR; push direto na `main` é recusado.

## Regras de negócio que não podem quebrar

Pedidos
- Nome/preço dos itens, endereço e taxa são históricos: não recalcule pelo catálogo atual.
- Entrega exige endereço; retirada tem endereço nulo e taxa zero. Rejeite produtos repetidos antes de comparar contagens.
- Fluxo: entrega Novo → EmPreparo → Pronto → SaiuParaEntrega → Entregue; retirada Pronto → Entregue. Sem saltos, retornos ou repetição. Não renumere os status (Cancelado=6, terminal).
- Cancelar só em Novo ou EmPreparo.
- Datas de consulta em Brasília, convertidas para UTC; aplique filtros antes de contagem e paginação.
- Excluir cliente/produto vinculado retorna `409`; nunca apague histórico por cascata.
- `CriarPedidoAsync` roda em transação Serializable e confirma com `CommitAsync` logo após o `SaveChangesAsync`. Mantenha.
- PATCH de status, registro de pagamento e PATCH de disponibilidade também usam transação Serializable com `CommitAsync`; conflito `40001` retorna `409`. Não remova essas transações: elas cobrem a corrida pagamento × cancelamento e a venda no instante em que a disponibilidade muda.

Pagamento e reembolso
- Um pagamento integral por pedido, valor calculado no backend (itens + taxa), data UTC, independente do status. Índice único em `Pagamento.PedidoId`, FK Restrict; novo pagamento em pedido cancelado retorna `409`.
- Reembolso manual: só para pedido cancelado com pagamento, sem corpo, copia `Pagamento.Valor`, um por pagamento (`IX_Reembolsos_PagamentoId`, Restrict). Não apague o pagamento nem altere o status Cancelado.
- `pago` e `reembolsoPendente` são `bool?` (omitido não filtra). Pendente = cancelado + pagamento + sem reembolso; `false` é o complemento; `true` com `pago=false` dá lista vazia, não erro.

Cardápio
- Marmitas exigem `Produto.Ativo` e `ItemCardapio` disponível na data atual de Brasília (`America/Sao_Paulo`, nunca `DateTime.Today`); bebidas exigem só `Ativo`. Disponibilidade é um indicador, sem estoque.
- Valide a disponibilidade em `CarregarProdutosDoPedidoAsync`, com uma consulta para todas as marmitas; rejeite o pedido inteiro se alguma falhar.
- `ItemCardapio`: `DateOnly` + `ProdutoId`, FK Restrict, índice único `IX_ItensCardapio_Data_ProdutoId`. POST só aceita marmita ativa; o PATCH usa `bool?` com `Required`.
- `Tipo` (Marmita=1, Bebida=2) de produto vinculado ao cardápio não pode mudar (PUT retorna `409`).

## Autenticação e perfis

- `[Authorize]` na classe de todo controller, inclusive os novos. São públicos apenas `login` e `renovar`, com `[AllowAnonymous]`. Para restringir ao Admin: `[Authorize(Policy = Politicas.SomenteAdmin)]` na ação (`Papeis.Admin`). Perfis: Admin=1, Funcionario=2 (não renumere).
- Somente Admin: cadastrar usuários; revogar sessões de outro usuário; POST/PUT/DELETE de produtos; PUT/DELETE de clientes; POST do cardápio; POST de reembolso. Todo o resto vale para os dois perfis, inclusive cancelar pedido. Não altere sem decisão explícita.
- Senhas só com `PasswordHasher<Usuario>`; login inválido retorna `400` com mensagem única. Nunca exponha `SenhaHash`, a chave JWT nem tokens em log.
- A API valida `Jwt:ChaveSecreta` na subida (mínimo de 32 caracteres), emissor, audiência, access token entre 5 e 60 minutos (padrão 15) e refresh entre 1 e 90 dias (padrão 7). O `appsettings.json` deixa a chave vazia; o valor real fica em User Secrets ou variável de ambiente. Não reintroduza placeholder.
- Sessão: access token JWT curto, sem consulta ao banco; refresh token opaco persistido só como SHA-256 em `RefreshTokens`, com rotação na renovação e revogação por família em caso de reuso. `logout` revoga todos os refresh do usuário; o Admin pode revogar os de outro usuário. A revogação não invalida o access token já emitido (vale até o `exp`). Rotacionar `Jwt:ChaveSecreta` invalida todos. Não troque esse desenho sem decisão explícita.
- Limpeza: `LimpezaRefreshTokensHostedService` roda na subida e a cada `LimpezaRefreshTokens:IntervaloEmHoras` (1–168, padrão 24), apagando tokens com `ExpiraEm` ou `RevogadoEm` anteriores à retenção de `LimpezaRefreshTokens:RetencaoEmDias` (1–365, padrão 30). Preserve tokens ativos e recém-revogados: a retenção sustenta a detecção de reuso, e token apagado passa a responder apenas `401` em vez de derrubar a família. Falhas na rotina são logadas e repetidas no próximo intervalo.
- O primeiro Admin é criado pelo seed `AdminInicial` (`Nome`, `Email`, `Senha` em User Secrets), apenas quando não existe nenhum Admin. O seed não sobrescreve usuário existente.
- `UseHsts`/`UseHttpsRedirection` rodam apenas fora de Development (em Development, o `307` descartava `Authorization`); com proxy reverso, configure `ForwardedHeaders` e `AllowedHosts`.

## Não faça

- Não entregue módulos completos em exercício orientado; não repita testes já aprovados sem motivo concreto.
- Não mude versões, contratos ou regras por conveniência; não reescreva migrations aplicadas nem apague bancos.
- Não versione credenciais, chave JWT, `bin/`, `obj/` ou dados reais.
- Não declare build, teste, commit, push ou deploy sem execução ou confirmação. Não faça push sem autorização.

## Checklist antes de finalizar

- [ ] Conferir o diff; rodar build/testes pertinentes (em docs, validar comandos e caminhos).
- [ ] Migrations: revisar `Up()`/`Down()` e incluir designer e snapshot.
- [ ] Endpoints novos ou alterados: `[Authorize]`, respostas 401/403 e `.http` com `Authorization`.
- [ ] Atualizar o README se o contrato ou a execução mudou e o PROGRESSO (conclusão e próxima tarefa), separando o executado do não verificado.
