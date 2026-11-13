using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using GestaoMarmitaria.Api.Features.Autenticacao;
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
    public async Task Login_normaliza_caixa_e_espacos_do_email()
    {
        var resposta = await EnviarAsync(HttpMethod.Post, "/api/autenticacao/login", new
        {
            email = $"  {ApiIntegrationFixture.EmailAdmin.ToUpperInvariant()}  ",
            senha = ApiIntegrationFixture.SenhaAdmin
        });

        Assert.Equal(HttpStatusCode.OK, resposta.StatusCode);

        var token = await LerAsync<TokenRespostaDto>(resposta);

        Assert.Equal(ApiIntegrationFixture.EmailAdmin, token.Email);
    }

    [Fact]
    public async Task Registro_e_login_usam_o_email_normalizado()
    {
        var email = $"Usuario.Misto-{Guid.NewGuid():N}@Marmitaria.Test";
        var senha = "SenhaSegura123!";

        var registro = await EnviarAsync(
            HttpMethod.Post,
            "/api/autenticacao/registrar",
            new
            {
                nome = "Usuario Misto",
                email = $"  {email}  ",
                senha,
                perfil = (int)PerfilUsuario.Funcionario
            },
            await TokenAdminAsync());

        Assert.Equal(HttpStatusCode.Created, registro.StatusCode);

        var login = await EnviarAsync(HttpMethod.Post, "/api/autenticacao/login", new
        {
            email = email.ToLowerInvariant(),
            senha
        });

        Assert.Equal(HttpStatusCode.OK, login.StatusCode);

        var token = await LerAsync<TokenRespostaDto>(login);

        Assert.Equal(email.ToLowerInvariant(), token.Email);
    }

    [Fact]
    public async Task Registro_com_email_equivalente_em_outra_caixa_retorna_409()
    {
        var email = $"Caixa-{Guid.NewGuid():N}@Marmitaria.Test";
        var corpo = new
        {
            nome = "Usuario Caixa",
            email,
            senha = "SenhaSegura123!",
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
            new
            {
                corpo.nome,
                email = email.ToUpperInvariant(),
                corpo.senha,
                corpo.perfil
            },
            await TokenAdminAsync());

        Assert.Equal(HttpStatusCode.Created, primeiro.StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, segundo.StatusCode);
    }

    [Fact]
    public async Task Funcionario_nao_pode_registrar_usuario()
    {
        var resposta = await EnviarAsync(HttpMethod.Post, "/api/autenticacao/registrar", new
        {
            nome = "Usuario Bloqueado",
            email = $"bloqueado-{Guid.NewGuid():N}@marmitaria.test",
            senha = "SenhaSegura123!",
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
            senha = "SenhaSegura123!",
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
    public async Task Registrar_usuario_com_senha_curta_retorna_400()
    {
        var resposta = await EnviarAsync(
            HttpMethod.Post,
            "/api/autenticacao/registrar",
            new
            {
                nome = "Senha Curta",
                email = $"senha-curta-{Guid.NewGuid():N}@marmitaria.test",
                senha = "Curta123!",
                perfil = (int)PerfilUsuario.Funcionario
            },
            await TokenAdminAsync());

        Assert.Equal(HttpStatusCode.BadRequest, resposta.StatusCode);

        using var json = JsonDocument.Parse(await resposta.Content.ReadAsStringAsync());

        Assert.True(json.RootElement.TryGetProperty("errors", out var errors));
        Assert.True(errors.TryGetProperty("Senha", out _));
    }

    [Fact]
    public async Task Registrar_usuario_com_senha_longa_retorna_400()
    {
        var resposta = await EnviarAsync(
            HttpMethod.Post,
            "/api/autenticacao/registrar",
            new
            {
                nome = "Senha Longa",
                email = $"senha-longa-{Guid.NewGuid():N}@marmitaria.test",
                senha = new string('a', 129),
                perfil = (int)PerfilUsuario.Funcionario
            },
            await TokenAdminAsync());

        Assert.Equal(HttpStatusCode.BadRequest, resposta.StatusCode);
    }

    [Fact]
    public async Task Registrar_usuario_com_senha_no_limite_minimo_retorna_201()
    {
        var resposta = await EnviarAsync(
            HttpMethod.Post,
            "/api/autenticacao/registrar",
            new
            {
                nome = "Senha Limite",
                email = $"senha-limite-{Guid.NewGuid():N}@marmitaria.test",
                senha = new string('a', 10),
                perfil = (int)PerfilUsuario.Funcionario
            },
            await TokenAdminAsync());

        Assert.Equal(HttpStatusCode.Created, resposta.StatusCode);
    }

    [Fact]
    public async Task Login_acima_do_limite_de_tentativas_retorna_429()
    {
        using var factory = Fixture.CriarFactoryComLimiteDeLogin(limiteTentativas: 3);
        using var cliente = factory.CreateClient();

        for (var i = 0; i < 3; i++)
        {
            var tentativa = await cliente.PostAsJsonAsync("/api/autenticacao/login", new
            {
                email = ApiIntegrationFixture.EmailAdmin,
                senha = "senha-incorreta"
            });

            Assert.Equal(HttpStatusCode.BadRequest, tentativa.StatusCode);
        }

        var bloqueada = await cliente.PostAsJsonAsync("/api/autenticacao/login", new
        {
            email = ApiIntegrationFixture.EmailAdmin,
            senha = ApiIntegrationFixture.SenhaAdmin
        });

        Assert.Equal(HttpStatusCode.TooManyRequests, bloqueada.StatusCode);
        Assert.Contains("Muitas tentativas", await bloqueada.Content.ReadAsStringAsync());
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
