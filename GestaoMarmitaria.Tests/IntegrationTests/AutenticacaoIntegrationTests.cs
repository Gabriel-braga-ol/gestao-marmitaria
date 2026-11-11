using System.Net;
using GestaoMarmitaria.Api.Dtos;
using GestaoMarmitaria.Api.Enums;

namespace GestaoMarmitaria.Tests.IntegrationTests;

public class AutenticacaoIntegrationTests : IntegracaoTesteBase
{
    public AutenticacaoIntegrationTests(ApiIntegrationFixture fixture) : base(fixture)
    {
    }

    [Fact]
    public async Task Endpoint_protegido_sem_token_retorna_401()
    {
        var resposta = await EnviarAsync(HttpMethod.Get, "/api/pedidos");

        Assert.Equal(HttpStatusCode.Unauthorized, resposta.StatusCode);
    }

    [Fact]
    public async Task Login_com_credenciais_invalidas_retorna_400_com_a_mesma_mensagem()
    {
        var senhaErrada = await EnviarAsync(HttpMethod.Post, "/api/autenticacao/login", new
        {
            email = ApiIntegrationFixture.EmailAdmin,
            senha = "senha-incorreta"
        });

        var emailInexistente = await EnviarAsync(HttpMethod.Post, "/api/autenticacao/login", new
        {
            email = $"nao-existe-{Guid.NewGuid():N}@marmitaria.test",
            senha = "senha-incorreta"
        });

        var mensagemSenhaErrada = await AssertErroAsync(senhaErrada, HttpStatusCode.BadRequest);
        var mensagemEmailInexistente = await AssertErroAsync(emailInexistente, HttpStatusCode.BadRequest);

        Assert.Equal(mensagemSenhaErrada, mensagemEmailInexistente);
        Assert.Contains("inválidos", mensagemSenhaErrada);
    }

    [Fact]
    public async Task Login_do_admin_criado_pelo_seed_retorna_200_com_token()
    {
        var resposta = await EnviarAsync(HttpMethod.Post, "/api/autenticacao/login", new
        {
            email = ApiIntegrationFixture.EmailAdmin,
            senha = ApiIntegrationFixture.SenhaAdmin
        });

        Assert.Equal(HttpStatusCode.OK, resposta.StatusCode);

        var token = await LerAsync<TokenRespostaDto>(resposta);

        Assert.False(string.IsNullOrWhiteSpace(token.Token));
        Assert.Equal(ApiIntegrationFixture.EmailAdmin, token.Email);
        Assert.Equal("Admin", token.Perfil);
    }

    [Fact]
    public async Task Funcionario_nao_pode_registrar_usuario()
    {
        var resposta = await EnviarAsync(HttpMethod.Post, "/api/autenticacao/registrar", new
        {
            nome = "Usuario Bloqueado",
            email = $"bloqueado-{Guid.NewGuid():N}@marmitaria.test",
            senha = "Senha123!",
            perfil = (int)PerfilUsuario.Funcionario
        }, await TokenFuncionarioAsync());

        Assert.Equal(HttpStatusCode.Forbidden, resposta.StatusCode);
    }

    [Fact]
    public async Task Funcionario_nao_pode_criar_produto()
    {
        var resposta = await EnviarAsync(HttpMethod.Post, "/api/produtos", new
        {
            nome = "Produto Bloqueado",
            descricao = "teste",
            preco = 10.00m,
            tipo = (int)TipoProduto.Marmita
        }, await TokenFuncionarioAsync());

        Assert.Equal(HttpStatusCode.Forbidden, resposta.StatusCode);
    }

    [Fact]
    public async Task Funcionario_pode_cancelar_pedido()
    {
        var produto = await CriarProdutoAsync();
        var cliente = await CriarClienteAsync();
        await AdicionarAoCardapioAsync(produto.Id);

        var pedido = await CriarPedidoAsync(cliente.Id, TipoEntrega.Retirada, null, (produto.Id, 1));

        var resposta = await AlterarStatusAsync(
            pedido.Id,
            StatusPedido.Cancelado,
            await TokenFuncionarioAsync());

        Assert.Equal(HttpStatusCode.NoContent, resposta.StatusCode);

        var atualizado = await ConsultarPedidoAsync(pedido.Id);

        Assert.Equal(StatusPedido.Cancelado, atualizado.Status);
    }

    [Fact]
    public async Task Registrar_usuario_com_email_duplicado_retorna_409()
    {
        var corpo = new
        {
            nome = "Usuario Duplicado",
            email = $"duplicado-{Guid.NewGuid():N}@marmitaria.test",
            senha = "Senha123!",
            perfil = (int)PerfilUsuario.Funcionario
        };

        var primeiro = await EnviarAsync(
            HttpMethod.Post,
            "/api/autenticacao/registrar",
            corpo,
            await TokenAdminAsync());

        var segundo = await EnviarAsync(
            HttpMethod.Post,
            "/api/autenticacao/registrar",
            corpo,
            await TokenAdminAsync());

        Assert.Equal(HttpStatusCode.Created, primeiro.StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, segundo.StatusCode);
    }

    [Fact]
    public async Task Token_invalido_retorna_401()
    {
        var resposta = await EnviarAsync(
            HttpMethod.Get,
            "/api/pedidos",
            token: "token-invalido");

        Assert.Equal(HttpStatusCode.Unauthorized, resposta.StatusCode);
    }
}
