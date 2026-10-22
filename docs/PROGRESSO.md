# Progresso — Gestão Marmitaria

**Atualizado em:** 21/10/2026.  
**Referência anterior inspecionada:** commit `319f938` (`fix: small changes`), branch `main`, antes do marco de pedidos. Esta atualização usa os trechos e resultados fornecidos na sessão; não houve nova inspeção do repositório.  
**Versionamento:** commit do marco de pedidos confirmado por Gabriel; hash e push não informados. Esta revisão documental é posterior ao commit confirmado.  
**Situação:** CRUDs de produtos e clientes e criação/consulta por ID de pedidos com itens concluídos. Próxima tarefa: retorno `409 Conflict` nas exclusões de clientes e produtos vinculados.

Este documento registra o andamento e as decisões para permitir a retomada em outra conversa. As regras de trabalho estão no [AGENTS.md](../AGENTS.md); as instruções de execução e os contratos da API estão no [README](../README.md).

## Ponto exato de retomada

O marco de pedidos foi implementado, a migration aplicada e os cenários abaixo verificados manualmente. Gabriel confirmou o commit. Não há confirmação de push desse novo commit, nem de commit desta revisão documental.

**Próxima tarefa:** ajustar `ExcluirCliente` para consultar `_context.Pedidos.AnyAsync(p => p.ClienteId == id)` após o `NotFound()` e antes do `Remove`. Se houver vínculo, retornar `Conflict` com a mensagem `O cliente possui pedidos e não pode ser excluído.`. Depois aplicar a mesma proteção no DELETE de produtos, verificando itens vinculados pelo `ProdutoId`.

As restrições já existem no banco. O tratamento HTTP `409` foi proposto, mas ainda não foi implementado nem testado. Começar pela revisão do método de exclusão de clientes, mantendo a mentoria gradual.

Não recriar modelos, DTOs, migration ou endpoints já concluídos; confira o código e o Git antes de avançar.

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

**Atualização de continuidade:** os três documentos foram revisados após a confirmação do commit do marco de pedidos. A inclusão desta revisão em um novo commit ainda não foi confirmada.

### 6. Pedidos e itens

- Modelos `Pedido` e `ItemPedido`, enums `TipoEntrega` e `StatusPedido`.
- Relacionamentos e restrições de exclusão em `AppDbContext`; migration criada e aplicada. O identificador exato da nova migration não foi informado nesta sessão.
- `CriarPedidoDto` e contrato de entrada dos itens com Data Annotations e validações condicionais via `IValidatableObject`.
- `PedidoRespostaDto` e `ItemPedidoRespostaDto`, sem navegações que produzam ciclos no JSON.
- `GET /api/pedidos/{id}` com `AsNoTracking()`, `Include(p => p.Itens)`, `404` e resposta mapeada.
- `POST /api/pedidos` com verificação de cliente, busca conjunta de produtos, bloqueio de inexistentes/inativos e dicionário por ID para montar os itens.
- Nome e preço copiados do banco para preservar o histórico; quantidade vem do DTO.
- Pedido e itens salvos em uma chamada a `SaveChangesAsync()`, seguida de resposta com `CreatedAtAction` para o GET.
- Mapeamento da resposta ainda repetido no GET e POST; extração posterior planejada.

| Regra consolidada | Implementação |
| --- | --- |
| Entrega | `tipoEntrega = 1`, endereço obrigatório com `Trim()`, taxa R$ 5,00 |
| Retirada | `tipoEntrega = 2`, endereço persistido como `null`, taxa zero |
| Data/status | UTC e `Novo` (`1`) inicialmente |
| Itens | Coleção não vazia, sem elementos nulos e sem produtos repetidos |
| Quantidade | Maior que zero |
| Produtos | Devem existir e estar ativos |
| Subtotal/total | Quantidade × preço histórico; soma dos subtotais + taxa |
| Observações | Opcionais, com `Trim()` |
| Histórico | Nome, preço e endereço registrados no pedido/itens |

## Endpoints concluídos

| Método | Produtos | Clientes | Respostas principais |
| --- | --- | --- | --- |
| GET | `/api/produtos` | `/api/clientes` | `200`, lista ordenada por ID |
| GET | `/api/produtos/{id}` | `/api/clientes/{id}` | `200` ou `404` |
| POST | `/api/produtos` | `/api/clientes` | `201` com objeto e `Location`; `400` para entrada inválida |
| PUT | `/api/produtos/{id}` | `/api/clientes/{id}` | `204`, `400` ou `404` |
| DELETE | `/api/produtos/{id}` | `/api/clientes/{id}` | `204` ou `404` para os casos sem vínculo; `409` para vinculados ainda pendente |

Consultas usam `AsNoTracking()`. As buscas destinadas à atualização preservam rastreamento. Exclusões são físicas quando não há vínculo. Já existem pedidos associados e restrições de exclusão no banco; o tratamento `409` nos controllers está pendente. Não há filtro automático de produtos por `Ativo`, mas a criação de pedido rejeita produtos inativos.

| Método | Rota de pedidos | Respostas implementadas |
| --- | --- | --- |
| GET | `/api/pedidos/{id}` | `200` com DTO e itens; `404` |
| POST | `/api/pedidos` | `201` com DTO e `Location`; `400` para entrada ou referências inválidas |

Listagem, edição, exclusão e alteração de status de pedidos ainda não foram implementadas.

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

**Testes manuais do marco de pedidos, confirmados pelo usuário em 21/10/2026 (horário local; timestamps dos pedidos já aparecem em 22/10 UTC):**

