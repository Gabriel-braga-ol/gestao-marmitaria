using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace GestaoMarmitaria.Api.Migrations
{
    /// <inheritdoc />
    public partial class AdicionarAuditoriaOperacoes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "DataRegistro",
                table: "Reembolsos",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "UsuarioRegistroId",
                table: "Reembolsos",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "DataRegistro",
                table: "Pagamentos",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "UsuarioRegistroId",
                table: "Pagamentos",
                type: "integer",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "HistoricosStatusPedido",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    PedidoId = table.Column<int>(type: "integer", nullable: false),
                    StatusAnterior = table.Column<int>(type: "integer", nullable: false),
                    StatusNovo = table.Column<int>(type: "integer", nullable: false),
                    UsuarioId = table.Column<int>(type: "integer", nullable: true),
                    DataAlteracao = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_HistoricosStatusPedido", x => x.Id);
                    table.ForeignKey(
                        name: "FK_HistoricosStatusPedido_Pedidos_PedidoId",
                        column: x => x.PedidoId,
                        principalTable: "Pedidos",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_HistoricosStatusPedido_Usuarios_UsuarioId",
                        column: x => x.UsuarioId,
                        principalTable: "Usuarios",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Reembolsos_UsuarioRegistroId",
                table: "Reembolsos",
                column: "UsuarioRegistroId");

            migrationBuilder.CreateIndex(
                name: "IX_Pagamentos_UsuarioRegistroId",
                table: "Pagamentos",
                column: "UsuarioRegistroId");

            migrationBuilder.CreateIndex(
                name: "IX_HistoricosStatusPedido_PedidoId",
                table: "HistoricosStatusPedido",
                column: "PedidoId");

            migrationBuilder.CreateIndex(
                name: "IX_HistoricosStatusPedido_UsuarioId",
                table: "HistoricosStatusPedido",
                column: "UsuarioId");

            migrationBuilder.AddForeignKey(
                name: "FK_Pagamentos_Usuarios_UsuarioRegistroId",
                table: "Pagamentos",
                column: "UsuarioRegistroId",
                principalTable: "Usuarios",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Reembolsos_Usuarios_UsuarioRegistroId",
                table: "Reembolsos",
                column: "UsuarioRegistroId",
                principalTable: "Usuarios",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Pagamentos_Usuarios_UsuarioRegistroId",
                table: "Pagamentos");

            migrationBuilder.DropForeignKey(
                name: "FK_Reembolsos_Usuarios_UsuarioRegistroId",
                table: "Reembolsos");

            migrationBuilder.DropTable(
                name: "HistoricosStatusPedido");

            migrationBuilder.DropIndex(
                name: "IX_Reembolsos_UsuarioRegistroId",
                table: "Reembolsos");

            migrationBuilder.DropIndex(
                name: "IX_Pagamentos_UsuarioRegistroId",
                table: "Pagamentos");

            migrationBuilder.DropColumn(
                name: "DataRegistro",
                table: "Reembolsos");

            migrationBuilder.DropColumn(
                name: "UsuarioRegistroId",
                table: "Reembolsos");

            migrationBuilder.DropColumn(
                name: "DataRegistro",
                table: "Pagamentos");

            migrationBuilder.DropColumn(
                name: "UsuarioRegistroId",
                table: "Pagamentos");
        }
    }
}
