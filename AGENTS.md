# AGENTS.md

## Visão geral

Conduza este projeto de portfólio como mentoria prática até o deploy.
Explique uma tarefa por vez; deixe Gabriel implementar, salvo pedido explícito de edição direta.

## Comandos

Execute na raiz; configure conexão, `Jwt:ChaveSecreta` e ambiente Development conforme o README antes das migrations.

```bash
dotnet user-secrets set "Jwt:ChaveSecreta" "[CHAVE_LONGA_E_ALEATORIA]" --project GestaoMarmitaria.Api
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
- Confira o nome MVC de ações com `Async`; preserve o `ActionName` explícito dos GETs de pagamento, reembolso e item do cardápio ao gerar seu `Location`.
- Use `AsNoTracking()` em consultas só de leitura; preserve rastreamento nas alterações.
- Retorne erros manuais de validação com `BadRequest(new { mensagem = "..." })`.
- Preserve o contrato automático `400` com `errors` das Data Annotations; não altere esse formato fora do escopo.
- Use `decimal` para dinheiro e mantenha validações dos DTOs coerentes com o mapeamento.
- Escreva descrições de commits em pt-BR, mantendo prefixos como feat:, fix: e docs:.
- Limite cada alteração à tarefa acordada.

## Regras de negócio que não podem quebrar

- Preserve nome/preço históricos, endereço e taxa; não recalcule pedidos antigos pelo catálogo atual.
- Exija endereço na entrega; mantenha endereço nulo e taxa zero na retirada.
- Preserve o fluxo distinto de entrega/retirada e os bloqueios de saltos, retornos e repetição.
- Bloqueie exclusão de cliente/produto vinculado com `409`; nunca apague o histórico por cascata.
- Rejeite produtos repetidos antes de comparar as quantidades de IDs solicitados e produtos encontrados.
- Interprete datas de consulta em Brasília, converta limites para UTC e filtre antes da contagem/paginação.
- Mantenha pagamento separado do andamento; preserve o pagamento original após cancelamento/reembolso.
- Permita cancelar somente em Novo ou EmPreparo; Cancelado=6 é terminal. Não renumere os status existentes.
- Bloqueie novo pagamento para pedido cancelado com 409.
- Não permita alterar o `Tipo` de produto vinculado ao cardápio: o PUT que tente mudá-lo retorna 409; reenviar o mesmo tipo é permitido.
- Confirme reembolso manual apenas em pedido cancelado e com pagamento; copie integralmente Pagamento.Valor e use data UTC.
- Preserve a relação de no máximo um reembolso por pagamento, índice único IX_Reembolsos_PagamentoId e exclusão Restrict.
- O POST de reembolso não recebe valor/data e não realiza transferência bancária; não apague o pagamento nem altere o status Cancelado.
- Pago é bool? e filtra existência de pagamento antes da contagem/paginação; cancelado ou reembolsado com pagamento continua em pago=true.
- ReembolsoPendente é bool?: true exige Cancelado, pagamento existente e reembolso ausente; false é o complemento; omitido não filtra.
- Aplique ReembolsoPendente antes da contagem/paginação e combine com os demais filtros; true com Pago=false resulta em lista vazia, não erro.
- Preserve a navegação opcional Pagamento.Reembolso associada ao WithOne do relacionamento existente; o filtro não exige Include.
- Registre um pagamento integral por pedido; calcule o valor no backend pelos itens históricos mais a taxa, com data UTC.
- Preserve índice único em `Pagamento.PedidoId` e exclusão restrita; retorne `409` para duplicidade, inclusive na gravação.
- Trate somente a violação do índice esperado como duplicidade; não converta qualquer falha do banco em `409`.

## Cardápio diário

- TipoProduto: Marmita=1, Bebida=2. Validar Tipo em ambos os DTOs e salvá-lo no POST/PUT de produtos.
- Marmitas exigem Produto.Ativo e ItemCardapio disponível na data atual de Brasília; bebidas exigem apenas Produto.Ativo.
- Disponibilidade é um indicador, sem estoque/quantidade. Não adicionar controle quantitativo fora de tarefa acordada.
- ItemCardapio usa DateOnly, ProdutoId e Disponivel. Preservar FK Restrict e índice único IX_ItensCardapio_Data_ProdutoId.
- Validar duplicidade antecipadamente e tratar somente a violação desse índice como 409 no cadastro.
- POST do cardápio aceita apenas marmitas ativas. Data é obrigatória; bool? com Required no PATCH distingue omissão de false.
- GET por data exige data explícita, retorna lista inclusive de itens indisponíveis, ordena por nome/Id e usa [] quando vazia. Não confundir essa listagem administrativa com uma lista de produtos liberados para venda.
- Nome/preço do cardápio vêm do catálogo atual; nome/preço históricos pertencem aos itens do pedido.
- PATCH de disponibilidade afeta somente um item/data, sem alterar Produto.Ativo ou pedidos existentes.
- Validar disponibilidade em CarregarProdutosDoPedidoAsync, antes de montar/salvar o pedido; usar uma consulta para todas as marmitas e rejeitar o pedido inteiro se alguma estiver indisponível.
- Converter UTC para America/Sao_Paulo para determinar o dia. Não usar DateTime.Today do servidor nem uma data fixa.
- Bloquear DELETE de produto com vínculo em ItensPedido ou ItensCardapio com 409; o cenário isolado do vínculo somente com cardápio está no `.http`, mas sua execução não foi confirmada.
- Não reescrever as migrations aplicadas AdicionarTipoProduto e CriarTabelaItensCardapio; incluir seus designers e snapshot no Git.
- Há referência explícita orientada e informada como adicionada de EFCore.Relational 10.0.13 à API para alinhar dependências. Não declarar eliminação dos avisos sem confirmação.
- `CriarPedidoAsync` roda em transação `IsolationLevel.Serializable` e converte `DbUpdateException` em 409. Qualquer ajuste deve manter essa transação e confirmá-la com `CommitAsync`; a proteção contra a corrida entre PATCH de disponibilidade e criação de pedido ainda não foi testada nem avaliada quanto ao alcance, em tarefa própria.

## Autenticação e autorização

- `Usuario` (Id, Nome, Email único, SenhaHash, Perfil) e `PerfilUsuario` (Admin=1, Funcionario=2). Não renumere o enum nem reescreva a migration `CriarTabelaUsuarios`; inclua designer e snapshot no Git.
- Gere e verifique senhas apenas com `PasswordHasher<Usuario>`; nunca grave, registre em log ou devolva senha em texto.
- A chave `Jwt:ChaveSecreta` vem de User Secrets ou variável de ambiente; o `appsettings.json` mantém só o placeholder. Nunca versione a chave real.
- O token carrega as claims `NameIdentifier`, `Email` e `Role`. Mantenha emissor, audiência e expiração em `JwtSettings`; não hardcode esses valores.
- Controllers operacionais levam `[Authorize]` na classe, inclusive os novos. Somente `AutenticacaoController` (`registrar` e `login`) é público; ao proteger por perfil, use `[Authorize(Roles = "...")]` com os nomes do enum.
- Login com credenciais incorretas retorna 400 com mensagem única, sem distinguir e-mail de senha. Preserve esse comportamento.
- `UseHttpsRedirection` está comentado em `Program.cs` porque o redirecionamento 307 em `http://` descartava o cabeçalho `Authorization`. Reative-o apenas com HTTPS configurado.
- Alertas conhecidos, a tratar em tarefas próprias: `/registrar` público aceita `perfil` e permite criar Admin; `ShowPII = true` e `Debug.WriteLine` no `Program.cs` devem ficar restritos ao desenvolvimento; `TokenService` usa `Encoding.ASCII` enquanto o `Program.cs` usa UTF8 para a mesma chave; `WeatherForecastController` do template continua público; as mensagens da autenticação usam português de Portugal ("registado", "utilizador"), diferente do restante do projeto.

