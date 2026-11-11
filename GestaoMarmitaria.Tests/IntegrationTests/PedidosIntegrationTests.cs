using System.Net;
using System.Text.Json;
using GestaoMarmitaria.Api.Enums;

namespace GestaoMarmitaria.Tests.IntegrationTests;

public class PedidosIntegrationTests : IntegracaoTesteBase
{
    public PedidosIntegrationTests(ApiIntegrationFixture fixture) : base(fixture)
    {
    }

    [Fact]
    public async Task Pedido_de_retirada_nao_cobra_taxa_nem_exige_endereco()
    {
        var produto = await CriarProdutoAsync(preco: 20.00m);
        var cliente = await CriarClienteAsync();
        await AdicionarAoCardapioAsync(produto.Id);

        var pedido = await CriarPedidoAsync(
            cliente.Id,
            TipoEntrega.Retirada,
            null,
            (produto.Id, 2));

        Assert.Equal(TipoEntrega.Retirada, pedido.TipoEntrega);
        Assert.Null(pedido.EnderecoEntrega);
        Assert.Equal(0.00m, pedido.TaxaEntrega);
        Assert.Equal(40.00m, pedido.Total);
    }

    [Fact]
    public async Task Pedido_de_entrega_cobra_a_taxa_configurada()
    {
        var produto = await CriarProdutoAsync(preco: 20.00m);
        var cliente = await CriarClienteAsync();
        await AdicionarAoCardapioAsync(produto.Id);

        var pedido = await CriarPedidoAsync(
            cliente.Id,
            TipoEntrega.Entrega,
            "Rua das Entregas, 50",
            (produto.Id, 2));

        Assert.Equal(TipoEntrega.Entrega, pedido.TipoEntrega);
        Assert.Equal("Rua das Entregas, 50", pedido.EnderecoEntrega);
        Assert.Equal(ApiIntegrationFixture.TaxaEntrega, pedido.TaxaEntrega);
        Assert.Equal(45.00m, pedido.Total);
    }

    [Fact]
    public async Task Pedido_de_entrega_sem_endereco_retorna_400_com_errors()
    {
        var produto = await CriarProdutoAsync();
        var cliente = await CriarClienteAsync();
        await AdicionarAoCardapioAsync(produto.Id);

        var resposta = await EnviarAsync(
            HttpMethod.Post,
            "/api/pedidos",
            MontarPedido(cliente.Id, TipoEntrega.Entrega, null, (produto.Id, 1)),
            await TokenAdminAsync());

        Assert.Equal(HttpStatusCode.BadRequest, resposta.StatusCode);

        using var json = JsonDocument.Parse(await resposta.Content.ReadAsStringAsync());

        Assert.True(json.RootElement.TryGetProperty("errors", out var errors));
        Assert.True(errors.TryGetProperty("EnderecoEntrega", out _));
    }

    [Fact]
    public async Task Pedido_com_produto_repetido_retorna_400()
    {
        var produto = await CriarProdutoAsync();
        var cliente = await CriarClienteAsync();
        await AdicionarAoCardapioAsync(produto.Id);

        var resposta = await EnviarAsync(
            HttpMethod.Post,
            "/api/pedidos",
            MontarPedido(cliente.Id, TipoEntrega.Retirada, null, (produto.Id, 1), (produto.Id, 2)),
            await TokenAdminAsync());

        Assert.Equal(HttpStatusCode.BadRequest, resposta.StatusCode);

        using var json = JsonDocument.Parse(await resposta.Content.ReadAsStringAsync());

        Assert.True(json.RootElement.TryGetProperty("errors", out var errors));

        var mensagens = errors.GetProperty("Itens")
            .EnumerateArray()
            .Select(item => item.GetString())
            .ToArray();

        Assert.Contains(mensagens, mensagem => mensagem is not null && mensagem.Contains("repetidos"));
    }

    [Fact]
    public async Task Pedido_com_marmita_fora_do_cardapio_retorna_400()
    {
        var produto = await CriarProdutoAsync();
        var cliente = await CriarClienteAsync();

        var resposta = await EnviarAsync(
            HttpMethod.Post,
            "/api/pedidos",
            MontarPedido(cliente.Id, TipoEntrega.Retirada, null, (produto.Id, 1)),
            await TokenAdminAsync());

        var mensagem = await AssertErroAsync(resposta, HttpStatusCode.BadRequest);

        Assert.Contains("indisponíveis", mensagem);
    }

