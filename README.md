# Gestão Marmitaria

API REST para organizar produtos, clientes, pedidos, pagamentos e reembolsos de uma marmitaria, da criação à entrega ou retirada.
Projeto de portfólio de Gabriel Braga, desenvolvido de forma incremental. Backend funcional; frontend e autenticação ainda não implementados.

## Tecnologias

C# · .NET 10 · ASP.NET Core Web API · Entity Framework Core 10 · Npgsql · PostgreSQL 18 · xUnit · Git.
Ferramentas de desenvolvimento: Visual Studio, pgAdmin e requisições em arquivo `.http`.

## Funcionalidades

- CRUD de produtos e clientes, com validações e normalização de telefone.
- Criação de pedidos com itens, preços históricos, totais e taxa de entrega configurável.
- Consulta por ID e listagem com filtros por cliente, status, período, existência de pagamento e pendência de reembolso, com paginação.
- Atualização de status conforme o fluxo de entrega ou retirada.
- Registro e consulta de pagamento integral por pedido, separado do andamento e sem integração bancária.
- Cancelamento em Novo ou EmPreparo e registro manual de reembolso integral.
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
| PATCH | `/api/pedidos/{id}/status` | Atualizar andamento ou cancelar |
| GET / POST | `/api/pedidos/{id}/pagamento` | Consultar / registrar pagamento integral |
| GET / POST | `/api/pedidos/{id}/reembolso` | Consultar / confirmar reembolso integral |

Consultas retornam `200`; cadastros, `201` com `Location`; atualizações/exclusões, `204`.
Entradas inválidas retornam `400`; registros inexistentes, `404`; conflitos de exclusão, transição ou pagamento duplicado, `409`.
Produtos e clientes retornam listas; pedidos retornam `itens`, `pagina`, `tamanhoPagina`, `totalRegistros` e `totalPaginas`.
Na listagem de pedidos, `status`, `clienteId`, `dataInicial`, `dataFinal`, `pago` e `reembolsoPendente` são opcionais.
`pago=true` seleciona pedidos com pagamento registrado; `pago=false`, sem pagamento; omitido inclui ambos.
Os filtros são combináveis e aplicados antes da contagem e paginação. Página padrão 1, tamanho padrão 20, máximo 100.
O filtro verifica o histórico de pagamento: pedidos cancelados e reembolsados continuam em `pago=true`.
Exemplo: `/api/pedidos?dataInicial=2026-10-01&dataFinal=2026-10-29&pagina=1&tamanhoPagina=20`.

### Filtro de reembolso pendente

| Parâmetro | Resultado |
| --- | --- |
| Omitido | Não filtra por pendência de reembolso |
| `reembolsoPendente=true` | Cancelados com pagamento e sem reembolso registrado |
| `reembolsoPendente=false` | Todos os pedidos sem essa pendência, inclusive não cancelados e cancelados sem pagamento |

O valor `false` não significa apenas reembolsados. O filtro combina-se com os demais antes da contagem/paginação.
Exemplo: `/api/pedidos?reembolsoPendente=true&pagina=1&tamanhoPagina=20`.
A combinação `reembolsoPendente=true&pago=false` retorna `200` com lista vazia e total zero.
Valores inválidos, como `reembolsoPendente=abc`, retornam `400`.
Após registrar a devolução, o pedido sai do grupo pendente e entra no grupo sem pendência, preservando seu pagamento histórico.

## Regras de negócio

- Cliente deve existir; produtos devem existir e estar ativos. Pedidos exigem itens, quantidades positivas e produtos sem repetição.
- Entrega exige endereço e aplica `Entrega:Taxa`; retirada persiste endereço nulo e taxa zero.
- Nome/preço dos itens, endereço e taxa permanecem registrados no pedido, independentemente de alterações futuras.
- Cada pedido aceita um pagamento integral; o backend calcula o valor pelos itens históricos mais a taxa e registra a data em UTC.
- Formas: Pix=1, Dinheiro=2, CartaoCredito=3 e CartaoDebito=4; o POST recebe apenas `formaPagamento`.
- Registrar pagamento não altera o status do pedido. O GET de pagamento retorna `404` se não houver registro.
- Entrega: Novo → EmPreparo → Pronto → SaiuParaEntrega → Entregue. Retirada: Pronto → Entregue.
- Saltos, retornos, repetição e mudanças após Entregue são bloqueados. Clientes/produtos vinculados não podem ser excluídos.
- Datas são armazenadas em UTC; filtros usam dias de Brasília, com início inclusivo e fim exclusivo no dia seguinte.
- Período invertido e data final `9999-12-31` são rejeitados; horários inexistentes no início do dia avançam até o primeiro minuto válido.

