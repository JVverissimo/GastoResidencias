using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace project_API.Migrations
{
    /// <inheritdoc />
    public partial class TransacoesComCPF : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Transacoes_Pessoas_PessoaId",
                table: "Transacoes");

            migrationBuilder.DropIndex(
                name: "IX_Transacoes_PessoaId",
                table: "Transacoes");

            migrationBuilder.DropColumn(
                name: "PessoaId",
                table: "Transacoes");

            migrationBuilder.AddColumn<string>(
                name: "PessoaCpf",
                table: "Transacoes",
                type: "character varying(14)",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddUniqueConstraint(
                name: "AK_Pessoas_Cpf",
                table: "Pessoas",
                column: "Cpf");

            migrationBuilder.CreateIndex(
                name: "IX_Transacoes_PessoaCpf",
                table: "Transacoes",
                column: "PessoaCpf");

            migrationBuilder.AddForeignKey(
                name: "FK_Transacoes_Pessoas_PessoaCpf",
                table: "Transacoes",
                column: "PessoaCpf",
                principalTable: "Pessoas",
                principalColumn: "Cpf",
                onDelete: ReferentialAction.Cascade);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Transacoes_Pessoas_PessoaCpf",
                table: "Transacoes");

            migrationBuilder.DropIndex(
                name: "IX_Transacoes_PessoaCpf",
                table: "Transacoes");

            migrationBuilder.DropUniqueConstraint(
                name: "AK_Pessoas_Cpf",
                table: "Pessoas");

            migrationBuilder.DropColumn(
                name: "PessoaCpf",
                table: "Transacoes");

            migrationBuilder.AddColumn<int>(
                name: "PessoaId",
                table: "Transacoes",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.CreateIndex(
                name: "IX_Transacoes_PessoaId",
                table: "Transacoes",
                column: "PessoaId");

            migrationBuilder.AddForeignKey(
                name: "FK_Transacoes_Pessoas_PessoaId",
                table: "Transacoes",
                column: "PessoaId",
                principalTable: "Pessoas",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }
    }
}
