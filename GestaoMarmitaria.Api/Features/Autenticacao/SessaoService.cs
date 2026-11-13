using GestaoMarmitaria.Api.Infra.Data;
using GestaoMarmitaria.Api.Infra.Regras;
using GestaoMarmitaria.Api.Infra.Services;
using GestaoMarmitaria.Api.Models;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using System.Linq.Expressions;

namespace GestaoMarmitaria.Api.Features.Autenticacao;

public interface ISessaoService
{
    Task<RegistroUsuarioResultado> RegistrarAsync(
        RegistrarUsuarioDto dto,
        CancellationToken cancellationToken = default);

    Task<LoginResultado> LoginAsync(
        LoginDto dto,
        CancellationToken cancellationToken = default);

    Task<RenovacaoResultado> RenovarAsync(
        RenovarTokenDto dto,
        CancellationToken cancellationToken = default);

    Task LogoutAsync(
        int usuarioId,
        CancellationToken cancellationToken = default);

    Task<RevogacaoSessoesResultado> RevogarSessoesAsync(
        int usuarioId,
        CancellationToken cancellationToken = default);
}

public enum SituacaoRegistroUsuario
{
    Criado,
    EmailJaCadastrado
}

public sealed record RegistroUsuarioResultado(
    SituacaoRegistroUsuario Situacao,
    int? UsuarioId = null,
    string? Mensagem = null);

public enum SituacaoLogin
{
    Autenticado,
    Invalido
}

public sealed record LoginResultado(
    SituacaoLogin Situacao,
    TokenRespostaDto? Token = null,
    string? Mensagem = null);

public enum SituacaoRenovacao
{
    Renovado,
    Invalido,
    ConflitoConcorrencia
}

public sealed record RenovacaoResultado(
    SituacaoRenovacao Situacao,
    TokenRespostaDto? Token = null,
    string? Mensagem = null);

public enum SituacaoRevogacaoSessoes
{
    Revogado,
    UsuarioNaoEncontrado,
    ConflitoConcorrencia
}

public sealed record RevogacaoSessoesResultado(
    SituacaoRevogacaoSessoes Situacao,
    string? Mensagem = null);

public class SessaoService : ISessaoService
{
    // Hash de uma senha fictícia: usado para gastar o mesmo tempo quando o e-mail não existe.
    // Gerado uma única vez, na primeira construção do serviço.
    private static string? _hashFicticio;

    private readonly AppDbContext _context;
    private readonly ITokenService _tokenService;
    private readonly IRefreshTokenService _refreshTokenService;
    private readonly IPasswordHasher<Usuario> _passwordHasher;
    private readonly TimeProvider _timeProvider;
    private readonly ILogger<SessaoService> _logger;

    public SessaoService(
        AppDbContext context,
        ITokenService tokenService,
        IRefreshTokenService refreshTokenService,
        IPasswordHasher<Usuario> passwordHasher,
        TimeProvider timeProvider,
        ILogger<SessaoService> logger)
    {
        _context = context;
        _tokenService = tokenService;
        _refreshTokenService = refreshTokenService;
        _passwordHasher = passwordHasher;
        _timeProvider = timeProvider;
        _logger = logger;

        LazyInitializer.EnsureInitialized(
            ref _hashFicticio,
            () => _passwordHasher.HashPassword(
                new Usuario(),
                "senha-ficticia-para-igualar-tempo"));
    }

    public async Task<RegistroUsuarioResultado> RegistrarAsync(
        RegistrarUsuarioDto dto,
        CancellationToken cancellationToken = default)
    {
        var email = NormalizacaoEmail.Normalizar(dto.Email);

        var emailExiste = await _context.Usuarios
            .AnyAsync(u => u.Email == email, cancellationToken);
        if (emailExiste)
        {
            return new RegistroUsuarioResultado(
                SituacaoRegistroUsuario.EmailJaCadastrado,
                Mensagem: "O e-mail informado já está registrado.");
        }

        var usuario = new Usuario
        {
            Nome = dto.Nome,
            Email = email,
            Perfil = dto.Perfil
        };

        usuario.SenhaHash = _passwordHasher.HashPassword(usuario, dto.Senha);

        _context.Usuarios.Add(usuario);
        await _context.SaveChangesAsync(cancellationToken);

        return new RegistroUsuarioResultado(
            SituacaoRegistroUsuario.Criado,
            usuario.Id,
            "Usuário registrado com sucesso.");
    }

