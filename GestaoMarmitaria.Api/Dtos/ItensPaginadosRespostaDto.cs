namespace GestaoMarmitaria.Api.Dtos
{
    public class ItensPaginadosRespostaDto<T>
    {
        public List<T> Itens { get; set; } = new();

        public int Pagina { get; set; }

        public int TamanhoPagina { get; set; }

        public int TotalRegistros { get; set; }

        public int TotalPaginas => (int)Math.Ceiling((double)TotalRegistros / TamanhoPagina);
    }
}
