using GestaoMarmitaria.Api.Features.Pagamentos;
using GestaoMarmitaria.Api.Infra.Seguranca;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace GestaoMarmitaria.Api.Features.Pedidos;

[Authorize]
[ApiController]
[Route("api/[controller]")]
public class PedidosController : ControllerBase
{
    private readonly IPedidoService _pedidoService;
    private readonly IPagamentoService _pagamentoService;

    public PedidosController(
        IPedidoService pedidoService,
        IPagamentoService pagamentoService)
    {
        _pedidoService = pedidoService;
        _pagamentoService = pagamentoService;
    }

    [HttpGet]
    public async Task<IActionResult> GetPedidos([FromQuery] ConsultarPedidosDto filtros)
    {
        var resultado = await _pedidoService.ConsultarAsync(filtros);

        if (resultado.Erro is not null)
        {
            return BadRequest(new { mensagem = resultado.Erro });
        }

        return Ok(resultado.Resposta);
    }

    [HttpGet("{id:int}")]
    public async Task<IActionResult> GetPedidoPorId(int id)
    {
        var pedido = await _pedidoService.ObterPorIdAsync(id);

        return pedido is null ? NotFound() : Ok(pedido);
    }

    [HttpPost]
    public async Task<IActionResult> CriarPedidoAsync(CriarPedidoDto dto)
    {
        var resultado = await _pedidoService.CriarAsync(dto);

        return resultado.Situacao switch
        {
            SituacaoCriacaoPedido.Validacao =>
                BadRequest(new { mensagem = resultado.Mensagem }),
            SituacaoCriacaoPedido.ConflitoConcorrencia =>
                Conflict(new { mensagem = resultado.Mensagem }),
            _ => CreatedAtAction(
                nameof(GetPedidoPorId),
                new { id = resultado.Pedido!.Id },
                resultado.Pedido)
        };
    }

    [HttpPatch("{id:int}/status")]
    public async Task<IActionResult> AtualizarStatusPedido(int id, AtualizarStatusPedidoDto dto)
    {
        var resultado = await _pedidoService.AlterarStatusAsync(
            id,
            dto,
            User.ObterUsuarioId());

        return resultado.Situacao switch
        {
            SituacaoAlteracaoStatus.PedidoNaoEncontrado => NotFound(),
            SituacaoAlteracaoStatus.TransicaoInvalida =>
                Conflict(new { mensagem = resultado.Mensagem }),
            SituacaoAlteracaoStatus.ConflitoConcorrencia =>
                Conflict(new { mensagem = resultado.Mensagem }),
            _ => NoContent()
        };
    }

    [HttpGet("{id:int}/historico")]
    [ActionName(nameof(GetHistoricoPorPedidoAsync))]
    public async Task<IActionResult> GetHistoricoPorPedidoAsync(int id)
    {
        var historico = await _pedidoService.ObterHistoricoAsync(id);

        return historico is null ? NotFound() : Ok(historico);
    }

    [HttpGet("{id:int}/pagamento")]
    [ActionName(nameof(GetPagamentoPorPedidoAsync))]
    public async Task<IActionResult> GetPagamentoPorPedidoAsync(int id)
    {
        var pagamento = await _pagamentoService.ObterPagamentoAsync(id);

        return pagamento is null ? NotFound() : Ok(pagamento);
    }

    [HttpGet("{id:int}/reembolso")]
    [ActionName(nameof(GetReembolsoPorPedidoAsync))]
    public async Task<IActionResult> GetReembolsoPorPedidoAsync(int id)
    {
        var reembolso = await _pagamentoService.ObterReembolsoAsync(id);

        return reembolso is null ? NotFound() : Ok(reembolso);
    }

    [HttpPost("{id:int}/pagamento")]
    public async Task<IActionResult> RegistrarPagamentoAsync(int id, RegistrarPagamentoDto dto)
    {
        var resultado = await _pagamentoService.RegistrarPagamentoAsync(
            id,
            dto,
            User.ObterUsuarioId());

        return resultado.Situacao switch
        {
            SituacaoRegistroPagamento.PedidoNaoEncontrado => NotFound(),
            SituacaoRegistroPagamento.Conflito =>
                Conflict(new { mensagem = resultado.Mensagem }),
            _ => CreatedAtAction(
                nameof(GetPagamentoPorPedidoAsync),
                new { id = resultado.Pagamento!.PedidoId },
                resultado.Pagamento)
        };
    }

    [Authorize(Policy = Politicas.SomenteAdmin)]
    [HttpPost("{id:int}/reembolso")]
    public async Task<IActionResult> RegistrarReembolsoAsync(int id)
    {
        var resultado = await _pagamentoService.RegistrarReembolsoAsync(
            id,
            User.ObterUsuarioId());

        return resultado.Situacao switch
        {
            SituacaoRegistroReembolso.PedidoNaoEncontrado => NotFound(),
            SituacaoRegistroReembolso.Conflito =>
                Conflict(new { mensagem = resultado.Mensagem }),
            _ => CreatedAtAction(
                nameof(GetReembolsoPorPedidoAsync),
                new { id },
                resultado.Reembolso)
        };
    }
}
