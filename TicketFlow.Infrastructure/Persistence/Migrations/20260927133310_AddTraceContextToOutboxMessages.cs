using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TicketFlow.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddTraceContextToOutboxMessages : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "TraceParent",
                table: "OutboxMessages",
                type: "varchar(128)",
                unicode: false,
                maxLength: 128,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "TraceState",
                table: "OutboxMessages",
                type: "varchar(512)",
                unicode: false,
                maxLength: 512,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "TraceParent",
                table: "OutboxMessages");

            migrationBuilder.DropColumn(
                name: "TraceState",
                table: "OutboxMessages");
        }
    }
}
