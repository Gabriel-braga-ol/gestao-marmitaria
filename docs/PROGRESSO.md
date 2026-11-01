# Progresso — Gestão Marmitaria

Atualizado em 01/11/2026, horário de Brasília.
Base: documentos anteriores, código e capturas enviados e verificações confirmadas por Gabriel.
Esta revisão é documental; o assistente não executou a API, PostgreSQL ou testes nesta atualização.

## Ponto de retomada

Último marco implementado: cardápio diário e validação de disponibilidade na criação de pedidos.
Commit `feat: implementar cardápio diário e validar disponibilidade nos pedidos` confirmado por Gabriel; hash e push não informados.
Próxima tarefa: verificar o DELETE de um produto vinculado somente ao cardápio, sem itens em pedidos. Usar um produto de teste para isolar o novo bloqueio; não usar produto com pedidos, pois isso exercitaria a proteção antiga.
README.md e AGENTS.md devem ficar na raiz; PROGRESSO.md em docs/. Esta revisão documental ainda precisa ser aplicada e commitada no repositório local.
Não repetir testes aprovados nem recriar como pendentes os filtros de pagamento/reembolso, cancelamento ou o cardápio já implementado.
A suíte teve 28 testes aprovados, 0 falhas e 0 ignorados, conforme saída enviada em 01/11. Após o ajuste de dependências, Gabriel informou os comandos como feitos e dispensou o envio de novo relatório; ausência dos avisos MSB3277 não foi confirmada.
O assistente revisou os arquivos/trechos enviados, mas não executou a aplicação, os testes ou o Git do usuário.

## Marcos concluídos

| Marco | Entrega |
| --- | --- |
| Base | API .NET 10, PostgreSQL, EF Core, migrations e configuração local |
| Produtos/clientes | CRUD, validações e normalização de telefone |
| Pedidos | Criação com itens, consulta por ID, preços históricos e totais |
| Proteção do histórico | DELETE vinculado retorna 409; exclusão sem vínculo continua disponível |
| Entrega/retirada | Endereço condicional, taxa configurável e preservação dos valores históricos |
| Andamento | PATCH e fluxos distintos para entrega e retirada |
| Consulta | Filtros por status/cliente, ordenação estável e paginação |
| Testes unitários | Projeto GestaoMarmitaria.Tests, xUnit, 28 testes de status aprovados em 01/11, conforme saída enviada |
| Período | Datas opcionais, validação e conversão de Brasília para UTC |
| Refatoração | Auxiliares de filtros, paginação, produtos, montagem e mapeamento |
| Pagamentos | Modelo, enum, DTOs, relacionamento, migration, GET/POST e bloqueio de duplicidade |
| Filtro por pagamento | Pago opcional, navegação Pedido.Pagamento e combinações com paginação |
| Cancelamento | Novo/EmPreparo → Cancelado, bloqueios e testes da regra |
| Reembolsos | Modelo, mapeamento, migration aplicada, DTO, GET/POST e proteção contra duplicidade |
| Filtro de reembolso pendente | Contrato bool?, navegação, consulta combinável e paginação; verificações manuais confirmadas |
| Cardápio diário | Tipos de produto, itens por data, cadastro, consultas, disponibilidade e validação em novos pedidos; verificação isolada de exclusão ainda pendente |

## Decisões que orientam a continuidade

