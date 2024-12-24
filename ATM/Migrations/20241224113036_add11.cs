using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ATM.Migrations
{
    /// <inheritdoc />
    public partial class add11 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "Id",
                table: "importExcels",
                type: "integer",
                nullable: false,
                defaultValue: 0);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Id",
                table: "importExcels");
        }
    }
}