    [Fact]
    public async Task Pedido_com_bebida_nao_exige_cardapio()
    {
        var bebida = await CriarProdutoAsync(TipoProduto.Bebida, 5.00m);
        var cliente = await CriarClienteAsync();

        var pedido = await CriarPedidoAsync(cliente.Id, TipoEntrega.Retirada, null, (bebida.Id, 1));

        Assert.Equal(5.00m, pedido.Total);
    }

    [Fact]
    public async Task Nome_e_preco_do_item_permanecem_historicos_apos_alterar_o_produto()
    {
        var produto = await CriarProdutoAsync(preco: 20.00m);
        var cliente = await CriarClienteAsync();
        await AdicionarAoCardapioAsync(produto.Id);

        var pedido = await CriarPedidoAsync(cliente.Id, TipoEntrega.Retirada, null, (produto.Id, 2));

        var alteracao = await EnviarAsync(HttpMethod.Put, $"/api/produtos/{produto.Id}", new
        {
            nome = "Produto Alterado",
            descricao = "Preço novo",
            preco = 99.00m,
            tipo = (int)TipoProduto.Marmita,
            ativo = true
        }, await TokenAdminAsync());

        Assert.Equal(HttpStatusCode.NoContent, alteracao.StatusCode);

        var consultado = await ConsultarPedidoAsync(pedido.Id);

        var item = Assert.Single(consultado.Itens);
        Assert.Equal(produto.Nome, item.NomeProduto);
        Assert.Equal(20.00m, item.PrecoUnitario);
        Assert.Equal(40.00m, consultado.Total);
    }

    [Fact]
    public async Task Filtro_por_status_e_cliente_isola_os_pedidos()
    {
        var produto = await CriarProdutoAsync();
        await AdicionarAoCardapioAsync(produto.Id);

        var clienteCancelado = await CriarClienteAsync();
        var clienteNovo = await CriarClienteAsync();

        var pedidoCancelado = await CriarPedidoAsync(
            clienteCancelado.Id, TipoEntrega.Retirada, null, (produto.Id, 1));

        await CriarPedidoAsync(clienteNovo.Id, TipoEntrega.Retirada, null, (produto.Id, 1));

        var cancelamento = await AlterarStatusAsync(pedidoCancelado.Id, StatusPedido.Cancelado);
        Assert.Equal(HttpStatusCode.NoContent, cancelamento.StatusCode);

        var porStatus = await ConsultarPedidosAsync(
            $"status={(int)StatusPedido.Cancelado}&clienteId={clienteCancelado.Id}&tamanhoPagina=100");

        var unico = Assert.Single(porStatus.Itens);
        Assert.Equal(pedidoCancelado.Id, unico.Id);
        Assert.Equal(1, porStatus.TotalRegistros);

        var porCliente = await ConsultarPedidosAsync(
            $"clienteId={clienteNovo.Id}&tamanhoPagina=100");

        Assert.Single(porCliente.Itens);
        Assert.Equal(clienteNovo.Id, porCliente.Itens[0].ClienteId);
    }

    [Fact]
    public async Task Filtro_por_pago_separa_pedidos_com_e_sem_pagamento()
    {
        var produto = await CriarProdutoAsync();
        await AdicionarAoCardapioAsync(produto.Id);

        var cliente = await CriarClienteAsync();
        var semPagamento = await CriarPedidoAsync(
            cliente.Id, TipoEntrega.Retirada, null, (produto.Id, 1));
        var comPagamento = await CriarPedidoAsync(
            cliente.Id, TipoEntrega.Retirada, null, (produto.Id, 1));

        var pagamento = await RegistrarPagamentoAsync(comPagamento.Id);
        Assert.Equal(HttpStatusCode.Created, pagamento.StatusCode);

        var pagos = await ConsultarPedidosAsync($"clienteId={cliente.Id}&pago=true&tamanhoPagina=100");
        var naoPagos = await ConsultarPedidosAsync($"clienteId={cliente.Id}&pago=false&tamanhoPagina=100");

        Assert.Equal(comPagamento.Id, Assert.Single(pagos.Itens).Id);
        Assert.Equal(semPagamento.Id, Assert.Single(naoPagos.Itens).Id);
    }

