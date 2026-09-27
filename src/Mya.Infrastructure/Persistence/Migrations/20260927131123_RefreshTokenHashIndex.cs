using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Mya.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class RefreshTokenHashIndex : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateIndex(
                name: "IX_RefreshToken_TokenHash",
                table: "RefreshToken",
                column: "TokenHash",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_RefreshToken_TokenHash",
                table: "RefreshToken");
        }
    }
}