- A regra está em `Regras/RegrasStatusPedido.cs`, separada do controller e chamada pelo PATCH.
- A extração usa uma classe estática, não um serviço registrado na injeção de dependência.
- O controller continua acessando `AppDbContext`; não há camada própria de serviços/repositórios.
- `AplicarFiltros` compõe `IQueryable`; `CountAsync` e `ToListAsync` executam as consultas.
- `BuscarPaginaAsync` preserva deslocamento em `long`, ordenação por DataCriacao/Id decrescentes e página excedente vazia.
- `CarregarProdutosDoPedidoAsync` carrega produtos em conjunto e retorna dicionário ou mensagem de erro.
- `MontarPedido`/`CriarItemPedido` preservam dados históricos; pedido e itens usam um SaveChangesAsync.
- `ConsultarPedidosDto` implementa `IValidatableObject`; DataInicial/DataFinal são `DateOnly?`.
- Datas iguais ou apenas um limite são aceitos; início posterior ao fim retorna 400 automático com `errors`.
- O período inclui ambos os dias em Brasília: limite inicial inclusivo e próximo dia exclusivo, convertidos para UTC.
- O auxiliar `ConverterInicioDoDiaParaUtc` usa America/Sao_Paulo e avança por minutos enquanto IsInvalidTime.
- Data final 9999-12-31 retorna 400 com `mensagem`, evitando estouro no AddDays(1).
- A taxa atual é 5 em Entrega:Taxa; retirada usa zero. Pedidos antigos mantêm os valores.
- Taxa ausente/negativa falha no construtor do controller; não há validação própria no startup.
- A documentação define Async como convenção; ações legadas enviadas ainda não seguem o sufixo.
- Validações manuais usam `mensagem`; Data Annotations mantêm o formato automático `errors`.

## Pagamentos: implementação concluída

- `Pagamento`: Id, PedidoId, FormaPagamento, Valor e DataPagamento; navegação Pedido inicializada com `null!`.
- `DataPagamento` recebe `DateTime.UtcNow`; `Valor` usa numeric(18,2).
- `FormaPagamento`: Pix=1, Dinheiro=2, CartaoCredito=3 e CartaoDebito=4.
- Pedido possui navegação opcional Pagamento, mapeada em WithOne(p => p.Pagamento).
- Um pedido pode ter zero ou um pagamento; todo pagamento exige pedido existente. Registro manual, sem integração bancária.
- `AppDbContext.Pagamentos`: relação HasOne/WithOne, FK PedidoId, exclusão Restrict e índice único IX_Pagamentos_PedidoId.
- Migration `20261030022657_CriarTabelaPagamentos` revisada e aplicada; tabela confirmada no pgAdmin.
- `RegistrarPagamentoDto` recebe somente FormaPagamento; EnumDataType rejeita 0, 99 e omissão.
- `PagamentoRespostaDto`: Id, PedidoId, FormaPagamento, Valor e DataPagamento; sem navegação Pedido.
- `GET /api/pedidos/{id}/pagamento`: busca por PedidoId com AsNoTracking; 200 com DTO ou 404 sem registro.
- O GET não distingue pedido inexistente de pedido sem pagamento; ambos retornam 404.
- `POST /api/pedidos/{id}/pagamento`: busca pedido e itens; inexistente 404, pagamento existente 409, cadastro 201.
- Pedido cancelado não aceita novo pagamento: 409. Na implementação enviada, duplicidade é verificada antes do cancelamento, definindo prioridade da mensagem.
- O valor vem de Sum(Quantidade * PrecoUnitario) + TaxaEntrega; não recebe valor/data do consumidor nem altera o status.
- Auxiliares extraídos: `CalcularTotalPedido`, `MontarPagamento` e `MapearPagamentoParaResposta`.
- `CreatedAtAction` aponta para GetPagamentoPorPedidoAsync usando PedidoId; o GET tem ActionName explícito para preservar o sufixo Async.
- AnyAsync verifica duplicidade; catch filtrado de DbUpdateException trata UniqueViolation apenas de IX_Pagamentos_PedidoId como 409.
- A proteção simultânea existe no banco/código, mas ainda não foi exercitada com requisições concorrentes.

## Sessão de 31/10 — Filtro, cancelamento e reembolso

