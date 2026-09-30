using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Mya.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class SiteBookingUrl : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "BookingUrl",
                table: "SiteContent",
                type: "character varying(200)",
                maxLength: 200,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "BookingUrl",
                table: "SiteContent");
        }
    }
}
