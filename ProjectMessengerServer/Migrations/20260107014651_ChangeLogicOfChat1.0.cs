using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ProjectMessengerServer.Migrations
{
    /// <inheritdoc />
    public partial class ChangeLogicOfChat10 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_ChatMembers_Messages_LastReadMessageId",
                table: "ChatMembers");

            migrationBuilder.AddForeignKey(
                name: "FK_ChatMembers_Messages_LastReadMessageId",
                table: "ChatMembers",
                column: "LastReadMessageId",
                principalTable: "Messages",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_ChatMembers_Messages_LastReadMessageId",
                table: "ChatMembers");

            migrationBuilder.AddForeignKey(
                name: "FK_ChatMembers_Messages_LastReadMessageId",
                table: "ChatMembers",
                column: "LastReadMessageId",
                principalTable: "Messages",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }
    }
}
