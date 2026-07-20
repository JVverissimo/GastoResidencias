using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace project_API.Migrations
{
    /// <inheritdoc />
    public partial class AdicionarCpfUnicoEmPessoa : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Cpf",
                table: "Pessoas",
                type: "character varying(14)",
                maxLength: 14,
                nullable: false,
                defaultValue: "");

            migrationBuilder.CreateIndex(
                name: "IX_Pessoas_Cpf",
                table: "Pessoas",
                column: "Cpf",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Pessoas_Cpf",
                table: "Pessoas");

            migrationBuilder.DropColumn(
                name: "Cpf",
                table: "Pessoas");
        }
    }
}
