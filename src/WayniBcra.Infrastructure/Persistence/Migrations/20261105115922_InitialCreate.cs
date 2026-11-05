using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace WayniBcra.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "debtors",
                columns: table => new
                {
                    identification_number = table.Column<string>(type: "character varying(11)", maxLength: 11, nullable: false),
                    max_credit_status = table.Column<string>(type: "character varying(2)", maxLength: 2, nullable: false),
                    total_financing_amount = table.Column<decimal>(type: "numeric(20,1)", precision: 20, scale: 1, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_debtors", x => x.identification_number);
                });

            migrationBuilder.CreateTable(
                name: "financial_entities",
                columns: table => new
                {
                    entity_code = table.Column<string>(type: "character varying(5)", maxLength: 5, nullable: false),
                    total_financing_amount = table.Column<decimal>(type: "numeric(20,1)", precision: 20, scale: 1, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_financial_entities", x => x.entity_code);
                });

            migrationBuilder.CreateTable(
                name: "import_jobs",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    file_name = table.Column<string>(type: "character varying(260)", maxLength: 260, nullable: false),
                    status = table.Column<int>(type: "integer", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    started_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    finished_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    processed_debtors = table.Column<int>(type: "integer", nullable: false),
                    processed_entities = table.Column<int>(type: "integer", nullable: false),
                    error_message = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_import_jobs", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "staging_records",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    import_job_id = table.Column<Guid>(type: "uuid", nullable: false),
                    entity_code = table.Column<string>(type: "character varying(5)", maxLength: 5, nullable: false),
                    identification_type = table.Column<string>(type: "character varying(2)", maxLength: 2, nullable: false),
                    identification_number = table.Column<string>(type: "character varying(11)", maxLength: 11, nullable: false),
                    credit_status = table.Column<string>(type: "character varying(2)", maxLength: 2, nullable: false),
                    financing_amount = table.Column<decimal>(type: "numeric(12,1)", precision: 12, scale: 1, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_staging_records", x => x.id);
                });

            migrationBuilder.CreateIndex(
                name: "ix_debtors_total_financing_amount",
                table: "debtors",
                column: "total_financing_amount",
                descending: new bool[0]);

            migrationBuilder.CreateIndex(
                name: "ix_staging_records_import_job_id",
                table: "staging_records",
                column: "import_job_id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "debtors");

            migrationBuilder.DropTable(
                name: "financial_entities");

            migrationBuilder.DropTable(
                name: "import_jobs");

            migrationBuilder.DropTable(
                name: "staging_records");
        }
    }
}
