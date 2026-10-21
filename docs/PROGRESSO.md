# Progresso — Gestão Marmitaria

**Atualizado em:** 21/10/2026.  
**Código consultado nesta atualização:** commit `319f938` (`fix: small changes`), da branch `main`.  
**Situação:** CRUD de produtos e clientes concluído com PostgreSQL. Próximo marco: pedidos e itens.

Este documento registra o andamento e as decisões para permitir a retomada em outra conversa. As regras de trabalho estão no [AGENTS.md](../AGENTS.md); as instruções de execução e os contratos da API estão no [README](../README.md).

## Ponto exato de retomada

Os dois CRUDs foram implementados, testados manualmente pelo usuário e enviados ao GitHub. O README também está no repositório consultado. Não há modelo, DTO, controller ou migration de pedidos nesta versão.

**Próxima tarefa:** discutir a modelagem de `Pedido` e `ItemPedido` antes de escrever código, começando pela relação entre cliente, pedido e produtos, pela quantidade e pelo preço unitário histórico de cada item.

Não recriar DTOs ou endpoints de produtos/clientes nem repetir os testes aprovados como parte automática da retomada. Verifique mudanças no repositório desde esta atualização antes de prosseguir.

## Objetivo e decisões de base

Construir um sistema para o dono e os funcionários de uma marmitaria organizarem clientes, produtos, cardápio, pedidos, pagamentos e entrega ou retirada. O desenvolvimento é gradual, orientado ao aprendizado e ao portfólio, com objetivo de chegar ao deploy.

