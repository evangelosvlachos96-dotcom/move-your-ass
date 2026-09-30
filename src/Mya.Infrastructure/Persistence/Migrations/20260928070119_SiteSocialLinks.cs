using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Mya.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class SiteSocialLinks : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "SocialLinksJson",
                table: "SiteContent",
                type: "character varying(4000)",
                maxLength: 4000,
                nullable: true);

            // Carry whatever is already in the per-network columns into the new list, in the
            // order the old editor showed them. Additive in both directions: the old columns are
            // left exactly as they are, so Down() only has to drop the new one and nothing is
            // lost if this is rolled back.
            //
            // Written as SQL rather than as C# over the rows because it is one row on one table
            // and a migration that opens the DbContext is a migration that breaks the next time
            // the model changes. jsonb_build_* does the quoting, so no value is ever concatenated
            // into a JSON string by hand.
            migrationBuilder.Sql("""
                UPDATE "SiteContent"
                SET "SocialLinksJson" = links.json
                FROM (
                    SELECT
                        s."Id" AS id,
                        (
                            SELECT jsonb_agg(entry ORDER BY entry_order)::text
                            FROM (
                                VALUES
                                    (1, 'instagram', s."Instagram"),
                                    (2, 'youtube',   s."YouTube"),
                                    (3, 'tiktok',    s."TikTok"),
                                    (4, 'facebook',  s."Facebook"),
                                    (5, 'whatsapp',  s."WhatsApp"),
                                    (6, 'website',   s."Website")
                            ) AS v(entry_order, network, value)
                            CROSS JOIN LATERAL (
                                SELECT jsonb_build_object('network', v.network, 'value', v.value) AS entry
                            ) built
                            WHERE v.value IS NOT NULL AND btrim(v.value) <> ''
                        ) AS json
                    FROM "SiteContent" s
                ) AS links
                WHERE "SiteContent"."Id" = links.id
                  AND links.json IS NOT NULL
                  AND "SiteContent"."SocialLinksJson" IS NULL;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "SocialLinksJson",
                table: "SiteContent");
        }
    }
}
