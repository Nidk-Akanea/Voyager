using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Voyager.Persistence.PostgreSql.Migrations
{
    /// <inheritdoc />
    public partial class InitialTripsAndTravelers : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "travelers",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    avatar_content = table.Column<byte[]>(type: "bytea", nullable: true),
                    avatar_format = table.Column<int>(type: "integer", nullable: true),
                    birth_date = table.Column<DateOnly>(type: "date", nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "clock_timestamp()"),
                    description = table.Column<string>(type: "text", nullable: true),
                    is_profile = table.Column<bool>(type: "boolean", nullable: false),
                    name = table.Column<string>(type: "text", nullable: false),
                    nickname = table.Column<string>(type: "text", nullable: true),
                    owner_id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_travelers", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "trips",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "clock_timestamp()"),
                    creator_id = table.Column<Guid>(type: "uuid", nullable: false),
                    group_id = table.Column<Guid>(type: "uuid", nullable: false),
                    name = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_trips", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "trip_travelers",
                columns: table => new
                {
                    traveler_id = table.Column<Guid>(type: "uuid", nullable: false),
                    trip_id = table.Column<Guid>(type: "uuid", nullable: false),
                    added_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "clock_timestamp()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_trip_travelers", x => new { x.trip_id, x.traveler_id });
                    table.ForeignKey(
                        name: "FK_trip_travelers_travelers_traveler_id",
                        column: x => x.traveler_id,
                        principalTable: "travelers",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_trip_travelers_trips_trip_id",
                        column: x => x.trip_id,
                        principalTable: "trips",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_travelers_owner_id_created_at",
                table: "travelers",
                columns: new[] { "owner_id", "created_at" });

            migrationBuilder.CreateIndex(
                name: "ux_travelers_profile_per_owner",
                table: "travelers",
                column: "owner_id",
                unique: true,
                filter: "\"is_profile\"");

            migrationBuilder.CreateIndex(
                name: "ix_trip_travelers_traveler_id",
                table: "trip_travelers",
                column: "traveler_id");

            migrationBuilder.CreateIndex(
                name: "ix_trips_group_id_created_at",
                table: "trips",
                columns: new[] { "group_id", "created_at" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "trip_travelers");

            migrationBuilder.DropTable(
                name: "travelers");

            migrationBuilder.DropTable(
                name: "trips");
        }
    }
}
