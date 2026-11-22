using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Trono.Modules.Identity.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddPushNotificationPreference : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "PushNotificationsEnabled",
                schema: "identity",
                table: "AspNetUsers",
                type: "boolean",
                nullable: false,
                defaultValue: false);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "PushNotificationsEnabled",
                schema: "identity",
                table: "AspNetUsers");
        }
    }
}
