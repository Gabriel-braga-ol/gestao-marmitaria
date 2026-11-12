using GestaoMarmitaria.Api.Infra.Data;
using GestaoMarmitaria.Api.Infra.Seguranca;
using GestaoMarmitaria.Api.Features.Autenticacao;
using GestaoMarmitaria.Api.Models;
using GestaoMarmitaria.Api.Infra.Regras;
using GestaoMarmitaria.Api.Infra.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using System.Data;
using System.Security.Claims;

namespace GestaoMarmitaria.Api.Features.Autenticacao;

[Authorize]
[ApiController]
[Route("api/[controller]")]
public class AutenticacaoController : ControllerBase
{
    // Hash de uma senha fictícia: usado para gastar o mesmo tempo quando o e-mail não existe.
    // Gerado uma única vez, na primeira construção do controller.
    private static string? _hashFicticio;

    private readonly AppDbContext _context;
    private readonly ITokenService _tokenService;
    private readonly IRefreshTokenService _refreshTokenService;
    private readonly IPasswordHasher<Usuario> _passwordHasher;
    private readonly TimeProvider _timeProvider;
    private readonly ILogger<AutenticacaoController> _logger;

    public AutenticacaoController(
        AppDbContext context,
        ITokenService tokenService,
        IRefreshTokenService refreshTokenService,
        IPasswordHasher<Usuario> passwordHasher,
        TimeProvider timeProvider,
        ILogger<AutenticacaoController> logger)
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

    [Authorize(Policy = Politicas.SomenteAdmin)]
    [HttpPost("registrar")]
    public async Task<IActionResult> RegistrarAsync([FromBody] RegistrarUsuarioDto dto)
    {
        var email = NormalizacaoEmail.Normalizar(dto.Email);

        var emailExiste = await _context.Usuarios.AnyAsync(u => u.Email == email);
        if (emailExiste)
        {
            return Conflict(new { mensagem = "O e-mail informado já está registado." });
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

        return StatusCode(201, new { mensagem = "Utilizador registado com sucesso.", usuarioId = usuario.Id });
    }

    [AllowAnonymous]
    [EnableRateLimiting("login")]
    [HttpPost("login")]
    public async Task<IActionResult> LoginAsync([FromBody] LoginDto dto)
    {
        var email = NormalizacaoEmail.Normalizar(dto.Email);

        var usuario = await _context.Usuarios
            .AsNoTracking()
            .FirstOrDefaultAsync(u => u.Email == email);

        if (usuario is null)
        {
            // Mesmo custo de verificação de hash do caminho com usuário existente.
            _passwordHasher.VerifyHashedPassword(new Usuario(), _hashFicticio!, dto.Senha);

            _logger.LogWarning(
                "Tentativa de login inválida para o e-mail {Email}.",
                email);

            return BadRequest(new { mensagem = "E-mail ou senha inválidos." });
        }

        var resultado = _passwordHasher.VerifyHashedPassword(usuario, usuario.SenhaHash, dto.Senha);
        if (resultado == PasswordVerificationResult.Failed)
        {
            _logger.LogWarning(
                "Tentativa de login inválida para o e-mail {Email}.",
                email);

            return BadRequest(new { mensagem = "E-mail ou senha inválidos." });
        }

        var token = _tokenService.GerarToken(
            usuario.Id.ToString(),
            usuario.Email,
            usuario.Perfil.ToString());

        var refreshToken = _refreshTokenService.Gerar(usuario.Id, Guid.NewGuid());
        _context.RefreshTokens.Add(refreshToken.Registro);
        await _context.SaveChangesAsync();

        return Ok(MontarResposta(token, refreshToken, usuario));
    }

    [AllowAnonymous]
    [HttpPost("renovar")]
    public async Task<IActionResult> RenovarAsync([FromBody] RenovarTokenDto dto)
    {
        await using var transacao = await _context.Database
            .BeginTransactionAsync(IsolationLevel.Serializable);

        try
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

            return Ok(MontarResposta(token, novo, registro.Usuario));
        }
        catch (Exception ex) when (FalhasBanco.EhFalhaDeSerializacao(ex))
        {
            return Conflict(new
            {
                mensagem = "Ocorreu um conflito ao renovar a sessão. Tente novamente."
            });
        }
    }

    [Authorize]
    [HttpPost("logout")]
    public async Task<IActionResult> LogoutAsync()
    {
        var usuarioId = ObterUsuarioId();
        if (usuarioId is null) return Unauthorized();

        var agora = _timeProvider.GetUtcNow().UtcDateTime;
        var ativos = await _context.RefreshTokens
            .Where(r => r.UsuarioId == usuarioId && r.RevogadoEm == null)
            .ToListAsync();

        foreach (var ativo in ativos)
        {
            ativo.RevogadoEm = agora;
        }

        await _context.SaveChangesAsync();

        return NoContent();
    }

    [Authorize(Policy = Politicas.SomenteAdmin)]
    [HttpPost("usuarios/{usuarioId:int}/revogar")]
    public async Task<IActionResult> RevogarSessoesAsync(int usuarioId)
    {
        var usuarioExiste = await _context.Usuarios
            .AsNoTracking()
            .AnyAsync(u => u.Id == usuarioId);

        if (!usuarioExiste) return NotFound();

        await using var transacao = await _context.Database
            .BeginTransactionAsync(IsolationLevel.Serializable);

        try
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

            return NoContent();
        }
        catch (Exception ex) when (FalhasBanco.EhFalhaDeSerializacao(ex))
        {
            return Conflict(new
            {
                mensagem = "Ocorreu um conflito ao revogar as sessões. Tente novamente."
            });
        }
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

    private static UnauthorizedObjectResult RefreshTokenInvalido()
    {
        return new UnauthorizedObjectResult(new
        {
            mensagem = "Refresh token inválido ou expirado."
        });
    }

    private int? ObterUsuarioId()
    {
        var valor = User.FindFirstValue(ClaimTypes.NameIdentifier);

        return int.TryParse(valor, out var usuarioId) ? usuarioId : null;
    }
}