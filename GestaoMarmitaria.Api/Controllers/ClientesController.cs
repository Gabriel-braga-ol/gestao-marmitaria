using GestaoMarmitaria.Api.Models;
using GestaoMarmitaria.Api.Dtos;
using GestaoMarmitaria.Api.Data;
using Microsoft.AspNetCore.Mvc;
using System.Collections.Generic;
using Microsoft.EntityFrameworkCore;

namespace GestaoMarmitaria.Api.Controllers
{
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
        public async Task<IActionResult> GetClientes()
        {
            var clientes = await _context.Clientes
                .AsNoTracking()
                .OrderBy(c => c.Id)
                .ToListAsync();

            return Ok(clientes);
        }

        [HttpGet("{id:int}")]
        public async Task<IActionResult> GetClientePorId(int id)
        {
            var clientes = await _context.Clientes
                .AsNoTracking()
                .FirstOrDefaultAsync(c => c.Id == id);

            if (clientes is null) return NotFound();

            return Ok(clientes);
        }

        [HttpPost]
        public async Task<IActionResult> CriarCliente([FromBody] CriarClienteDto dto)
        {
            var telefoneNormalizado = new string(dto.Telefone.Where(char.IsDigit).ToArray());

            var novoCliente = new Cliente
            {
                Nome = dto.Nome,
                Telefone = telefoneNormalizado,
                Endereco = dto.Endereco
            };

            _context.Clientes.Add(novoCliente);
            await _context.SaveChangesAsync();

            return CreatedAtAction(nameof(GetClientePorId), new { id = novoCliente.Id }, novoCliente);
        }

        [HttpPut("{id:int}")]
        public async Task<IActionResult> AtualizarCliente(int id, [FromBody] AtualizarClienteDto dto)
        {
            var clienteExistente = await _context.Clientes.FirstOrDefaultAsync(c => c.Id == id);
            var telefoneNormalizado = new string(dto.Telefone.Where(char.IsDigit).ToArray());

            if (clienteExistente is null) return NotFound();

            clienteExistente.Nome = dto.Nome;
            clienteExistente.Telefone = telefoneNormalizado;
            clienteExistente.Endereco = dto.Endereco;

            await _context.SaveChangesAsync();

            return NoContent();
        }

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
    }
}
