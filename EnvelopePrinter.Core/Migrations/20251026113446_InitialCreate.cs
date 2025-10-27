using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace EnvelopePrinter.Core.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Recipients",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    OrganizationName = table.Column<string>(type: "TEXT", nullable: false),
                    AddressLine1 = table.Column<string>(type: "TEXT", nullable: false),
                    City = table.Column<string>(type: "TEXT", nullable: false),
                    PostalCode = table.Column<string>(type: "TEXT", nullable: false),
                    Region = table.Column<string>(type: "TEXT", nullable: false),
                    Country = table.Column<string>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Recipients", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Templates",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Name = table.Column<string>(type: "TEXT", nullable: false),
                    EnvelopeWidth = table.Column<double>(type: "REAL", nullable: false),
                    EnvelopeHeight = table.Column<double>(type: "REAL", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Templates", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "TemplateItems",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    PositionX = table.Column<double>(type: "REAL", nullable: false),
                    PositionY = table.Column<double>(type: "REAL", nullable: false),
                    Width = table.Column<double>(type: "REAL", nullable: false),
                    Height = table.Column<double>(type: "REAL", nullable: false),
                    FontFamily = table.Column<string>(type: "TEXT", nullable: false),
                    FontSize = table.Column<int>(type: "INTEGER", nullable: false),
                    IsItalic = table.Column<bool>(type: "INTEGER", nullable: false),
                    ContentBindingPath = table.Column<string>(type: "TEXT", nullable: false),
                    StaticText = table.Column<string>(type: "TEXT", nullable: false),
                    Name = table.Column<string>(type: "TEXT", nullable: false),
                    IsImage = table.Column<bool>(type: "INTEGER", nullable: false),
                    ImagePath = table.Column<string>(type: "TEXT", nullable: false),
                    Background = table.Column<string>(type: "TEXT", nullable: false),
                    BorderBrush = table.Column<string>(type: "TEXT", nullable: false),
                    BorderThickness = table.Column<double>(type: "REAL", nullable: false),
                    CornerRadius = table.Column<double>(type: "REAL", nullable: false),
                    FontWeight = table.Column<string>(type: "TEXT", nullable: false),
                    HorizontalAlignment = table.Column<string>(type: "TEXT", nullable: false),
                    VerticalAlignment = table.Column<string>(type: "TEXT", nullable: false),
                    Stretch = table.Column<string>(type: "TEXT", nullable: false),
                    Foreground = table.Column<string>(type: "TEXT", nullable: false),
                    TextAlignment = table.Column<string>(type: "TEXT", nullable: false),
                    Padding = table.Column<double>(type: "REAL", nullable: false),
                    Opacity = table.Column<double>(type: "REAL", nullable: false),
                    ZIndex = table.Column<int>(type: "INTEGER", nullable: false),
                    TemplateId = table.Column<int>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TemplateItems", x => x.Id);
                    table.ForeignKey(
                        name: "FK_TemplateItems_Templates_TemplateId",
                        column: x => x.TemplateId,
                        principalTable: "Templates",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_TemplateItems_TemplateId",
                table: "TemplateItems",
                column: "TemplateId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Recipients");

            migrationBuilder.DropTable(
                name: "TemplateItems");

            migrationBuilder.DropTable(
                name: "Templates");
        }
    }
}
