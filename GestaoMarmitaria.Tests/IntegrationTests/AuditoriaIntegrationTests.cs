using System.Net;
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
}
