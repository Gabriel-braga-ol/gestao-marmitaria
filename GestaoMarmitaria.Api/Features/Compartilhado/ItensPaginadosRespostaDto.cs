namespace GestaoMarmitaria.Api.Features.Compartilhado
{
    public class ItensPaginadosRespostaDto<T>
    {
        public List<T> Itens { get; set; } = new();

        public int Pagina { get; set; }

        public int TamanhoPagina { get; set; }

        public int TotalRegistros { get; set; }

        public int TotalPaginas => (int)Math.Ceiling((double)TotalRegistros / TamanhoPagina);

        public static ItensPaginadosRespostaDto<T> Criar(
            List<T> itens,
            PaginacaoDto paginacao,
            int totalRegistros)
        {
            return new ItensPaginadosRespostaDto<T>
            {
                Itens = itens,
                Pagina = paginacao.Pagina,
                TamanhoPagina = paginacao.TamanhoPagina,
                TotalRegistros = totalRegistros
            };
        }
    }
}
