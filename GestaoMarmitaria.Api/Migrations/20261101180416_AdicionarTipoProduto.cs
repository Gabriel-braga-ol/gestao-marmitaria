using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GestaoMarmitaria.Api.Migrations
{
    /// <inheritdoc />
    public partial class AdicionarTipoProduto : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "Tipo",
                table: "Produtos",
                type: "integer",
                nullable: false,
                defaultValue: 1);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Tipo",
                table: "Produtos");
        }
    }
}
