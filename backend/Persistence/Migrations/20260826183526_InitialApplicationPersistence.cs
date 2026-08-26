using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace backend.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class InitialApplicationPersistence : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "local_profiles",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    DisplayName = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    SelectedFplTeamId = table.Column<int>(type: "integer", nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_local_profiles", x => x.Id);
                    table.CheckConstraint("ck_local_profiles_selected_fpl_team_id", "\"SelectedFplTeamId\" IS NULL OR \"SelectedFplTeamId\" > 0");
                });

            migrationBuilder.CreateTable(
                name: "recommendation_history",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    FplTeamId = table.Column<int>(type: "integer", nullable: false),
                    Kind = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    PayloadJson = table.Column<string>(type: "jsonb", nullable: false),
                    CalculatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    RecordedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_recommendation_history", x => x.Id);
                    table.CheckConstraint("ck_recommendation_history_fpl_team_id", "\"FplTeamId\" > 0");
                });

            migrationBuilder.CreateTable(
                name: "recommendation_snapshots",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    FplTeamId = table.Column<int>(type: "integer", nullable: false),
                    Kind = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    PayloadJson = table.Column<string>(type: "jsonb", nullable: false),
                    CalculatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    ExpiresAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_recommendation_snapshots", x => x.Id);
                    table.CheckConstraint("ck_recommendation_snapshots_fpl_team_id", "\"FplTeamId\" > 0");
                });

            migrationBuilder.CreateTable(
                name: "application_settings",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    ProfileId = table.Column<Guid>(type: "uuid", nullable: false),
                    Key = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    ValueJson = table.Column<string>(type: "jsonb", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_application_settings", x => x.Id);
                    table.ForeignKey(
                        name: "FK_application_settings_local_profiles_ProfileId",
                        column: x => x.ProfileId,
                        principalTable: "local_profiles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_application_settings_ProfileId_Key",
                table: "application_settings",
                columns: new[] { "ProfileId", "Key" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_local_profiles_SelectedFplTeamId",
                table: "local_profiles",
                column: "SelectedFplTeamId");

            migrationBuilder.CreateIndex(
                name: "IX_recommendation_history_FplTeamId_Kind_RecordedAt",
                table: "recommendation_history",
                columns: new[] { "FplTeamId", "Kind", "RecordedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_recommendation_snapshots_ExpiresAt",
                table: "recommendation_snapshots",
                column: "ExpiresAt");

            migrationBuilder.CreateIndex(
                name: "IX_recommendation_snapshots_FplTeamId_Kind",
                table: "recommendation_snapshots",
                columns: new[] { "FplTeamId", "Kind" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "application_settings");

            migrationBuilder.DropTable(
                name: "recommendation_history");

            migrationBuilder.DropTable(
                name: "recommendation_snapshots");

            migrationBuilder.DropTable(
                name: "local_profiles");
        }
    }
}
