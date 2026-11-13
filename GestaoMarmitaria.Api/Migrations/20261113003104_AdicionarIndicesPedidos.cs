using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GestaoMarmitaria.Api.Migrations
{
    /// <inheritdoc />
    public partial class AdicionarIndicesPedidos : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateIndex(
                name: "IX_Pedidos_DataCriacao",
                table: "Pedidos",
                column: "DataCriacao");

            migrationBuilder.CreateIndex(
                name: "IX_Pedidos_Status",
                table: "Pedidos",
                column: "Status");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Pedidos_DataCriacao",
                table: "Pedidos");

            migrationBuilder.DropIndex(
                name: "IX_Pedidos_Status",
                table: "Pedidos");
        }
    }
}
