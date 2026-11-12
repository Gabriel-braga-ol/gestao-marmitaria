using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using GestaoMarmitaria.Api.Features.Cardapio;
using GestaoMarmitaria.Api.Features.Pagamentos;
using GestaoMarmitaria.Api.Features.Pedidos;
using GestaoMarmitaria.Api.Enums;
using GestaoMarmitaria.Api.Models;
using Microsoft.EntityFrameworkCore;

namespace GestaoMarmitaria.Tests.IntegrationTests;

[Collection(ApiIntegrationCollection.Nome)]
public abstract class IntegracaoTesteBase : IDisposable
{
    private static readonly JsonSerializerOptions OpcoesJson = new(JsonSerializerDefaults.Web);

    protected static readonly TimeZoneInfo FusoBrasilia =
        TimeZoneInfo.FindSystemTimeZoneById("America/Sao_Paulo");

    protected ApiIntegrationFixture Fixture { get; }

    protected HttpClient Client { get; }

    protected IntegracaoTesteBase(ApiIntegrationFixture fixture)
    {
        Fixture = fixture;
        Client = fixture.CriarCliente();
    }

    protected static DateOnly HojeEmBrasilia()
    {
        return DateOnly.FromDateTime(
            TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, FusoBrasilia));
    }

    protected Task<string> TokenAdminAsync()
    {
        return Fixture.TokenAdminAsync();
    }

    protected Task<string> TokenFuncionarioAsync()
    {
        return Fixture.TokenFuncionarioAsync();
    }

    protected async Task<int> ObterUsuarioIdAsync(string email)
    {
        await using var contexto = Fixture.CriarDbContext();

        return await contexto.Usuarios
            .AsNoTracking()
            .Where(u => u.Email == email)
            .Select(u => u.Id)
            .SingleAsync();
    }

    protected async Task<HttpResponseMessage> EnviarAsync(
        HttpMethod metodo,
        string url,
        object? corpo = null,
        string? token = null)
    {
        using var requisicao = new HttpRequestMessage(metodo, url);

        if (token is not null)
        {
            requisicao.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        }

        if (corpo is not null)
        {
            requisicao.Content = JsonContent.Create(corpo, options: OpcoesJson);
        }

        return await Client.SendAsync(requisicao);
    }

    protected static async Task<T> LerAsync<T>(HttpResponseMessage resposta)
    {
        var valor = await resposta.Content.ReadFromJsonAsync<T>(OpcoesJson);

        Assert.NotNull(valor);

        return valor!;
    }

    protected static async Task<string> LerMensagemAsync(HttpResponseMessage resposta)
    {
        var json = await resposta.Content.ReadFromJsonAsync<JsonElement>(OpcoesJson);

        return json.TryGetProperty("mensagem", out var mensagem)
            ? mensagem.GetString() ?? string.Empty
            : string.Empty;
    }

    protected static async Task<string> AssertErroAsync(
        HttpResponseMessage resposta,
        HttpStatusCode esperado)
    {
        Assert.Equal(esperado, resposta.StatusCode);

        return await LerMensagemAsync(resposta);
    }

    protected async Task<Produto> CriarProdutoAsync(
        TipoProduto tipo = TipoProduto.Marmita,
        decimal preco = 20.00m,
        string? nome = null)
    {
        var resposta = await EnviarAsync(HttpMethod.Post, "/api/produtos", new
        {
            nome = nome ?? $"Produto {Guid.NewGuid():N}",
            descricao = "Produto criado pelos testes de integração",
            preco,
            tipo = (int)tipo
        }, await TokenAdminAsync());

        Assert.Equal(HttpStatusCode.Created, resposta.StatusCode);

        return await LerAsync<Produto>(resposta);
    }

    protected async Task DesativarProdutoAsync(Produto produto)
    {
        var resposta = await EnviarAsync(HttpMethod.Put, $"/api/produtos/{produto.Id}", new
        {
            nome = produto.Nome,
            descricao = produto.Descricao,
            preco = produto.Preco,
            tipo = (int)produto.Tipo,
            ativo = false
        }, await TokenAdminAsync());

        Assert.Equal(HttpStatusCode.NoContent, resposta.StatusCode);
    }

    protected async Task<Cliente> CriarClienteAsync(string? nome = null)
    {
        var resposta = await EnviarAsync(HttpMethod.Post, "/api/clientes", new
        {
            nome = nome ?? $"Cliente {Guid.NewGuid():N}",
            telefone = "(21) 99999-0000",
            endereco = "Rua de Teste, 100"
        }, await TokenAdminAsync());

        Assert.Equal(HttpStatusCode.Created, resposta.StatusCode);

        return await LerAsync<Cliente>(resposta);
    }

    protected async Task<ItemCardapioRespostaDto> AdicionarAoCardapioAsync(
        int produtoId,
        DateOnly? data = null)
    {
        var resposta = await EnviarAsync(HttpMethod.Post, "/api/cardapio", new
        {
            data = data ?? HojeEmBrasilia(),
            produtoId
        }, await TokenAdminAsync());

        Assert.Equal(HttpStatusCode.Created, resposta.StatusCode);

        return await LerAsync<ItemCardapioRespostaDto>(resposta);
    }

    protected async Task<PedidoRespostaDto> CriarPedidoAsync(
        int clienteId,
        TipoEntrega tipoEntrega = TipoEntrega.Retirada,
        string? enderecoEntrega = null,
        params (int ProdutoId, int Quantidade)[] itens)
    {
        var resposta = await EnviarAsync(
            HttpMethod.Post,
            "/api/pedidos",
            MontarPedido(clienteId, tipoEntrega, enderecoEntrega, itens),
            await TokenAdminAsync());

        Assert.Equal(HttpStatusCode.Created, resposta.StatusCode);

        return await LerAsync<PedidoRespostaDto>(resposta);
    }

    protected static object MontarPedido(
        int clienteId,
        TipoEntrega tipoEntrega,
        string? enderecoEntrega,
        params (int ProdutoId, int Quantidade)[] itens)
    {
        return new
        {
            clienteId,
            tipoEntrega = (int)tipoEntrega,
            enderecoEntrega,
            itens = itens
                .Select(i => new { produtoId = i.ProdutoId, quantidade = i.Quantidade })
                .ToArray()
        };
    }

    protected async Task<HttpResponseMessage> RegistrarPagamentoAsync(
        int pedidoId,
        string? token = null)
    {
        return await EnviarAsync(
            HttpMethod.Post,
            $"/api/pedidos/{pedidoId}/pagamento",
            new { formaPagamento = (int)FormaPagamento.Pix },
            token ?? await TokenAdminAsync());
    }

    protected async Task<HttpResponseMessage> RegistrarReembolsoAsync(
        int pedidoId,
        string? token = null)
    {
        return await EnviarAsync(
            HttpMethod.Post,
            $"/api/pedidos/{pedidoId}/reembolso",
            null,
            token ?? await TokenAdminAsync());
    }

    protected async Task<HttpResponseMessage> AlterarStatusAsync(
        int pedidoId,
        StatusPedido status,
        string? token = null)
    {
        return await EnviarAsync(
            HttpMethod.Patch,
            $"/api/pedidos/{pedidoId}/status",
            new { status = (int)status },
            token ?? await TokenAdminAsync());
    }

    protected async Task<PedidoRespostaDto> ConsultarPedidoAsync(
        int pedidoId,
        string? token = null)
    {
        var resposta = await EnviarAsync(
            HttpMethod.Get,
            $"/api/pedidos/{pedidoId}",
            token: token ?? await TokenAdminAsync());

        Assert.Equal(HttpStatusCode.OK, resposta.StatusCode);

        return await LerAsync<PedidoRespostaDto>(resposta);
    }

    protected async Task<PedidosPaginadosRespostaDto> ConsultarPedidosAsync(
        string filtros,
        string? token = null)
    {
        var resposta = await EnviarAsync(
            HttpMethod.Get,
            $"/api/pedidos?{filtros}",
            token: token ?? await TokenAdminAsync());

        Assert.Equal(HttpStatusCode.OK, resposta.StatusCode);

        return await LerAsync<PedidosPaginadosRespostaDto>(resposta);
    }

    protected async Task<PagamentoRespostaDto> ConsultarPagamentoAsync(int pedidoId)
    {
        var resposta = await EnviarAsync(
            HttpMethod.Get,
            $"/api/pedidos/{pedidoId}/pagamento",
            token: await TokenAdminAsync());

        Assert.Equal(HttpStatusCode.OK, resposta.StatusCode);

        return await LerAsync<PagamentoRespostaDto>(resposta);
    }

    public void Dispose()
    {
        Client.Dispose();
    }
}
