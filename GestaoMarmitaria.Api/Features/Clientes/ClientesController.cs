using GestaoMarmitaria.Api.Models;
using GestaoMarmitaria.Api.Features.Clientes;
using GestaoMarmitaria.Api.Features.Compartilhado;
using GestaoMarmitaria.Api.Infra.Data;
using GestaoMarmitaria.Api.Infra.Regras;
using GestaoMarmitaria.Api.Infra.Seguranca;
using Microsoft.AspNetCore.Mvc;
using System.Collections.Generic;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Authorization;

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
        public async Task<IActionResult> GetClientes([FromQuery] ConsultarClientesDto filtros)
        {
            var consulta = _context.Clientes.AsNoTracking();

            var totalRegistros = await consulta.CountAsync();
            var quantidadeIgnorada = (long)(filtros.Pagina - 1) * filtros.TamanhoPagina;

            var clientes = quantidadeIgnorada >= totalRegistros
                ? new List<Cliente>()
                : await consulta
                    .OrderBy(c => c.Id)
                    .Skip((int)quantidadeIgnorada)
                    .Take(filtros.TamanhoPagina)
                    .ToListAsync();

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
        public async Task<IActionResult> GetClientePorId(int id)
        {
            var clientes = await _context.Clientes
                .AsNoTracking()
                .FirstOrDefaultAsync(c => c.Id == id);

            if (clientes is null) return NotFound();

            return Ok(MapearClienteParaResposta(clientes));
        }

        [HttpPost]
        public async Task<IActionResult> CriarCliente([FromBody] CriarClienteDto dto)
        {
            var telefoneNormalizado = NormalizacaoTelefone.Normalizar(dto.Telefone);

            var novoCliente = new Cliente
            {
                Nome = dto.Nome,
                Telefone = telefoneNormalizado,
                Endereco = dto.Endereco
            };

            _context.Clientes.Add(novoCliente);
            await _context.SaveChangesAsync();

            return CreatedAtAction(
                nameof(GetClientePorId),
                new { id = novoCliente.Id },
                MapearClienteParaResposta(novoCliente));
        }

        [Authorize(Policy = Politicas.SomenteAdmin)]
        [HttpPut("{id:int}")]
        public async Task<IActionResult> AtualizarCliente(int id, [FromBody] AtualizarClienteDto dto)
        {
            var clienteExistente = await _context.Clientes.FirstOrDefaultAsync(c => c.Id == id);
            var telefoneNormalizado = NormalizacaoTelefone.Normalizar(dto.Telefone);

            if (clienteExistente is null) return NotFound();

            clienteExistente.Nome = dto.Nome;
            clienteExistente.Telefone = telefoneNormalizado;
            clienteExistente.Endereco = dto.Endereco;

            await _context.SaveChangesAsync();

            return NoContent();
        }

        [Authorize(Policy = Politicas.SomenteAdmin)]
        [HttpDelete("{id:int}")]
        public async Task<IActionResult> ExcluirCliente(int id)
        {
            var clienteExistente = await _context.Clientes.FirstOrDefaultAsync(c => c.Id == id);

            if (clienteExistente is null) return NotFound();

            var possuiPedidos = await _context.Pedidos
                .AnyAsync(p => p.ClienteId == id);

            if (possuiPedidos)
            {
                return Conflict(new { mensagem = "O cliente possui pedidos e não pode ser excluído." });
            }

            _context.Clientes.Remove(clienteExistente);
            await _context.SaveChangesAsync();

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
