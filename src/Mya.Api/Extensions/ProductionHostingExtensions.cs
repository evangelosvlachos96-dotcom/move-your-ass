using System.Globalization;
using Microsoft.AspNetCore.HttpOverrides;

namespace Mya.Api.Extensions;

public static class ProductionHostingExtensions
{
    public static void AddProductionHosting(this WebApplicationBuilder builder)
    {
        ArgumentNullException.ThrowIfNull(builder);
        if (builder.Configuration["PORT"] is { Length: > 0 } port)
        {
            if (!int.TryParse(port, NumberStyles.None, CultureInfo.InvariantCulture, out var number)
                || number is < 1 or > 65535)
            {
                throw new InvalidOperationException("PORT must be a number between 1 and 65535.");
            }
            builder.WebHost.UseUrls($"http://0.0.0.0:{number}");
        }

        builder.Services.Configure<ForwardedHeadersOptions>(options =>
        {
            options.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
            options.ForwardLimit = 1;
            // Render's ingress is the trust boundary; its proxy addresses are not static.
            // Only enable this on Render, where public requests must pass through that ingress.
            // Never accept forwarded Host: email links use the validated App:PublicOrigin.
            if (builder.Configuration["RENDER"] == "true")
            {
                options.KnownIPNetworks.Clear();
                options.KnownProxies.Clear();
            }
        });
    }
}
