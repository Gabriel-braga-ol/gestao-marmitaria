using System.ComponentModel.DataAnnotations;

namespace GestaoMarmitaria.Api.Features.Compartilhado
{
    /// <summary>
    /// Parâmetros de paginação compartilhados pelas listagens.
    /// </summary>
    public abstract class PaginacaoDto
    {
        [Range(1, int.MaxValue, ErrorMessage = "A página deve ser maior que zero.")]
        public int Pagina { get; set; } = 1;

        [Range(1, 100, ErrorMessage = "O tamanho da página deve estar entre 1 e 100.")]
        public int TamanhoPagina { get; set; } = 20;
    }
}
