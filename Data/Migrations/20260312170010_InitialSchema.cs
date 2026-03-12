using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ATLAS.Data.Migrations
{
    /// <inheritdoc />
    public partial class InitialSchema : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Assets",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Name = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false),
                    Description = table.Column<string>(type: "TEXT", maxLength: 2000, nullable: true),
                    Owner = table.Column<string>(type: "TEXT", maxLength: 100, nullable: true),
                    Department = table.Column<string>(type: "TEXT", maxLength: 100, nullable: true),
                    Location = table.Column<string>(type: "TEXT", maxLength: 200, nullable: true),
                    Status = table.Column<int>(type: "INTEGER", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    LastUpdated = table.Column<DateTime>(type: "TEXT", nullable: true),
                    Notes = table.Column<string>(type: "TEXT", maxLength: 500, nullable: true),
                    Discriminator = table.Column<string>(type: "TEXT", maxLength: 21, nullable: false),
                    CloudProvider = table.Column<int>(type: "INTEGER", nullable: true),
                    ResourceType = table.Column<string>(type: "TEXT", maxLength: 100, nullable: true),
                    Region = table.Column<string>(type: "TEXT", maxLength: 100, nullable: true),
                    AccountId = table.Column<string>(type: "TEXT", maxLength: 100, nullable: true),
                    ResourceId = table.Column<string>(type: "TEXT", maxLength: 200, nullable: true),
                    ComputerType = table.Column<int>(type: "INTEGER", nullable: true),
                    IpAddress = table.Column<string>(type: "TEXT", maxLength: 45, nullable: true),
                    OperatingSystem = table.Column<string>(type: "TEXT", maxLength: 100, nullable: true),
                    RamGb = table.Column<int>(type: "INTEGER", nullable: true),
                    CpuModel = table.Column<string>(type: "TEXT", maxLength: 100, nullable: true),
                    DomainJoined = table.Column<bool>(type: "INTEGER", nullable: true),
                    SerialNumber = table.Column<string>(type: "TEXT", maxLength: 100, nullable: true),
                    MobileDeviceType = table.Column<int>(type: "INTEGER", nullable: true),
                    MobileDevice_OperatingSystem = table.Column<string>(type: "TEXT", maxLength: 100, nullable: true),
                    Imei = table.Column<string>(type: "TEXT", maxLength: 20, nullable: true),
                    PhoneNumber = table.Column<string>(type: "TEXT", maxLength: 20, nullable: true),
                    Carrier = table.Column<string>(type: "TEXT", maxLength: 50, nullable: true),
                    NetworkDeviceType = table.Column<int>(type: "INTEGER", nullable: true),
                    NetworkDevice_IpAddress = table.Column<string>(type: "TEXT", maxLength: 45, nullable: true),
                    MacAddress = table.Column<string>(type: "TEXT", maxLength: 20, nullable: true),
                    Firmware = table.Column<string>(type: "TEXT", maxLength: 50, nullable: true),
                    PortCount = table.Column<int>(type: "INTEGER", nullable: true),
                    Managed = table.Column<bool>(type: "INTEGER", nullable: true),
                    PrinterType = table.Column<int>(type: "INTEGER", nullable: true),
                    Printer_IpAddress = table.Column<string>(type: "TEXT", maxLength: 45, nullable: true),
                    Model = table.Column<string>(type: "TEXT", maxLength: 100, nullable: true),
                    ColorCapable = table.Column<bool>(type: "INTEGER", nullable: true),
                    NetworkConnected = table.Column<bool>(type: "INTEGER", nullable: true),
                    ApplicationType = table.Column<int>(type: "INTEGER", nullable: true),
                    Vendor = table.Column<string>(type: "TEXT", maxLength: 100, nullable: true),
                    Version = table.Column<string>(type: "TEXT", maxLength: 50, nullable: true),
                    Url = table.Column<string>(type: "TEXT", maxLength: 500, nullable: true),
                    LicenseType = table.Column<string>(type: "TEXT", maxLength: 100, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Assets", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Vulnerabilities",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    CveId = table.Column<string>(type: "TEXT", maxLength: 30, nullable: true),
                    Title = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false),
                    Description = table.Column<string>(type: "TEXT", maxLength: 2000, nullable: true),
                    Severity = table.Column<int>(type: "INTEGER", nullable: false),
                    CvssScore = table.Column<double>(type: "REAL", nullable: true),
                    AffectedSoftware = table.Column<string>(type: "TEXT", maxLength: 500, nullable: true),
                    RemediationGuidance = table.Column<string>(type: "TEXT", maxLength: 500, nullable: true),
                    DiscoveredAt = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Vulnerabilities", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "AssetVulnerabilities",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    AssetId = table.Column<int>(type: "INTEGER", nullable: false),
                    VulnerabilityId = table.Column<int>(type: "INTEGER", nullable: false),
                    Status = table.Column<int>(type: "INTEGER", nullable: false),
                    DetectedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    RemediatedAt = table.Column<DateTime>(type: "TEXT", nullable: true),
                    Notes = table.Column<string>(type: "TEXT", maxLength: 500, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AssetVulnerabilities", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AssetVulnerabilities_Assets_AssetId",
                        column: x => x.AssetId,
                        principalTable: "Assets",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_AssetVulnerabilities_Vulnerabilities_VulnerabilityId",
                        column: x => x.VulnerabilityId,
                        principalTable: "Vulnerabilities",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_AssetVulnerabilities_AssetId",
                table: "AssetVulnerabilities",
                column: "AssetId");

            migrationBuilder.CreateIndex(
                name: "IX_AssetVulnerabilities_VulnerabilityId",
                table: "AssetVulnerabilities",
                column: "VulnerabilityId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AssetVulnerabilities");

            migrationBuilder.DropTable(
                name: "Assets");

            migrationBuilder.DropTable(
                name: "Vulnerabilities");
        }
    }
}
