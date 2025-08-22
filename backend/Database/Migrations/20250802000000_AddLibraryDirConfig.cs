using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace NzbWebDAV.Database.Migrations
{
    public partial class AddLibraryDirConfig : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.InsertData(
                table: "ConfigItems",
                columns: new[] { "ConfigName", "ConfigValue" },
                values: new object[,]
                {
                    { "media.library-dir", "" },
                }
            );
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Intentionally left blank
        }
    }
}