    public async Task<LoginResultado> LoginAsync(
        LoginDto dto,
        CancellationToken cancellationToken = default)
    {
        var email = NormalizacaoEmail.Normalizar(dto.Email);

        var usuario = await _context.Usuarios
            .AsNoTracking()
            .FirstOrDefaultAsync(u => u.Email == email, cancellationToken);

        if (usuario is null)
        {
            // Mesmo custo de verificação de hash do caminho com usuário existente.
            _passwordHasher.VerifyHashedPassword(new Usuario(), _hashFicticio!, dto.Senha);

            return LoginInvalido(email);
        }

        var verificacaoSenha = _passwordHasher.VerifyHashedPassword(
            usuario,
            usuario.SenhaHash,
            dto.Senha);

        if (verificacaoSenha == PasswordVerificationResult.Failed)
        {
            return LoginInvalido(email);
        }

        return new LoginResultado(
            SituacaoLogin.Autenticado,
            await CriarSessaoAsync(usuario, cancellationToken));
    }

    public async Task<RenovacaoResultado> RenovarAsync(
        RenovarTokenDto dto,
        CancellationToken cancellationToken = default)
    {
        return await ExecucaoTransacional.ExecutarSerializavelAsync(
            _context,
            transacao => RenovarComTransacaoAsync(transacao, dto, cancellationToken),
            () => new RenovacaoResultado(
                SituacaoRenovacao.ConflitoConcorrencia,
                Mensagem: "Ocorreu um conflito ao renovar a sessão. Tente novamente."),
            cancellationToken);
    }

    private async Task<RenovacaoResultado> RenovarComTransacaoAsync(
        IDbContextTransaction transacao,
        RenovarTokenDto dto,
        CancellationToken cancellationToken)
    {
        var agora = _timeProvider.GetUtcNow().UtcDateTime;
        var refreshTokenAtual = await LocalizarRefreshTokenAsync(
            dto.RefreshToken,
            cancellationToken);

        if (refreshTokenAtual is null || refreshTokenAtual.ExpiraEm <= agora)
        {
            return RefreshTokenInvalido();
        }

        if (refreshTokenAtual.RevogadoEm is not null)
        {
            return await TratarReusoAsync(
                refreshTokenAtual,
                agora,
                transacao,
                cancellationToken);
        }

        if (refreshTokenAtual.Usuario is null)
        {
            return RefreshTokenInvalido();
        }

        var novoRefreshToken = await RotacionarAsync(
            refreshTokenAtual,
            agora,
            transacao,
            cancellationToken);

        var accessToken = GerarAccessToken(refreshTokenAtual.Usuario);

        return new RenovacaoResultado(
            SituacaoRenovacao.Renovado,
            MontarResposta(accessToken, novoRefreshToken, refreshTokenAtual.Usuario));
    }

    private Task<RefreshToken?> LocalizarRefreshTokenAsync(
        string refreshToken,
        CancellationToken cancellationToken)
    {
        var hash = _refreshTokenService.CalcularHash(refreshToken);

        return _context.RefreshTokens
            .Include(r => r.Usuario)
            .FirstOrDefaultAsync(r => r.Hash == hash, cancellationToken);
    }

    private async Task<RenovacaoResultado> TratarReusoAsync(
        RefreshToken refreshTokenAtual,
        DateTime agora,
        IDbContextTransaction transacao,
        CancellationToken cancellationToken)
    {
        // Reuso de um token já rotacionado/revogado: derruba a família inteira.
        await RevogarFamiliaAsync(refreshTokenAtual.FamiliaId, agora, cancellationToken);

        // O commit precisa acontecer antes da resposta "inválido": sem ele,
        // a revogação da família seria descartada junto com a transação.
        await transacao.CommitAsync(cancellationToken);

        return RefreshTokenInvalido();
    }

    private async Task<RefreshTokenGerado> RotacionarAsync(
        RefreshToken refreshTokenAtual,
        DateTime agora,
        IDbContextTransaction transacao,
        CancellationToken cancellationToken)
    {
        var novoRefreshToken = _refreshTokenService.Gerar(
            refreshTokenAtual.UsuarioId,
            refreshTokenAtual.FamiliaId);

        refreshTokenAtual.RevogadoEm = agora;
        _context.RefreshTokens.Add(novoRefreshToken.Registro);

        await _context.SaveChangesAsync(cancellationToken);

        // Segundo SaveChanges: o SubstituidoPorId depende do Id gerado pelo banco
        // no SaveChanges anterior. Não juntar os dois.
        refreshTokenAtual.SubstituidoPorId = novoRefreshToken.Registro.Id;
        await _context.SaveChangesAsync(cancellationToken);
        await transacao.CommitAsync(cancellationToken);

        return novoRefreshToken;
    }

