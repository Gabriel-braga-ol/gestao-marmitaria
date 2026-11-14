using GestaoMarmitaria.Api.Infra.Seguranca;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace GestaoMarmitaria.Api.Features.Relatorios;

[Authorize]
[ApiController]
[Route("api/[controller]")]
public class RelatoriosController : ControllerBase
{
    private readonly IRelatorioFinanceiroService _relatorioFinanceiroService;

    public RelatoriosController(IRelatorioFinanceiroService relatorioFinanceiroService)
    {
        _relatorioFinanceiroService = relatorioFinanceiroService;
    }

    [Authorize(Policy = Politicas.SomenteAdmin)]
    [HttpGet("financeiro")]
    public async Task<IActionResult> GetRelatorioFinanceiroAsync(
        [FromQuery] ConsultarRelatorioFinanceiroDto filtros,
        CancellationToken cancellationToken = default)
    {
        var resultado = await _relatorioFinanceiroService.ConsultarAsync(
            filtros,
            cancellationToken);

        if (resultado.Erro is not null)
        {
            return BadRequest(new { mensagem = resultado.Erro });
        }

        return Ok(resultado.Relatorio);
    }
}
