using System.Net;
using System.Net.Http.Headers;
using Microsoft.AspNetCore.Mvc.Testing;

namespace GestaoMarmitaria.Tests.IntegrationTests;

public class SegurancaHttpIntegrationTests : IntegracaoTesteBase
{
    public SegurancaHttpIntegrationTests(ApiIntegrationFixture fixture) : base(fixture)
    {
    }

    [Fact]
    public async Task Em_producao_requisicao_http_redireciona_para_https()
    {
        using var factory = Fixture.CriarFactoryEmProducao();
        using var cliente = factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false
        });

        var resposta = await cliente.GetAsync("http://localhost/api/status");

        Assert.Equal(HttpStatusCode.TemporaryRedirect, resposta.StatusCode);
        Assert.NotNull(resposta.Headers.Location);
        Assert.Equal("https", resposta.Headers.Location!.Scheme);
        Assert.Equal(7163, resposta.Headers.Location.Port);
    }

    [Fact]
    public async Task Em_producao_host_fora_do_AllowedHosts_retorna_400()
    {
        using var factory = Fixture.CriarFactoryEmProducao();
        using var cliente = factory.CreateClient();

        var resposta = await cliente.GetAsync("http://host-nao-configurado.test/api/status");

        Assert.Equal(HttpStatusCode.BadRequest, resposta.StatusCode);
    }

    [Fact]
    public async Task Em_development_host_permissivo_nao_bloqueia()
    {
        using var requisicao = new HttpRequestMessage(
            HttpMethod.Get,
            "http://host-livre.test/api/status");

        requisicao.Headers.Authorization =
            new AuthenticationHeaderValue("Bearer", await TokenAdminAsync());

        var resposta = await Client.SendAsync(requisicao);

        Assert.Equal(HttpStatusCode.OK, resposta.StatusCode);
    }
}
