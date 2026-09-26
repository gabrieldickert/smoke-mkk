using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace SmokeMkk.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddLocations : Migration
    {
        // Hand-edited (docs/PLAN.md §4.2): data-preserving. Every existing machine row becomes its own location
        // (same id, slug, name, address, coordinates, URL) and keeps its stock; the site columns are dropped last.
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Locations",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Slug = table.Column<string>(type: "text", nullable: false),
                    Name = table.Column<string>(type: "text", nullable: false),
                    Street = table.Column<string>(type: "text", nullable: false),
                    PostalCode = table.Column<string>(type: "text", nullable: false),
                    City = table.Column<string>(type: "text", nullable: false),
                    Lat = table.Column<double>(type: "double precision", nullable: false),
                    Lng = table.Column<double>(type: "double precision", nullable: false),
                    GoogleMapsUrl = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Locations", x => x.Id);
                });

            migrationBuilder.Sql("""
                INSERT INTO "Locations" ("Id", "Slug", "Name", "Street", "PostalCode", "City", "Lat", "Lng", "GoogleMapsUrl")
                SELECT "Id", "Slug", "Name", "Street", "PostalCode", "City", "Lat", "Lng", "GoogleMapsUrl" FROM "Machines";
                """);
            // Explicit ids bypass the identity sequence; move it past them (empty table: next value stays 1).
            migrationBuilder.Sql("""
                SELECT setval(pg_get_serial_sequence('"Locations"', 'Id'),
                    COALESCE((SELECT MAX("Id") FROM "Locations"), 1), (SELECT COUNT(*) FROM "Locations") > 0);
                """);

            migrationBuilder.CreateIndex(
                name: "IX_Locations_Slug",
                table: "Locations",
                column: "Slug",
                unique: true);

            migrationBuilder.AddColumn<int>(
                name: "LocationId",
                table: "Machines",
                type: "integer",
                nullable: true);

            migrationBuilder.Sql("""UPDATE "Machines" SET "LocationId" = "Id";""");

            migrationBuilder.AlterColumn<int>(
                name: "LocationId",
                table: "Machines",
                type: "integer",
                nullable: false,
                oldClrType: typeof(int),
                oldType: "integer",
                oldNullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Machines_LocationId",
                table: "Machines",
                column: "LocationId");

            migrationBuilder.AddForeignKey(
                name: "FK_Machines_Locations_LocationId",
                table: "Machines",
                column: "LocationId",
                principalTable: "Locations",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddColumn<string>(
                name: "Label",
                table: "Machines",
                type: "text",
                nullable: false,
                defaultValue: "");

            migrationBuilder.DropIndex(
                name: "IX_Machines_Slug",
                table: "Machines");

            foreach (var column in new[] { "Slug", "Name", "Street", "PostalCode", "City", "Lat", "Lng", "GoogleMapsUrl" })
                migrationBuilder.DropColumn(name: column, table: "Machines");
        }

        // ponytail: no way back. Once a location holds several machines, folding it into the old one-row-per-machine
        // shape would duplicate slugs and invent addresses. Restore a backup taken before this migration instead.
        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder) =>
            throw new NotSupportedException("AddLocations cannot be reverted; restore a database backup taken before it.");
    }
}
