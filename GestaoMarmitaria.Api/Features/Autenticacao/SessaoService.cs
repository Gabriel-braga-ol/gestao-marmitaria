using GestaoMarmitaria.Api.Infra.Data;
using GestaoMarmitaria.Api.Infra.Regras;
using GestaoMarmitaria.Api.Infra.Services;
using GestaoMarmitaria.Api.Models;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace GestaoMarmitaria.Api.Features.Autenticacao;

public interface ISessaoService
{
    Task<RegistroUsuarioResultado> RegistrarAsync(RegistrarUsuarioDto dto);

    Task<LoginResultado> LoginAsync(LoginDto dto);

    Task<RenovacaoResultado> RenovarAsync(RenovarTokenDto dto);

    Task LogoutAsync(int usuarioId);

    Task<RevogacaoSessoesResultado> RevogarSessoesAsync(int usuarioId);
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

    public async Task<RegistroUsuarioResultado> RegistrarAsync(RegistrarUsuarioDto dto)
    {
        var email = NormalizacaoEmail.Normalizar(dto.Email);

        var emailExiste = await _context.Usuarios.AnyAsync(u => u.Email == email);
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
        await _context.SaveChangesAsync();

        return new RegistroUsuarioResultado(
            SituacaoRegistroUsuario.Criado,
            usuario.Id,
            "Usuário registrado com sucesso.");
    }

    public async Task<LoginResultado> LoginAsync(LoginDto dto)
    {
        var email = NormalizacaoEmail.Normalizar(dto.Email);

        var usuario = await _context.Usuarios
            .AsNoTracking()
            .FirstOrDefaultAsync(u => u.Email == email);

        if (usuario is null)
        {
            // Mesmo custo de verificação de hash do caminho com usuário existente.
            _passwordHasher.VerifyHashedPassword(new Usuario(), _hashFicticio!, dto.Senha);

            return LoginInvalido(email);
        }

        var resultado = _passwordHasher.VerifyHashedPassword(
            usuario,
            usuario.SenhaHash,
            dto.Senha);

        if (resultado == PasswordVerificationResult.Failed)
        {
            return LoginInvalido(email);
        }

        var token = _tokenService.GerarToken(
            usuario.Id.ToString(),
            usuario.Email,
            usuario.Perfil.ToString());

        var refreshToken = _refreshTokenService.Gerar(usuario.Id, Guid.NewGuid());
        _context.RefreshTokens.Add(refreshToken.Registro);
        await _context.SaveChangesAsync();

        return new LoginResultado(
            SituacaoLogin.Autenticado,
            MontarResposta(token, refreshToken, usuario));
    }

    public async Task<RenovacaoResultado> RenovarAsync(RenovarTokenDto dto)
    {
        return await ExecucaoTransacional.ExecutarSerializavelAsync(
            _context,
            async transacao =>
            {
                var agora = _timeProvider.GetUtcNow().UtcDateTime;
                var hash = _refreshTokenService.CalcularHash(dto.RefreshToken);

                var registro = await _context.RefreshTokens
                    .Include(r => r.Usuario)
                    .FirstOrDefaultAsync(r => r.Hash == hash);

                if (registro is null || registro.ExpiraEm <= agora)
                {
                    return RefreshTokenInvalido();
                }

                if (registro.RevogadoEm is not null)
                {
                    // Reuso de um token já rotacionado/revogado: derruba a família inteira.
                    await RevogarFamiliaAsync(registro.FamiliaId, agora);
                    await transacao.CommitAsync();

                    return RefreshTokenInvalido();
                }

                if (registro.Usuario is null)
                {
                    return RefreshTokenInvalido();
                }

                var novo = _refreshTokenService.Gerar(registro.UsuarioId, registro.FamiliaId);
                registro.RevogadoEm = agora;
                _context.RefreshTokens.Add(novo.Registro);

                await _context.SaveChangesAsync();

                registro.SubstituidoPorId = novo.Registro.Id;
                await _context.SaveChangesAsync();
                await transacao.CommitAsync();

                var token = _tokenService.GerarToken(
                    registro.Usuario.Id.ToString(),
                    registro.Usuario.Email,
                    registro.Usuario.Perfil.ToString());

                return new RenovacaoResultado(
                    SituacaoRenovacao.Renovado,
                    MontarResposta(token, novo, registro.Usuario));
            },
            () => new RenovacaoResultado(
                SituacaoRenovacao.ConflitoConcorrencia,
                Mensagem: "Ocorreu um conflito ao renovar a sessão. Tente novamente."));
    }

    public async Task LogoutAsync(int usuarioId)
    {
        var agora = _timeProvider.GetUtcNow().UtcDateTime;
        var ativos = await _context.RefreshTokens
            .Where(r => r.UsuarioId == usuarioId && r.RevogadoEm == null)
            .ToListAsync();

        foreach (var ativo in ativos)
        {
            ativo.RevogadoEm = agora;
        }

        await _context.SaveChangesAsync();
    }

    public async Task<RevogacaoSessoesResultado> RevogarSessoesAsync(int usuarioId)
    {
        var usuarioExiste = await _context.Usuarios
            .AsNoTracking()
            .AnyAsync(u => u.Id == usuarioId);

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
                var ativos = await _context.RefreshTokens
                    .Where(r => r.UsuarioId == usuarioId && r.RevogadoEm == null)
                    .ToListAsync();

                foreach (var ativo in ativos)
                {
                    ativo.RevogadoEm = agora;
                }

                await _context.SaveChangesAsync();
                await transacao.CommitAsync();

                return new RevogacaoSessoesResultado(SituacaoRevogacaoSessoes.Revogado);
            },
            () => new RevogacaoSessoesResultado(
                SituacaoRevogacaoSessoes.ConflitoConcorrencia,
                "Ocorreu um conflito ao revogar as sessões. Tente novamente."));
    }

    private LoginResultado LoginInvalido(string email)
    {
        _logger.LogWarning(
            "Tentativa de login inválida para o e-mail {Email}.",
            email);

        return new LoginResultado(
            SituacaoLogin.Invalido,
            Mensagem: "E-mail ou senha inválidos.");
    }

    private async Task RevogarFamiliaAsync(Guid familiaId, DateTime agora)
    {
        var ativos = await _context.RefreshTokens
            .Where(r => r.FamiliaId == familiaId && r.RevogadoEm == null)
            .ToListAsync();

        foreach (var ativo in ativos)
        {
            ativo.RevogadoEm = agora;
        }

        await _context.SaveChangesAsync();
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
