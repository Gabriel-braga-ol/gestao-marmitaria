# Gestão Marmitaria

API REST para organizar produtos, cardápio diário, clientes, pedidos, pagamentos e reembolsos de uma marmitaria, da criação à entrega ou retirada.
Projeto de portfólio em desenvolvimento incremental. Backend funcional com autenticação JWT e perfis (Admin e Funcionário); frontend ainda não implementado.

## Tecnologias

C# · .NET 10 · ASP.NET Core Web API · EF Core 10 · Npgsql · PostgreSQL 18 · JWT Bearer · `PasswordHasher` (Identity) · xUnit · Git.
Apoio: Visual Studio, pgAdmin e requisições em arquivo `.http`.

## Funcionalidades

- CRUD de produtos (marmita ou bebida) e clientes, com validações e telefone normalizado.
- Cardápio diário por data, com disponibilidade; marmitas só são vendidas se estiverem disponíveis no cardápio de hoje (Brasília).
- Pedidos de entrega ou retirada com itens, preço histórico, total e taxa configurável; listagem filtrada e paginada.
- Fluxo de status, cancelamento, pagamento integral e reembolso integral, ambos registrados manualmente.
- Proteções: exclusão de cliente ou produto vinculado bloqueada; `tipo` de produto no cardápio não muda; criação de pedido em transação Serializable.
- Login JWT, cadastro de usuários só pelo Admin e permissões por perfil.

## Como rodar

**Pré-requisitos:** SDK .NET 10, PostgreSQL, Git e `dotnet-ef` compatível com o EF Core 10. Crie o banco `gestao_marmitaria_db` (sem dados iniciais).

```bash
git clone https://github.com/Gabriel-braga-ol/gestao-marmitaria.git
cd gestao-marmitaria/GestaoMarmitaria.Api
dotnet restore
dotnet user-secrets set "ConnectionStrings:DefaultConnection" "Host=localhost;Port=5432;Database=gestao_marmitaria_db;Username=[USUARIO];Password=[SENHA]"
dotnet user-secrets set "Jwt:ChaveSecreta" "[CHAVE_LONGA_E_ALEATORIA]"
```

O `appsettings.json` já traz `Entrega:Taxa` (5.00) e a seção `Jwt` (emissor `GestaoMarmitariaApi`, audiência `GestaoMarmitariaApp`, 8 horas e um placeholder no lugar da chave). Use uma chave de pelo menos 32 caracteres e nunca versione credenciais.

No PowerShell, na pasta da API:

```powershell
$env:ASPNETCORE_ENVIRONMENT = "Development"
dotnet ef database update
dotnet run --launch-profile http
```

- A API sobe em `http://localhost:5149`; o OpenAPI fica em `/openapi/v1.json` (sem Swagger UI). Teste pelo `GestaoMarmitaria.Api.http`, enviando `Authorization: Bearer [TOKEN]`.
- `UseHttpsRedirection` está comentado em `Program.cs`: o redirecionamento `307` fazia o cliente descartar o cabeçalho `Authorization` (resultando em `401`). Reative-o junto com HTTPS no deploy.
- Como `registrar` exige Admin, o primeiro Admin precisa existir antes (seed/script, ou liberando a rota temporariamente em desenvolvimento).
- Fluxo inicial: login, cliente, produtos ativos (`tipo` 1 = Marmita, 2 = Bebida) e, para vender marmita, inclusão no cardápio de hoje. Bebidas ativas dispensam o cardápio.

## Endpoints

| Método | Rota | Operação |
| --- | --- | --- |
| POST | `/api/autenticacao/registrar` | Cadastrar usuário (só Admin) |
| POST | `/api/autenticacao/login` | Autenticar e receber o token (público) |
| GET | `/api/status` | Status estático da API (não verifica o banco) |
| GET / POST | `/api/produtos` | Listar / cadastrar produtos |
| GET / PUT / DELETE | `/api/produtos/{id}` | Consultar / atualizar / excluir produto |
| GET / POST | `/api/cardapio` | Listar itens da data / incluir marmita |
| GET | `/api/cardapio/{id}` | Consultar item do cardápio |
| PATCH | `/api/cardapio/{id}/disponibilidade` | Alterar disponibilidade do item |
| GET / POST | `/api/clientes` | Listar / cadastrar clientes |
| GET / PUT / DELETE | `/api/clientes/{id}` | Consultar / atualizar / excluir cliente |
| GET / POST | `/api/pedidos` | Listar com filtros / criar pedido |
| GET | `/api/pedidos/{id}` | Consultar pedido com itens |
| PATCH | `/api/pedidos/{id}/status` | Atualizar andamento ou cancelar |
| GET / POST | `/api/pedidos/{id}/pagamento` | Consultar / registrar pagamento |
| GET / POST | `/api/pedidos/{id}/reembolso` | Consultar / confirmar reembolso |

