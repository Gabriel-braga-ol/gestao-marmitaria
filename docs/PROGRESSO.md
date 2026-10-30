# Progresso — Gestão Marmitaria

Atualizado em 29/10/2026, horário de Brasília.
Base: documentos anteriores, código e capturas enviados e verificações confirmadas por Gabriel.
Esta revisão é documental; o assistente não executou a API, PostgreSQL ou testes nesta atualização.

## Ponto de retomada

Concluídos: testes unitários de status, filtros por data e refatoração de consulta/criação de pedidos.
Os commits de testes e filtros foram confirmados pelo usuário; hashes e push posterior não informados.
Esta revisão dos três documentos ainda precisa ser aplicada e commitada no repositório local.
Próximo marco sugerido: registro de pagamentos separado do andamento; modelagem ainda não iniciada.
Antes de implementar, definir contrato, vínculo com pedido e prevenção de pagamento duplicado.
Não retomar testes de status ou filtros por data como tarefas ainda não feitas.

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

Os cenários de datas abrangem consulta sem período, limites isolados, mesmo dia, vários dias,
combinação com cliente/status/paginação, período invertido, data impossível, data máxima e 04/11/2018.
A confirmação do conjunto veio por mensagem; não foram fornecidos corpos das respostas ou logs individuais.
Os 15 testes unitários cobrem a regra de status; não são testes de integração HTTP/PostgreSQL.

## Git e documentação

- Paginação: commit confirmado ao encerrar a sessão de 23/10.
- Extração da regra: comandos preparados; o status posterior não mostrava alterações nessa classe/controller.
- Testes: commit `test: cover order status transition rules` confirmado.
- Datas/refatoração: commit `feat: filter orders by date range and refactor order operations` confirmado.
- Na captura anterior ao último commit, main estava sincronizada com origin/main e havia três arquivos modificados.
- Não inferir push do último commit nem registrar hashes sem consulta ao Git.
- Aplicar README.md e AGENTS.md na raiz; PROGRESSO.md em docs/.

## Pendências

- Definir e implementar pagamentos; preservar a separação entre situação financeira e andamento.
- Consolidar regras de cancelamento/reembolso; não tratar decisões antigas divergentes como definitivas.
- Implementar cardápio, autenticação/perfis, frontend React/TypeScript e relatórios.
- Ampliar automação para datas/integração quando pertinente; configurar CI e deploy.
- Tratar concorrência do PATCH e das exclusões; contagem/página não usam snapshot transacional próprio.
- Avaliar pesquisa e filtros de produtos/clientes conforme a necessidade.
- Swagger UI não configurada; OpenAPI disponível em desenvolvimento.
- Confirmar alterações no Git local antes da próxima implementação; não há bloqueio técnico confirmado.
