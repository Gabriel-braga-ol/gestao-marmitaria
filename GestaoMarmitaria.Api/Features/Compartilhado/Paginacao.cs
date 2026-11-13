using Microsoft.EntityFrameworkCore;

namespace GestaoMarmitaria.Api.Features.Compartilhado;

public static class Paginacao
{
    /// <summary>
    /// Aplica a janela da página em uma consulta já ordenada. Página além do
    /// total devolve lista vazia, não erro.
    /// </summary>
    public static async Task<List<T>> BuscarPaginaAsync<T>(
        IQueryable<T> consulta,
        PaginacaoDto paginacao,
        int totalRegistros,
        CancellationToken cancellationToken = default)
    {
        var quantidadeIgnorada =
            (long)(paginacao.Pagina - 1) * paginacao.TamanhoPagina;

        if (quantidadeIgnorada >= totalRegistros)
        {
            return new List<T>();
        }

        return await consulta
            .Skip((int)quantidadeIgnorada)
            .Take(paginacao.TamanhoPagina)
            .ToListAsync(cancellationToken);
    }
}