- Exceto o `login`, tudo exige token (`401` sem ele); ações só do Admin respondem `403` ao Funcionário.
- Consultas `200`; cadastros `201` com `Location`; atualizações e exclusões `204`; entrada inválida `400`; inexistente `404`; conflito (vínculo, transição, duplicidade, tipo, concorrência) `409`.
- `GET /api/pedidos` retorna `itens`, `pagina`, `tamanhoPagina`, `totalRegistros` e `totalPaginas`. Filtros opcionais e combináveis, aplicados antes da contagem: `status`, `clienteId`, `dataInicial`, `dataFinal` (dias de Brasília), `pago` e `reembolsoPendente`. Página padrão 1, tamanho 20, máximo 100.
  Exemplo: `/api/pedidos?dataInicial=2026-10-01&dataFinal=2026-10-29&pagina=1&tamanhoPagina=20`.
- `pago=true`: com pagamento registrado (inclui cancelados e reembolsados); `pago=false`: sem pagamento.
- `reembolsoPendente=true`: cancelados com pagamento e sem reembolso; `false`: todos os demais. `true` com `pago=false` retorna lista vazia; valor inválido, `400`.

### Produtos e cardápio diário

- `tipo`: Marmita=1, Bebida=2 (0, 99 e omissão retornam `400`). O PUT que mude o `tipo` de um produto presente no cardápio retorna `409`.
- `POST /api/cardapio` com `{ "data": "2026-11-01", "produtoId": 2 }` inclui uma marmita ativa, já disponível. Produto inexistente, bebida ou inativo retorna `400`; repetição na mesma data, `409`. O mesmo produto pode estar em vários dias.
- `GET /api/cardapio?data=AAAA-MM-DD` (data obrigatória) é a lista de gerenciamento: inclui itens indisponíveis e retorna `[]` sem itens. Nome e preço vêm do catálogo atual.
- `PATCH /api/cardapio/{id}/disponibilidade` com `{ "disponivel": false }` retorna `204`; não altera `Produto.Ativo`, outros dias nem pedidos antigos. Não há controle de estoque.
- Pedido com marmita ausente ou indisponível no cardápio de hoje é rejeitado inteiro com `400`. Cardápio futuro não libera a venda de hoje.

## Autenticação e autorização

- `Usuario`: Nome, Email (único), SenhaHash (`PasswordHasher<Usuario>`) e Perfil (Admin=1, Funcionario=2).
- `registrar` (`nome`, `email`, `senha` com mínimo de 6 caracteres, `perfil`): `201` com `usuarioId`; e-mail repetido `409`.
- `login` (`[AllowAnonymous]`): `200` com `token`, `email` e `perfil`; credenciais inválidas retornam `400` com mensagem única.
- Token HS256 com claims `NameIdentifier`, `Email` e `Role`, validade de 8 horas e sem tolerância de relógio.
- Todos os controllers têm `[Authorize]` na classe; as ações exclusivas do Admin acrescentam `[Authorize(Roles = "Admin")]`.

| Operação | Admin | Funcionário |
| --- | --- | --- |
| Consultar e cadastrar clientes | Sim | Sim |
| Editar e excluir clientes | Sim | Não |
| Criar e consultar pedidos, atualizar andamento, cancelar, registrar pagamento | Sim | Sim |
| Consultar reembolso | Sim | Sim |
| Confirmar reembolso | Sim | Não |
| Consultar produtos e cardápio; alterar disponibilidade | Sim | Sim |
| Cadastrar, editar e excluir produtos; incluir marmita no cardápio | Sim | Não |
| Cadastrar usuários | Sim | Não |

O Funcionário também cancela pedidos; se o pedido estiver pago, a devolução fica pendente até o Admin confirmar o reembolso. O sistema não registra qual usuário realizou cada alteração. O relatório financeiro (Admin) está previsto, mas não implementado.
Antes de um deploy: chave JWT em cofre ou variável de ambiente e HTTPS reativado.

