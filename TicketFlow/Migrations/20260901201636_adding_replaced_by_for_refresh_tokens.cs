using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TicketFlow.Migrations
{
    /// <inheritdoc />
    public partial class adding_replaced_by_for_refresh_tokens : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "ReplacedBy",
                table: "RefreshTokens",
                type: "nvarchar(max)",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ReplacedBy",
                table: "RefreshTokens");
        }
    }
}
