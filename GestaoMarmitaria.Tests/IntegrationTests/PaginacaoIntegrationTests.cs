using System.Net;
using GestaoMarmitaria.Api.Features.Compartilhado;
using GestaoMarmitaria.Api.Models;

namespace GestaoMarmitaria.Tests.IntegrationTests;

public class PaginacaoIntegrationTests : IntegracaoTesteBase
{
    public PaginacaoIntegrationTests(ApiIntegrationFixture fixture) : base(fixture)
    {
    }

    [Fact]
    public async Task Clientes_retornam_envelope_paginado()
    {
        await CriarClienteAsync();
        await CriarClienteAsync();

        var resposta = await EnviarAsync(
            HttpMethod.Get,
            "/api/clientes?pagina=1&tamanhoPagina=1",
            token: await TokenAdminAsync());

        Assert.Equal(HttpStatusCode.OK, resposta.StatusCode);

        var pagina = await LerAsync<ItensPaginadosRespostaDto<Cliente>>(resposta);

        Assert.Equal(1, pagina.Pagina);
        Assert.Equal(1, pagina.TamanhoPagina);
        Assert.Single(pagina.Itens);
        Assert.True(pagina.TotalRegistros >= 2);
        Assert.Equal(pagina.TotalRegistros, pagina.TotalPaginas);
    }

    [Fact]
    public async Task Produtos_retornam_envelope_paginado()
    {
        var primeiro = await CriarProdutoAsync(nome: $"Produto Paginado {Guid.NewGuid():N}");
        var segundo = await CriarProdutoAsync(nome: $"Produto Paginado {Guid.NewGuid():N}");

        var resposta = await EnviarAsync(
            HttpMethod.Get,
            "/api/produtos?pagina=1&tamanhoPagina=100",
            token: await TokenAdminAsync());

        Assert.Equal(HttpStatusCode.OK, resposta.StatusCode);

        var pagina = await LerAsync<ItensPaginadosRespostaDto<Produto>>(resposta);

        Assert.Equal(1, pagina.Pagina);
        Assert.Equal(100, pagina.TamanhoPagina);
        Assert.True(pagina.TotalRegistros >= 2);
        Assert.Contains(pagina.Itens, p => p.Id == primeiro.Id);
        Assert.Contains(pagina.Itens, p => p.Id == segundo.Id);
        Assert.Equal(
            (int)Math.Ceiling((double)pagina.TotalRegistros / pagina.TamanhoPagina),
            pagina.TotalPaginas);
    }

    [Fact]
    public async Task Pagina_alem_do_total_retorna_lista_vazia_com_totais()
    {
        await CriarProdutoAsync();

        var resposta = await EnviarAsync(
            HttpMethod.Get,
            "/api/produtos?pagina=100000&tamanhoPagina=100",
            token: await TokenAdminAsync());

        Assert.Equal(HttpStatusCode.OK, resposta.StatusCode);

        var pagina = await LerAsync<ItensPaginadosRespostaDto<Produto>>(resposta);

        Assert.Empty(pagina.Itens);
        Assert.Equal(100000, pagina.Pagina);
        Assert.True(pagina.TotalRegistros >= 1);
    }

    [Theory]
    [InlineData("/api/clientes?pagina=0")]
    [InlineData("/api/clientes?tamanhoPagina=101")]
    [InlineData("/api/produtos?pagina=0")]
    [InlineData("/api/produtos?tamanhoPagina=101")]
    public async Task Paginacao_invalida_retorna_400(string url)
    {
        var resposta = await EnviarAsync(
            HttpMethod.Get,
            url,
            token: await TokenAdminAsync());

        Assert.Equal(HttpStatusCode.BadRequest, resposta.StatusCode);
    }
}
