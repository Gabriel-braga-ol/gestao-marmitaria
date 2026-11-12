using System.Net;
using GestaoMarmitaria.Api.Features.Pagamentos;
using GestaoMarmitaria.Api.Enums;

namespace GestaoMarmitaria.Tests.IntegrationTests;

public class PagamentoReembolsoIntegrationTests : IntegracaoTesteBase
{
    public PagamentoReembolsoIntegrationTests(ApiIntegrationFixture fixture) : base(fixture)
    {
    }

    [Fact]
    public async Task Pagamento_calcula_o_valor_no_backend_com_itens_e_taxa()
    {
        var marmita = await CriarProdutoAsync(preco: 20.00m);
        var bebida = await CriarProdutoAsync(TipoProduto.Bebida, 5.00m);
        await AdicionarAoCardapioAsync(marmita.Id);

        var cliente = await CriarClienteAsync();
        var pedido = await CriarPedidoAsync(
            cliente.Id,
            TipoEntrega.Entrega,
            "Rua do Pagamento, 10",
            (marmita.Id, 2),
            (bebida.Id, 1));

        var resposta = await RegistrarPagamentoAsync(pedido.Id);

        Assert.Equal(HttpStatusCode.Created, resposta.StatusCode);

        var pagamento = await LerAsync<PagamentoRespostaDto>(resposta);

        Assert.Equal(pedido.Id, pagamento.PedidoId);
        Assert.Equal(50.00m, pagamento.Valor);
        Assert.Equal(FormaPagamento.Pix, pagamento.FormaPagamento);
        Assert.Equal(DateTimeKind.Utc, pagamento.DataPagamento.Kind);
    }

    [Fact]
    public async Task Pagamento_duplicado_retorna_409()
    {
        var produto = await CriarProdutoAsync();
        await AdicionarAoCardapioAsync(produto.Id);
        var cliente = await CriarClienteAsync();
        var pedido = await CriarPedidoAsync(cliente.Id, TipoEntrega.Retirada, null, (produto.Id, 1));

        Assert.Equal(HttpStatusCode.Created, (await RegistrarPagamentoAsync(pedido.Id)).StatusCode);

        var duplicado = await RegistrarPagamentoAsync(pedido.Id);
        var mensagem = await AssertErroAsync(duplicado, HttpStatusCode.Conflict);

        Assert.Contains("já possui pagamento", mensagem);

        var pagamento = await ConsultarPagamentoAsync(pedido.Id);
        Assert.Equal(pedido.Total, pagamento.Valor);
    }

    [Fact]
    public async Task Pagamento_em_pedido_cancelado_retorna_409()
    {
        var produto = await CriarProdutoAsync();
        await AdicionarAoCardapioAsync(produto.Id);
        var cliente = await CriarClienteAsync();
        var pedido = await CriarPedidoAsync(cliente.Id, TipoEntrega.Retirada, null, (produto.Id, 1));

        Assert.Equal(HttpStatusCode.NoContent, (await AlterarStatusAsync(pedido.Id, StatusPedido.Cancelado)).StatusCode);

        var resposta = await RegistrarPagamentoAsync(pedido.Id);
        var mensagem = await AssertErroAsync(resposta, HttpStatusCode.Conflict);

        Assert.Contains("cancelado", mensagem);
    }

    [Fact]
    public async Task Pagamento_pode_ser_registrado_independente_do_status()
    {
        var produto = await CriarProdutoAsync();
        await AdicionarAoCardapioAsync(produto.Id);
        var cliente = await CriarClienteAsync();
        var pedido = await CriarPedidoAsync(cliente.Id, TipoEntrega.Retirada, null, (produto.Id, 1));

        Assert.Equal(HttpStatusCode.NoContent, (await AlterarStatusAsync(pedido.Id, StatusPedido.EmPreparo)).StatusCode);

        var resposta = await RegistrarPagamentoAsync(pedido.Id);

        Assert.Equal(HttpStatusCode.Created, resposta.StatusCode);
    }

