using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GestaoMarmitaria.Api.Migrations
{
    /// <inheritdoc />
    public partial class NormalizarEmailsUsuarios : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Falha com mensagem clara se houver e-mails que colidam após a normalização,
            // em vez de deixar a violação do índice único sem contexto.
            migrationBuilder.Sql(
                """
                DO $$
                BEGIN
                    IF EXISTS (
                        SELECT 1
                        FROM "Usuarios"
                        GROUP BY lower(btrim("Email"))
                        HAVING count(*) > 1
                    ) THEN
                        RAISE EXCEPTION 'Existem Usuarios com e-mails equivalentes apos a normalizacao. Corrija os duplicados antes de aplicar esta migration.';
                    END IF;
                END $$;
                """);

            migrationBuilder.Sql(
                """
                UPDATE "Usuarios"
                SET "Email" = lower(btrim("Email"))
                WHERE "Email" <> lower(btrim("Email"));
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // A normalização não é reversível: a caixa e os espaços originais não foram armazenados.
        }
    }
}
