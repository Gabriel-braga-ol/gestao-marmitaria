# Progresso — Gestão Marmitaria

**Atualizado em:** sessão de 23/10/2026 (horário de Brasília).  
**Referência anterior inspecionada:** commit `319f938` (`fix: small changes`), branch `main`, antes do marco de pedidos. Esta atualização usa os trechos e resultados fornecidos na sessão; não houve nova inspeção do repositório.  
**Versionamento:** commit original de pedidos confirmado em 21/10; hash e push não informados. Commits sugeridos nesta sessão e aplicação desta revisão ao repositório ainda não confirmados.  
**Situação:** exclusões protegidas, mapeamento compartilhado, taxa configurável, listagem, transições de status, filtros e paginação concluídos e verificados manualmente pelo usuário.

Este documento registra o andamento e as decisões para permitir a retomada em outra conversa. As regras de trabalho estão no [AGENTS.md](../AGENTS.md); as instruções de execução e os contratos da API estão no [README](../README.md).

## Ponto exato de retomada

Última tarefa concluída: paginação de `GET /api/pedidos`, com o método completo revisado e os testes propostos confirmados por Gabriel com “tudo certo”. A resposta usa `PedidosPaginadosRespostaDto`; não retorna mais uma lista direta.

Esta revisão atualiza os três documentos. Ainda é necessário copiar/aplicar os arquivos ao repositório local, com `PROGRESSO.md` em `docs/`, e verificar o Git. Não houve nova inspeção do repositório ou execução da aplicação pelo assistente. Nenhum dos commits sugeridos nesta sessão foi explicitamente confirmado.

Próximo passo sugerido após conferir documentação/Git: introduzir testes automatizados das transições de status e discutir uma extração mínima da regra para permitir testes diretos. Essa é uma recomendação de continuidade, ainda não aprovada como próximo marco. Pagamentos, cardápio, filtros por data e autenticação permanecem futuros; nenhuma dessas tarefas foi iniciada.

Não repetir os testes manuais já confirmados sem mudança ou dúvida concreta; não recriar DTOs/endpoints concluídos.

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
- Mapeamento inicialmente repetido no GET e POST; nesta sessão foi extraído para `MapearPedidoParaResposta`, também usado na listagem.

| Regra consolidada | Implementação |
| --- | --- |
| Entrega | `tipoEntrega = 1`, endereço obrigatório com `Trim()`, taxa de `Entrega:Taxa`, adotada em R$ 5,00 |
| Retirada | `tipoEntrega = 2`, endereço persistido como `null`, taxa zero |
| Data/status | UTC e `Novo` (`1`) inicialmente |
| Itens | Coleção não vazia, sem elementos nulos e sem produtos repetidos |
| Quantidade | Maior que zero |
| Produtos | Devem existir e estar ativos |
| Subtotal/total | Quantidade × preço histórico; soma dos subtotais + taxa |
| Observações | Opcionais, com `Trim()` |
| Histórico | Nome, preço e endereço registrados no pedido/itens |

### 7. Exclusões, mapeamento e taxa configurável

- Cliente vinculado: `Pedidos.AnyAsync(p => p.ClienteId == id)` antes do Remove; `409` com `O cliente possui pedidos e não pode ser excluído.`.
- Produto vinculado: `ItensPedido.AnyAsync(i => i.ProdutoId == id)`; `409` com `O produto possui pedidos e não pode ser excluído.`.
- `404` para registro inexistente e `204` para exclusão sem vínculo mantidos.
- `MapearPedidoParaResposta(Pedido pedido)` privado estático centraliza a resposta. No POST é chamado após SaveChangesAsync para incluir IDs gerados.
- `IConfiguration` e `_taxaEntrega` no controller; `Entrega:Taxa` em appsettings.json. Ausência/valor negativo lança InvalidOperationException no construtor. Não é validação de startup; texto não numérico falha na conversão.
- Taxa copiada apenas para novos pedidos de entrega; retirada zero e histórico preservado. Alteração testada de 5 para 7 e restauração para 5 confirmada na sequência.

### 8. Listagem e transições de status

- `GET /api/pedidos` com AsNoTracking, Include(Itens), DataCriacao e Id decrescentes; resposta depois evoluída para paginação.
- `AtualizarStatusPedidoDto` com EnumDataType. Status: Novo=1, EmPreparo=2, Pronto=3, SaiuParaEntrega=4, Entregue=5.
- `PATCH /api/pedidos/{id}/status` busca com rastreamento, retorna 404 se inexistente, verifica `PodeAlterarStatus`, atribui o status e salva; sucesso 204.
- `PodeAlterarStatus` privado estático: Novo→EmPreparo→Pronto; entrega Pronto→SaiuParaEntrega→Entregue; retirada Pronto→Entregue.
- Saltos, retornos, repetição e alteração após entregue retornam 409 com `A transição de status solicitada não é permitida.`. Enum inválido ou omitido: 400. Cancelamento não implementado.