| Cenário | Evidência confirmada |
| --- | --- |
| Pedido inexistente | `404 Not Found` |
| Produto inexistente | `400` com `Um ou mais produtos informados não existem.` |
| Pedido de entrega | Resposta com pedido ID 1, produto ID 2, duas unidades a R$ 18,50, subtotal R$ 37,00, taxa R$ 5,00 e total R$ 42,00 |
| GET do pedido salvo | Usuário confirmou `200` com os mesmos itens e total |
| Retirada com endereço enviado | Pedido ID 2, endereço nulo, taxa zero e total R$ 18,50 |
| Endereço de entrega em espaços | `400`, erro em `EnderecoEntrega` |
| Produto repetido | `400`, erro em `Itens` |
| Quantidade zero | `400`, erro em `Itens[0].Quantidade` |
| Lista vazia | `400`, erro em `Itens` |
| Item nulo | `400`, mensagem própria sem erro interno |
| Modalidade zero | `400`, erro em `TipoEntrega` |
| Cliente inexistente | Mensagem `O cliente informado não existe.` |
| Produto inativo | Mensagem `O pedido contém produtos inativos.` |
| Alterar nome e preço do produto para R$ 20,00 | Pedido ID 1 manteve nome original, preço R$ 18,50, subtotal R$ 37,00 e total R$ 42,00 |

O código revisado usa `CreatedAtAction`, mas o usuário não confirmou separadamente o status `201` nem o cabeçalho `Location` dos POSTs. Também não repetiu o status nas respostas de cliente inexistente e produto inativo. Não registrar essas verificações específicas como executadas. Não houve nova confirmação de reinicialização da API ou compilação ao fechar este marco.

Ainda não há projeto de testes automatizados ou CI. O arquivo `.http` contém cenários manuais; a existência das requisições não comprova sua execução.

## Visão de negócio para as próximas etapas

As definições abaixo orientam a evolução. Criação, itens e entrega/retirada já existem; os demais recursos continuam planejados:

- Produtos abrangem marmitas P/M/G, bebidas, doces e adicionais. Atualmente o modelo é genérico, sem categorias ou tamanhos estruturados.
- Cardápio diário e disponibilidade.
- Pedido com cliente, itens, quantidades, observações e entrega ou retirada.
- Entrega com estimativa, sem promessa de horário exato.
- Fluxo de entrega: Novo → Em preparo → Pronto → Saiu para entrega → Entregue.
- Na retirada: Pronto → Entregue, sem etapa de saída para entrega.
- Pagamento registrado separadamente do andamento, com Pix, crédito, débito ou dinheiro, antes ou depois da entrega.
- Perfis planejados: dono com gestão de produtos/preços, clientes, pedidos, pagamentos, financeiro e funcionários; funcionário com clientes, pedidos/andamento e registro de pagamentos. Detalhes serão consolidados na etapa de autorização.

## Próxima sequência de trabalho

1. Revisar e implementar a proteção no DELETE de clientes, com `AnyAsync` e `409` antes do `Remove`.
2. Testar cliente vinculado (`409`, mantendo dados), cliente sem pedidos (`204`) e ID inexistente (`404`).
3. Implementar e verificar a proteção equivalente de produtos vinculados a itens.
4. Extrair o mapeamento repetido de `PedidoRespostaDto` para um método compartilhado, sem mudar o contrato.
5. Definir a próxima tarefa de pedidos: listagem e/ou transições de status, em pequenos passos.

Essa sequência é de planejamento, não de implementação concluída. Não fazer toda a etapa de uma vez durante a mentoria.

## Pendências e limites conhecidos

| Tema | Situação |
| --- | --- |
| Cancelamento, reembolso e reposição | Histórico recuperado anteriormente apresentou divergências; consolidar as regras antes de implementar |
| Pedidos e itens | Criação e GET por ID concluídos; demais operações pendentes |
| Exclusão de entidades vinculadas | Restrição no banco aplicada; resposta `409` nos controllers pendente |
| Mapeamento de resposta | Duplicado no GET e POST; extração posterior |
| Taxa de entrega | Fixa no backend em R$ 5,00; configuração dinâmica não implementada |
| Autenticação e permissões | Não implementadas |
| Cardápio e pagamentos | Planejados |
| Frontend e deploy | Não iniciados |
| Pesquisa, filtros e paginação | Não implementados |
| Unicidade de telefone ou produto | Não há regra implementada; não presumir que telefone deva ser único |
| Tratamento global de exceções e concorrência | Ainda não implementado de forma própria |
| `WeatherForecast` | Exemplo do template ainda presente; remoção não faz parte do marco atual |
| Testes automatizados | Ainda não implementados |

Não há bloqueio técnico confirmado para retomar o tratamento dos DELETEs vinculados.

## Rotina para manter este registro

Ao concluir um marco, atualize a data, a referência de código quando conhecida, as entregas, os testes efetivamente confirmados e a próxima tarefa. Registre dúvidas sem transformá-las em decisões. Se apenas parte da etapa passou nos testes, mantenha o restante pendente.

Texto sugerido para retomar em uma nova conversa:

> Leia AGENTS.md, docs/PROGRESSO.md, README.md e o código relacionado no repositório. Retome pela próxima tarefa pendente, mantendo a mentoria gradual. Produtos e clientes já têm CRUD com PostgreSQL e pedidos já têm POST e GET por ID com itens. O commit desse marco foi confirmado. Retome pelo retorno `409` no DELETE de clientes vinculados e depois de produtos; não repita etapas concluídas sem razão concreta.
