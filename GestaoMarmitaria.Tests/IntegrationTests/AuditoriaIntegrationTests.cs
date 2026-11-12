using System.Net;
using GestaoMarmitaria.Api.Features.Pagamentos;
using GestaoMarmitaria.Api.Features.Pedidos;
using GestaoMarmitaria.Api.Enums;
using Microsoft.EntityFrameworkCore;

namespace GestaoMarmitaria.Tests.IntegrationTests;

public class AuditoriaIntegrationTests : IntegracaoTesteBase
{
    public AuditoriaIntegrationTests(ApiIntegrationFixture fixture) : base(fixture)
    {
    }

    [Fact]
    public async Task Pagamento_registra_usuario_e_data_do_token()
    {
        var produto = await CriarProdutoAsync();
        await AdicionarAoCardapioAsync(produto.Id);
        var cliente = await CriarClienteAsync();
        var pedido = await CriarPedidoAsync(cliente.Id, TipoEntrega.Retirada, null, (produto.Id, 1));

        Assert.Equal(HttpStatusCode.Created, (await RegistrarPagamentoAsync(pedido.Id)).StatusCode);

        var usuarioId = await ObterUsuarioIdAsync(ApiIntegrationFixture.EmailAdmin);

        await using var contexto = Fixture.CriarDbContext();
        var pagamento = await contexto.Pagamentos
            .AsNoTracking()
            .SingleAsync(p => p.PedidoId == pedido.Id);

        Assert.Equal(usuarioId, pagamento.UsuarioRegistroId);
        Assert.NotNull(pagamento.DataRegistro);
        Assert.Equal(DateTimeKind.Utc, pagamento.DataRegistro!.Value.Kind);
    }

    [Fact]
    public async Task Reembolso_registra_usuario_e_data_do_token()
    {
        var produto = await CriarProdutoAsync();
        await AdicionarAoCardapioAsync(produto.Id);
        var cliente = await CriarClienteAsync();
        var pedido = await CriarPedidoAsync(cliente.Id, TipoEntrega.Retirada, null, (produto.Id, 1));

        Assert.Equal(HttpStatusCode.Created, (await RegistrarPagamentoAsync(pedido.Id)).StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, (await AlterarStatusAsync(pedido.Id, StatusPedido.Cancelado)).StatusCode);
        Assert.Equal(HttpStatusCode.Created, (await RegistrarReembolsoAsync(pedido.Id)).StatusCode);

        var usuarioId = await ObterUsuarioIdAsync(ApiIntegrationFixture.EmailAdmin);

        await using var contexto = Fixture.CriarDbContext();
        var reembolso = await contexto.Reembolsos
            .AsNoTracking()
            .SingleAsync(r => r.Pagamento.PedidoId == pedido.Id);

        Assert.Equal(usuarioId, reembolso.UsuarioRegistroId);
        Assert.NotNull(reembolso.DataRegistro);
        Assert.Equal(DateTimeKind.Utc, reembolso.DataRegistro!.Value.Kind);
    }

    [Fact]
    public async Task Mudancas_de_status_registram_historico_com_o_usuario_do_token()
    {
        var produto = await CriarProdutoAsync();
        await AdicionarAoCardapioAsync(produto.Id);
        var cliente = await CriarClienteAsync();
        var pedido = await CriarPedidoAsync(cliente.Id, TipoEntrega.Retirada, null, (produto.Id, 1));

        var tokenAdmin = await TokenAdminAsync();
        var tokenFuncionario = await TokenFuncionarioAsync();

        var adminId = await ObterUsuarioIdAsync(ApiIntegrationFixture.EmailAdmin);
        var funcionarioId = await ObterUsuarioIdAsync(ApiIntegrationFixture.EmailFuncionario);

        Assert.Equal(
            HttpStatusCode.NoContent,
            (await AlterarStatusAsync(pedido.Id, StatusPedido.EmPreparo, tokenAdmin)).StatusCode);

        Assert.Equal(
            HttpStatusCode.NoContent,
            (await AlterarStatusAsync(pedido.Id, StatusPedido.Cancelado, tokenFuncionario)).StatusCode);

        await using var contexto = Fixture.CriarDbContext();
        var historico = await contexto.HistoricosStatusPedido
            .AsNoTracking()
            .Where(h => h.PedidoId == pedido.Id)
            .OrderBy(h => h.Id)
            .ToListAsync();

        Assert.Equal(2, historico.Count);

        Assert.Equal(StatusPedido.Novo, historico[0].StatusAnterior);
        Assert.Equal(StatusPedido.EmPreparo, historico[0].StatusNovo);
        Assert.Equal(adminId, historico[0].UsuarioId);
        Assert.Equal(DateTimeKind.Utc, historico[0].DataAlteracao.Kind);

        Assert.Equal(StatusPedido.EmPreparo, historico[1].StatusAnterior);
        Assert.Equal(StatusPedido.Cancelado, historico[1].StatusNovo);
        Assert.Equal(funcionarioId, historico[1].UsuarioId);
        Assert.Equal(DateTimeKind.Utc, historico[1].DataAlteracao.Kind);
    }