### 9. Filtros e paginação de pedidos

- `ConsultarPedidosDto` via FromQuery com Status e ClienteId opcionais. EnumDataType e Range validam automaticamente; respostas inválidas usam errors, substituindo as validações manuais anteriores.
- `Pagina` padrão 1, Range 1–int.MaxValue; `TamanhoPagina` padrão 20, Range 1–100.
- Filtros combinados com AND antes de executar; cliente positivo inexistente retorna página vazia, não 404.
- CountAsync após filtros conta TotalRegistros. Deslocamento `(long)(filtros.Pagina - 1) * filtros.TamanhoPagina` evita estouro. Busca ocorre somente se deslocamento < total, com ordenação, Skip e Take no banco.
- `PedidosPaginadosRespostaDto`: Itens (lista de PedidoRespostaDto), Pagina, TamanhoPagina, TotalRegistros e TotalPaginas calculado com Math.Ceiling. A listagem deixou de retornar uma lista direta.
- Página além dos resultados retorna 200, itens vazios e totais preservados; ausência de correspondências gera totais zero. Paginação e contagem usam consultas distintas, sem snapshot transacional próprio.

## Endpoints concluídos

| Método | Produtos | Clientes | Respostas principais |
| --- | --- | --- | --- |
| GET | `/api/produtos` | `/api/clientes` | `200`, lista ordenada por ID |
| GET | `/api/produtos/{id}` | `/api/clientes/{id}` | `200` ou `404` |
| POST | `/api/produtos` | `/api/clientes` | `201` com objeto e `Location`; `400` para entrada inválida |
| PUT | `/api/produtos/{id}` | `/api/clientes/{id}` | `204`, `400` ou `404` |
| DELETE | `/api/produtos/{id}` | `/api/clientes/{id}` | `204` sem vínculo, `404` inexistente e `409` para vinculados |

Consultas usam `AsNoTracking()`. As buscas destinadas à atualização preservam rastreamento. Exclusões são físicas quando não há vínculo. Já existem pedidos associados e restrições de exclusão no banco; os controllers verificam o vínculo e retornam `409`. Não há filtro automático de produtos por `Ativo`, mas a criação de pedido rejeita produtos inativos.

| Método | Rota de pedidos | Respostas implementadas |
| --- | --- | --- |
| GET | `/api/pedidos` | `200` com objeto paginado e filtros; `400` para parâmetros inválidos |
| GET | `/api/pedidos/{id}` | `200` com DTO e itens; `404` |
| POST | `/api/pedidos` | `201` com DTO e `Location`; `400` para entrada ou referências inválidas |

Listagem e alteração de status estão concluídas. Edição geral e exclusão de pedidos ainda não foram implementadas.

`PATCH /api/pedidos/{id}/status`: `204`, `400`, `404` ou `409`, conforme o contrato acima.

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

No encerramento do marco de 21/10, `201` e `Location` não tiveram confirmação separada. Na sessão de 23/10, a captura do pedido 10 confirmou ambos e o GET correspondente foi confirmado pelo usuário. Também não repetiu o status nas respostas de cliente inexistente e produto inativo. Não registrar essas verificações específicas como executadas. Não houve nova confirmação de reinicialização da API ou compilação ao fechar este marco.

Ainda não há projeto de testes automatizados ou CI. O arquivo `.http` contém cenários manuais; a existência das requisições não comprova sua execução.

### Testes confirmados na sessão de 23/10/2026

Os testes abaixo foram executados manualmente por Gabriel e confirmados na conversa, não pelo assistente:

| Etapa | Evidência |
| --- | --- |
| DELETE de cliente | Com pedidos 409 e GET 200 após tentativa; sem pedidos 204; repetição 404 |
| DELETE de produto | Com itens 409 e preservação; sem vínculo 204; repetição 404 |
| Mapeamento | POST 201 e Location observado na captura do pedido 10; consulta posterior confirmada e dados preservados |
| Taxa | Novas entregas com 5 e 7; pedido anterior mantém 5; retirada zero; sequência com restauração a 5 confirmada |
| Listagem | Captura 200: pedido 13 antes do 12; retirada com subtotal/total 24 e taxa zero; pedido 12 com taxa histórica 7. Comparação do GET por ID confirmada |
| Transições | Fluxos completos de entrega e retirada; retirada bloqueia 3→4; saltos, repetição, retorno e pós-entrega 409; 0/99/corpo vazio 400; inexistente com status válido 404; conferência via GET |
| Filtros | Status e cliente isolados e combinados; sem correspondências; status 0/99 e cliente 0/negativo rejeitados |
| DTO de consulta | Filtros preservados e erros automáticos em errors para status 99 e cliente 0 |
| Paginação | Páginas 1/2 com tamanho 5 sem sobreposição nos dados testados; filtros, contagem, arredondamento e ordenação; página int.MaxValue com tamanho 100 vazia; página 0/tamanho 101 rejeitados; padrões 1/20 |

