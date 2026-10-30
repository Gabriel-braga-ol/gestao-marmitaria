# Progresso — Gestão Marmitaria

Atualizado em 30/10/2026, horário de Brasília, ao encerrar a sessão iniciada em 29/10.
Base: documentos anteriores, código e capturas enviados e verificações confirmadas por Gabriel.
Esta revisão é documental; o assistente não executou a API, PostgreSQL ou testes nesta atualização.

## Ponto de retomada

Último marco concluído: registro e consulta de pagamento integral, migration aplicada e verificações manuais confirmadas.
Commit de pagamentos confirmado pelo usuário em 30/10; hash e push não informados.
Esta revisão dos três documentos ainda precisa ser aplicada e commitada no repositório local.
Próximo passo sugerido: consultas de pedidos pagos/pendentes; definir o contrato antes de implementar.
Nenhuma nova tarefa foi iniciada após o commit de pagamentos. Concorrência simultânea permanece sem teste específico.
Não recriar pagamentos, testes de status ou filtros por data como se estivessem pendentes.

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
| Testes unitários | Projeto GestaoMarmitaria.Tests, xUnit e 15 casos aprovados |
| Período | Datas opcionais, validação e conversão de Brasília para UTC |
| Refatoração | Auxiliares de filtros, paginação, produtos, montagem e mapeamento |
| Pagamentos | Modelo, enum, DTOs, relacionamento, migration, GET/POST e bloqueio de duplicidade |

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
- Um pedido pode ter zero ou um pagamento; todo pagamento exige pedido existente. Registro manual, sem integração bancária.
- `AppDbContext.Pagamentos`: relação HasOne/WithOne, FK PedidoId, exclusão Restrict e índice único IX_Pagamentos_PedidoId.
- Migration `20261030022657_CriarTabelaPagamentos` revisada e aplicada; tabela confirmada no pgAdmin.
- `RegistrarPagamentoDto` recebe somente FormaPagamento; EnumDataType rejeita 0, 99 e omissão.
- `PagamentoRespostaDto`: Id, PedidoId, FormaPagamento, Valor e DataPagamento; sem navegação Pedido.
- `GET /api/pedidos/{id}/pagamento`: busca por PedidoId com AsNoTracking; 200 com DTO ou 404 sem registro.
- O GET não distingue pedido inexistente de pedido sem pagamento; ambos retornam 404.
- `POST /api/pedidos/{id}/pagamento`: busca pedido e itens; inexistente 404, pagamento existente 409, cadastro 201.
- O valor vem de Sum(Quantidade * PrecoUnitario) + TaxaEntrega; não recebe valor/data do consumidor nem altera o status.
- Auxiliares extraídos: `CalcularTotalPedido`, `MontarPagamento` e `MapearPagamentoParaResposta`.
- `CreatedAtAction` aponta para GetPagamentoPorPedidoAsync usando PedidoId; o GET tem ActionName explícito para preservar o sufixo Async.
- AnyAsync verifica duplicidade; catch filtrado de DbUpdateException trata UniqueViolation apenas de IX_Pagamentos_PedidoId como 409.
- A proteção simultânea existe no banco/código, mas ainda não foi exercitada com requisições concorrentes.

## Verificações confirmadas

As execuções abaixo ocorreram no ambiente do usuário; não constituem execução pelo assistente.

| Grupo | Evidência |
| --- | --- |
| CRUD | Sucesso, entrada inválida, inexistência, exclusão e persistência após reinício, nas sessões anteriores |
| Pedidos | Entrega/retirada, dados históricos, totais, cliente/produto inválido e produto inativo |
| Exclusões | Vínculo retorna 409 e preserva registro; sem vínculo 204; repetição 404 |
| Status HTTP | Fluxos, conflitos 409, enum inválido 400 e pedido inexistente 404 |
| Paginação | Filtros combinados, padrões 1/20, limites, totais e página muito alta |
| Unidade | 15 casos xUnit aprovados: transições permitidas e principais bloqueios |
| Após refatoração | Compilação, testes existentes e criação com entrega/retirada confirmados |
| Datas | 11 cenários manuais propostos no .http, confirmados pelo usuário como concluídos |
| Pagamentos | Compilação; POST 201 e Location; GET 200; valor integral e status preservado; repetição 409; enum inválido/omitido 400 |
| Persistência do pagamento | Pedido inexistente 404 e mesmo pagamento consultado após reiniciar a API, confirmados pelo usuário |

Os cenários de datas abrangem consulta sem período, limites isolados, mesmo dia, vários dias,
combinação com cliente/status/paginação, período invertido, data impossível, data máxima e 04/11/2018.
A confirmação do conjunto veio por mensagem; não foram fornecidos corpos das respostas ou logs individuais.
Os 15 testes unitários cobrem a regra de status; não são testes de integração HTTP/PostgreSQL.
Para pagamentos, foi enviada captura do 404 inicial; as demais verificações foram confirmadas por mensagem, sem respostas completas.
Não houve nova execução comprovada dos 15 testes após a inclusão de pagamentos; não registrar regressão automatizada como executada.

## Git e documentação

- Paginação: commit confirmado ao encerrar a sessão de 23/10.
- Extração da regra: comandos preparados; o status posterior não mostrava alterações nessa classe/controller.
- Testes: commit `test: cover order status transition rules` confirmado.
- Datas/refatoração: commit `feat: filter orders by date range and refactor order operations` confirmado.
- Pagamentos: commit `feat: register and retrieve full order payments` confirmado por Gabriel.
- A captura pré-commit mostrou controller, contexto, .http e snapshot modificados; modelo, enum, dois DTOs e migration/designer novos.
- Hashes não informados; o assistente não inspecionou o repositório nem executou os commits.
- Não inferir push do último commit nem registrar hashes sem consulta ao Git.
- Aplicar README.md e AGENTS.md na raiz; PROGRESSO.md em docs/.

## Pendências

- Avaliar consulta de pedidos pagos/pendentes; o registro e a consulta individual de pagamento estão concluídos.
- Consolidar regras de cancelamento/reembolso; não tratar decisões antigas divergentes como definitivas.
- Implementar cardápio, autenticação/perfis, frontend React/TypeScript e relatórios.
- Ampliar automação para datas/integração quando pertinente; configurar CI e deploy.
- Testar pagamentos simultâneos e ampliar cobertura de pagamentos; ainda não há testes automatizados desse fluxo.
- Tratar concorrência do PATCH e das exclusões; contagem/página não usam snapshot transacional próprio.
- Avaliar pesquisa e filtros de produtos/clientes conforme a necessidade.
- Swagger UI não configurada; OpenAPI disponível em desenvolvimento.
- Confirmar alterações no Git local antes da próxima implementação; não há bloqueio técnico confirmado.