    [Fact]
    public async Task Reembolso_exige_pedido_cancelado()
    {
        var produto = await CriarProdutoAsync();
        await AdicionarAoCardapioAsync(produto.Id);
        var cliente = await CriarClienteAsync();
        var pedido = await CriarPedidoAsync(cliente.Id, TipoEntrega.Retirada, null, (produto.Id, 1));

        Assert.Equal(HttpStatusCode.Created, (await RegistrarPagamentoAsync(pedido.Id)).StatusCode);

        var resposta = await RegistrarReembolsoAsync(pedido.Id);
        var mensagem = await AssertErroAsync(resposta, HttpStatusCode.Conflict);

        Assert.Contains("cancelado", mensagem);
    }

    [Fact]
    public async Task Reembolso_exige_pagamento_registrado()
    {
        var produto = await CriarProdutoAsync();
        await AdicionarAoCardapioAsync(produto.Id);
        var cliente = await CriarClienteAsync();
        var pedido = await CriarPedidoAsync(cliente.Id, TipoEntrega.Retirada, null, (produto.Id, 1));

        Assert.Equal(HttpStatusCode.NoContent, (await AlterarStatusAsync(pedido.Id, StatusPedido.Cancelado)).StatusCode);

        var resposta = await RegistrarReembolsoAsync(pedido.Id);
        var mensagem = await AssertErroAsync(resposta, HttpStatusCode.Conflict);

        Assert.Contains("não possui pagamento", mensagem);
    }

    [Fact]
    public async Task Reembolso_copia_o_valor_do_pagamento_e_e_unico()
    {
        var produto = await CriarProdutoAsync(preco: 30.00m);
        await AdicionarAoCardapioAsync(produto.Id);
        var cliente = await CriarClienteAsync();
        var pedido = await CriarPedidoAsync(cliente.Id, TipoEntrega.Retirada, null, (produto.Id, 2));

        var pagamento = await RegistrarPagamentoAsync(pedido.Id);
        Assert.Equal(HttpStatusCode.Created, pagamento.StatusCode);

        Assert.Equal(HttpStatusCode.NoContent, (await AlterarStatusAsync(pedido.Id, StatusPedido.Cancelado)).StatusCode);

        var resposta = await RegistrarReembolsoAsync(pedido.Id);

        Assert.Equal(HttpStatusCode.Created, resposta.StatusCode);

        var reembolso = await LerAsync<ReembolsoRespostaDto>(resposta);

        Assert.Equal(60.00m, reembolso.Valor);
        Assert.Equal(DateTimeKind.Utc, reembolso.DataReembolso.Kind);

        var duplicado = await RegistrarReembolsoAsync(pedido.Id);
        var mensagem = await AssertErroAsync(duplicado, HttpStatusCode.Conflict);

        Assert.Contains("já possui reembolso", mensagem);

        var consulta = await EnviarAsync(
            HttpMethod.Get,
            $"/api/pedidos/{pedido.Id}/reembolso",
            token: await TokenAdminAsync());

        Assert.Equal(HttpStatusCode.OK, consulta.StatusCode);

        var consultado = await LerAsync<ReembolsoRespostaDto>(consulta);
        Assert.Equal(reembolso.Id, consultado.Id);

        var pedidoAtualizado = await ConsultarPedidoAsync(pedido.Id);
        Assert.Equal(StatusPedido.Cancelado, pedidoAtualizado.Status);
    }

    [Fact]
    public async Task Funcionario_nao_pode_registrar_reembolso()
    {
        var produto = await CriarProdutoAsync();
        await AdicionarAoCardapioAsync(produto.Id);
        var cliente = await CriarClienteAsync();
        var pedido = await CriarPedidoAsync(cliente.Id, TipoEntrega.Retirada, null, (produto.Id, 1));

        Assert.Equal(HttpStatusCode.Created, (await RegistrarPagamentoAsync(pedido.Id)).StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, (await AlterarStatusAsync(pedido.Id, StatusPedido.Cancelado)).StatusCode);

        var resposta = await RegistrarReembolsoAsync(pedido.Id, await TokenFuncionarioAsync());

        Assert.Equal(HttpStatusCode.Forbidden, resposta.StatusCode);
    }
}