    [Fact]
    public async Task Criacao_do_pedido_nao_gera_historico_de_status()
    {
        var produto = await CriarProdutoAsync();
        await AdicionarAoCardapioAsync(produto.Id);
        var cliente = await CriarClienteAsync();
        var pedido = await CriarPedidoAsync(cliente.Id, TipoEntrega.Retirada, null, (produto.Id, 1));

        await using var contexto = Fixture.CriarDbContext();

        Assert.False(await contexto.HistoricosStatusPedido
            .AsNoTracking()
            .AnyAsync(h => h.PedidoId == pedido.Id));
    }

    [Fact]
    public async Task Historico_de_status_expoe_transicoes_e_usuario_para_os_dois_perfis()
    {
        var produto = await CriarProdutoAsync();
        await AdicionarAoCardapioAsync(produto.Id);
        var cliente = await CriarClienteAsync();
        var pedido = await CriarPedidoAsync(cliente.Id, TipoEntrega.Retirada, null, (produto.Id, 1));

        var tokenAdmin = await TokenAdminAsync();
        var tokenFuncionario = await TokenFuncionarioAsync();

        Assert.Equal(
            HttpStatusCode.NoContent,
            (await AlterarStatusAsync(pedido.Id, StatusPedido.EmPreparo, tokenAdmin)).StatusCode);
        Assert.Equal(
            HttpStatusCode.NoContent,
            (await AlterarStatusAsync(pedido.Id, StatusPedido.Cancelado, tokenFuncionario)).StatusCode);

        var respostaAdmin = await EnviarAsync(
            HttpMethod.Get,
            $"/api/pedidos/{pedido.Id}/historico",
            token: tokenAdmin);

        Assert.Equal(HttpStatusCode.OK, respostaAdmin.StatusCode);

        var historico = await LerAsync<List<HistoricoStatusPedidoRespostaDto>>(respostaAdmin);

        var adminId = await ObterUsuarioIdAsync(ApiIntegrationFixture.EmailAdmin);
        var funcionarioId = await ObterUsuarioIdAsync(ApiIntegrationFixture.EmailFuncionario);

        Assert.Equal(2, historico.Count);

        Assert.Equal(StatusPedido.Novo, historico[0].StatusAnterior);
        Assert.Equal(StatusPedido.EmPreparo, historico[0].StatusNovo);
        Assert.Equal(adminId, historico[0].UsuarioId);
        Assert.Equal("Admin Integracao", historico[0].UsuarioNome);
        Assert.Equal(DateTimeKind.Utc, historico[0].DataAlteracao.Kind);

        Assert.Equal(StatusPedido.EmPreparo, historico[1].StatusAnterior);
        Assert.Equal(StatusPedido.Cancelado, historico[1].StatusNovo);
        Assert.Equal(funcionarioId, historico[1].UsuarioId);
        Assert.Equal("Funcionario Integracao", historico[1].UsuarioNome);
        Assert.Equal(DateTimeKind.Utc, historico[1].DataAlteracao.Kind);

        var respostaFuncionario = await EnviarAsync(
            HttpMethod.Get,
            $"/api/pedidos/{pedido.Id}/historico",
            token: tokenFuncionario);

        Assert.Equal(HttpStatusCode.OK, respostaFuncionario.StatusCode);
    }

