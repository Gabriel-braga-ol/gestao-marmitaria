using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace GestaoMarmitaria.Api.Data;

public static class FalhasBanco
{
    public static bool EhFalhaDeSerializacao(Exception ex)
    {
        return ex is PostgresException { SqlState: PostgresErrorCodes.SerializationFailure }
            || ex is DbUpdateException
            {
                InnerException: PostgresException
                {
                    SqlState: PostgresErrorCodes.SerializationFailure
                }
            };
    }

    public static bool EhViolacaoDeIndiceUnico(Exception ex, string nomeIndice)
    {
        return ex is DbUpdateException { InnerException: PostgresException postgresException }
            && postgresException.SqlState == PostgresErrorCodes.UniqueViolation
            && postgresException.ConstraintName == nomeIndice;
    }
}
