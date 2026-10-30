# Gestão Marmitaria

API REST para organizar produtos, clientes e pedidos de uma marmitaria, da criação à entrega ou retirada.
Projeto de portfólio de Gabriel Braga, desenvolvido de forma incremental. Backend funcional; frontend, pagamentos e autenticação ainda não implementados.

## Tecnologias

C# · .NET 10 · ASP.NET Core Web API · Entity Framework Core 10 · Npgsql · PostgreSQL 18 · xUnit · Git.
Ferramentas de desenvolvimento: Visual Studio, pgAdmin e requisições em arquivo `.http`.

## Funcionalidades

- CRUD de produtos e clientes, com validações e normalização de telefone.
- Criação de pedidos com itens, preços históricos, totais e taxa de entrega configurável.
- Consulta por ID e listagem com filtros por cliente, status e período, com paginação.
- Atualização de status conforme o fluxo de entrega ou retirada.
- Bloqueio de exclusão de clientes e produtos vinculados a pedidos.

## Como rodar

### Pré-requisitos

Instale o SDK do .NET 10, PostgreSQL e Git. Configure o `dotnet-ef` compatível com o EF Core 10.
Crie o banco `gestao_marmitaria_db`; o projeto não inclui dados iniciais.

### Clonar e configurar

```bash
git clone https://github.com/Gabriel-braga-ol/gestao-marmitaria.git
cd gestao-marmitaria/GestaoMarmitaria.Api
dotnet restore
```

Em `appsettings.json`, inclua a seção abaixo, preservando as demais configurações:

```json
{ "Entrega": { "Taxa": 5.00 } }
```

Configure a conexão com User Secrets, dentro da pasta da API:

```bash
dotnet user-secrets set "ConnectionStrings:DefaultConnection" "Host=localhost;Port=5432;Database=gestao_marmitaria_db;Username=[USUARIO];Password=[SENHA]"
```

Substitua os placeholders pelos dados locais. Não versione credenciais; a conexão usa a chave `ConnectionStrings:DefaultConnection`.

### Migrations e execução

No PowerShell, na pasta da API:

```powershell
$env:ASPNETCORE_ENVIRONMENT = "Development"
dotnet ef database update
dotnet run --launch-profile http
```

O perfil HTTP usa `http://localhost:5149`; confira `Properties/launchSettings.json` e a saída do terminal.
Cadastre um cliente e um produto ativo antes de criar um pedido.

### Swagger / OpenAPI

Em desenvolvimento, consulte `http://localhost:5149/openapi/v1.json`.
Swagger UI não está configurada; teste os endpoints pelo arquivo `GestaoMarmitaria.Api.http` ou outro cliente HTTP.

## Endpoints

| Método | Rota | Operação |
| --- | --- | --- |
| GET | `/api/status` | Status estático da API; não verifica o banco |
| GET / POST | `/api/produtos` | Listar / cadastrar produtos |
| GET / PUT / DELETE | `/api/produtos/{id}` | Consultar / atualizar / excluir produto |
| GET / POST | `/api/clientes` | Listar / cadastrar clientes |
| GET / PUT / DELETE | `/api/clientes/{id}` | Consultar / atualizar / excluir cliente |
| GET / POST | `/api/pedidos` | Listar com filtros / criar pedido |
| GET | `/api/pedidos/{id}` | Consultar pedido com itens e total |
| PATCH | `/api/pedidos/{id}/status` | Atualizar andamento |

Consultas retornam `200`; cadastros, `201` com `Location`; atualizações/exclusões, `204`.
Entradas inválidas retornam `400`; registros inexistentes, `404`; conflitos de exclusão ou transição, `409`.
Produtos e clientes retornam listas; pedidos retornam `itens`, `pagina`, `tamanhoPagina`, `totalRegistros` e `totalPaginas`.
Na listagem de pedidos: `status`, `clienteId`, `dataInicial` e `dataFinal` são opcionais; página padrão 1, tamanho padrão 20, máximo 100.
Exemplo: `/api/pedidos?dataInicial=2026-10-01&dataFinal=2026-10-29&pagina=1&tamanhoPagina=20`.

## Regras de negócio

- Cliente deve existir; produtos devem existir e estar ativos. Pedidos exigem itens, quantidades positivas e produtos sem repetição.
- Entrega exige endereço e aplica `Entrega:Taxa`; retirada persiste endereço nulo e taxa zero.
- Nome/preço dos itens, endereço e taxa permanecem registrados no pedido, independentemente de alterações futuras.
- Entrega: Novo → EmPreparo → Pronto → SaiuParaEntrega → Entregue. Retirada: Pronto → Entregue.
- Saltos, retornos, repetição e mudanças após Entregue são bloqueados. Clientes/produtos vinculados não podem ser excluídos.
- Datas são armazenadas em UTC; filtros usam dias de Brasília, com início inclusivo e fim exclusivo no dia seguinte.
- Período invertido e data final `9999-12-31` são rejeitados; horários inexistentes no início do dia avançam até o primeiro minuto válido.

## Decisões técnicas

- **DTOs:** definem entradas e validações; respostas de pedidos evitam expor navegações e ciclos de serialização.
- **Preço no item:** preserva o valor da venda mesmo quando o catálogo muda.
- **Transições validadas:** impedem mudanças incompatíveis com o fluxo operacional.
- **Regra extraída:** `RegrasStatusPedido` isola a decisão do controller e permite testes sem HTTP ou banco. Hoje é uma classe estática; um serviço dedicado poderá facilitar injeção e reutilização se surgirem dependências.
- **Métodos com foco:** auxiliares separam filtros, paginação, carregamento de produtos e montagem do pedido; controllers ainda acessam `AppDbContext` diretamente.

## Testes

Na raiz da solução, execute:

```bash
dotnet test GestaoMarmitaria.slnx
```

Há 15 casos unitários xUnit de transições de status, com execução aprovada confirmada pelo autor.
O arquivo `.http` reúne cenários manuais de CRUD, pedidos, filtros e paginação; não é uma suíte automatizada.
Os testes unitários não verificam HTTP ou PostgreSQL. Os filtros por data foram verificados manualmente.

## Estrutura de pastas

- `GestaoMarmitaria.Api/Controllers/`: endpoints e coordenação das operações.
- `GestaoMarmitaria.Api/Dtos/`, `Models/` e `Enums/`: contratos, entidades e enumerações.
- `GestaoMarmitaria.Api/Regras/`: regra de transição de status.
- `GestaoMarmitaria.Api/Data/` e `Migrations/`: contexto, mapeamento e evolução do banco.
- `GestaoMarmitaria.Tests/`: testes unitários.
- `docs/PROGRESSO.md`: estado atual, verificações e ponto de retomada.

## Próximos passos

Registro de pagamentos separado do andamento; cardápio diário; autenticação e perfis; frontend React/TypeScript; relatórios, CI e deploy.
Cancelamento e reembolso precisam de regras consolidadas antes da implementação.