A confirmação de paginação foi a última recebida. Não foram executados novos testes de configuração ausente/negativa, concorrência ou automação nesta revisão. A orientação de compilar acompanhou as verificações, mas não houve log de build fornecido nesta atualização.

## Visão de negócio para as próximas etapas

As definições abaixo orientam a evolução. Criação, itens, entrega/retirada, listagem e fluxo de status já existem; cardápio, pagamentos e perfis continuam planejados:

- Produtos abrangem marmitas P/M/G, bebidas, doces e adicionais. Atualmente o modelo é genérico, sem categorias ou tamanhos estruturados.
- Cardápio diário e disponibilidade.
- Pedido com cliente, itens, quantidades, observações e entrega ou retirada.
- Entrega com estimativa, sem promessa de horário exato.
- Fluxo de entrega: Novo → Em preparo → Pronto → Saiu para entrega → Entregue.
- Na retirada: Pronto → Entregue, sem etapa de saída para entrega.
- Pagamento registrado separadamente do andamento, com Pix, crédito, débito ou dinheiro, antes ou depois da entrega.
- Perfis planejados: dono com gestão de produtos/preços, clientes, pedidos, pagamentos, financeiro e funcionários; funcionário com clientes, pedidos/andamento e registro de pagamentos. Detalhes serão consolidados na etapa de autorização.

## Próxima sequência de trabalho

1. Aplicar esta documentação ao repositório: README.md e AGENTS.md na raiz; PROGRESSO.md em docs/.
2. Conferir git status/diff e histórico para identificar o que ainda precisa de commit. Não presumir os commits anteriormente sugeridos como feitos.
3. Escolher o próximo marco com Gabriel. Recomendação: testes automatizados da regra de status, com extração mínima para torná-la testável e cobertura de entrega, retirada e transições inválidas.
4. Após consolidar essa cobertura, avaliar filtros por data ou registro separado de pagamentos, em pequenas tarefas. São opções futuras, não funcionalidades autorizadas ou iniciadas nesta revisão.

As etapas de exclusão, mapeamento, taxa, status, filtros e paginação já terminaram; não retomá-las como pendências.

## Pendências e limites conhecidos

| Tema | Situação |
| --- | --- |
| Cancelamento, reembolso e reposição | Histórico recuperado anteriormente apresentou divergências; consolidar as regras antes de implementar |
| Pedidos e itens | Criação, GET por ID, listagem paginada e PATCH de status concluídos; edição geral/exclusão não implementadas |
| Exclusão de entidades vinculadas | Restrição no banco e resposta `409` nos controllers implementadas e verificadas |
| Mapeamento de resposta | Centralizado em MapearPedidoParaResposta e reutilizado nos três caminhos |
| Taxa de entrega | Configurável por Entrega:Taxa; adotada em 5, retirada zero e histórico preservado; sem interface administrativa |
| Autenticação e permissões | Não implementadas |
| Cardápio e pagamentos | Planejados |
| Frontend e deploy | Não iniciados |
| Pesquisa, filtros e paginação | Pedidos: status/cliente e paginação implementados; data/pesquisa e filtros de produtos/clientes pendentes |
| Unicidade de telefone ou produto | Não há regra implementada; não presumir que telefone deva ser único |
| Tratamento global de exceções e concorrência | Ainda não implementado de forma própria |
| `WeatherForecast` | Exemplo do template ainda presente; remoção não faz parte do marco atual |
| Testes automatizados | Ainda não implementados |

Não há bloqueio técnico confirmado. Controle de concorrência do PATCH e das exclusões não foi implementado; contagem/listagem paginada podem observar mudanças entre consultas concorrentes.

## Rotina para manter este registro

Ao concluir um marco, atualize a data, a referência de código quando conhecida, as entregas, os testes efetivamente confirmados e a próxima tarefa. Registre dúvidas sem transformá-las em decisões. Se apenas parte da etapa passou nos testes, mantenha o restante pendente.

Texto sugerido para retomar em uma nova conversa:

> Leia AGENTS.md, docs/PROGRESSO.md, README.md e o código relacionado, confira o Git e mantenha a mentoria gradual. A paginação de pedidos foi a última tarefa concluída e verificada. DELETEs protegidos, mapeamento compartilhado, taxa configurável, PATCH de status e filtros por cliente/status já estão concluídos. Confira se a documentação foi aplicada e se os commits sugeridos foram feitos. O próximo marco ainda precisa ser escolhido; a recomendação é começar a cobertura automatizada das transições de status, sem refazer funcionalidades concluídas.
