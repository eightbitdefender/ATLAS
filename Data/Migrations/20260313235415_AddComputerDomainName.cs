using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ATLAS.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddComputerDomainName : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "DomainName",
                table: "Assets",
                type: "TEXT",
                maxLength: 100,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "DomainName",
                table: "Assets");
        }
    }
}
