using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ProjectMessengerServer.Migrations
{
    /// <inheritdoc />
    public partial class ChangeMessageLogic : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "MessageInChatId",
                table: "Messages",
                type: "integer",
                nullable: false,
                defaultValue: 0);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "MessageInChatId",
                table: "Messages");
        }
    }
}