### Cancelamento e reembolso

- Cancelado=6; permitido somente a partir de Novo=1 e EmPreparo=2, para entrega e retirada.
- A partir de Pronto, o cancelamento é bloqueado. Cancelado não permite avanço, retorno ou repetição.
- Cancelar preserva itens, valores e pagamento original. Registrar novo pagamento em cancelado retorna `409`.
- Pedido cancelado sem pagamento não exige reembolso. Com pagamento e sem reembolso, a devolução está pendente; essa situação é inferida dos registros, sem status próprio persistido.
- O funcionário devolve o dinheiro e confirma com `POST /api/pedidos/{id}/reembolso`, sem corpo. Não há movimentação bancária automática.
- O backend copia `Pagamento.Valor` integralmente, incluindo a taxa histórica, e registra a data em UTC.
- POST: `201` com DTO e `Location`; pedido inexistente `404`; pedido não cancelado, sem pagamento ou reembolso duplicado `409`.
- GET: `200` com Id, PagamentoId, Valor e DataReembolso; `404` para pedido inexistente ou sem reembolso.
- O pagamento original permanece no histórico após o reembolso; o pedido permanece Cancelado.

## Decisões técnicas

- **DTOs:** definem entradas e validações; respostas de pedidos evitam expor navegações e ciclos de serialização.
- **Preço no item:** preserva o valor da venda mesmo quando o catálogo muda.
- **Transições validadas:** impedem mudanças incompatíveis com o fluxo operacional.
- **Regra extraída:** `RegrasStatusPedido` isola a decisão do controller e permite testes sem HTTP ou banco. Hoje é uma classe estática; um serviço dedicado poderá facilitar injeção e reutilização se surgirem dependências.
- **Métodos com foco:** auxiliares separam filtros, paginação, carregamento de produtos e montagem do pedido; controllers ainda acessam `AppDbContext` diretamente.
- **Pagamento único:** índice único em `PedidoId` impede duplicidade; violação desse índice na gravação retorna `409`.

- **Reembolso único:** índice único em `Reembolso.PagamentoId`, FK com exclusão restrita e tratamento específico de `IX_Reembolsos_PagamentoId`.
- **Auxiliares de reembolso:** `MontarReembolso` e `MapearReembolsoParaResposta`; GET com `ActionName` explícito para gerar `Location`.

## Testes

Na raiz da solução, execute:

```bash
dotnet test GestaoMarmitaria.slnx
```

A suíte de regras de status foi ampliada de 15 para 28 casos previstos, incluindo cancelamentos permitidos, bloqueados e impossibilidade de retorno ao fluxo. Gabriel confirmou aprovação nas etapas; não foi fornecida a saída final com a contagem.
O arquivo `.http` reúne cenários manuais de CRUD, pedidos, filtros, paginação, pagamentos, cancelamento e reembolsos; não é uma suíte automatizada.
Pagamento: cadastro `201`/`Location`, consulta `200`, valor, status preservado, duplicidade `409`, validações `400`, inexistência `404` e persistência após reinício confirmados pelo autor.
Cancelamento e reembolso: verificações manuais confirmadas de sucesso, bloqueios, inexistência, persistência após reinício e preservação do pagamento original. Filtros por pagamento e reembolso pendente, combinações, paginação, valor inválido e mudança de grupo após devolução também confirmados manualmente pelo autor.
Os testes unitários cobrem status, não HTTP/PostgreSQL. Datas, pagamentos e reembolsos foram verificados manualmente; concorrência simultânea desses fluxos ainda não foi testada.

## Estrutura de pastas

- `GestaoMarmitaria.Api/Controllers/`: endpoints e coordenação das operações.
- `GestaoMarmitaria.Api/Dtos/`, `Models/` e `Enums/`: contratos, entidades e enumerações.
- `GestaoMarmitaria.Api/Regras/`: regra de transição de status.
- `GestaoMarmitaria.Api/Data/` e `Migrations/`: contexto, mapeamento e evolução do banco.
- `GestaoMarmitaria.Tests/`: testes unitários.
- `docs/PROGRESSO.md`: estado atual, verificações e ponto de retomada.

## Próximos passos

Cardápio diário; autenticação e perfis; frontend React/TypeScript; relatórios, CI e deploy.
Ampliar testes de integração dos filtros, pagamentos e reembolsos e tratar concorrência.
