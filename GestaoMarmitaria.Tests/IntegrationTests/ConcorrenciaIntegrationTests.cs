using System.Net;
using GestaoMarmitaria.Api.Features.Pagamentos;
using GestaoMarmitaria.Api.Features.Pedidos;
using GestaoMarmitaria.Api.Enums;

namespace GestaoMarmitaria.Tests.IntegrationTests;

public class ConcorrenciaIntegrationTests : IntegracaoTesteBase
{
    public ConcorrenciaIntegrationTests(ApiIntegrationFixture fixture) : base(fixture)
    {
    }

    [Fact]
    public async Task Dois_pagamentos_simultaneos_gravam_apenas_um()
    {
        var produto = await CriarProdutoAsync();
        await AdicionarAoCardapioAsync(produto.Id);
        var cliente = await CriarClienteAsync();
        var pedido = await CriarPedidoAsync(cliente.Id, TipoEntrega.Retirada, null, (produto.Id, 1));

        var respostas = await ExecutarSimultaneamenteAsync(
            () => RegistrarPagamentoAsync(pedido.Id),
            () => RegistrarPagamentoAsync(pedido.Id));

        var status = respostas.Select(r => r.StatusCode).OrderBy(s => (int)s).ToArray();

        Assert.Equal(new[] { HttpStatusCode.Created, HttpStatusCode.Conflict }, status);

        var pagamento = await ConsultarPagamentoAsync(pedido.Id);
        Assert.Equal(pedido.Total, pagamento.Valor);
    }

    [Fact]
    public async Task Duas_transicoes_simultaneas_aplicam_apenas_uma()
    {
        var produto = await CriarProdutoAsync();
        await AdicionarAoCardapioAsync(produto.Id);
        var cliente = await CriarClienteAsync();
        var pedido = await CriarPedidoAsync(cliente.Id, TipoEntrega.Retirada, null, (produto.Id, 1));

        var respostas = await ExecutarSimultaneamenteAsync(
            () => AlterarStatusAsync(pedido.Id, StatusPedido.EmPreparo),
            () => AlterarStatusAsync(pedido.Id, StatusPedido.EmPreparo));

        var status = respostas.Select(r => r.StatusCode).OrderBy(s => (int)s).ToArray();

        Assert.Equal(new[] { HttpStatusCode.NoContent, HttpStatusCode.Conflict }, status);

        var pedidoFinal = await ConsultarPedidoAsync(pedido.Id);
        Assert.Equal(StatusPedido.EmPreparo, pedidoFinal.Status);
    }

    [Fact]
    public async Task Pagamento_e_cancelamento_simultaneos_terminam_em_estado_consistente()
    {
        var produto = await CriarProdutoAsync();
        await AdicionarAoCardapioAsync(produto.Id);
        var cliente = await CriarClienteAsync();
        var pedido = await CriarPedidoAsync(cliente.Id, TipoEntrega.Retirada, null, (produto.Id, 1));

        var tokenFuncionario = await TokenFuncionarioAsync();

        var respostas = await ExecutarSimultaneamenteAsync(
            () => RegistrarPagamentoAsync(pedido.Id),
            () => AlterarStatusAsync(pedido.Id, StatusPedido.Cancelado, tokenFuncionario));

        var pagamento = respostas[0];
        var cancelamento = respostas[1];

        Assert.Equal(HttpStatusCode.NoContent, cancelamento.StatusCode);
        Assert.True(
            pagamento.StatusCode == HttpStatusCode.Created ||
            pagamento.StatusCode == HttpStatusCode.Conflict,
            $"Status inesperado do pagamento: {(int)pagamento.StatusCode}");

        var pedidoFinal = await ConsultarPedidoAsync(pedido.Id);
        Assert.Equal(StatusPedido.Cancelado, pedidoFinal.Status);

        var consultaPagamento = await EnviarAsync(
            HttpMethod.Get,
            $"/api/pedidos/{pedido.Id}/pagamento",
            token: await TokenAdminAsync());

        if (pagamento.StatusCode == HttpStatusCode.Created)
        {
            Assert.Equal(HttpStatusCode.OK, consultaPagamento.StatusCode);
        }
        else
        {
            Assert.Equal(HttpStatusCode.NotFound, consultaPagamento.StatusCode);
        }
    }

    [Fact]
    public async Task Dois_reembolsos_simultaneos_gravam_apenas_um()
    {
        var produto = await CriarProdutoAsync();
        await AdicionarAoCardapioAsync(produto.Id);
        var cliente = await CriarClienteAsync();
        var pedido = await CriarPedidoAsync(cliente.Id, TipoEntrega.Retirada, null, (produto.Id, 1));

        Assert.Equal(HttpStatusCode.Created, (await RegistrarPagamentoAsync(pedido.Id)).StatusCode);
        Assert.Equal(
            HttpStatusCode.NoContent,
            (await AlterarStatusAsync(pedido.Id, StatusPedido.Cancelado)).StatusCode);

        var respostas = await ExecutarSimultaneamenteAsync(
            () => RegistrarReembolsoAsync(pedido.Id),
            () => RegistrarReembolsoAsync(pedido.Id));

        var status = respostas.Select(r => r.StatusCode).OrderBy(s => (int)s).ToArray();

        Assert.Equal(new[] { HttpStatusCode.Created, HttpStatusCode.Conflict }, status);

        var consulta = await EnviarAsync(
            HttpMethod.Get,
            $"/api/pedidos/{pedido.Id}/reembolso",
            token: await TokenAdminAsync());

        Assert.Equal(HttpStatusCode.OK, consulta.StatusCode);

        var reembolso = await LerAsync<ReembolsoRespostaDto>(consulta);
        Assert.Equal(pedido.Total, reembolso.Valor);
    }

    [Fact]
    public async Task Cinco_pedidos_simultaneos_sao_criados()
    {
        var produto = await CriarProdutoAsync();
        await AdicionarAoCardapioAsync(produto.Id);
        var cliente = await CriarClienteAsync();
        var token = await TokenAdminAsync();
        var corpo = MontarPedido(cliente.Id, TipoEntrega.Retirada, null, (produto.Id, 1));

        var acoes = Enumerable.Range(0, 5)
            .Select(_ => (Func<Task<HttpResponseMessage>>)(() =>
                EnviarAsync(HttpMethod.Post, "/api/pedidos", corpo, token)))
            .ToArray();

        var respostas = await ExecutarSimultaneamenteAsync(acoes);

        Assert.All(respostas, resposta =>
            Assert.Equal(HttpStatusCode.Created, resposta.StatusCode));

        var ids = new HashSet<int>();

        foreach (var resposta in respostas)
        {
            var pedido = await LerAsync<PedidoRespostaDto>(resposta);
            ids.Add(pedido.Id);
        }

        Assert.Equal(5, ids.Count);
    }

    private static async Task<HttpResponseMessage[]> ExecutarSimultaneamenteAsync(
        params Func<Task<HttpResponseMessage>>[] acoes)
    {
        var inicio = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);

        var tarefas = acoes
            .Select(async acao =>
            {
                await inicio.Task;
                return await acao();
            })
            .ToArray();

        inicio.SetResult(true);

        return await Task.WhenAll(tarefas);
    }
}
