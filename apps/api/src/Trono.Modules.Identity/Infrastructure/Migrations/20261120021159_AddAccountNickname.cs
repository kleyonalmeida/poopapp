using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Trono.Modules.Identity.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddAccountNickname : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Nickname",
                schema: "identity",
                table: "AspNetUsers",
                type: "character varying(40)",
                maxLength: 40,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Nickname",
                schema: "identity",
                table: "AspNetUsers");
        }
    }
}
