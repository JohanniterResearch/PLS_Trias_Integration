using System;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PLS.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddPatientChangeHistory : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "patient_changes",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("MySql:ValueGenerationStrategy", MySqlValueGenerationStrategy.IdentityColumn),
                    patient_id = table.Column<int>(type: "int", nullable: false),
                    field_id = table.Column<int>(type: "int", nullable: false),
                    value = table.Column<string>(type: "json", nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    changed_at = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    received_at = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    accepted = table.Column<bool>(type: "tinyint(1)", nullable: false),
                    reason = table.Column<string>(type: "varchar(32)", maxLength: 32, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    actor_id = table.Column<string>(type: "varchar(128)", maxLength: 128, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    device_id = table.Column<string>(type: "varchar(128)", maxLength: 128, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_patient_changes", x => x.id);
                    table.ForeignKey(
                        name: "FK_patient_changes_patients_patient_id",
                        column: x => x.patient_id,
                        principalTable: "patients",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateIndex(
                name: "IX_patient_changes_patient_id_field_id_accepted_changed_at",
                table: "patient_changes",
                columns: new[] { "patient_id", "field_id", "accepted", "changed_at" });

            migrationBuilder.CreateIndex(
                name: "IX_patient_changes_patient_id_field_id_changed_at",
                table: "patient_changes",
                columns: new[] { "patient_id", "field_id", "changed_at" });

            migrationBuilder.CreateIndex(
                name: "IX_patient_changes_patient_id_id",
                table: "patient_changes",
                columns: new[] { "patient_id", "id" });
            // Existing records have no field history. Capture their current state once,
            // using the previous row timestamp as a conservative migration baseline.
            migrationBuilder.Sql("""
                INSERT INTO patient_changes (`patient_id`, `field_id`, `value`, `changed_at`, `received_at`, `accepted`, `reason`)
                SELECT id, 1, IF(atmung IS NULL, 'null', IF(atmung, 'true', 'false')), COALESCE(updated_at, created_at, '1970-01-01'), UTC_TIMESTAMP(6), 1, 'baseline' FROM patients;

                INSERT INTO patient_changes (`patient_id`, `field_id`, `value`, `changed_at`, `received_at`, `accepted`, `reason`)
                SELECT id, 2, IF(blutung IS NULL, 'null', IF(blutung, 'true', 'false')), COALESCE(updated_at, created_at, '1970-01-01'), UTC_TIMESTAMP(6), 1, 'baseline' FROM patients;

                INSERT INTO patient_changes (`patient_id`, `field_id`, `value`, `changed_at`, `received_at`, `accepted`, `reason`)
                SELECT id, 3, COALESCE(JSON_QUOTE(triagefarbe), 'null'), COALESCE(updated_at, created_at, '1970-01-01'), UTC_TIMESTAMP(6), 1, 'baseline' FROM patients;

                INSERT INTO patient_changes (`patient_id`, `field_id`, `value`, `changed_at`, `received_at`, `accepted`, `reason`)
                SELECT id, 4, JSON_OBJECT('lat', latitude_patient, 'lng', longitude_patient), COALESCE(updated_at, created_at, '1970-01-01'), UTC_TIMESTAMP(6), 1, 'baseline' FROM patients;

                INSERT INTO patient_changes (`patient_id`, `field_id`, `value`, `changed_at`, `received_at`, `accepted`, `reason`)
                SELECT patient_id, 5, JSON_QUOTE(status), COALESCE(updated_at, created_at, '1970-01-01'), UTC_TIMESTAMP(6), 1, 'baseline' FROM patient_ambulanzprotokoll_page1;

                INSERT INTO patient_changes (`patient_id`, `field_id`, `value`, `changed_at`, `received_at`, `accepted`, `reason`)
                SELECT source.id_patient, fields.field_id, COALESCE(JSON_EXTRACT(source.body_parts, fields.json_path), 'null'),
                    COALESCE(source.updated_at, source.created_at, '1970-01-01'), UTC_TIMESTAMP(6), 1, 'baseline'
                FROM bodies source CROSS JOIN (
                SELECT 100 AS field_id, '$.hals_vorne' AS json_path
                UNION ALL SELECT 101 AS field_id, '$.brust_links' AS json_path
                UNION ALL SELECT 102 AS field_id, '$.brust_rechts' AS json_path
                UNION ALL SELECT 103 AS field_id, '$.leiste_links_vorne' AS json_path
                UNION ALL SELECT 104 AS field_id, '$.leiste_rechts_vorne' AS json_path
                UNION ALL SELECT 105 AS field_id, '$.oberschenkel_rechts_vorne' AS json_path
                UNION ALL SELECT 106 AS field_id, '$.unterschenkel_links_vorne' AS json_path
                UNION ALL SELECT 107 AS field_id, '$.oberschenkel_links_vorne' AS json_path
                UNION ALL SELECT 108 AS field_id, '$.unterschenkel_rechts_vorne' AS json_path
                UNION ALL SELECT 109 AS field_id, '$.oberarm_links_vorne' AS json_path
                UNION ALL SELECT 110 AS field_id, '$.oberarm_rechts_vorne' AS json_path
                UNION ALL SELECT 111 AS field_id, '$.unterarm_links_vorne' AS json_path
                UNION ALL SELECT 112 AS field_id, '$.unterarm_rechts_vorne' AS json_path
                UNION ALL SELECT 113 AS field_id, '$.genital_vorne' AS json_path
                UNION ALL SELECT 114 AS field_id, '$.kopf_vorne' AS json_path
                UNION ALL SELECT 115 AS field_id, '$.schulter_links_vorne' AS json_path
                UNION ALL SELECT 116 AS field_id, '$.schulter_rechts_vorne' AS json_path
                UNION ALL SELECT 117 AS field_id, '$.huefte_links_vorne' AS json_path
                UNION ALL SELECT 118 AS field_id, '$.huefte_rechts_vorne' AS json_path
                UNION ALL SELECT 119 AS field_id, '$.knie_links_vorne' AS json_path
                UNION ALL SELECT 120 AS field_id, '$.knie_rechts_vorne' AS json_path
                UNION ALL SELECT 121 AS field_id, '$.ellbogen_rechts_vorne' AS json_path
                UNION ALL SELECT 122 AS field_id, '$.ellbogen_links_vorne' AS json_path
                UNION ALL SELECT 123 AS field_id, '$.fuss_rechts_vorne' AS json_path
                UNION ALL SELECT 124 AS field_id, '$.fuss_links_vorne' AS json_path
                UNION ALL SELECT 125 AS field_id, '$.auge_rechts' AS json_path
                UNION ALL SELECT 126 AS field_id, '$.auge_links' AS json_path
                UNION ALL SELECT 127 AS field_id, '$.mund' AS json_path
                UNION ALL SELECT 128 AS field_id, '$.hand_links_vorne' AS json_path
                UNION ALL SELECT 129 AS field_id, '$.hand_rechts_vorne' AS json_path
                UNION ALL SELECT 130 AS field_id, '$.kopf_hinten' AS json_path
                UNION ALL SELECT 131 AS field_id, '$.hals_hinten' AS json_path
                UNION ALL SELECT 132 AS field_id, '$.ruecken_rechts' AS json_path
                UNION ALL SELECT 133 AS field_id, '$.oberschenkel_rechts_hinten' AS json_path
                UNION ALL SELECT 134 AS field_id, '$.unterschenkel_links_hinten' AS json_path
                UNION ALL SELECT 135 AS field_id, '$.oberschenkel_links_hinten' AS json_path
                UNION ALL SELECT 136 AS field_id, '$.unterschenkel_rechts_hinten' AS json_path
                UNION ALL SELECT 137 AS field_id, '$.oberarm_links_hinten' AS json_path
                UNION ALL SELECT 138 AS field_id, '$.oberarm_rechts_hinten' AS json_path
                UNION ALL SELECT 139 AS field_id, '$.unterarm_links_hinten' AS json_path
                UNION ALL SELECT 140 AS field_id, '$.unterarm_rechts_hinten' AS json_path
                UNION ALL SELECT 141 AS field_id, '$.becken_links_hinten' AS json_path
                UNION ALL SELECT 142 AS field_id, '$.ruecken_links' AS json_path
                UNION ALL SELECT 143 AS field_id, '$.becken_rechts_hinten' AS json_path
                UNION ALL SELECT 144 AS field_id, '$.genital_hinten' AS json_path
                UNION ALL SELECT 145 AS field_id, '$.huefte_rechts_hinten' AS json_path
                UNION ALL SELECT 146 AS field_id, '$.huefte_links_hinten' AS json_path
                UNION ALL SELECT 147 AS field_id, '$.knie_links_hinten' AS json_path
                UNION ALL SELECT 148 AS field_id, '$.knie_rechts_hinten' AS json_path
                UNION ALL SELECT 149 AS field_id, '$.ellbogen_rechts_hinten' AS json_path
                UNION ALL SELECT 150 AS field_id, '$.ellbogen_links_hinten' AS json_path
                UNION ALL SELECT 151 AS field_id, '$.fuss_rechts_hinten' AS json_path
                UNION ALL SELECT 152 AS field_id, '$.fuss_links_hinten' AS json_path
                UNION ALL SELECT 153 AS field_id, '$.hand_links_hinten' AS json_path
                UNION ALL SELECT 154 AS field_id, '$.hand_rechts_hinten' AS json_path
                UNION ALL SELECT 155 AS field_id, '$.schulter_rechts_hinten' AS json_path
                UNION ALL SELECT 156 AS field_id, '$.schulter_links_hinten' AS json_path
                UNION ALL SELECT 157 AS field_id, '$.brustwirbel' AS json_path
                UNION ALL SELECT 158 AS field_id, '$.lendenwirbel' AS json_path
                ) fields;

                INSERT INTO patient_changes (`patient_id`, `field_id`, `value`, `changed_at`, `received_at`, `accepted`, `reason`)
                SELECT source.patient_id, fields.field_id, COALESCE(JSON_EXTRACT(source.form_state, fields.json_path), 'null'),
                    COALESCE(source.updated_at, source.created_at, '1970-01-01'), UTC_TIMESTAMP(6), 1, 'baseline'
                FROM patient_ambulanzprotokoll_page1 source CROSS JOIN (
                SELECT 1000 AS field_id, '$.incident.ambulanzort' AS json_path
                UNION ALL SELECT 1001 AS field_id, '$.incident.datum' AS json_path
                UNION ALL SELECT 1002 AS field_id, '$.incident.uhrzeit_beginn' AS json_path
                UNION ALL SELECT 1003 AS field_id, '$.incident.dnr_san_1' AS json_path
                UNION ALL SELECT 1004 AS field_id, '$.incident.dnr_san_2' AS json_path
                UNION ALL SELECT 1005 AS field_id, '$.incident.dnr_san_3' AS json_path
                UNION ALL SELECT 1006 AS field_id, '$.incident.dnr_na' AS json_path
                UNION ALL SELECT 1007 AS field_id, '$.incident.pls_nummer' AS json_path
                UNION ALL SELECT 1008 AS field_id, '$.incident.funkrufname' AS json_path
                UNION ALL SELECT 1009 AS field_id, '$.patient.familienname' AS json_path
                UNION ALL SELECT 1010 AS field_id, '$.patient.vorname' AS json_path
                UNION ALL SELECT 1011 AS field_id, '$.patient.geschlecht' AS json_path
                UNION ALL SELECT 1012 AS field_id, '$.patient.vers_nr' AS json_path
                UNION ALL SELECT 1013 AS field_id, '$.patient.geburtsdatum' AS json_path
                UNION ALL SELECT 1014 AS field_id, '$.patient.adresse' AS json_path
                UNION ALL SELECT 1015 AS field_id, '$.patient.staat' AS json_path
                UNION ALL SELECT 1016 AS field_id, '$.patient.telefon' AS json_path
                UNION ALL SELECT 1017 AS field_id, '$.patient.arbeitgeber' AS json_path
                UNION ALL SELECT 1018 AS field_id, '$.patient.versicherungstraeger' AS json_path
                UNION ALL SELECT 1019 AS field_id, '$.patient.familienstand' AS json_path
                UNION ALL SELECT 1020 AS field_id, '$.assessment_primary.naca' AS json_path
                UNION ALL SELECT 1021 AS field_id, '$.assessment_primary.atemweg' AS json_path
                UNION ALL SELECT 1022 AS field_id, '$.assessment_primary.atmung' AS json_path
                UNION ALL SELECT 1023 AS field_id, '$.assessment_primary.kreislauf' AS json_path
                UNION ALL SELECT 1024 AS field_id, '$.assessment_primary.bewusstsein' AS json_path
                UNION ALL SELECT 1025 AS field_id, '$.assessment_primary.angen_notfallzeit.zeit' AS json_path
                UNION ALL SELECT 1026 AS field_id, '$.assessment_primary.angen_notfallzeit.gt24h' AS json_path
                UNION ALL SELECT 1027 AS field_id, '$.assessment_primary.angen_notfallzeit.unbekannt' AS json_path
                UNION ALL SELECT 1028 AS field_id, '$.assessment_secondary.anamnese_text' AS json_path
                UNION ALL SELECT 1029 AS field_id, '$.assessment_secondary.bodymap' AS json_path
                UNION ALL SELECT 1030 AS field_id, '$.medications_administered' AS json_path
                UNION ALL SELECT 1031 AS field_id, '$.vitals.pupillen.R' AS json_path
                UNION ALL SELECT 1032 AS field_id, '$.vitals.pupillen.L' AS json_path
                UNION ALL SELECT 1033 AS field_id, '$.vitals.schmerz' AS json_path
                UNION ALL SELECT 1034 AS field_id, '$.vitals.schmerz_nicht_beurteilbar' AS json_path
                UNION ALL SELECT 1035 AS field_id, '$.vitals.gcs_augenoeffnen' AS json_path
                UNION ALL SELECT 1036 AS field_id, '$.vitals.gcs_verbale_reaktion' AS json_path
                UNION ALL SELECT 1037 AS field_id, '$.vitals.gcs_motorische_reaktion' AS json_path
                UNION ALL SELECT 1038 AS field_id, '$.vitals.gcs_summe' AS json_path
                UNION ALL SELECT 1039 AS field_id, '$.vitals.keine' AS json_path
                UNION ALL SELECT 1040 AS field_id, '$.vitals.rr' AS json_path
                UNION ALL SELECT 1041 AS field_id, '$.vitals.puls' AS json_path
                UNION ALL SELECT 1042 AS field_id, '$.vitals.puls_rhythmus' AS json_path
                UNION ALL SELECT 1043 AS field_id, '$.vitals.af' AS json_path
                UNION ALL SELECT 1044 AS field_id, '$.vitals.temp' AS json_path
                UNION ALL SELECT 1045 AS field_id, '$.vitals.bz' AS json_path
                UNION ALL SELECT 1046 AS field_id, '$.vitals.etco2' AS json_path
                UNION ALL SELECT 1047 AS field_id, '$.vitals.spo2' AS json_path
                UNION ALL SELECT 1048 AS field_id, '$.vitals.o2_l_min' AS json_path
                UNION ALL SELECT 1049 AS field_id, '$.vitals.o2_beatmung_l_min' AS json_path
                UNION ALL SELECT 1050 AS field_id, '$.measures.herz_kreislauf.massnahmen' AS json_path
                UNION ALL SELECT 1051 AS field_id, '$.measures.herz_kreislauf.dnr' AS json_path
                UNION ALL SELECT 1052 AS field_id, '$.measures.herz_kreislauf.anzahl' AS json_path
                UNION ALL SELECT 1053 AS field_id, '$.measures.herz_kreislauf.letzte_joule' AS json_path
                UNION ALL SELECT 1054 AS field_id, '$.measures.herz_kreislauf.freq' AS json_path
                UNION ALL SELECT 1055 AS field_id, '$.measures.herz_kreislauf.mv' AS json_path
                UNION ALL SELECT 1056 AS field_id, '$.measures.atmung.massnahmen' AS json_path
                UNION ALL SELECT 1057 AS field_id, '$.measures.atmung.dnr' AS json_path
                UNION ALL SELECT 1058 AS field_id, '$.measures.atmung.af' AS json_path
                UNION ALL SELECT 1059 AS field_id, '$.measures.atmung.amv' AS json_path
                UNION ALL SELECT 1060 AS field_id, '$.measures.atmung.peep' AS json_path
                UNION ALL SELECT 1061 AS field_id, '$.measures.weitere_massnahmen.massnahmen' AS json_path
                UNION ALL SELECT 1062 AS field_id, '$.measures.weitere_massnahmen.abbinden_zeit' AS json_path
                UNION ALL SELECT 1063 AS field_id, '$.measures.weitere_massnahmen.lagerung_art' AS json_path
                UNION ALL SELECT 1064 AS field_id, '$.history.allergien' AS json_path
                UNION ALL SELECT 1065 AS field_id, '$.history.medikamente' AS json_path
                UNION ALL SELECT 1066 AS field_id, '$.history.patientengeschichte_vorerkrankungen' AS json_path
                UNION ALL SELECT 1067 AS field_id, '$.history.letzte_orale_aufnahme' AS json_path
                UNION ALL SELECT 1068 AS field_id, '$.history.ereignisse_zuvor' AS json_path
                UNION ALL SELECT 1069 AS field_id, '$.history.risikofaktoren' AS json_path
                UNION ALL SELECT 1070 AS field_id, '$.disposition.abschlussart' AS json_path
                UNION ALL SELECT 1071 AS field_id, '$.disposition.klinischer_zustand' AS json_path
                UNION ALL SELECT 1072 AS field_id, '$.disposition.uhrzeit_ende' AS json_path
                UNION ALL SELECT 1073 AS field_id, '$.disposition.org' AS json_path
                UNION ALL SELECT 1074 AS field_id, '$.disposition.typ' AS json_path
                UNION ALL SELECT 1075 AS field_id, '$.disposition.kennung' AS json_path
                UNION ALL SELECT 1076 AS field_id, '$.disposition.angehoerige_in_kenntnis' AS json_path
                UNION ALL SELECT 1077 AS field_id, '$.disposition.kontaktdaten' AS json_path
                UNION ALL SELECT 1078 AS field_id, '$.signatures.entlass_san_na' AS json_path
                ) fields;
                """);

        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "patient_changes");
        }
    }
}