    [Fact]
    public async Task Filtro_por_reembolso_pendente_acompanha_o_reembolso()
    {
        var produto = await CriarProdutoAsync();
        await AdicionarAoCardapioAsync(produto.Id);

        var cliente = await CriarClienteAsync();
        var pedido = await CriarPedidoAsync(cliente.Id, TipoEntrega.Retirada, null, (produto.Id, 1));

        Assert.Equal(HttpStatusCode.Created, (await RegistrarPagamentoAsync(pedido.Id)).StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, (await AlterarStatusAsync(pedido.Id, StatusPedido.Cancelado)).StatusCode);

        var pendentes = await ConsultarPedidosAsync(
            $"clienteId={cliente.Id}&reembolsoPendente=true&tamanhoPagina=100");

        Assert.Equal(pedido.Id, Assert.Single(pendentes.Itens).Id);

        Assert.Equal(HttpStatusCode.Created, (await RegistrarReembolsoAsync(pedido.Id)).StatusCode);

        var aindaPendentes = await ConsultarPedidosAsync(
            $"clienteId={cliente.Id}&reembolsoPendente=true&tamanhoPagina=100");
        var semPendencia = await ConsultarPedidosAsync(
            $"clienteId={cliente.Id}&reembolsoPendente=false&tamanhoPagina=100");

        Assert.Empty(aindaPendentes.Itens);
        Assert.Equal(pedido.Id, Assert.Single(semPendencia.Itens).Id);

        var combinacaoVazia = await ConsultarPedidosAsync(
            $"clienteId={cliente.Id}&pago=false&reembolsoPendente=true&tamanhoPagina=100");

        Assert.Equal(0, combinacaoVazia.TotalRegistros);
        Assert.Empty(combinacaoVazia.Itens);
    }

    [Fact]
    public async Task Filtro_por_data_converte_o_dia_de_brasilia_para_utc()
    {
        var produto = await CriarProdutoAsync();
        await AdicionarAoCardapioAsync(produto.Id);

        var cliente = await CriarClienteAsync();
        var pedido = await CriarPedidoAsync(cliente.Id, TipoEntrega.Retirada, null, (produto.Id, 1));

        // 15/01/2026 02:30 UTC = 14/01/2026 23:30 em Brasília (UTC-3).
        await Fixture.DefinirDataCriacaoAsync(
            pedido.Id,
            new DateTime(2026, 1, 15, 2, 30, 0, DateTimeKind.Utc));

        var noDiaDeBrasilia = await ConsultarPedidosAsync(
            $"clienteId={cliente.Id}&dataInicial=2026-01-14&dataFinal=2026-01-14&tamanhoPagina=100");
        var noDiaSeguinte = await ConsultarPedidosAsync(
            $"clienteId={cliente.Id}&dataInicial=2026-01-15&dataFinal=2026-01-15&tamanhoPagina=100");
        var noDiaAnterior = await ConsultarPedidosAsync(
            $"clienteId={cliente.Id}&dataInicial=2026-01-13&dataFinal=2026-01-13&tamanhoPagina=100");

        Assert.Equal(pedido.Id, Assert.Single(noDiaDeBrasilia.Itens).Id);
        Assert.Empty(noDiaSeguinte.Itens);
        Assert.Empty(noDiaAnterior.Itens);
    }

    [Fact]
    public async Task Data_final_9999_retorna_400()
    {
        var resposta = await EnviarAsync(
            HttpMethod.Get,
            "/api/pedidos?dataFinal=9999-12-31",
            token: await TokenAdminAsync());

        var mensagem = await AssertErroAsync(resposta, HttpStatusCode.BadRequest);

        Assert.Contains("data final", mensagem, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Paginacao_respeita_pagina_e_tamanho()
    {
        var produto = await CriarProdutoAsync();
        await AdicionarAoCardapioAsync(produto.Id);

        var cliente = await CriarClienteAsync();

        for (var i = 0; i < 3; i++)
        {
            await CriarPedidoAsync(cliente.Id, TipoEntrega.Retirada, null, (produto.Id, 1));
        }

        var pagina = await ConsultarPedidosAsync(
            $"clienteId={cliente.Id}&pagina=2&tamanhoPagina=2");

        Assert.Equal(3, pagina.TotalRegistros);
        Assert.Equal(2, pagina.Pagina);
        Assert.Equal(2, pagina.TamanhoPagina);
        Assert.Single(pagina.Itens);
    }
}
