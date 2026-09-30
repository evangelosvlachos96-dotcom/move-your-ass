using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Mya.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class VideoCoverAndSiteContent : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "CoverObjectKey",
                table: "Video",
                type: "character varying(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<long>(
                name: "CoverSizeBytes",
                table: "Video",
                type: "bigint",
                nullable: true);

            migrationBuilder.AddColumn<long>(
                name: "ThumbnailSizeBytes",
                table: "Video",
                type: "bigint",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "SiteContent",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    PhotoObjectKey = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    PhotoSizeBytes = table.Column<long>(type: "bigint", nullable: true),
                    TrainerName = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: true),
                    Tagline = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    AboutMarkdown = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: true),
                    ContactEmail = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    Phone = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: true),
                    Instagram = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    YouTube = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    TikTok = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    Facebook = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    WhatsApp = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    Website = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    UpdatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    Revision = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SiteContent", x => x.Id);
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "SiteContent");

            migrationBuilder.DropColumn(
                name: "CoverObjectKey",
                table: "Video");

            migrationBuilder.DropColumn(
                name: "CoverSizeBytes",
                table: "Video");

            migrationBuilder.DropColumn(
                name: "ThumbnailSizeBytes",
                table: "Video");
        }
    }
}
