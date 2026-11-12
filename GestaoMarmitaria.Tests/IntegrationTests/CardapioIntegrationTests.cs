using System.Net;
using GestaoMarmitaria.Api.Features.Cardapio;
using GestaoMarmitaria.Api.Enums;

namespace GestaoMarmitaria.Tests.IntegrationTests;

public class CardapioIntegrationTests : IntegracaoTesteBase
{
    public CardapioIntegrationTests(ApiIntegrationFixture fixture) : base(fixture)
    {
    }

    [Fact]
    public async Task Cardapio_rejeita_bebida()
    {
        var bebida = await CriarProdutoAsync(TipoProduto.Bebida, 5.00m);

        var resposta = await EnviarAsync(HttpMethod.Post, "/api/cardapio", new
        {
            data = HojeEmBrasilia(),
            produtoId = bebida.Id
        }, await TokenAdminAsync());

        var mensagem = await AssertErroAsync(resposta, HttpStatusCode.BadRequest);

        Assert.Contains("Somente marmitas", mensagem);
    }

    [Fact]
    public async Task Cardapio_rejeita_marmita_inativa()
    {
        var marmita = await CriarProdutoAsync();
        await DesativarProdutoAsync(marmita);

        var resposta = await EnviarAsync(HttpMethod.Post, "/api/cardapio", new
        {
            data = HojeEmBrasilia(),
            produtoId = marmita.Id
        }, await TokenAdminAsync());

        var mensagem = await AssertErroAsync(resposta, HttpStatusCode.BadRequest);

        Assert.Contains("inativo", mensagem);
    }

    [Fact]
    public async Task Cardapio_nao_aceita_o_mesmo_produto_duas_vezes_na_data()
    {
        var marmita = await CriarProdutoAsync();
        var item = await AdicionarAoCardapioAsync(marmita.Id);

        var resposta = await EnviarAsync(HttpMethod.Post, "/api/cardapio", new
        {
            data = item.Data,
            produtoId = marmita.Id
        }, await TokenAdminAsync());

        var mensagem = await AssertErroAsync(resposta, HttpStatusCode.Conflict);

        Assert.Contains("já está no cardápio", mensagem);
    }

    [Fact]
    public async Task Consulta_do_cardapio_exige_data_e_retorna_os_itens_do_dia()
    {
        var marmita = await CriarProdutoAsync();
        var item = await AdicionarAoCardapioAsync(marmita.Id);

        var semData = await EnviarAsync(
            HttpMethod.Get,
            "/api/cardapio",
            token: await TokenAdminAsync());

        Assert.Equal(HttpStatusCode.BadRequest, semData.StatusCode);

        var comData = await EnviarAsync(
            HttpMethod.Get,
            $"/api/cardapio?data={item.Data:yyyy-MM-dd}",
            token: await TokenAdminAsync());

        Assert.Equal(HttpStatusCode.OK, comData.StatusCode);

        var itens = await LerAsync<List<ItemCardapioRespostaDto>>(comData);

        Assert.Contains(itens, i => i.Id == item.Id && i.ProdutoId == marmita.Id);
    }

    [Fact]
    public async Task Funcionario_pode_alterar_a_disponibilidade()
    {
        var marmita = await CriarProdutoAsync();
        var item = await AdicionarAoCardapioAsync(marmita.Id);

        var resposta = await EnviarAsync(
            HttpMethod.Patch,
            $"/api/cardapio/{item.Id}/disponibilidade",
            new { disponivel = false },
            await TokenFuncionarioAsync());

        Assert.Equal(HttpStatusCode.NoContent, resposta.StatusCode);

        var consulta = await EnviarAsync(
            HttpMethod.Get,
            $"/api/cardapio/{item.Id}",
            token: await TokenAdminAsync());

        var consultado = await LerAsync<ItemCardapioRespostaDto>(consulta);

        Assert.False(consultado.Disponivel);
    }

    [Fact]
    public async Task Pedido_rejeita_marmita_indisponivel_e_aceita_depois_de_reativar()
    {
        var marmita = await CriarProdutoAsync();
        var item = await AdicionarAoCardapioAsync(marmita.Id);
        var cliente = await CriarClienteAsync();

        var desativar = await EnviarAsync(
            HttpMethod.Patch,
            $"/api/cardapio/{item.Id}/disponibilidade",
            new { disponivel = false },
            await TokenAdminAsync());

        Assert.Equal(HttpStatusCode.NoContent, desativar.StatusCode);

        var bloqueado = await EnviarAsync(
            HttpMethod.Post,
            "/api/pedidos",
            MontarPedido(cliente.Id, TipoEntrega.Retirada, null, (marmita.Id, 1)),
            await TokenAdminAsync());

        var mensagem = await AssertErroAsync(bloqueado, HttpStatusCode.BadRequest);
        Assert.Contains("indisponíveis", mensagem);

        var reativar = await EnviarAsync(
            HttpMethod.Patch,
            $"/api/cardapio/{item.Id}/disponibilidade",
            new { disponivel = true },
            await TokenAdminAsync());

        Assert.Equal(HttpStatusCode.NoContent, reativar.StatusCode);

        var aceito = await EnviarAsync(
            HttpMethod.Post,
            "/api/pedidos",
            MontarPedido(cliente.Id, TipoEntrega.Retirada, null, (marmita.Id, 1)),
            await TokenAdminAsync());

        Assert.Equal(HttpStatusCode.Created, aceito.StatusCode);
    }
}
