using System.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;

namespace GestaoMarmitaria.Api.Infra.Data;

public static class ExecucaoTransacional
{
    /// <summary>
    /// Abre uma transação Serializable, executa a operação e converte conflito de
    /// serialização no resultado informado. A operação decide quando confirmar.
    /// </summary>
    public static async Task<T> ExecutarSerializavelAsync<T>(
        AppDbContext context,
        Func<IDbContextTransaction, Task<T>> operacao,
        Func<T> aoConflitar,
        CancellationToken cancellationToken = default)
    {
        await using var transacao = await context.Database
            .BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken);

        try
        {
            return await operacao(transacao);
        }
        catch (Exception ex) when (FalhasBanco.EhFalhaDeSerializacao(ex))
        {
            return aoConflitar();
        }
    }
}