| Decisão | Definição |
| --- | --- |
| Backend | C# / ASP.NET Core Web API, .NET 10, controllers |
| Banco | PostgreSQL; desenvolvimento local com a versão 18 |
| Persistência | Entity Framework Core com Npgsql |
| Frontend futuro | React com TypeScript |
| Ambiente habitual | Windows, Visual Studio e pgAdmin |
| Testes atuais | Requisições manuais no arquivo `.http` |
| Repositório | [Gabriel-braga-ol/gestao-marmitaria](https://github.com/Gabriel-braga-ol/gestao-marmitaria) |
| Banco local | `gestao_marmitaria_db` |
| Conexão | `ConnectionStrings:DefaultConnection`, nos Segredos do Usuário em desenvolvimento |
| Arquitetura atual | Um projeto de API, com controllers acessando `AppDbContext` diretamente |

## Marcos concluídos

### 1. Estrutura e primeiros endpoints

- Solução `GestaoMarmitaria` e projeto `GestaoMarmitaria.Api`.
- Controllers, modelos e DTOs organizados em pastas.
- Endpoint `GET /api/status`.
- OpenAPI em desenvolvimento, sem interface Swagger UI configurada.
- Configuração inicial do Git e do repositório no GitHub.
- Primeira implementação de produtos usando lista em memória, depois substituída integralmente pelo banco.

### 2. Produtos com PostgreSQL

Modelo `Produto`: `Id`, `Nome`, `Descricao`, `Preco` e `Ativo`.

| Contrato / campo | Regra implementada |
| --- | --- |
| `CriarProdutoDto` | Nome, descrição e preço |
| `AtualizarProdutoDto` | Nome, descrição, preço e ativo |
| Nome | Obrigatório; até 100 caracteres |
| Descrição | Opcional; até 500 caracteres |
| Preço | DTO aceita de `0.01` a `9999.99`; banco usa `numeric(6,2)` |
| Ativo | `true` no cadastro; atualizável pelo PUT |
| ID | Gerado pelo PostgreSQL |

Todos os endpoints foram migrados para `_context.Produtos`, com métodos assíncronos e gravação por `SaveChangesAsync()`. A antiga lista `_produtos` foi removida.

### 3. Configuração de persistência

- Instalação de `Microsoft.EntityFrameworkCore.Design` e `Npgsql.EntityFrameworkCore.PostgreSQL`.
- `AppDbContext` com construtor que recebe `DbContextOptions<AppDbContext>`.
- Registro via `AddDbContext` e `UseNpgsql` no `Program.cs`.
- Mapeamento de textos, obrigatoriedade e precisão em `OnModelCreating`.
- Banco da marmitaria separado do banco de outros projetos.
- Aplicação e inspeção das migrations no pgAdmin confirmadas pelo usuário.

| Migration | Tabela |
| --- | --- |
| `20261018220914_CriarTabelaProdutos` | `Produtos` |
| `20261019012449_CriarTabelaClientes` | `Clientes` |

O EF mantém `__EFMigrationsHistory`. As migrations e o snapshot estão versionados. Não há seed de registros nem aplicação automática de migrations na inicialização.

### 4. Clientes com PostgreSQL

Modelo `Cliente`: `Id`, `Nome`, `Telefone` e `Endereco`.

| Contrato / campo | Regra implementada |
| --- | --- |
| DTOs | `CriarClienteDto` e `AtualizarClienteDto` |
| Nome | Obrigatório; até 100 caracteres |
| Telefone | Obrigatório; entrada até 20 caracteres, com expressão regular |
| Endereço | Opcional; até 300 caracteres |
| ID | Gerado pelo PostgreSQL |

O telefone aceita formatos como `(21) 99999-9999`, `21 99999-9999` e `21999999999`. A regra é estrutural: não comprova existência do telefone ou validade do DDD e não aceita `+55`.

Expressão adotada nos dois DTOs:

```csharp
@"^(?:\([1-9][0-9]\)|[1-9][0-9])[ -]?[0-9]{4,5}-?[0-9]{4}$"
```

No cadastro e na atualização, o telefone é normalizado com `Where(char.IsDigit)` antes de salvar. Exemplo: `(21) 99999-9999` fica `21999999999`.

O envio de `endereco: null` é aceito; no PUT, remove o endereço anterior.

### 5. Documentação e continuidade

- README criado e já presente no repositório consultado em 21/10/2026.
- `AGENTS.md` preparado com regras permanentes de mentoria e trabalho no repositório.
- Este registro de progresso preparado para continuidade entre conversas.

**Versionamento desta atualização:** estes dois arquivos de continuidade foram preparados localmente. Commit e push deles ainda não foram confirmados pelo usuário. Essa pendência não se refere aos CRUDs, já publicados.

## Endpoints concluídos

| Método | Produtos | Clientes | Respostas principais |
| --- | --- | --- | --- |
| GET | `/api/produtos` | `/api/clientes` | `200`, lista ordenada por ID |
| GET | `/api/produtos/{id}` | `/api/clientes/{id}` | `200` ou `404` |
| POST | `/api/produtos` | `/api/clientes` | `201` com objeto e `Location`; `400` para entrada inválida |
| PUT | `/api/produtos/{id}` | `/api/clientes/{id}` | `204`, `400` ou `404` |
| DELETE | `/api/produtos/{id}` | `/api/clientes/{id}` | `204` ou `404` |

Consultas usam `AsNoTracking()`. As buscas destinadas à atualização preservam rastreamento. Exclusões são físicas. Ainda não há pedidos associados ou filtros automáticos pelo campo `Ativo`.

## Evidência de testes e inspeção

**Testes manuais confirmados por Gabriel durante a sessão de 18/10/2026:**

- Listagens vazias retornando `200` com `[]`.
- Cadastro válido retornando `201` e consulta do ID retornando `200`.
- Consulta de ID inexistente retornando `404`.
- Validações de nome, preço e telefone retornando `400`.
- Atualização válida retornando `204` e dados alterados no GET.
- Atualização inválida preservando os valores anteriores.
- Atualização de endereço para `null`.
- Normalização de telefone no cadastro e na atualização.
- Exclusão retornando `204`, ausência na listagem e `404` em consulta/repetição.
- Permanência dos cadastros, atualizações e exclusões após reiniciar a API.
- Compilação bem-sucedida ao concluir as alterações.

**Inspeção de 21/10/2026:** controllers, contexto, configuração, dependências e README conferidos no commit `319f938`. Essa inspeção não representa uma nova execução dos testes no ambiente de Gabriel.

Ainda não há projeto de testes automatizados ou CI. O arquivo `.http` contém cenários manuais; a existência das requisições não comprova sua execução.

## Visão de negócio para as próximas etapas

As definições abaixo orientam o produto futuro, mas ainda não estão implementadas:

- Produtos abrangem marmitas P/M/G, bebidas, doces e adicionais. Atualmente o modelo é genérico, sem categorias ou tamanhos estruturados.
- Cardápio diário e disponibilidade.
- Pedido com cliente, itens, quantidades, observações e entrega ou retirada.
- Entrega com estimativa, sem promessa de horário exato.
- Fluxo de entrega: Novo → Em preparo → Pronto → Saiu para entrega → Entregue.
- Na retirada: Pronto → Entregue, sem etapa de saída para entrega.
- Pagamento registrado separadamente do andamento, com Pix, crédito, débito ou dinheiro, antes ou depois da entrega.
- Perfis planejados: dono com gestão de produtos/preços, clientes, pedidos, pagamentos, financeiro e funcionários; funcionário com clientes, pedidos/andamento e registro de pagamentos. Detalhes serão consolidados na etapa de autorização.

## Próximo marco: pedidos e itens

Sequência proposta, a ser refinada na mentoria:

1. Definir os campos mínimos de `Pedido` e `ItemPedido`, os relacionamentos e as regras de criação.
2. Definir a preservação do preço unitário cobrado em cada item, sem depender do preço atual de `Produto`.
3. Decidir como o pedido registra o endereço de entrega e preserva seu histórico após alterações no cadastro do cliente.
4. Definir quantidade, cálculo de valores, entrega/retirada e comportamento para produto inativo.
5. Definir a proteção do histórico antes de permitir exclusão de clientes e produtos vinculados; evitar exclusão em cascata por conveniência.
6. Implementar os modelos e mapeamentos em tarefas pequenas, revisar e gerar a migration.
7. Evoluir DTOs, endpoints e testes conforme as regras forem implementadas.

Não iniciar toda essa sequência de uma vez. A primeira tarefa é a discussão de modelagem; nomes finais e regras ainda devem ser definidos.

## Pendências e limites conhecidos

| Tema | Situação |
| --- | --- |
| Cancelamento, reembolso e reposição | Histórico recuperado anteriormente apresentou divergências; consolidar as regras antes de implementar |
| Pedidos e itens | Não iniciados |
| Autenticação e permissões | Não implementadas |
| Cardápio e pagamentos | Planejados |
| Frontend e deploy | Não iniciados |
| Pesquisa, filtros e paginação | Não implementados |
| Unicidade de telefone ou produto | Não há regra implementada; não presumir que telefone deva ser único |
| Tratamento global de exceções e concorrência | Ainda não implementado de forma própria |
| `WeatherForecast` | Exemplo do template ainda presente; remoção não faz parte do marco atual |
| Testes automatizados | Ainda não implementados |

Não há bloqueio técnico confirmado para iniciar a modelagem de pedidos.

## Rotina para manter este registro

Ao concluir um marco, atualize a data, a referência de código quando conhecida, as entregas, os testes efetivamente confirmados e a próxima tarefa. Registre dúvidas sem transformá-las em decisões. Se apenas parte da etapa passou nos testes, mantenha o restante pendente.

Texto sugerido para retomar em uma nova conversa:

> Leia AGENTS.md, docs/PROGRESSO.md, README.md e o código relacionado no repositório. Retome pela próxima tarefa pendente, mantendo a mentoria gradual. Produtos e clientes já têm CRUD com PostgreSQL; não repita essas etapas sem uma razão concreta.
