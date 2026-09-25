using System.Data.Common;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace Mya.Infrastructure.Persistence.Interceptors;

/// <summary>
/// Retries opening a connection while a serverless Azure SQL database resumes from auto-pause.
/// The first login after a pause fails with error 40613 for up to about a minute; without a retry
/// the first request of the day returns a 500 (ADR-016).
/// <para>
/// Only <see cref="SqlConnection.Open"/> is retried, not commands. EF's
/// <c>EnableRetryOnFailure</c> is deliberately not used: it rejects the user-initiated
/// transactions the handlers open, and a retried command could run twice.
/// </para>
/// </summary>
public sealed class SqlConnectionRetryInterceptor : DbConnectionInterceptor
{
    private static readonly SqlRetryLogicBaseProvider OpenRetry = SqlConfigurableRetryFactory.CreateExponentialRetryProvider(
        new SqlRetryLogicOption
        {
            NumberOfTries = 6,
            DeltaTime = TimeSpan.FromSeconds(2),
            MaxTimeInterval = TimeSpan.FromSeconds(20),
            // Azure SQL "database unavailable/resuming", throttling and dropped-connection errors.
            TransientErrors = [40613, 40197, 40501, 49918, 49919, 49920, 4221, 10928, 10929, 10053, 10054, 10060, 233, 64, -2],
        });

    public override DbConnection ConnectionCreated(ConnectionCreatedEventData eventData, DbConnection result)
    {
        if (result is SqlConnection sqlConnection)
        {
            sqlConnection.RetryLogicProvider = OpenRetry;
        }

        return result;
    }
}