- Pago: bool? no ConsultarPedidosDto; omitido não filtra, true exige pagamento, false exige ausência. Aplicado em AplicarFiltros antes da contagem/paginação.
- Filtro utiliza navegação sem Include de pagamento. Compilação confirmada e has-pending-model-changes sem alterações; não houve migration para essa etapa.
- Filtros simples/combinados, paginação, página excedente e pago inválido confirmados manualmente.
- Cancelado=6 preserva numeração anterior. RegrasStatusPedido permite cancelar em Novo/EmPreparo, para entrega e retirada; demais estados bloqueados e Cancelado terminal.
- A suíte passou de 15 para 28 casos previstos: quatro de cancelamento permitido, sete bloqueados e duas execuções com cinco destinos cada para Cancelado. Aprovação confirmada nas etapas; contagem final não exibida.
- PATCH existente passou a aplicar a regra; DTO EnumDataType aceita 6. A ação legada permanece sem sufixo Async.
- Pedido 1 cancelado com pagamento de 42,00 preservado; pedido 11 cancelado em preparo e sem pagamento; pedido 12 entregue teve cancelamento bloqueado.
- Reembolso: Id, PagamentoId, Valor decimal e DataReembolso com DateTime.UtcNow; navegação Pagamento = null!.
- Contexto: DbSet Reembolsos, Valor numeric(18,2), HasOne/WithOne, FK PagamentoId, Restrict e índice único IX_Reembolsos_PagamentoId.
- Migration 20261031232345_CriarTabelaReembolsos revisada (Up/Down), aplicada e tabela confirmada. Preservar designer e snapshot.
- DTO ReembolsoRespostaDto com Id, PagamentoId, Valor e DataReembolso; sem data inicializada nem navegação.
- Auxiliares MontarReembolso (copia Pagamento.Valor integralmente) e MapearReembolsoParaResposta.
- GET /api/pedidos/{id}/reembolso usa AsNoTracking e r.Pagamento.PedidoId; 200 com DTO ou 404 sem registro/inexistente. ActionName explícito preserva nome Async para CreatedAtAction.
- Houve NullReferenceException por mapear ausência de reembolso; corrigida a verificação null antes do mapeamento, e 404 confirmado.
- POST /api/pedidos/{id}/reembolso não recebe corpo: pedido inexistente 404; não cancelado, sem pagamento ou duplicado 409; sucesso 201 com DTO e Location.
- AnyAsync verifica duplicidade e catch filtrado trata somente UniqueViolation de IX_Reembolsos_PagamentoId. Proteção simultânea ainda não exercitada.
- Reembolso confirma devolução manual já realizada; não transfere dinheiro. Preserva pagamento original e pedido Cancelado.
- Pendência de devolução é inferida: cancelado com pagamento e sem reembolso. Não há coluna própria; o filtro específico foi implementado na continuação descrita abaixo.
- Pedido 1: cadastro/consulta do reembolso de 42,00 confirmados; duplicidade bloqueada. Pedido 11 sem pagamento e pedido 12 entregue bloqueados. Inexistente 404 confirmado.
- Após reinício: mesmo reembolso (ID, valor, data), pagamento original e status 6 confirmados por mensagem.
- Nenhuma execução foi feita pelo assistente; não há testes automatizados de HTTP/PostgreSQL ou reembolso confirmados.

## Continuação de 31/10 — Filtro de reembolso pendente

- Adicionado bool? ReembolsoPendente ao ConsultarPedidosDto.
- True: Cancelado e Pagamento != null e Pagamento.Reembolso == null; false: complemento dessas condições; omitido não aplica filtro.
- False inclui não cancelados, cancelados sem pagamento e já reembolsados, não apenas reembolsados.
- Adicionada navegação opcional Reembolso? em Pagamento, associada no WithOne(p => p.Reembolso) do mapeamento de Reembolso.
- Compilação confirmada; saída de has-pending-model-changes confirmou ausência de alterações. Não foi necessária migration.
- AplicarFiltros usa navegações sem Include, antes da contagem e paginação, preservando filtros existentes.
- Pedido 5, pago em 24,00 e inicialmente Novo, foi cancelado para criar pendência. Inclusão em true e exclusão em false confirmadas.
- Pedidos 1 (reembolsado), 11 (cancelado sem pagamento) e 12 (entregue) conferidos como sem pendência pelo usuário.
- Confirmados manualmente: true/false, totais, true combinado com pago=false (vazio), página de tamanho 1, página excedente e parâmetro inválido (400).
- Reembolso do pedido 5 registrado no teste; Gabriel confirmou sua saída de true e inclusão em false após orientação de como conferir os objetos de pedido e a consulta individual do reembolso.
- A captura isolada mostrava 200 em false e pedidos 6/4; a confirmação da mudança do pedido 5 veio depois por mensagem, não pela captura.
- Testes do filtro foram manuais. Não houve nova execução confirmada da suíte unitária nesta etapa nem testes automatizados do filtro.
- Commit da implementação confirmado por mensagem. Documentação anterior também teve commit confirmado; esta nova revisão documental ainda deve ser aplicada e commitada localmente.

