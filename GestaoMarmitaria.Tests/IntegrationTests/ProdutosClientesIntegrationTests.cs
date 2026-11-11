using System.Net;
using GestaoMarmitaria.Api.Enums;
using GestaoMarmitaria.Api.Models;

namespace GestaoMarmitaria.Tests.IntegrationTests;

public class ProdutosClientesIntegrationTests : IntegracaoTesteBase
{
    public ProdutosClientesIntegrationTests(ApiIntegrationFixture fixture) : base(fixture)
    {
    }

    [Fact]
    public async Task Trocar_o_tipo_de_produto_vinculado_ao_cardapio_retorna_409()
    {
        var marmita = await CriarProdutoAsync();
        await AdicionarAoCardapioAsync(marmita.Id);

        var resposta = await EnviarAsync(HttpMethod.Put, $"/api/produtos/{marmita.Id}", new
        {
            nome = marmita.Nome,
            descricao = marmita.Descricao,
            preco = marmita.Preco,
            tipo = (int)TipoProduto.Bebida,
            ativo = true
        }, await TokenAdminAsync());

        var mensagem = await AssertErroAsync(resposta, HttpStatusCode.Conflict);

        Assert.Contains("alterar o tipo", mensagem);

        var consulta = await EnviarAsync(
            HttpMethod.Get,
            $"/api/produtos/{marmita.Id}",
            token: await TokenAdminAsync());

        var produto = await LerAsync<Produto>(consulta);

        Assert.Equal(TipoProduto.Marmita, produto.Tipo);
    }

    [Fact]
    public async Task Excluir_produto_vinculado_somente_ao_cardapio_retorna_409()
    {
        var marmita = await CriarProdutoAsync();
        await AdicionarAoCardapioAsync(marmita.Id);

        var resposta = await EnviarAsync(
            HttpMethod.Delete,
            $"/api/produtos/{marmita.Id}",
            token: await TokenAdminAsync());

        var mensagem = await AssertErroAsync(resposta, HttpStatusCode.Conflict);

        Assert.Contains("cardápio", mensagem);

        var consulta = await EnviarAsync(
            HttpMethod.Get,
            $"/api/produtos/{marmita.Id}",
            token: await TokenAdminAsync());

        Assert.Equal(HttpStatusCode.OK, consulta.StatusCode);
    }

    [Fact]
    public async Task Excluir_produto_vinculado_a_pedido_retorna_409()
    {
        var marmita = await CriarProdutoAsync();
        await AdicionarAoCardapioAsync(marmita.Id);
        var cliente = await CriarClienteAsync();
        await CriarPedidoAsync(cliente.Id, TipoEntrega.Retirada, null, (marmita.Id, 1));

        var resposta = await EnviarAsync(
            HttpMethod.Delete,
            $"/api/produtos/{marmita.Id}",
            token: await TokenAdminAsync());

        Assert.Equal(HttpStatusCode.Conflict, resposta.StatusCode);
    }

    [Fact]
    public async Task Excluir_produto_sem_vinculo_retorna_204()
    {
        var bebida = await CriarProdutoAsync(TipoProduto.Bebida, 5.00m);

        var resposta = await EnviarAsync(
            HttpMethod.Delete,
            $"/api/produtos/{bebida.Id}",
            token: await TokenAdminAsync());

        Assert.Equal(HttpStatusCode.NoContent, resposta.StatusCode);

        var consulta = await EnviarAsync(
            HttpMethod.Get,
            $"/api/produtos/{bebida.Id}",
            token: await TokenAdminAsync());

        Assert.Equal(HttpStatusCode.NotFound, consulta.StatusCode);
    }

    [Fact]
    public async Task Excluir_cliente_com_pedidos_retorna_409()
    {
        var produto = await CriarProdutoAsync();
        await AdicionarAoCardapioAsync(produto.Id);
        var cliente = await CriarClienteAsync();
        await CriarPedidoAsync(cliente.Id, TipoEntrega.Retirada, null, (produto.Id, 1));

        var resposta = await EnviarAsync(
            HttpMethod.Delete,
            $"/api/clientes/{cliente.Id}",
            token: await TokenAdminAsync());

        var mensagem = await AssertErroAsync(resposta, HttpStatusCode.Conflict);

        Assert.Contains("possui pedidos", mensagem);

        var consulta = await EnviarAsync(
            HttpMethod.Get,
            $"/api/clientes/{cliente.Id}",
            token: await TokenAdminAsync());

        Assert.Equal(HttpStatusCode.OK, consulta.StatusCode);
    }

    [Fact]
    public async Task Funcionario_nao_pode_excluir_produto_nem_cliente()
    {
        var bebida = await CriarProdutoAsync(TipoProduto.Bebida, 5.00m);
        var cliente = await CriarClienteAsync();
        var token = await TokenFuncionarioAsync();

        var excluirProduto = await EnviarAsync(
            HttpMethod.Delete,
            $"/api/produtos/{bebida.Id}",
            token: token);

        var excluirCliente = await EnviarAsync(
            HttpMethod.Delete,
            $"/api/clientes/{cliente.Id}",
            token: token);

        Assert.Equal(HttpStatusCode.Forbidden, excluirProduto.StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, excluirCliente.StatusCode);
    }
}
