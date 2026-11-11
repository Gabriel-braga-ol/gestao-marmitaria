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
dotnet user-secrets set "AdminInicial:Nome" "Administrador"
dotnet user-secrets set "AdminInicial:Email" "admin@marmitaria.com"
dotnet user-secrets set "AdminInicial:Senha" "[SENHA_DO_ADMIN]"
```

O `appsettings.json` já traz `Entrega:Taxa` (5.00), `RateLimiting:Login` (5 tentativas por IP a cada 60 segundos), a seção `Jwt` (emissor `GestaoMarmitariaApi`, audiência `GestaoMarmitariaApp`, 8 horas e chave vazia), `AllowedHosts` restrito a `localhost;127.0.0.1` e `AdminInicial` vazio. A API recusa subir se `Jwt:ChaveSecreta` tiver menos de 32 caracteres. O seed `AdminInicial` é opcional: com os três valores vazios, ele não executa; configuração parcial ou inválida falha na subida com mensagem clara. Use User Secrets ou variável de ambiente e nunca versione credenciais.

No PowerShell, na pasta da API:

```powershell
$env:ASPNETCORE_ENVIRONMENT = "Development"
dotnet ef database update
dotnet run --launch-profile http
```

- A API sobe em `http://localhost:5149`; o OpenAPI fica em `/openapi/v1.json` (sem Swagger UI). Teste pelo `GestaoMarmitaria.Api.http`; veja abaixo como obter os tokens sem versionar credenciais.
- Em **Development** a API sobe em HTTP e não redireciona (o `307` fazia o cliente descartar o `Authorization`, resultando em `401`). Fora de Development, `UseHsts` e `UseHttpsRedirection` são ativados. `AllowedHosts` no `appsettings.json` é `localhost;127.0.0.1` e deve ser configurado para o domínio real no deploy; em Development, `appsettings.Development.json` mantém `AllowedHosts: "*"`.
- Como `registrar` exige Admin, o primeiro Admin é criado pelo seed `AdminInicial` (User Secrets), executado na subida somente quando ainda não existe nenhum usuário Admin. O seed não sobrescreve usuário existente; se o e-mail já estiver cadastrado, ele apenas registra um aviso.
- Fluxo inicial: login, cliente, produtos ativos (`tipo` 1 = Marmita, 2 = Bebida) e, para vender marmita, inclusão no cardápio de hoje. Bebidas ativas dispensam o cardápio.

### HTTPS, hosts e CORS no deploy

- `UseHsts` e `UseHttpsRedirection` só rodam fora de Development. O HSTS é enviado apenas para hosts não locais.
- Atrás de proxy reverso (Nginx, IIS, balanceador), configure `ForwardedHeaders` **antes** de `UseHsts`/`UseHttpsRedirection` para respeitar `X-Forwarded-Proto` e `X-Forwarded-For`, com proxies/redes conhecidos. Sem isso, o redirect pode entrar em loop e o rate limiting por IP enxerga todos os clientes com o IP do proxy:

  ```csharp
  builder.Services.Configure<ForwardedHeadersOptions>(options =>
  {
      options.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
      options.KnownProxies.Add(IPAddress.Parse("[IP_DO_PROXY]"));
  });

  app.UseForwardedHeaders();
  ```

- Defina `AllowedHosts` para o domínio público (lista separada por `;`), por exemplo na variável de ambiente `AllowedHosts=api.exemplo.com`.
- CORS não está habilitado (não há frontend). Quando existir, registre uma política nomeada com a origem explícita vinda de configuração e use `WithOrigins(...)`; não use `AllowAnyOrigin` combinado com credenciais.

### Testes manuais e obtenção dos tokens

O `GestaoMarmitaria.Api.http` não versiona credenciais nem tokens. Copie `GestaoMarmitaria.Api/http-client.env.json.example` para `GestaoMarmitaria.Api/http-client.env.json` — arquivo ignorado pelo Git — e preencha os valores do ambiente `dev`:

- `adminEmail` e `adminSenha`: o Admin criado pelo seed `AdminInicial`.
- `funcionarioEmail` e `funcionarioSenha`: o Funcionário que será cadastrado pela própria requisição de registro.

Para obter os tokens:

1. Execute a requisição `loginAdmin`; a resposta preenche `loginAdmin.response.body.token`, usado pelas demais requisições de Admin.
2. Com esse token, execute o `registrar` do Funcionário.
3. Execute `loginFuncionario`; a resposta preenche `loginFuncionario.response.body.token`, usado pelas requisições de Funcionário.

Selecione o ambiente `dev` no cliente HTTP (VS Code REST Client ou Visual Studio). O arquivo de exemplo contém apenas valores fictícios; nunca versione o `http-client.env.json` com credenciais reais. Se alguma chave ou senha de exemplo já tiver sido exposta, rotacione `Jwt:ChaveSecreta` e as senhas dos usuários.

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
- Consultas `200`; cadastros `201` com `Location`; atualizações e exclusões `204`; entrada inválida `400`; inexistente `404`; conflito (vínculo, transição, duplicidade, tipo, concorrência) `409`; excesso de tentativas de login `429`.
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
- `registrar` (`nome`, `email`, `senha` com mínimo de 10 e máximo de 128 caracteres, `perfil`): `201` com `usuarioId`; e-mail repetido `409`.
- `login` (`[AllowAnonymous]`): `200` com `token`, `email` e `perfil`; credenciais inválidas retornam `400` com mensagem única. A rota tem rate limiting por IP: por padrão, 5 requisições a cada 60 segundos; o excedente retorna `429` com `Retry-After`. Os limites ficam em `RateLimiting:Login` (`LimiteTentativas` e `JanelaEmSegundos`) e o estado é mantido em memória por processo — em várias instâncias, cada uma tem seu próprio contador.
- O e-mail é normalizado (Trim + minúsculas) no cadastro, no login e no seed, e a consulta de login usa `AsNoTracking()`. Quando o e-mail não existe, o login executa uma verificação de hash fictícia para igualar o tempo de resposta. A migration `NormalizarEmailsUsuarios` normaliza linhas antigas e aborta com mensagem clara se houver e-mails equivalentes que colidam.
- Tentativas de login inválidas são registradas com `ILogger` incluindo o e-mail; senha, token e hash nunca são registrados.
- Token HS256 com claims `NameIdentifier`, `Email` e `Role`, validade de 8 horas e sem tolerância de relógio.
- Todos os controllers têm `[Authorize]` na classe; as ações exclusivas do Admin acrescentam `[Authorize(Roles = "Admin")]`.
- O seed `AdminInicial` cria o primeiro Admin a partir de `Nome`, `Email` e `Senha` em User Secrets. Ele só age quando não há Admin e não altera usuários existentes.

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

O Funcionário também cancela pedidos; se o pedido estiver pago, a devolução fica pendente até o Admin confirmar o reembolso. O relatório financeiro (Admin) está previsto, mas não implementado.
Antes de um deploy: chave JWT em cofre ou variável de ambiente e HTTPS reativado.

### Auditoria

- Cada `PATCH /api/pedidos/{id}/status` (inclusive cancelamento) insere uma linha em `HistoricoStatusPedido` com status anterior, status novo, usuário (`ClaimTypes.NameIdentifier`) e data UTC. A tabela tem FK `Restrict` para `Pedidos` e `Usuarios`, então o histórico não é apagado por cascata.
- `Pagamento` e `Reembolso` ganharam `UsuarioRegistroId` e `DataRegistro`, anuláveis e com FK `Restrict` para `Usuarios`. Linhas anteriores à migration permanecem com os campos nulos (usuário desconhecido).
- Os campos de auditoria não são expostos nas respostas; não há endpoint para consultar o histórico ainda.
- A migration `AdicionarAuditoriaOperacoes` cria a tabela e as colunas sem alterar dados existentes; o `Down()` remove apenas a estrutura de auditoria.

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
- A criação de pedido, a atualização de status, o registro de pagamento e o PATCH de disponibilidade rodam em transação Serializable com `CommitAsync`; falha de serialização (SQLSTATE 40001, solta ou envolvida por `DbUpdateException`/`InvalidOperationException` do EF) retorna `409`. A concorrência é coberta pelos testes de integração, inclusive o `40001` envolvido.
- `FalhasBanco` centraliza a detecção de `40001` (percorrendo a cadeia de exceções) e da violação do índice único esperado. `JwtSettings.Validar` e `AdminInicialSettings.Validar` falham cedo quando a configuração está ausente ou inválida.
- `Microsoft.EntityFrameworkCore.Relational` 10.0.13 foi referenciado explicitamente para alinhar com o EFCore.Design 10.0.13 (Npgsql 10.0.3).