## Sessão de 01/11 — Cardápio diário

### Regras e implementação

- Disponibilidade apenas por indicador, sem controle de quantidade/estoque. Cardápio tem data explícita e pode ser preparado para dias futuros.
- `TipoProduto`: Marmita=1 e Bebida=2; `Produto.Tipo` incluído no modelo, DTOs de criação/atualização e atribuições do controller.
- `EnumDataType` rejeita 0, 99 e omissão de Tipo; verificação manual confirmada. Cadastro de bebida e edição de Tipo (1 e retorno para 2) confirmados.
- Migration `20261101180416_AdicionarTipoProduto`: coluna integer não nula, defaultValue ajustado de 0 para 1 antes da aplicação. A captura do banco mostrava apenas marmitas existentes (IDs 2, 3, 5, 6 e 7); aplicação com DEFAULT 1 e Done exibida.
- `ItemCardapio`: Id, Data (DateOnly), ProdutoId, Disponivel=true e navegação Produto=null!.
- `AppDbContext.ItensCardapio`: FK ProdutoId com Restrict e índice único `IX_ItensCardapio_Data_ProdutoId`. Índice adicional por ProdutoId criado pelo EF.
- Migration `20261101183238_CriarTabelaItensCardapio` revisada em Up/Down; aplicação confirmada por Gabriel. Data usa date; Disponivel usa boolean, sem default SQL configurado. Preservar designer e snapshot.
- Exclusão de produto: Gabriel reuniu AnyAsync de ItensPedido e ItensCardapio em um if com ||, retornando 409 e mensagem comum. Compilação confirmada; bloqueio por vínculo somente com cardápio ainda não verificado.
- `CriarItemCardapioDto`: Data DateOnly? com Required e ProdutoId com Range(1, int.MaxValue). Disponivel não é recebido; usa inicialização do modelo.
- `ItemCardapioRespostaDto`: Id, Data, ProdutoId, NomeProduto, Preco e Disponivel. Nome/preço vêm do catálogo atual; não são cópias históricas do cardápio.
- `AtualizarDisponibilidadeCardapioDto`: bool? Disponivel com Required, distinguindo false de campo omitido/null.
- `CardapioController` usa contexto injetado diretamente. GET por ID e listagem projetam DTO com Select, sem Include e com AsNoTracking.
- GET /api/cardapio/{id}: 200 ou 404; ActionName explícito preserva GetItemCardapioPorIdAsync para Location.
- POST /api/cardapio: produto inexistente, bebida ou produto inativo retornam 400; duplicidade por data/produto retorna 409; cadastro retorna 201 com DTO e Location.
- POST verifica duplicidade com AnyAsync e trata somente UniqueViolation do índice IX_ItensCardapio_Data_ProdutoId como 409. Concorrência não foi testada.
- GET /api/cardapio?data=AAAA-MM-DD: data obrigatória; 200 com lista ordenada por nome/Id; sem registros retorna []; ausência de data retorna 400 automático. Lista administrativa inclui itens indisponíveis e não filtra Produto.Ativo.
- PATCH /api/cardapio/{id}/disponibilidade: consulta rastreada, altera apenas Disponivel, salva e retorna 204; inexistente 404. Não altera Produto.Ativo nem outros dias.
- `CarregarProdutosDoPedidoAsync`: após validar existência/Ativo, separa marmitas e calcula a data atual a partir de DateTime.UtcNow convertido para America/Sao_Paulo. Conta itens disponíveis para os IDs nessa data; contagem diferente bloqueia com 400 e mensagem de marmitas indisponíveis.
- A validação permanece dentro do auxiliar, antes de montar/salvar pedido. Bebidas precisam estar ativas e não exigem vínculo com cardápio. Pedido com qualquer marmita inválida é rejeitado inteiro.
- Datas de pedido continuam UTC. Alterar disponibilidade não modifica pedidos históricos. A leitura de disponibilidade e a gravação não têm coordenação transacional própria contra alterações simultâneas.

