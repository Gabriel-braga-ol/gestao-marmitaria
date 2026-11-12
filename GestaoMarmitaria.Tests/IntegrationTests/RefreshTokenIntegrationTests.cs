using System.Net;
using GestaoMarmitaria.Api.Infra.Configurations;
using GestaoMarmitaria.Api.Features.Autenticacao;
using GestaoMarmitaria.Api.Enums;
using GestaoMarmitaria.Api.Infra.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace GestaoMarmitaria.Tests.IntegrationTests;

public class RefreshTokenIntegrationTests : IntegracaoTesteBase
{
    private static readonly IRefreshTokenService HashService = new RefreshTokenService(
        Options.Create(new JwtSettings()));

    public RefreshTokenIntegrationTests(ApiIntegrationFixture fixture) : base(fixture)
    {
    }

    [Fact]
    public async Task Login_retorna_access_refresh_e_expiracoes()
    {
        var resposta = await LoginAsync(
            ApiIntegrationFixture.EmailAdmin,
            ApiIntegrationFixture.SenhaAdmin);

        Assert.Equal(HttpStatusCode.OK, resposta.StatusCode);

        var token = await LerAsync<TokenRespostaDto>(resposta);
        var agora = DateTime.UtcNow;

        Assert.False(string.IsNullOrWhiteSpace(token.Token));
        Assert.False(string.IsNullOrWhiteSpace(token.RefreshToken));
        Assert.NotEqual(token.Token, token.RefreshToken);
        Assert.Equal(DateTimeKind.Utc, token.ExpiraEm.Kind);
        Assert.Equal(DateTimeKind.Utc, token.RefreshExpiraEm.Kind);
        Assert.InRange(token.ExpiraEm, agora.AddMinutes(10), agora.AddMinutes(20));
        Assert.InRange(token.RefreshExpiraEm, agora.AddDays(6), agora.AddDays(8));
        Assert.Equal(ApiIntegrationFixture.EmailAdmin, token.Email);
        Assert.Equal("Admin", token.Perfil);

        await using var contexto = Fixture.CriarDbContext();
        var registro = await contexto.RefreshTokens
            .AsNoTracking()
            .SingleAsync(r => r.Hash == HashService.CalcularHash(token.RefreshToken));

        Assert.Equal(await ObterUsuarioIdAsync(ApiIntegrationFixture.EmailAdmin), registro.UsuarioId);
        Assert.NotEqual(token.RefreshToken, registro.Hash);
        Assert.Equal(64, registro.Hash.Length);
        Assert.Null(registro.RevogadoEm);
        Assert.NotEqual(Guid.Empty, registro.FamiliaId);
    }

    [Fact]
    public async Task Renovar_rotaciona_o_refresh_e_revoga_o_anterior()
    {
        var login = await LerAsync<TokenRespostaDto>(await LoginAsync(
            ApiIntegrationFixture.EmailAdmin,
            ApiIntegrationFixture.SenhaAdmin));

        var resposta = await RenovarAsync(login.RefreshToken);

        Assert.Equal(HttpStatusCode.OK, resposta.StatusCode);

        var renovado = await LerAsync<TokenRespostaDto>(resposta);

        Assert.NotEqual(login.Token, renovado.Token);
        Assert.NotEqual(login.RefreshToken, renovado.RefreshToken);
        Assert.Equal(ApiIntegrationFixture.EmailAdmin, renovado.Email);
        Assert.Equal("Admin", renovado.Perfil);
        Assert.Equal(DateTimeKind.Utc, renovado.ExpiraEm.Kind);
        Assert.Equal(DateTimeKind.Utc, renovado.RefreshExpiraEm.Kind);

        await using var contexto = Fixture.CriarDbContext();
        var antigo = await contexto.RefreshTokens
            .AsNoTracking()
            .SingleAsync(r => r.Hash == HashService.CalcularHash(login.RefreshToken));
        var novo = await contexto.RefreshTokens
            .AsNoTracking()
            .SingleAsync(r => r.Hash == HashService.CalcularHash(renovado.RefreshToken));

        Assert.NotNull(antigo.RevogadoEm);
        Assert.Null(novo.RevogadoEm);
        Assert.Equal(antigo.FamiliaId, novo.FamiliaId);
        Assert.Equal(novo.Id, antigo.SubstituidoPorId);
        Assert.Equal(antigo.UsuarioId, novo.UsuarioId);
    }