    public async Task LogoutAsync(
        int usuarioId,
        CancellationToken cancellationToken = default)
    {
        var agora = _timeProvider.GetUtcNow().UtcDateTime;

        await RevogarTokensAtivosAsync(
            r => r.UsuarioId == usuarioId,
            agora,
            cancellationToken);
    }

    public async Task<RevogacaoSessoesResultado> RevogarSessoesAsync(
        int usuarioId,
        CancellationToken cancellationToken = default)
    {
        var usuarioExiste = await _context.Usuarios
            .AsNoTracking()
            .AnyAsync(u => u.Id == usuarioId, cancellationToken);

        if (!usuarioExiste)
        {
            return new RevogacaoSessoesResultado(
                SituacaoRevogacaoSessoes.UsuarioNaoEncontrado);
        }

        return await ExecucaoTransacional.ExecutarSerializavelAsync(
            _context,
            async transacao =>
            {
                var agora = _timeProvider.GetUtcNow().UtcDateTime;

                await RevogarTokensAtivosAsync(
                    r => r.UsuarioId == usuarioId,
                    agora,
                    cancellationToken);

                await transacao.CommitAsync(cancellationToken);

                return new RevogacaoSessoesResultado(SituacaoRevogacaoSessoes.Revogado);
            },
            () => new RevogacaoSessoesResultado(
                SituacaoRevogacaoSessoes.ConflitoConcorrencia,
                "Ocorreu um conflito ao revogar as sessões. Tente novamente."),
            cancellationToken);
    }

    private async Task<TokenRespostaDto> CriarSessaoAsync(
        Usuario usuario,
        CancellationToken cancellationToken)
    {
        var accessToken = GerarAccessToken(usuario);
        var refreshToken = _refreshTokenService.Gerar(usuario.Id, Guid.NewGuid());

        _context.RefreshTokens.Add(refreshToken.Registro);
        await _context.SaveChangesAsync(cancellationToken);

        return MontarResposta(accessToken, refreshToken, usuario);
    }

    private TokenGerado GerarAccessToken(Usuario usuario)
    {
        return _tokenService.GerarToken(
            usuario.Id.ToString(),
            usuario.Email,
            usuario.Perfil.ToString());
    }

    private LoginResultado LoginInvalido(string email)
    {
        RegistrarTentativaInvalida(email);

        return new LoginResultado(
            SituacaoLogin.Invalido,
            Mensagem: "E-mail ou senha inválidos.");
    }

    private void RegistrarTentativaInvalida(string email)
    {
        _logger.LogWarning(
            "Tentativa de login inválida para o e-mail {Email}.",
            email);
    }

    private Task RevogarFamiliaAsync(
        Guid familiaId,
        DateTime agora,
        CancellationToken cancellationToken)
    {
        return RevogarTokensAtivosAsync(
            r => r.FamiliaId == familiaId,
            agora,
            cancellationToken);
    }

    private async Task RevogarTokensAtivosAsync(
        Expression<Func<RefreshToken, bool>> filtro,
        DateTime agora,
        CancellationToken cancellationToken)
    {
        var tokensAtivos = await _context.RefreshTokens
            .Where(filtro)
            .Where(r => r.RevogadoEm == null)
            .ToListAsync(cancellationToken);

        foreach (var tokenAtivo in tokensAtivos)
        {
            tokenAtivo.RevogadoEm = agora;
        }

        await _context.SaveChangesAsync(cancellationToken);
    }

    private static RenovacaoResultado RefreshTokenInvalido()
    {
        return new RenovacaoResultado(
            SituacaoRenovacao.Invalido,
            Mensagem: "Refresh token inválido ou expirado.");
    }

    private static TokenRespostaDto MontarResposta(
        TokenGerado token,
        RefreshTokenGerado refreshToken,
        Usuario usuario)
    {
        return new TokenRespostaDto
        {
            Token = token.Valor,
            ExpiraEm = token.ExpiraEm,
            RefreshToken = refreshToken.Valor,
            RefreshExpiraEm = refreshToken.Registro.ExpiraEm,
            Email = usuario.Email,
            Perfil = usuario.Perfil.ToString()
        };
    }
}