### Verificações manuais confirmadas

- Cardápio: cadastro, consulta por ID, 404 inexistente, duplicidade 409, bebida/inexistente/inativo 400, data ausente e ProdutoId=0 com 400 automático.
- Mesmo produto em 01/11 e 02/11 cadastrado; consulta de 01/11 não mistura 02/11; consulta sem data 400 e data sem itens 200 com [].
- PATCH false retornou 204; item permaneceu na listagem como indisponível e outro dia permaneceu disponível. Corpo vazio 400, retorno a true e consulta confirmados; inexistente 404.
- Ao iniciar o teste de pedidos, produto 2 ainda estava inativo, apesar de confirmação anterior. GET exibiu ativo=false; após PUT de reativação, criação funcionou.
- Pedido 14 exibido: retirada, produto 2, quantidade 1, preço/total 24,00 e taxa zero. Depois de indisponibilizar a marmita, GET do pedido 14 exibiu os mesmos dados.
- Novo pedido com marmita indisponível bloqueado; pedido 15 com bebida ativa 10 (Refrigerante lata 350 ml), total 6,00 e taxa zero exibido.
- Pedido misto com bebida 10 e marmita 2 indisponível bloqueado; após disponibilizar novamente, pedido misto com total esperado 30,00 confirmado por Gabriel, sem JSON/ID enviado.
- Produto 3 ativo fora do cardápio de hoje bloqueado. Inclusão somente no cardápio de 02/11 e manutenção do bloqueio em 01/11 confirmadas.
- Dados de teste ao encerrar: produto 2 reativado e item de 01/11 disponível; produto 2 também cadastrado em 02/11; produto 3 cadastrado somente em 02/11. Não presumir que esses estados continuarão iguais na retomada nem usar datas fixas antigas como hoje.

### Testes, dependências e Git

- Saída enviada: 28 testes, 28 bem-sucedidos, 0 falhas, 0 ignorados, duração de 4,8s; build com 3 avisos MSB3277 por EF Core/Relational/Abstractions 10.0.4 versus 10.0.13.
- Csproj de testes enviado não possui referência direta ao EF Core; referencia a API. API possui OpenApi 10.0.12, EFCore.Design 10.0.13 com PrivateAssets=all e Npgsql.EntityFrameworkCore.PostgreSQL 10.0.3.
- Orientado adicionar referência explícita Microsoft.EntityFrameworkCore.Relational 10.0.13 à API e executar clean, restore e test. Gabriel confirmou a execução, mas dispensou o relatório; não afirmar zero avisos nem nova contagem verificada.
- Os 28 testes automatizados cobrem status; não houve criação de testes automatizados de cardápio ou integração HTTP/PostgreSQL.
- Commit do cardápio confirmado por mensagem; comandos de status/diff informados como feitos, mas saídas não enviadas. Hash, diff final e push não inspecionados.

## Verificações confirmadas

As execuções abaixo ocorreram no ambiente do usuário; não constituem execução pelo assistente.