    [Fact]
    public async Task Renovar_com_refresh_reutilizado_revoga_a_familia()
    {
        var login = await LerAsync<TokenRespostaDto>(await LoginAsync(
            ApiIntegrationFixture.EmailAdmin,
            ApiIntegrationFixture.SenhaAdmin));

        var primeiro = await RenovarAsync(login.RefreshToken);

        Assert.Equal(HttpStatusCode.OK, primeiro.StatusCode);

        var renovado = await LerAsync<TokenRespostaDto>(primeiro);

        var reutilizado = await RenovarAsync(login.RefreshToken);

        Assert.Equal(HttpStatusCode.Unauthorized, reutilizado.StatusCode);

        var mensagem = await LerMensagemAsync(reutilizado);

        Assert.Contains("inválido", mensagem);

        var daFamiliaRevogada = await RenovarAsync(renovado.RefreshToken);

        Assert.Equal(HttpStatusCode.Unauthorized, daFamiliaRevogada.StatusCode);

        var novoLogin = await LoginAsync(
            ApiIntegrationFixture.EmailAdmin,
            ApiIntegrationFixture.SenhaAdmin);

        Assert.Equal(HttpStatusCode.OK, novoLogin.StatusCode);
    }

    [Fact]
    public async Task Renovar_com_refresh_inexistente_ou_expirado_retorna_401()
    {
        var inexistente = await RenovarAsync($"token-inexistente-{Guid.NewGuid():N}");

        Assert.Equal(HttpStatusCode.Unauthorized, inexistente.StatusCode);

        var login = await LerAsync<TokenRespostaDto>(await LoginAsync(
            ApiIntegrationFixture.EmailAdmin,
            ApiIntegrationFixture.SenhaAdmin));

        await using (var contexto = Fixture.CriarDbContext())
        {
            var registro = await contexto.RefreshTokens
                .SingleAsync(r => r.Hash == HashService.CalcularHash(login.RefreshToken));

            registro.ExpiraEm = DateTime.UtcNow.AddMinutes(-1);
            await contexto.SaveChangesAsync();
        }

        var expirado = await RenovarAsync(login.RefreshToken);

        Assert.Equal(HttpStatusCode.Unauthorized, expirado.StatusCode);
    }

    [Fact]
    public async Task Renovar_com_corpo_invalido_retorna_400()
    {
        var semToken = await EnviarAsync(
            HttpMethod.Post,
            "/api/autenticacao/renovar",
            new { });

        Assert.Equal(HttpStatusCode.BadRequest, semToken.StatusCode);

        var curto = await EnviarAsync(
            HttpMethod.Post,
            "/api/autenticacao/renovar",
            new { refreshToken = "curto" });

        Assert.Equal(HttpStatusCode.BadRequest, curto.StatusCode);
    }

    [Fact]
    public async Task Logout_revoga_todas_as_sessoes_mas_o_access_token_continua_valido()
    {
        var primeiro = await LerAsync<TokenRespostaDto>(await LoginAsync(
            ApiIntegrationFixture.EmailAdmin,
            ApiIntegrationFixture.SenhaAdmin));
        var segundo = await LerAsync<TokenRespostaDto>(await LoginAsync(
            ApiIntegrationFixture.EmailAdmin,
            ApiIntegrationFixture.SenhaAdmin));

        var logout = await EnviarAsync(
            HttpMethod.Post,
            "/api/autenticacao/logout",
            token: segundo.Token);

        Assert.Equal(HttpStatusCode.NoContent, logout.StatusCode);

        Assert.Equal(
            HttpStatusCode.Unauthorized,
            (await RenovarAsync(primeiro.RefreshToken)).StatusCode);
        Assert.Equal(
            HttpStatusCode.Unauthorized,
            (await RenovarAsync(segundo.RefreshToken)).StatusCode);

        var acesso = await EnviarAsync(
            HttpMethod.Get,
            "/api/pedidos",
            token: segundo.Token);

        Assert.Equal(HttpStatusCode.OK, acesso.StatusCode);
    }

    [Fact]
    public async Task Logout_sem_token_retorna_401()
    {
        var resposta = await EnviarAsync(HttpMethod.Post, "/api/autenticacao/logout");

        Assert.Equal(HttpStatusCode.Unauthorized, resposta.StatusCode);
    }

