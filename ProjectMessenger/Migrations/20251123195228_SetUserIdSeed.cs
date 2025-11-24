using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ProjectMessenger.Migrations
{
    /// <inheritdoc />
    public partial class SetUserIdSeed : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<int>(
                name: "Id",
                table: "Users",
                nullable: false
            )
            .Annotation("SqlServer:Identity", "10000, 1");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {

        }
    }
}
