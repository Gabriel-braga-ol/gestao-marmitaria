# AGENTS.md

## Visão geral

Conduza este projeto de portfólio como mentoria prática até o deploy.
Explique uma tarefa por vez; deixe Gabriel implementar, salvo pedido explícito de edição direta.

## Comandos

Execute na raiz; configure conexão e ambiente Development conforme o README antes das migrations.

```bash
dotnet build GestaoMarmitaria.slnx
dotnet run --project GestaoMarmitaria.Api --launch-profile http
dotnet test GestaoMarmitaria.slnx
dotnet ef migrations add [NomeDaMigration] --project GestaoMarmitaria.Api
dotnet ef database update --project GestaoMarmitaria.Api
```

## Arquitetura

- Preserve a organização atual; justifique novas camadas por uma necessidade concreta.
- Extraia decisões independentes de HTTP/banco para componentes testáveis; mantenha auxiliares locais quando suficientes.
- Divida métodos que acumulem validação, consulta, montagem e persistência; use nomes que revelem o objetivo.

## Convenções

- Use nomes de domínio em português.
- Termine métodos assíncronos novos ou alterados com `Async`; ao renomear ações, confira rotas e `CreatedAtAction`.
- Use `AsNoTracking()` em consultas só de leitura; preserve rastreamento nas alterações.
- Retorne erros manuais de validação com `BadRequest(new { mensagem = "..." })`.
- Preserve o contrato automático `400` com `errors` das Data Annotations; não altere esse formato fora do escopo.
- Use `decimal` para dinheiro e mantenha validações dos DTOs coerentes com o mapeamento.
- Escreva commits em inglês e limite cada alteração à tarefa acordada.

## Regras de negócio que não podem quebrar

- Preserve nome/preço históricos, endereço e taxa; não recalcule pedidos antigos pelo catálogo atual.
- Exija endereço na entrega; mantenha endereço nulo e taxa zero na retirada.
- Preserve o fluxo distinto de entrega/retirada e os bloqueios de saltos, retornos e repetição.
- Bloqueie exclusão de cliente/produto vinculado com `409`; nunca apague o histórico por cascata.
- Rejeite produtos repetidos antes de comparar as quantidades de IDs solicitados e produtos encontrados.
- Interprete datas de consulta em Brasília, converta limites para UTC e filtre antes da contagem/paginação.
- Mantenha pagamento separado do andamento; consolide cancelamento/reembolso antes de implementar.

## Não faça

- Não entregue módulos completos quando a tarefa for um exercício orientado.
- Não repita tarefas ou testes já aprovados sem alteração, falha ou dúvida concreta.
- Não mude versões, contratos ou regras de negócio por conveniência da refatoração.
- Não versione credenciais, `bin/`, `obj/` ou dados reais.
- Não apague bancos nem reescreva migrations aplicadas como solução padrão.
- Não declare build, teste, commit, push ou deploy sem execução ou confirmação.
- Não faça push nem altere histórico sem autorização; um pedido de commit não autoriza push.

## Checklist antes de finalizar

- [ ] Consulte `docs/PROGRESSO.md`, o código relacionado e o Git disponível antes de propor a próxima tarefa.
- [ ] Confira o diff e preserve mudanças alheias ao escopo.
- [ ] Execute build/testes pertinentes ao código; em documentação, valide comandos, caminhos e consistência.
- [ ] Revise `Up()` e `Down()` antes de aplicar migrations; inclua designer e snapshot.
- [ ] Registre separadamente execução própria, confirmação do usuário e itens não verificados.
- [ ] Atualize README se execução/contrato mudar; registre conclusão e próxima tarefa no PROGRESSO.
- [ ] Informe o resultado e eventuais pendências; não avance presumindo testes aprovados.
