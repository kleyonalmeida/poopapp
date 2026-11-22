using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Trono.Modules.Records.Migrations
{
    /// <inheritdoc />
    public partial class InitialPrivateRecords : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "records");

            migrationBuilder.CreateTable(
                name: "CreationReceipts",
                schema: "records",
                columns: table => new
                {
                    AccountId = table.Column<Guid>(type: "uuid", nullable: false),
                    Key = table.Column<Guid>(type: "uuid", nullable: false),
                    RecordId = table.Column<Guid>(type: "uuid", nullable: false),
                    ContentHash = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CreationReceipts", x => new { x.AccountId, x.Key });
                    table.ForeignKey(
                        name: "FK_CreationReceipts_AspNetUsers_AccountId",
                        column: x => x.AccountId,
                        principalSchema: "identity",
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Records",
                schema: "records",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    AccountId = table.Column<Guid>(type: "uuid", nullable: false),
                    OccurredAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    DurationSeconds = table.Column<int>(type: "integer", nullable: true),
                    Type = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    Notes = table.Column<string>(type: "character varying(280)", maxLength: 280, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Records", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Records_AspNetUsers_AccountId",
                        column: x => x.AccountId,
                        principalSchema: "identity",
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Records_AccountId_OccurredAt_Id",
                schema: "records",
                table: "Records",
                columns: new[] { "AccountId", "OccurredAt", "Id" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "CreationReceipts",
                schema: "records");

            migrationBuilder.DropTable(
                name: "Records",
                schema: "records");
        }
    }
}
