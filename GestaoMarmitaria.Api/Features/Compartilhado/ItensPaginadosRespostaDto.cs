namespace GestaoMarmitaria.Api.Features.Compartilhado
{
    public class ItensPaginadosRespostaDto<T>
    {
        public List<T> Itens { get; init; } = new();

        public int Pagina { get; init; }

        public int TamanhoPagina { get; init; }

        public int TotalRegistros { get; init; }

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
