using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SmokeMkk.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddVendonFields : Migration
    {
        // Hand-edited (docs/PLAN.md §4.2): data-preserving. Both columns are added nullable, the 39 existing machines
        // get their VendonId and the 3 photographed ones their PictureUrl by Id from the §3 tables, then VendonId
        // becomes non-null and unique. On an empty table the UPDATEs touch nothing and the seed supplies the values.
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "VendonId",
                table: "Machines",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "PictureUrl",
                table: "Machines",
                type: "text",
                nullable: true);

            migrationBuilder.Sql("UPDATE \"Machines\" SET \"VendonId\" = 396121 WHERE \"Id\" = 38;");
            migrationBuilder.Sql("UPDATE \"Machines\" SET \"VendonId\" = 337325 WHERE \"Id\" = 22;");
            migrationBuilder.Sql("UPDATE \"Machines\" SET \"VendonId\" = 574652 WHERE \"Id\" = 23;");
            migrationBuilder.Sql("UPDATE \"Machines\" SET \"VendonId\" = 574645 WHERE \"Id\" = 24;");
            migrationBuilder.Sql("UPDATE \"Machines\" SET \"VendonId\" = 574653 WHERE \"Id\" = 25;");
            migrationBuilder.Sql("UPDATE \"Machines\" SET \"VendonId\" = 574553 WHERE \"Id\" = 26;");
            migrationBuilder.Sql("UPDATE \"Machines\" SET \"VendonId\" = 574637 WHERE \"Id\" = 28;");
            migrationBuilder.Sql("UPDATE \"Machines\" SET \"VendonId\" = 574555 WHERE \"Id\" = 29;");
            migrationBuilder.Sql("UPDATE \"Machines\" SET \"VendonId\" = 574648 WHERE \"Id\" = 30;");
            migrationBuilder.Sql("UPDATE \"Machines\" SET \"VendonId\" = 574659 WHERE \"Id\" = 31;");
            migrationBuilder.Sql("UPDATE \"Machines\" SET \"VendonId\" = 578254 WHERE \"Id\" = 32;");
            migrationBuilder.Sql("UPDATE \"Machines\" SET \"VendonId\" = 574647 WHERE \"Id\" = 7;");
            migrationBuilder.Sql("UPDATE \"Machines\" SET \"VendonId\" = 574650 WHERE \"Id\" = 15;");
            migrationBuilder.Sql("UPDATE \"Machines\" SET \"VendonId\" = 520743 WHERE \"Id\" = 16;");
            migrationBuilder.Sql("UPDATE \"Machines\" SET \"VendonId\" = 337324 WHERE \"Id\" = 2;");
            migrationBuilder.Sql("UPDATE \"Machines\" SET \"VendonId\" = 574646 WHERE \"Id\" = 3;");
            migrationBuilder.Sql("UPDATE \"Machines\" SET \"VendonId\" = 574581 WHERE \"Id\" = 27;");
            migrationBuilder.Sql("UPDATE \"Machines\" SET \"VendonId\" = 574641 WHERE \"Id\" = 1;");
            migrationBuilder.Sql("UPDATE \"Machines\" SET \"VendonId\" = 574603 WHERE \"Id\" = 6;");
            migrationBuilder.Sql("UPDATE \"Machines\" SET \"VendonId\" = 574658 WHERE \"Id\" = 34;");
            migrationBuilder.Sql("UPDATE \"Machines\" SET \"VendonId\" = 396122 WHERE \"Id\" = 35;");
            migrationBuilder.Sql("UPDATE \"Machines\" SET \"VendonId\" = 574651 WHERE \"Id\" = 36;");
            migrationBuilder.Sql("UPDATE \"Machines\" SET \"VendonId\" = 574644 WHERE \"Id\" = 37;");
            migrationBuilder.Sql("UPDATE \"Machines\" SET \"VendonId\" = 574654 WHERE \"Id\" = 9;");
            migrationBuilder.Sql("UPDATE \"Machines\" SET \"VendonId\" = 574633 WHERE \"Id\" = 10;");
            migrationBuilder.Sql("UPDATE \"Machines\" SET \"VendonId\" = 574655 WHERE \"Id\" = 11;");
            migrationBuilder.Sql("UPDATE \"Machines\" SET \"VendonId\" = 574649 WHERE \"Id\" = 8;");
            migrationBuilder.Sql("UPDATE \"Machines\" SET \"VendonId\" = 576113 WHERE \"Id\" = 12;");
            migrationBuilder.Sql("UPDATE \"Machines\" SET \"VendonId\" = 577852 WHERE \"Id\" = 13;");
            migrationBuilder.Sql("UPDATE \"Machines\" SET \"VendonId\" = 554489 WHERE \"Id\" = 5;");
            migrationBuilder.Sql("UPDATE \"Machines\" SET \"VendonId\" = 542301 WHERE \"Id\" = 4;");
            migrationBuilder.Sql("UPDATE \"Machines\" SET \"VendonId\" = 552712 WHERE \"Id\" = 17;");
            migrationBuilder.Sql("UPDATE \"Machines\" SET \"VendonId\" = 552711 WHERE \"Id\" = 18;");
            migrationBuilder.Sql("UPDATE \"Machines\" SET \"VendonId\" = 549886 WHERE \"Id\" = 14;");
            migrationBuilder.Sql("UPDATE \"Machines\" SET \"VendonId\" = 396123 WHERE \"Id\" = 21;");
            migrationBuilder.Sql("UPDATE \"Machines\" SET \"VendonId\" = 574656 WHERE \"Id\" = 33;");
            migrationBuilder.Sql("UPDATE \"Machines\" SET \"VendonId\" = 577262 WHERE \"Id\" = 19;");
            migrationBuilder.Sql("UPDATE \"Machines\" SET \"VendonId\" = 574657 WHERE \"Id\" = 39;");
            migrationBuilder.Sql("UPDATE \"Machines\" SET \"VendonId\" = 574613 WHERE \"Id\" = 20;");

            migrationBuilder.Sql("UPDATE \"Machines\" SET \"PictureUrl\" = 'https://cloud.vendon.net/images/vending_pic/337324_654cc6cbaa811_large.jpg' WHERE \"Id\" = 2;");
            migrationBuilder.Sql("UPDATE \"Machines\" SET \"PictureUrl\" = 'https://cloud.vendon.net/images/vending_pic/337325_65f2e6bfef3e5_large.jpg' WHERE \"Id\" = 22;");
            migrationBuilder.Sql("UPDATE \"Machines\" SET \"PictureUrl\" = 'https://cloud.vendon.net/images/vending_pic/396122_6781202fc424f_large.jpg' WHERE \"Id\" = 35;");

            migrationBuilder.AlterColumn<int>(
                name: "VendonId",
                table: "Machines",
                type: "integer",
                nullable: false,
                oldClrType: typeof(int),
                oldType: "integer",
                oldNullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Machines_VendonId",
                table: "Machines",
                column: "VendonId",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Machines_VendonId",
                table: "Machines");

            migrationBuilder.DropColumn(
                name: "PictureUrl",
                table: "Machines");

            migrationBuilder.DropColumn(
                name: "VendonId",
                table: "Machines");
        }
    }
}