## Não faça

- Não entregue módulos completos quando a tarefa for um exercício orientado.
- Não repita tarefas ou testes já aprovados sem alteração, falha ou dúvida concreta.
- Não mude versões, contratos ou regras de negócio por conveniência da refatoração.
- Não versione credenciais, a chave JWT, tokens, `bin/`, `obj/` ou dados reais.
- Não deixe endpoint novo sem `[Authorize]` sem decisão explícita, nem exponha `SenhaHash` em respostas.
- Não apague bancos nem reescreva migrations aplicadas como solução padrão.
- Não declare build, teste, commit, push ou deploy sem execução ou confirmação.
- Não faça push nem altere histórico sem autorização; um pedido de commit não autoriza push.

## Checklist antes de finalizar

- [ ] Consulte `docs/PROGRESSO.md`, o código relacionado e o Git disponível antes de propor a próxima tarefa.
- [ ] Confira o diff e preserve mudanças alheias ao escopo.
- [ ] Execute build/testes pertinentes ao código; em documentação, valide comandos, caminhos e consistência.
- [ ] Revise `Up()` e `Down()` antes de aplicar migrations; inclua designer e snapshot.
- [ ] Registre separadamente execução própria, confirmação do usuário e itens não verificados.
- [ ] Ao criar ou alterar endpoints, confira `[Authorize]`/acesso público e os códigos 401/403 esperados; atualize o `.http` com o cabeçalho `Authorization`.
- [ ] Atualize README se execução/contrato mudar; registre conclusão e próxima tarefa no PROGRESSO.
- [ ] Informe o resultado e eventuais pendências; não avance presumindo testes aprovados.