    [Fact]
    public async Task Admin_revoga_as_sessoes_do_usuario()
    {
        var email = $"revogado-{Guid.NewGuid():N}@marmitaria.test";
        const string senha = "SenhaSegura123!";

        var registro = await EnviarAsync(
            HttpMethod.Post,
            "/api/autenticacao/registrar",
            new
            {
                nome = "Usuario Revogado",
                email,
                senha,
                perfil = (int)PerfilUsuario.Funcionario
            },
            await TokenAdminAsync());

        Assert.Equal(HttpStatusCode.Created, registro.StatusCode);

        var login = await LerAsync<TokenRespostaDto>(await LoginAsync(email, senha));
        var usuarioId = await ObterUsuarioIdAsync(email);

        var revogacao = await EnviarAsync(
            HttpMethod.Post,
            $"/api/autenticacao/usuarios/{usuarioId}/revogar",
            token: await TokenAdminAsync());

        Assert.Equal(HttpStatusCode.NoContent, revogacao.StatusCode);
        Assert.Equal(
            HttpStatusCode.Unauthorized,
            (await RenovarAsync(login.RefreshToken)).StatusCode);

        var novoLogin = await LoginAsync(email, senha);

        Assert.Equal(HttpStatusCode.OK, novoLogin.StatusCode);
    }

    [Fact]
    public async Task Revogacao_de_sessoes_exige_admin_e_usuario_existente()
    {
        var funcionario = await TokenFuncionarioAsync();
        var funcionarioId = await ObterUsuarioIdAsync(ApiIntegrationFixture.EmailFuncionario);

        var semPermissao = await EnviarAsync(
            HttpMethod.Post,
            $"/api/autenticacao/usuarios/{funcionarioId}/revogar",
            token: funcionario);

        Assert.Equal(HttpStatusCode.Forbidden, semPermissao.StatusCode);

        var inexistente = await EnviarAsync(
            HttpMethod.Post,
            "/api/autenticacao/usuarios/999999/revogar",
            token: await TokenAdminAsync());

        Assert.Equal(HttpStatusCode.NotFound, inexistente.StatusCode);

        var semToken = await EnviarAsync(
            HttpMethod.Post,
            $"/api/autenticacao/usuarios/{funcionarioId}/revogar");

        Assert.Equal(HttpStatusCode.Unauthorized, semToken.StatusCode);
    }

    [Fact]
    public async Task Duas_renovacoes_simultaneas_nao_deixam_duas_sessoes_ativas()
    {
        var login = await LerAsync<TokenRespostaDto>(await LoginAsync(
            ApiIntegrationFixture.EmailAdmin,
            ApiIntegrationFixture.SenhaAdmin));

        Guid familiaId;

        await using (var contexto = Fixture.CriarDbContext())
        {
            familiaId = await contexto.RefreshTokens
                .AsNoTracking()
                .Where(r => r.Hash == HashService.CalcularHash(login.RefreshToken))
                .Select(r => r.FamiliaId)
                .SingleAsync();
        }

        var respostas = await ExecutarSimultaneamenteAsync(
            () => RenovarAsync(login.RefreshToken),
            () => RenovarAsync(login.RefreshToken));

        var status = respostas.Select(r => r.StatusCode).ToArray();

        Assert.Equal(1, status.Count(s => s == HttpStatusCode.OK));

        Assert.All(
            status.Where(s => s != HttpStatusCode.OK),
            s => Assert.True(
                s == HttpStatusCode.Unauthorized || s == HttpStatusCode.Conflict,
                $"Status inesperado: {(int)s}"));

        await using var contextoFinal = Fixture.CriarDbContext();
        var ativos = await contextoFinal.RefreshTokens
            .AsNoTracking()
            .CountAsync(r => r.FamiliaId == familiaId && r.RevogadoEm == null);

        Assert.InRange(ativos, 0, 1);
    }

    private static async Task<HttpResponseMessage[]> ExecutarSimultaneamenteAsync(
        params Func<Task<HttpResponseMessage>>[] acoes)
    {
        var inicio = new TaskCompletionSource<bool>(
            TaskCreationOptions.RunContinuationsAsynchronously);

        var tarefas = acoes
            .Select(async acao =>
            {
                await inicio.Task;
                return await acao();
            })
            .ToArray();

        inicio.SetResult(true);

        return await Task.WhenAll(tarefas);
    }

    private async Task<HttpResponseMessage> LoginAsync(string email, string senha)
    {
        return await EnviarAsync(
            HttpMethod.Post,
            "/api/autenticacao/login",
            new { email, senha });
    }

    private async Task<HttpResponseMessage> RenovarAsync(string refreshToken)
    {
        return await EnviarAsync(
            HttpMethod.Post,
            "/api/autenticacao/renovar",
            new { refreshToken });
    }
}
