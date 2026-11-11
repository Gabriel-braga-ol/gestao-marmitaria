using GestaoMarmitaria.Api.Data;
using GestaoMarmitaria.Api.Dtos;
using GestaoMarmitaria.Api.Models;
using GestaoMarmitaria.Api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;

namespace GestaoMarmitaria.Api.Controllers;

[Authorize]
[ApiController]
[Route("api/[controller]")]
public class AutenticacaoController : ControllerBase
{
    private readonly AppDbContext _context;
    private readonly ITokenService _tokenService;
    private readonly PasswordHasher<Usuario> _passwordHasher;
    private readonly ILogger<AutenticacaoController> _logger;

    public AutenticacaoController(
        AppDbContext context,
        ITokenService tokenService,
        ILogger<AutenticacaoController> logger)
    {
        _context = context;
        _tokenService = tokenService;
        _logger = logger;
        _passwordHasher = new PasswordHasher<Usuario>();
    }

    [Authorize(Roles = "Admin")]
    [HttpPost("registrar")]
    public async Task<IActionResult> RegistrarAsync([FromBody] RegistrarUsuarioDto dto)
    {
        var emailExiste = await _context.Usuarios.AnyAsync(u => u.Email == dto.Email);
        if (emailExiste)
        {
            return Conflict(new { mensagem = "O e-mail informado já está registado." });
        }

        var usuario = new Usuario
        {
            Nome = dto.Nome,
            Email = dto.Email,
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
        var usuario = await _context.Usuarios.FirstOrDefaultAsync(u => u.Email == dto.Email);
        if (usuario is null)
        {
            _logger.LogWarning(
                "Tentativa de login inválida para o e-mail {Email}.",
                dto.Email);

            return BadRequest(new { mensagem = "E-mail ou senha inválidos." });
        }

        var resultado = _passwordHasher.VerifyHashedPassword(usuario, usuario.SenhaHash, dto.Senha);
        if (resultado == PasswordVerificationResult.Failed)
        {
            _logger.LogWarning(
                "Tentativa de login inválida para o e-mail {Email}.",
                usuario.Email);

            return BadRequest(new { mensagem = "E-mail ou senha inválidos." });
        }

        var token = _tokenService.GerarToken(
            usuario.Id.ToString(),
            usuario.Email,
            usuario.Perfil.ToString());

        return Ok(new TokenRespostaDto
        {
            Token = token,
            Email = usuario.Email,
            Perfil = usuario.Perfil.ToString()
        });
    }
}