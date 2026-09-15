using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TicketFlow.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddedProcessedJobModel : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ProcessedBackgroundJobs",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    IdempotencyKey = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    ProcessedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ProcessedBackgroundJobs", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ProcessedBackgroundJobs_IdempotencyKey",
                table: "ProcessedBackgroundJobs",
                column: "IdempotencyKey",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ProcessedBackgroundJobs");
        }
    }
}
