using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using PLS.Api.Data;

#nullable disable

namespace PLS.Api.Migrations;

[DbContext(typeof(PlsDbContext))]
[Migration("20260402120000_HardenTriagefarbe")]
public partial class HardenTriagefarbe : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("""
            UPDATE patients
            SET triagefarbe = CASE
                WHEN triagefarbe IS NULL THEN NULL
                WHEN LOWER(TRIM(triagefarbe)) = 'rot' THEN 'rot'
                WHEN LOWER(TRIM(triagefarbe)) = 'gelb' THEN 'gelb'
                WHEN LOWER(TRIM(triagefarbe)) = 'grün' THEN 'grün'
                WHEN LOWER(TRIM(triagefarbe)) = 'blau' THEN 'blau'
                WHEN LOWER(TRIM(triagefarbe)) = 'schwarz' THEN 'schwarz'
                ELSE NULL
            END;
            """);

        migrationBuilder.Sql("""
            UPDATE persons
            SET triagefarbe = CASE
                WHEN triagefarbe IS NULL THEN NULL
                WHEN LOWER(TRIM(triagefarbe)) = 'rot' THEN 'rot'
                WHEN LOWER(TRIM(triagefarbe)) = 'gelb' THEN 'gelb'
                WHEN LOWER(TRIM(triagefarbe)) = 'grün' THEN 'grün'
                WHEN LOWER(TRIM(triagefarbe)) = 'blau' THEN 'blau'
                WHEN LOWER(TRIM(triagefarbe)) = 'schwarz' THEN 'schwarz'
                ELSE NULL
            END;
            """);

        migrationBuilder.AlterColumn<string>(
            name: "triagefarbe",
            table: "patients",
            type: "varchar(16)",
            maxLength: 16,
            nullable: true,
            oldClrType: typeof(string),
            oldType: "longtext")
            .Annotation("MySql:CharSet", "utf8mb4")
            .OldAnnotation("MySql:CharSet", "utf8mb4");

        migrationBuilder.AlterColumn<string>(
            name: "triagefarbe",
            table: "persons",
            type: "varchar(16)",
            maxLength: 16,
            nullable: true,
            oldClrType: typeof(string),
            oldType: "longtext")
            .Annotation("MySql:CharSet", "utf8mb4")
            .OldAnnotation("MySql:CharSet", "utf8mb4");

        migrationBuilder.AddCheckConstraint(
            name: "CK_patients_triagefarbe_valid",
            table: "patients",
            sql: "triagefarbe IS NULL OR triagefarbe IN ('rot','gelb','grün','blau','schwarz')");

        migrationBuilder.AddCheckConstraint(
            name: "CK_persons_triagefarbe_valid",
            table: "persons",
            sql: "triagefarbe IS NULL OR triagefarbe IN ('rot','gelb','grün','blau','schwarz')");
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropCheckConstraint(
            name: "CK_patients_triagefarbe_valid",
            table: "patients");

        migrationBuilder.DropCheckConstraint(
            name: "CK_persons_triagefarbe_valid",
            table: "persons");

        migrationBuilder.AlterColumn<string>(
            name: "triagefarbe",
            table: "patients",
            type: "longtext",
            nullable: true,
            oldClrType: typeof(string),
            oldType: "varchar(16)",
            oldMaxLength: 16)
            .Annotation("MySql:CharSet", "utf8mb4")
            .OldAnnotation("MySql:CharSet", "utf8mb4");

        migrationBuilder.AlterColumn<string>(
            name: "triagefarbe",
            table: "persons",
            type: "longtext",
            nullable: true,
            oldClrType: typeof(string),
            oldType: "varchar(16)",
            oldMaxLength: 16)
            .Annotation("MySql:CharSet", "utf8mb4")
            .OldAnnotation("MySql:CharSet", "utf8mb4");
    }
}
