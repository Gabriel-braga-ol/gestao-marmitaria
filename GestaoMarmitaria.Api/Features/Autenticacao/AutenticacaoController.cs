using GestaoMarmitaria.Api.Infra.Seguranca;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace GestaoMarmitaria.Api.Features.Autenticacao;

[Authorize]
[ApiController]
[Route("api/[controller]")]
public class AutenticacaoController : ControllerBase
{
    private readonly ISessaoService _sessaoService;

    public AutenticacaoController(ISessaoService sessaoService)
    {
        _sessaoService = sessaoService;
    }

    [Authorize(Policy = Politicas.SomenteAdmin)]
    [HttpPost("registrar")]
    public async Task<IActionResult> RegistrarAsync(
        [FromBody] RegistrarUsuarioDto dto,
        CancellationToken cancellationToken = default)
    {
        var resultado = await _sessaoService.RegistrarAsync(dto, cancellationToken);

        if (resultado.Situacao == SituacaoRegistroUsuario.EmailJaCadastrado)
        {
            return Conflict(new { mensagem = resultado.Mensagem });
        }

        return StatusCode(201, new
        {
            mensagem = resultado.Mensagem,
            usuarioId = resultado.UsuarioId
        });
    }

    [AllowAnonymous]
    [EnableRateLimiting("login")]
    [HttpPost("login")]
    public async Task<IActionResult> LoginAsync(
        [FromBody] LoginDto dto,
        CancellationToken cancellationToken = default)
    {
        var resultado = await _sessaoService.LoginAsync(dto, cancellationToken);

        if (resultado.Situacao == SituacaoLogin.Invalido)
        {
            return BadRequest(new { mensagem = resultado.Mensagem });
        }

        return Ok(resultado.Token);
    }

    [AllowAnonymous]
    [HttpPost("renovar")]
    public async Task<IActionResult> RenovarAsync(
        [FromBody] RenovarTokenDto dto,
        CancellationToken cancellationToken = default)
    {
        var resultado = await _sessaoService.RenovarAsync(dto, cancellationToken);

        return resultado.Situacao switch
        {
            SituacaoRenovacao.Invalido =>
                Unauthorized(new { mensagem = resultado.Mensagem }),
            SituacaoRenovacao.ConflitoConcorrencia =>
                Conflict(new { mensagem = resultado.Mensagem }),
            _ => Ok(resultado.Token)
        };
    }

    [Authorize]
    [HttpPost("logout")]
    public async Task<IActionResult> LogoutAsync(
        CancellationToken cancellationToken = default)
    {
        var usuarioId = User.ObterUsuarioId();
        if (usuarioId is null) return Unauthorized();

        await _sessaoService.LogoutAsync(usuarioId.Value, cancellationToken);

        return NoContent();
    }

    [Authorize(Policy = Politicas.SomenteAdmin)]
    [HttpPost("usuarios/{usuarioId:int}/revogar")]
    public async Task<IActionResult> RevogarSessoesAsync(
        int usuarioId,
        CancellationToken cancellationToken = default)
    {
        var resultado = await _sessaoService.RevogarSessoesAsync(
            usuarioId,
            cancellationToken);

        return resultado.Situacao switch
        {
            SituacaoRevogacaoSessoes.UsuarioNaoEncontrado => NotFound(),
            SituacaoRevogacaoSessoes.ConflitoConcorrencia =>
                Conflict(new { mensagem = resultado.Mensagem }),
            _ => NoContent()
        };
    }
}
