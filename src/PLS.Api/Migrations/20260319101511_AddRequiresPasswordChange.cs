using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PLS.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddRequiresPasswordChange : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "RequiresPasswordChange",
                table: "users",
                type: "tinyint(1)",
                nullable: false,
                defaultValue: false);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "RequiresPasswordChange",
                table: "users");
        }
    }
}
