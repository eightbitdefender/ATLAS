using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ATLAS.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddBusinessStakeholder : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "BusinessStakeholder",
                table: "Assets",
                type: "TEXT",
                maxLength: 100,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "BusinessStakeholder",
                table: "Assets");
        }
    }
}
