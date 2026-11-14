using System.Net;
using System.Text.Json;
using GestaoMarmitaria.Api.Enums;
using GestaoMarmitaria.Api.Features.Relatorios;
using Microsoft.EntityFrameworkCore;

namespace GestaoMarmitaria.Tests.IntegrationTests;

public class RelatoriosIntegrationTests : IntegracaoTesteBase
{
    private static readonly DateTime DataFixaUtc =
        new(2000, 1, 15, 15, 0, 0, DateTimeKind.Utc);

    private const string PeriodoFixo =
        "dataInicial=2000-01-01&dataFinal=2000-01-31";

    public RelatoriosIntegrationTests(ApiIntegrationFixture fixture) : base(fixture)
    {
    }

    [Fact]
    public async Task Relatorio_soma_pagamentos_e_reembolsos_do_periodo()
    {
        var produtoVinte = await CriarProdutoAsync(preco: 20.00m);
        var produtoQuarenta = await CriarProdutoAsync(preco: 40.00m);
        await AdicionarAoCardapioAsync(produtoVinte.Id);
        await AdicionarAoCardapioAsync(produtoQuarenta.Id);

        var cliente = await CriarClienteAsync();

        var pedidoReembolsado = await CriarPedidoAsync(
            cliente.Id,
            TipoEntrega.Retirada,
            null,
            (produtoVinte.Id, 1));
        var pedidoPix = await CriarPedidoAsync(
            cliente.Id,
            TipoEntrega.Retirada,
            null,
            (produtoVinte.Id, 1));
        var pedidoDinheiro = await CriarPedidoAsync(
            cliente.Id,
            TipoEntrega.Retirada,
            null,
            (produtoQuarenta.Id, 1));
        var pedidoPendente = await CriarPedidoAsync(
            cliente.Id,
            TipoEntrega.Retirada,
            null,
            (produtoVinte.Id, 1));

        Assert.Equal(HttpStatusCode.Created, (await RegistrarPagamentoComFormaAsync(
            pedidoReembolsado.Id,
            FormaPagamento.Pix)).StatusCode);
        Assert.Equal(HttpStatusCode.Created, (await RegistrarPagamentoComFormaAsync(
            pedidoPix.Id,
            FormaPagamento.Pix)).StatusCode);
        Assert.Equal(HttpStatusCode.Created, (await RegistrarPagamentoComFormaAsync(
            pedidoDinheiro.Id,
            FormaPagamento.Dinheiro)).StatusCode);
        Assert.Equal(HttpStatusCode.Created, (await RegistrarPagamentoComFormaAsync(
            pedidoPendente.Id,
            FormaPagamento.CartaoCredito)).StatusCode);

        Assert.Equal(HttpStatusCode.NoContent, (await AlterarStatusAsync(
            pedidoReembolsado.Id,
            StatusPedido.Cancelado)).StatusCode);
        Assert.Equal(HttpStatusCode.Created, (await RegistrarReembolsoAsync(
            pedidoReembolsado.Id)).StatusCode);

        Assert.Equal(HttpStatusCode.NoContent, (await AlterarStatusAsync(
            pedidoPendente.Id,
            StatusPedido.Cancelado)).StatusCode);

        await MoverParaDataFixaAsync(
            pedidoReembolsado.Id,
            pedidoPix.Id,
            pedidoDinheiro.Id,
            pedidoPendente.Id);

        var resposta = await EnviarAsync(
            HttpMethod.Get,
            $"/api/relatorios/financeiro?{PeriodoFixo}",
            token: await TokenAdminAsync());

        Assert.Equal(HttpStatusCode.OK, resposta.StatusCode);

        var relatorio = await LerAsync<RelatorioFinanceiroRespostaDto>(resposta);

        Assert.Equal(new DateOnly(2000, 1, 1), relatorio.DataInicial);
        Assert.Equal(new DateOnly(2000, 1, 31), relatorio.DataFinal);
        Assert.Equal(4, relatorio.TotalPedidos);
        Assert.Equal(4, relatorio.PedidosPagos);
        Assert.Equal(1, relatorio.PedidosReembolsados);
        Assert.Equal(100.00m, relatorio.TotalPago);
        Assert.Equal(20.00m, relatorio.TotalReembolsado);
        Assert.Equal(20.00m, relatorio.TotalReembolsoPendente);
        Assert.Equal(80.00m, relatorio.TotalLiquido);

        var pix = Assert.Single(
            relatorio.PorFormaPagamento,
            resumo => resumo.FormaPagamento == FormaPagamento.Pix);
        Assert.Equal(2, pix.Quantidade);
        Assert.Equal(40.00m, pix.Total);

        var dinheiro = Assert.Single(
            relatorio.PorFormaPagamento,
            resumo => resumo.FormaPagamento == FormaPagamento.Dinheiro);
        Assert.Equal(1, dinheiro.Quantidade);
        Assert.Equal(40.00m, dinheiro.Total);

        var cartao = Assert.Single(
            relatorio.PorFormaPagamento,
            resumo => resumo.FormaPagamento == FormaPagamento.CartaoCredito);
        Assert.Equal(1, cartao.Quantidade);
        Assert.Equal(20.00m, cartao.Total);
    }