## Regras de negócio

- Cliente e produtos devem existir e estar ativos; os itens têm quantidade positiva e sem produtos repetidos.
- Entrega exige endereço e aplica `Entrega:Taxa`; retirada não tem endereço e a taxa é zero. Nome e preço dos itens, endereço e taxa ficam gravados no pedido.
- Status: entrega Novo → EmPreparo → Pronto → SaiuParaEntrega → Entregue; retirada Pronto → Entregue. Saltos, retornos e repetição são bloqueados.
- Cancelado (=6) só a partir de Novo ou EmPreparo; é terminal e preserva itens, valores e pagamento.
- Pagamento: um por pedido, integral, com valor calculado no backend (itens + taxa). Formas: Pix=1, Dinheiro=2, CartaoCredito=3, CartaoDebito=4. Não altera o status; pedido cancelado não aceita novo pagamento (`409`).
- Reembolso: `POST /api/pedidos/{id}/reembolso` sem corpo, só para pedido cancelado e pago; copia o valor do pagamento (taxa inclusa) e é único por pagamento (`409` se repetir). O pagamento original permanece no histórico. Apenas confirma uma devolução feita manualmente, sem movimentação bancária.
- Instantes são gravados em UTC; datas do cardápio usam `date`. Os filtros usam dias de Brasília (início inclusivo, fim exclusivo no dia seguinte); período invertido e data final `9999-12-31` retornam `400`.

## Decisões técnicas

- DTOs definem entradas e validações; respostas não expõem navegações.
- `RegrasStatusPedido` (classe estática) isola as transições e é testada sem HTTP nem banco. Os controllers acessam o `AppDbContext` diretamente, com auxiliares por responsabilidade.
- Índice único com FK Restrict garante um pagamento por pedido, um reembolso por pagamento e um item por data e produto; a violação do índice esperado retorna `409`.
- JWT sem sessão no servidor (`TokenService` + JwtBearer), com a chave em User Secrets. Hash de senha com `PasswordHasher`, sem adotar o Identity completo.
- A criação de pedido roda em transação Serializable com `CommitAsync`. O tratamento da falha de serialização (SQLSTATE 40001, solta ou dentro de `DbUpdateException`, devolvendo `409`) está aplicado em `CriarPedidoAsync` (`EhFalhaDeSerializacao`). A concorrência não foi testada, e o PATCH de disponibilidade não participa da transação (decisão pendente).
- `Microsoft.EntityFrameworkCore.Relational` 10.0.13 foi referenciado explicitamente para alinhar com o EFCore.Design 10.0.13 (Npgsql 10.0.3).

## Testes

```bash
dotnet test GestaoMarmitaria.slnx
```

- **Automatizados:** 28 testes de transição e cancelamento de status, aprovados em 01/11/2026 e não reexecutados após as mudanças de 05 e 06/11. Não há testes de integração HTTP/PostgreSQL nem de autenticação.
- **Manuais (`.http`):** CRUD, pedidos, filtros, datas, pagamento, cancelamento, reembolso, cardápio e, em 06/11, perfis (Admin, Funcionário e sem token) e persistência do pedido após o `CommitAsync`.
- **Não verificados:** concorrência (pedidos, pagamentos, disponibilidade), cancelamento por Funcionário e exclusão de produto vinculado somente ao cardápio.

## Estrutura de pastas

- `GestaoMarmitaria.Api/Controllers/`: endpoints e coordenação das operações.
- `Dtos/`, `Models/` e `Enums/`: contratos, entidades e enumerações.
- `Regras/`: regra de transição de status. `Services/` e `Configurations/`: `TokenService` e `JwtSettings`.
- `Data/` e `Migrations/`: contexto, mapeamento e evolução do banco.
- `GestaoMarmitaria.Tests/`: testes unitários. `docs/PROGRESSO.md`: estado atual e ponto de retomada.

## Próximos passos

- Testar a concorrência na criação de pedidos (o tratamento de `40001` está aplicado, mas não exercitado).
- Testar o cancelamento por Funcionário, a troca de `tipo` e a exclusão de produto vinculado só ao cardápio; definir a criação do primeiro Admin.
- Ampliar testes de integração, avaliar auditoria de alterações; frontend React/TypeScript, relatórios, CI e deploy.
