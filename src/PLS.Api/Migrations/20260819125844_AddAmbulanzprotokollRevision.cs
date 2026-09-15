using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PLS.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddAmbulanzprotokollRevision : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "revision",
                table: "patient_ambulanzprotokoll_page1",
                type: "int",
                nullable: false,
                defaultValue: 0);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "revision",
                table: "patient_ambulanzprotokoll_page1");
        }
    }
}