| Grupo | Evidência |
| --- | --- |
| CRUD | Sucesso, entrada inválida, inexistência, exclusão e persistência após reinício, nas sessões anteriores |
| Pedidos | Entrega/retirada, dados históricos, totais, cliente/produto inválido e produto inativo |
| Exclusões | Vínculo retorna 409 e preserva registro; sem vínculo 204; repetição 404 |
| Status HTTP | Fluxos, conflitos 409, enum inválido 400 e pedido inexistente 404 |
| Paginação | Filtros combinados, padrões 1/20, limites, totais e página muito alta |
| Unidade | 28 testes de status aprovados, 0 falhas e 0 ignorados; saída enviada em 01/11, antes do ajuste de dependências |
| Após refatoração | Compilação, testes existentes e criação com entrega/retirada confirmados |
| Datas | 11 cenários manuais propostos no .http, confirmados pelo usuário como concluídos |
| Pagamentos | Compilação; POST 201 e Location; GET 200; valor integral e status preservado; repetição 409; enum inválido/omitido 400 |
| Persistência do pagamento | Pedido inexistente 404 e mesmo pagamento consultado após reiniciar a API, confirmados pelo usuário |

Os cenários de datas abrangem consulta sem período, limites isolados, mesmo dia, vários dias,
combinação com cliente/status/paginação, período invertido, data impossível, data máxima e 04/11/2018.
A confirmação do conjunto veio por mensagem; não foram fornecidos corpos das respostas ou logs individuais.
Os testes unitários cobrem a regra de status; não são testes de integração HTTP/PostgreSQL.
Para pagamentos, foi enviada captura do 404 inicial; as demais verificações foram confirmadas por mensagem, sem respostas completas.
Na sessão de 31/10, a contagem final não havia sido exibida; em 01/11 foi fornecida a saída de 28 testes aprovados. O resultado após o ajuste de dependências não foi enviado.

## Git e documentação

- Paginação: commit confirmado ao encerrar a sessão de 23/10.
- Extração da regra: comandos preparados; o status posterior não mostrava alterações nessa classe/controller.
- Testes: commit `test: cover order status transition rules` confirmado.
- Datas/refatoração: commit `feat: filter orders by date range and refactor order operations` confirmado.
- Pagamentos: commit `feat: register and retrieve full order payments` confirmado por Gabriel.
- A captura pré-commit mostrou controller, contexto, .http e snapshot modificados; modelo, enum, dois DTOs e migration/designer novos.
- Hashes não informados; o assistente não inspecionou o repositório nem executou os commits.
- Filtro por pagamento e documentação: commits confirmados em 31/10.
- Cancelamento/reembolso: commit sugerido feat: permitir cancelamento de pedidos e registrar reembolso integral; Gabriel informou que subiu tudo, sem hash ou verificação remota.
- Descrições de commits agora em pt-BR, com prefixos convencionais.
- Não inventar hashes nem confundir confirmação do usuário com inspeção própria.
- Aplicar README.md e AGENTS.md na raiz; PROGRESSO.md em docs/.

## Pendências

- Próxima tarefa: testar exclusão de produto vinculado somente ao cardápio, sem pedidos, para isolar o novo bloqueio 409.
- Ampliar cobertura de integração dos filtros Pago/ReembolsoPendente quando pertinente.
- Cardápio implementado; ampliar testes de integração quando pertinente. Implementar autenticação/perfis, frontend React/TypeScript e relatórios.
- Avaliar regra para alteração de Tipo de produto já vinculado ao cardápio; PUT atual permite alterar Tipo sem proteção específica.
- Confirmar eliminação dos avisos MSB3277 se houver necessidade de nova execução; não exigir relatório já dispensado apenas para encerrar a sessão.
- Ampliar automação para datas/integração quando pertinente; configurar CI e deploy.
- Testar pagamentos/reembolsos simultâneos e ampliar cobertura de integração desses fluxos.
- Tratar corrida entre registro de pagamento e cancelamento: a validação atual lê o status antes de salvar, sem bloqueio/coordenação transacional que garanta esse vínculo entre requisições concorrentes.
- Tratar concorrência do PATCH e das exclusões; contagem/página não usam snapshot transacional próprio.
- Avaliar pesquisa e filtros de produtos/clientes conforme a necessidade.
- Swagger UI não configurada; OpenAPI disponível em desenvolvimento.
- Confirmar alterações no Git local antes da próxima implementação; não há bloqueio técnico confirmado.
