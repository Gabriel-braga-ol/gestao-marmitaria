using GestaoMarmitaria.Api.Features.Compartilhado;
using GestaoMarmitaria.Api.Infra.Data;
using GestaoMarmitaria.Api.Infra.Regras;
using GestaoMarmitaria.Api.Infra.Seguranca;
using GestaoMarmitaria.Api.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace GestaoMarmitaria.Api.Features.Clientes
{
    [Authorize]
    [ApiController]
    [Route("api/[controller]")]
    public class ClientesController : ControllerBase
    {
        private readonly AppDbContext _context;

        public ClientesController(AppDbContext context)
        {
            _context = context;
        }

        [HttpGet]
        public async Task<IActionResult> GetClientes(
            [FromQuery] ConsultarClientesDto filtros,
            CancellationToken cancellationToken = default)
        {
            var consulta = _context.Clientes.AsNoTracking();

            var totalRegistros = await consulta.CountAsync(cancellationToken);
            var quantidadeIgnorada = (long)(filtros.Pagina - 1) * filtros.TamanhoPagina;

            var clientes = quantidadeIgnorada >= totalRegistros
                ? new List<Cliente>()
                : await consulta
                    .OrderBy(c => c.Id)
                    .Skip((int)quantidadeIgnorada)
                    .Take(filtros.TamanhoPagina)
                    .ToListAsync(cancellationToken);

            var resposta = new ItensPaginadosRespostaDto<ClienteRespostaDto>
            {
                Itens = clientes.Select(MapearClienteParaResposta).ToList(),
                Pagina = filtros.Pagina,
                TamanhoPagina = filtros.TamanhoPagina,
                TotalRegistros = totalRegistros
            };

            return Ok(resposta);
        }

        [HttpGet("{id:int}")]
        public async Task<IActionResult> GetClientePorId(
            int id,
            CancellationToken cancellationToken = default)
        {
            var cliente = await _context.Clientes
                .AsNoTracking()
                .FirstOrDefaultAsync(c => c.Id == id, cancellationToken);

            if (cliente is null) return NotFound();

            return Ok(MapearClienteParaResposta(cliente));
        }

        [HttpPost]
        public async Task<IActionResult> CriarCliente(
            [FromBody] CriarClienteDto dto,
            CancellationToken cancellationToken = default)
        {
            var telefoneNormalizado = NormalizacaoTelefone.Normalizar(dto.Telefone);

            var novoCliente = new Cliente
            {
                Nome = dto.Nome,
                Telefone = telefoneNormalizado,
                Endereco = dto.Endereco
            };

            _context.Clientes.Add(novoCliente);
            await _context.SaveChangesAsync(cancellationToken);

            return CreatedAtAction(
                nameof(GetClientePorId),
                new { id = novoCliente.Id },
                MapearClienteParaResposta(novoCliente));
        }

        [Authorize(Policy = Politicas.SomenteAdmin)]
        [HttpPut("{id:int}")]
        public async Task<IActionResult> AtualizarCliente(
            int id,
            [FromBody] AtualizarClienteDto dto,
            CancellationToken cancellationToken = default)
        {
            var clienteExistente = await _context.Clientes
                .FirstOrDefaultAsync(c => c.Id == id, cancellationToken);
            var telefoneNormalizado = NormalizacaoTelefone.Normalizar(dto.Telefone);

            if (clienteExistente is null) return NotFound();

            clienteExistente.Nome = dto.Nome;
            clienteExistente.Telefone = telefoneNormalizado;
            clienteExistente.Endereco = dto.Endereco;

            await _context.SaveChangesAsync(cancellationToken);

            return NoContent();
        }

        [Authorize(Policy = Politicas.SomenteAdmin)]
        [HttpDelete("{id:int}")]
        public async Task<IActionResult> ExcluirCliente(
            int id,
            CancellationToken cancellationToken = default)
        {
            var clienteExistente = await _context.Clientes
                .FirstOrDefaultAsync(c => c.Id == id, cancellationToken);

            if (clienteExistente is null) return NotFound();

            var possuiPedidos = await _context.Pedidos
                .AnyAsync(p => p.ClienteId == id, cancellationToken);

            if (possuiPedidos)
            {
                return Conflict(new { mensagem = "O cliente possui pedidos e não pode ser excluído." });
            }

            _context.Clientes.Remove(clienteExistente);
            await _context.SaveChangesAsync(cancellationToken);

            return NoContent();
        }

        private static ClienteRespostaDto MapearClienteParaResposta(Cliente cliente)
        {
            return new ClienteRespostaDto
            {
                Id = cliente.Id,
                Nome = cliente.Nome,
                Telefone = cliente.Telefone,
                Endereco = cliente.Endereco
            };
        }
    }
}