## Testes

```bash
dotnet test GestaoMarmitaria.slnx
```

- **Automatizados:** 125 testes aprovados em 11/11/2026 — 66 unitários (28 de transição e cancelamento de status + 23 de falhas de banco, JWT e seed + 1 do `40001` envolvido + 5 de rate limiting + 4 de política de senha no seed + 5 de normalização de e-mail) e 59 de integração HTTP/PostgreSQL. Build com 0 avisos.
- **Integração:** usa `WebApplicationFactory<Program>` e PostgreSQL local. A fixture lê `ConnectionStrings:DefaultConnection` dos User Secrets (ou de variável de ambiente), cria um banco `gm_test_*` com as migrations, injeta a chave JWT e o `AdminInicial` de teste por variáveis de ambiente e apaga o banco ao final. O usuário do PostgreSQL precisa de permissão `CREATEDB`. Cobre `401`/`403`, login/registro, filtros e datas, dados históricos, pagamento, reembolso, cardápio, exclusões e concorrência. Para rodar só a integração: `dotnet test GestaoMarmitaria.slnx --filter "FullyQualifiedName~IntegrationTests"`.
- **Manuais (`.http`):** perfis (Admin, Funcionário e sem token), permissões (`401`/`403`), criação e consulta de pedido, clientes, produtos, cardápio, disponibilidade e reembolso. Em 07/11 o arquivo foi reduzido: só mantém requisições com `Authorization`, o `login` e os testes intencionais de `401`.
- **Validação em PostgreSQL (11/11):** banco separado `gestao_marmitaria_val_20261111a`. Seed criou o Admin e o login retornou `200`; reinício não duplicou o Admin. Confirmados `409` na troca de `tipo` e na exclusão de produto vinculado só ao cardápio, cancelamento por Funcionário (`204`, status `6`) e concorrência: pagamentos duplicados (`201`+`409`), status duplicados (`204`+`409`), pagamento × cancelamento (`201`+`204` ou `204`+`409`), pedido × disponibilidade (`201`+`204`) e 5 pedidos simultâneos (5×`201`). O `40001` apareceu nos testes automatizados e revelou o `500` corrigido em `FalhasBanco`.
- **Não verificados:** carga alta, CI e deploy.

## Estrutura de pastas

- `GestaoMarmitaria.Api/Controllers/`: endpoints e coordenação das operações.
- `Dtos/`, `Models/` e `Enums/`: contratos, entidades e enumerações.
- `Regras/`: regra de transição de status. `Services/` e `Configurations/`: `TokenService`, `AdminInicialSeeder`, `JwtSettings` e `AdminInicialSettings`.
- `Data/` e `Migrations/`: contexto, mapeamento, `FalhasBanco` e evolução do banco.
- `GestaoMarmitaria.Tests/`: testes unitários e de integração (`IntegrationTests/`). `docs/PROGRESSO.md`: estado atual e ponto de retomada.

## Próximos passos

- Expor consulta do histórico de status e dos campos de auditoria (hoje gravados, mas não retornados pela API).
- Rodar build e testes em CI com PostgreSQL como serviço; avaliar carga alta.
- Frontend React/TypeScript, relatórios, Swagger UI e deploy.