    [Fact]
    public async Task Relatorio_de_periodo_sem_pedidos_retorna_zeros()
    {
        var resposta = await EnviarAsync(
            HttpMethod.Get,
            "/api/relatorios/financeiro?dataInicial=1990-01-01&dataFinal=1990-01-31",
            token: await TokenAdminAsync());

        Assert.Equal(HttpStatusCode.OK, resposta.StatusCode);

        var relatorio = await LerAsync<RelatorioFinanceiroRespostaDto>(resposta);

        Assert.Equal(0, relatorio.TotalPedidos);
        Assert.Equal(0, relatorio.PedidosPagos);
        Assert.Equal(0, relatorio.PedidosReembolsados);
        Assert.Equal(0.00m, relatorio.TotalPago);
        Assert.Equal(0.00m, relatorio.TotalReembolsado);
        Assert.Equal(0.00m, relatorio.TotalReembolsoPendente);
        Assert.Equal(0.00m, relatorio.TotalLiquido);
        Assert.Empty(relatorio.PorFormaPagamento);
    }

    [Fact]
    public async Task Funcionario_nao_pode_consultar_o_relatorio()
    {
        var resposta = await EnviarAsync(
            HttpMethod.Get,
            $"/api/relatorios/financeiro?{PeriodoFixo}",
            token: await TokenFuncionarioAsync());

        Assert.Equal(HttpStatusCode.Forbidden, resposta.StatusCode);
    }

    [Fact]
    public async Task Relatorio_exige_autenticacao()
    {
        var resposta = await EnviarAsync(
            HttpMethod.Get,
            $"/api/relatorios/financeiro?{PeriodoFixo}");

        Assert.Equal(HttpStatusCode.Unauthorized, resposta.StatusCode);
    }

    [Fact]
    public async Task Periodo_invertido_retorna_400_com_errors()
    {
        var resposta = await EnviarAsync(
            HttpMethod.Get,
            "/api/relatorios/financeiro?dataInicial=2000-02-01&dataFinal=2000-01-31",
            token: await TokenAdminAsync());

        Assert.Equal(HttpStatusCode.BadRequest, resposta.StatusCode);

        using var json = JsonDocument.Parse(await resposta.Content.ReadAsStringAsync());

        Assert.True(json.RootElement.TryGetProperty("errors", out var errors));
        Assert.True(errors.TryGetProperty("DataInicial", out _));
        Assert.True(errors.TryGetProperty("DataFinal", out _));
    }

    [Fact]
    public async Task Datas_obrigatorias_retornam_400_com_errors()
    {
        var resposta = await EnviarAsync(
            HttpMethod.Get,
            "/api/relatorios/financeiro",
            token: await TokenAdminAsync());

        Assert.Equal(HttpStatusCode.BadRequest, resposta.StatusCode);

        using var json = JsonDocument.Parse(await resposta.Content.ReadAsStringAsync());

        Assert.True(json.RootElement.TryGetProperty("errors", out var errors));
        Assert.True(errors.TryGetProperty("DataInicial", out _));
        Assert.True(errors.TryGetProperty("DataFinal", out _));
    }

    [Fact]
    public async Task Data_final_maxima_retorna_400_com_mensagem()
    {
        var resposta = await EnviarAsync(
            HttpMethod.Get,
            "/api/relatorios/financeiro?dataInicial=2000-01-01&dataFinal=9999-12-31",
            token: await TokenAdminAsync());

        var mensagem = await AssertErroAsync(resposta, HttpStatusCode.BadRequest);

        Assert.Contains("31/12/9999", mensagem);
    }

    private async Task<HttpResponseMessage> RegistrarPagamentoComFormaAsync(
        int pedidoId,
        FormaPagamento formaPagamento)
    {
        return await EnviarAsync(
            HttpMethod.Post,
            $"/api/pedidos/{pedidoId}/pagamento",
            new { formaPagamento = (int)formaPagamento },
            await TokenAdminAsync());
    }

    private async Task MoverParaDataFixaAsync(params int[] pedidoIds)
    {
        await using var contexto = Fixture.CriarDbContext();

        await contexto.Pedidos
            .Where(pedido => pedidoIds.Contains(pedido.Id))
            .ExecuteUpdateAsync(atualizacao => atualizacao
                .SetProperty(pedido => pedido.DataCriacao, DataFixaUtc));
    }
}
