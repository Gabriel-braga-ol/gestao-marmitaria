# Progresso — Gestão Marmitaria

Atualizado em 31/10/2026, horário de Brasília.
Base: documentos anteriores, código e capturas enviados e verificações confirmadas por Gabriel.
Esta revisão é documental; o assistente não executou a API, PostgreSQL ou testes nesta atualização.

## Ponto de retomada

Último marco concluído: cancelamento e registro/consulta de reembolso integral, com verificações manuais confirmadas.
Gabriel informou commit e envio das alterações ("subi tudo"); hashes e estado remoto não foram inspecionados.
A captura enviada mostrava o estado anterior ao commit, com arquivos modificados/novos; não comprova push por si só.
Esta atualização documental deve substituir README.md e AGENTS.md na raiz e docs/PROGRESSO.md no repositório local e receber novo commit.
Próximo passo sugerido: definir consulta de reembolsos pendentes; ainda não iniciada nem acordada como próxima implementação.
Não recriar filtros de pagamento, cancelamento ou reembolso como pendentes. Concorrência permanece sem teste específico.

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
| Testes unitários | Projeto GestaoMarmitaria.Tests, xUnit, ampliado de 15 para 28 casos previstos; aprovação confirmada nas etapas |
| Período | Datas opcionais, validação e conversão de Brasília para UTC |
| Refatoração | Auxiliares de filtros, paginação, produtos, montagem e mapeamento |
| Pagamentos | Modelo, enum, DTOs, relacionamento, migration, GET/POST e bloqueio de duplicidade |
| Filtro por pagamento | Pago opcional, navegação Pedido.Pagamento e combinações com paginação |
| Cancelamento | Novo/EmPreparo → Cancelado, bloqueios e testes da regra |
| Reembolsos | Modelo, mapeamento, migration aplicada, DTO, GET/POST e proteção contra duplicidade |

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
- Pendência de devolução é inferida: cancelado com pagamento e sem reembolso. Não há coluna própria nem filtro específico implementado.
- Pedido 1: cadastro/consulta do reembolso de 42,00 confirmados; duplicidade bloqueada. Pedido 11 sem pagamento e pedido 12 entregue bloqueados. Inexistente 404 confirmado.
- Após reinício: mesmo reembolso (ID, valor, data), pagamento original e status 6 confirmados por mensagem.
- Nenhuma execução foi feita pelo assistente; não há testes automatizados de HTTP/PostgreSQL ou reembolso confirmados.

## Verificações confirmadas

As execuções abaixo ocorreram no ambiente do usuário; não constituem execução pelo assistente.

| Grupo | Evidência |
| --- | --- |
| CRUD | Sucesso, entrada inválida, inexistência, exclusão e persistência após reinício, nas sessões anteriores |
| Pedidos | Entrega/retirada, dados históricos, totais, cliente/produto inválido e produto inativo |
| Exclusões | Vínculo retorna 409 e preserva registro; sem vínculo 204; repetição 404 |
| Status HTTP | Fluxos, conflitos 409, enum inválido 400 e pedido inexistente 404 |
| Paginação | Filtros combinados, padrões 1/20, limites, totais e página muito alta |
| Unidade | Base de 15 casos ampliada com cancelamento, aprovação confirmada nas etapas; 28 previstos, sem relatório final |
| Após refatoração | Compilação, testes existentes e criação com entrega/retirada confirmados |
| Datas | 11 cenários manuais propostos no .http, confirmados pelo usuário como concluídos |
| Pagamentos | Compilação; POST 201 e Location; GET 200; valor integral e status preservado; repetição 409; enum inválido/omitido 400 |
| Persistência do pagamento | Pedido inexistente 404 e mesmo pagamento consultado após reiniciar a API, confirmados pelo usuário |

Os cenários de datas abrangem consulta sem período, limites isolados, mesmo dia, vários dias,
combinação com cliente/status/paginação, período invertido, data impossível, data máxima e 04/11/2018.
A confirmação do conjunto veio por mensagem; não foram fornecidos corpos das respostas ou logs individuais.
Os testes unitários cobrem a regra de status; não são testes de integração HTTP/PostgreSQL.
Para pagamentos, foi enviada captura do 404 inicial; as demais verificações foram confirmadas por mensagem, sem respostas completas.
Nesta sessão, Gabriel confirmou testes aprovados durante a ampliação da regra de status. Após a implementação final, confirmou os comandos como feitos, sem enviar relatório; não há comprovação da contagem ou saída final.

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

- Definir consulta de reembolsos pendentes e seu contrato antes de implementar; ainda não iniciada.
- Implementar cardápio, autenticação/perfis, frontend React/TypeScript e relatórios.
- Ampliar automação para datas/integração quando pertinente; configurar CI e deploy.
- Testar pagamentos/reembolsos simultâneos e ampliar cobertura de integração desses fluxos.
- Tratar corrida entre registro de pagamento e cancelamento: a validação atual lê o status antes de salvar, sem bloqueio/coordenação transacional que garanta esse vínculo entre requisições concorrentes.
- Tratar concorrência do PATCH e das exclusões; contagem/página não usam snapshot transacional próprio.
- Avaliar pesquisa e filtros de produtos/clientes conforme a necessidade.
- Swagger UI não configurada; OpenAPI disponível em desenvolvimento.
- Confirmar alterações no Git local antes da próxima implementação; não há bloqueio técnico confirmado.
