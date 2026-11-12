using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace GestaoMarmitaria.Api.Infra.Data;

public static class FalhasBanco
{
    public static bool EhFalhaDeSerializacao(Exception ex)
    {
        // O EF/Npgsql pode envolver o 40001 em DbUpdateException ou em
        // InvalidOperationException ("transient failure"), então percorremos a cadeia.
        for (var atual = ex; atual is not null; atual = atual.InnerException)
        {
            if (atual is PostgresException { SqlState: PostgresErrorCodes.SerializationFailure })
            {
                return true;
            }
        }

        return false;
    }

    public static bool EhViolacaoDeIndiceUnico(Exception ex, string nomeIndice)
    {
        return ex is DbUpdateException { InnerException: PostgresException postgresException }
            && postgresException.SqlState == PostgresErrorCodes.UniqueViolation
            && postgresException.ConstraintName == nomeIndice;
    }
}
