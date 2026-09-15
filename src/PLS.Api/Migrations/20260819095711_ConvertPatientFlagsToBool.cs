using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PLS.Api.Migrations
{
    /// <inheritdoc />
    public partial class ConvertPatientFlagsToBool : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Legacy rows store .NET-serialized "True"/"False" strings; normalize them to
            // 1/0 before the type change. Any other value has no defined meaning -> NULL.
            migrationBuilder.Sql(@"UPDATE patients SET
                atmung       = CASE WHEN atmung       IN ('True','true','1') THEN '1' WHEN atmung       IN ('False','false','0') THEN '0' ELSE NULL END,
                blutung      = CASE WHEN blutung      IN ('True','true','1') THEN '1' WHEN blutung      IN ('False','false','0') THEN '0' ELSE NULL END,
                radialispuls = CASE WHEN radialispuls IN ('True','true','1') THEN '1' WHEN radialispuls IN ('False','false','0') THEN '0' ELSE NULL END,
                transport    = CASE WHEN transport    IN ('True','true','1') THEN '1' WHEN transport    IN ('False','false','0') THEN '0' ELSE NULL END,
                dringend     = CASE WHEN dringend     IN ('True','true','1') THEN '1' WHEN dringend     IN ('False','false','0') THEN '0' ELSE NULL END,
                kontaminiert = CASE WHEN kontaminiert IN ('True','true','1') THEN '1' WHEN kontaminiert IN ('False','false','0') THEN '0' ELSE NULL END;");

            migrationBuilder.AlterColumn<bool>(
                name: "transport",
                table: "patients",
                type: "tinyint(1)",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "longtext",
                oldNullable: true)
                .OldAnnotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AlterColumn<bool>(
                name: "radialispuls",
                table: "patients",
                type: "tinyint(1)",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "longtext",
                oldNullable: true)
                .OldAnnotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AlterColumn<bool>(
                name: "kontaminiert",
                table: "patients",
                type: "tinyint(1)",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "longtext",
                oldNullable: true)
                .OldAnnotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AlterColumn<bool>(
                name: "dringend",
                table: "patients",
                type: "tinyint(1)",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "longtext",
                oldNullable: true)
                .OldAnnotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AlterColumn<bool>(
                name: "blutung",
                table: "patients",
                type: "tinyint(1)",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "longtext",
                oldNullable: true)
                .OldAnnotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AlterColumn<bool>(
                name: "atmung",
                table: "patients",
                type: "tinyint(1)",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "longtext",
                oldNullable: true)
                .OldAnnotation("MySql:CharSet", "utf8mb4");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<string>(
                name: "transport",
                table: "patients",
                type: "longtext",
                nullable: true,
                oldClrType: typeof(bool),
                oldType: "tinyint(1)",
                oldNullable: true)
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AlterColumn<string>(
                name: "radialispuls",
                table: "patients",
                type: "longtext",
                nullable: true,
                oldClrType: typeof(bool),
                oldType: "tinyint(1)",
                oldNullable: true)
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AlterColumn<string>(
                name: "kontaminiert",
                table: "patients",
                type: "longtext",
                nullable: true,
                oldClrType: typeof(bool),
                oldType: "tinyint(1)",
                oldNullable: true)
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AlterColumn<string>(
                name: "dringend",
                table: "patients",
                type: "longtext",
                nullable: true,
                oldClrType: typeof(bool),
                oldType: "tinyint(1)",
                oldNullable: true)
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AlterColumn<string>(
                name: "blutung",
                table: "patients",
                type: "longtext",
                nullable: true,
                oldClrType: typeof(bool),
                oldType: "tinyint(1)",
                oldNullable: true)
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AlterColumn<string>(
                name: "atmung",
                table: "patients",
                type: "longtext",
                nullable: true,
                oldClrType: typeof(bool),
                oldType: "tinyint(1)",
                oldNullable: true)
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.Sql(@"UPDATE patients SET
                atmung       = CASE atmung       WHEN '1' THEN 'True' WHEN '0' THEN 'False' ELSE NULL END,
                blutung      = CASE blutung      WHEN '1' THEN 'True' WHEN '0' THEN 'False' ELSE NULL END,
                radialispuls = CASE radialispuls WHEN '1' THEN 'True' WHEN '0' THEN 'False' ELSE NULL END,
                transport    = CASE transport    WHEN '1' THEN 'True' WHEN '0' THEN 'False' ELSE NULL END,
                dringend     = CASE dringend     WHEN '1' THEN 'True' WHEN '0' THEN 'False' ELSE NULL END,
                kontaminiert = CASE kontaminiert WHEN '1' THEN 'True' WHEN '0' THEN 'False' ELSE NULL END;");
        }
    }
}
