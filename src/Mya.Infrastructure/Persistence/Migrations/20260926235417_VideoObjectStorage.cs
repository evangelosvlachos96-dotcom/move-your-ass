using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Mya.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class VideoObjectStorage : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "ContentType",
                table: "Video",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<long>(
                name: "SizeBytes",
                table: "Video",
                type: "bigint",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ThumbnailObjectKey",
                table: "Video",
                type: "character varying(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "UploadId",
                table: "Video",
                type: "character varying(200)",
                maxLength: 200,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ContentType",
                table: "Video");

            migrationBuilder.DropColumn(
                name: "SizeBytes",
                table: "Video");

            migrationBuilder.DropColumn(
                name: "ThumbnailObjectKey",
                table: "Video");

            migrationBuilder.DropColumn(
                name: "UploadId",
                table: "Video");
        }
    }
}