    [Fact]
    public async Task Historico_de_pedido_sem_transicoes_retorna_lista_vazia()
    {
        var produto = await CriarProdutoAsync();
        await AdicionarAoCardapioAsync(produto.Id);
        var cliente = await CriarClienteAsync();
        var pedido = await CriarPedidoAsync(cliente.Id, TipoEntrega.Retirada, null, (produto.Id, 1));

        var resposta = await EnviarAsync(
            HttpMethod.Get,
            $"/api/pedidos/{pedido.Id}/historico",
            token: await TokenAdminAsync());

        Assert.Equal(HttpStatusCode.OK, resposta.StatusCode);

        var historico = await LerAsync<List<HistoricoStatusPedidoRespostaDto>>(resposta);

        Assert.Empty(historico);
    }

    [Fact]
    public async Task Historico_de_pedido_inexistente_retorna_404()
    {
        var resposta = await EnviarAsync(
            HttpMethod.Get,
            "/api/pedidos/999999/historico",
            token: await TokenAdminAsync());

        Assert.Equal(HttpStatusCode.NotFound, resposta.StatusCode);
    }

    [Fact]
    public async Task Historico_sem_token_retorna_401()
    {
        var resposta = await EnviarAsync(HttpMethod.Get, "/api/pedidos/1/historico");

        Assert.Equal(HttpStatusCode.Unauthorized, resposta.StatusCode);
    }

    [Fact]
    public async Task Consulta_de_pagamento_e_reembolso_expoe_auditoria_com_nome()
    {
        var produto = await CriarProdutoAsync();
        await AdicionarAoCardapioAsync(produto.Id);
        var cliente = await CriarClienteAsync();
        var pedido = await CriarPedidoAsync(cliente.Id, TipoEntrega.Retirada, null, (produto.Id, 1));

        var criacaoPagamento = await RegistrarPagamentoAsync(pedido.Id);

        Assert.Equal(HttpStatusCode.Created, criacaoPagamento.StatusCode);

        var pagamentoCriado = await LerAsync<PagamentoRespostaDto>(criacaoPagamento);
        var adminId = await ObterUsuarioIdAsync(ApiIntegrationFixture.EmailAdmin);

        Assert.Equal(adminId, pagamentoCriado.UsuarioRegistroId);
        Assert.Equal("Admin Integracao", pagamentoCriado.UsuarioRegistroNome);
        Assert.NotNull(pagamentoCriado.DataRegistro);
        Assert.Equal(DateTimeKind.Utc, pagamentoCriado.DataRegistro!.Value.Kind);

        var consultaPagamento = await EnviarAsync(
            HttpMethod.Get,
            $"/api/pedidos/{pedido.Id}/pagamento",
            token: await TokenAdminAsync());

        Assert.Equal(HttpStatusCode.OK, consultaPagamento.StatusCode);

        var pagamento = await LerAsync<PagamentoRespostaDto>(consultaPagamento);

        Assert.Equal(adminId, pagamento.UsuarioRegistroId);
        Assert.Equal("Admin Integracao", pagamento.UsuarioRegistroNome);
        Assert.NotNull(pagamento.DataRegistro);

        Assert.Equal(
            HttpStatusCode.NoContent,
            (await AlterarStatusAsync(pedido.Id, StatusPedido.Cancelado)).StatusCode);

        var criacaoReembolso = await RegistrarReembolsoAsync(pedido.Id);

        Assert.Equal(HttpStatusCode.Created, criacaoReembolso.StatusCode);

        var reembolsoCriado = await LerAsync<ReembolsoRespostaDto>(criacaoReembolso);

        Assert.Equal(adminId, reembolsoCriado.UsuarioRegistroId);
        Assert.Equal("Admin Integracao", reembolsoCriado.UsuarioRegistroNome);
        Assert.NotNull(reembolsoCriado.DataRegistro);

        var consultaReembolso = await EnviarAsync(
            HttpMethod.Get,
            $"/api/pedidos/{pedido.Id}/reembolso",
            token: await TokenAdminAsync());

        Assert.Equal(HttpStatusCode.OK, consultaReembolso.StatusCode);

        var reembolso = await LerAsync<ReembolsoRespostaDto>(consultaReembolso);

        Assert.Equal(adminId, reembolso.UsuarioRegistroId);
        Assert.Equal("Admin Integracao", reembolso.UsuarioRegistroNome);
        Assert.NotNull(reembolso.DataRegistro);
        Assert.Equal(DateTimeKind.Utc, reembolso.DataRegistro!.Value.Kind);
    }
}
