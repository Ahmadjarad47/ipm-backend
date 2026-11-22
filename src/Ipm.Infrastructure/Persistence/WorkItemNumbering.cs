using System.Globalization;
using Ipm.Application.Abstractions.Data;
using Ipm.Domain.Work;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;

namespace Ipm.Infrastructure.Persistence;

/// <summary>One <c>INSERT … ON CONFLICT DO UPDATE … RETURNING</c>: the first call of a project gets 101, every next call one more.</summary>
internal sealed class WorkItemNumbering(ApplicationDbContext db) : IWorkItemNumbering
{
    private static readonly string Sql =
        $"""
        INSERT INTO {Schemas.Work}.project_counters (project_id, last_number) VALUES (@project, {ProjectCounter.FirstNumber.ToString(CultureInfo.InvariantCulture)})
        ON CONFLICT (project_id) DO UPDATE SET last_number = {Schemas.Work}.project_counters.last_number + 1
        RETURNING last_number
        """;

    public async Task<int> NextAsync(Guid projectId, CancellationToken cancellationToken)
    {
        var connection = db.Database.GetDbConnection();
        var opened = connection.State != System.Data.ConnectionState.Open;
        if (opened)
        {
            await connection.OpenAsync(cancellationToken);
        }

        try
        {
            await using var command = connection.CreateCommand();
            command.CommandText = Sql;
            command.Transaction = db.Database.CurrentTransaction?.GetDbTransaction();
            var parameter = command.CreateParameter();
            parameter.ParameterName = "project";
            parameter.Value = projectId;
            command.Parameters.Add(parameter);
            return Convert.ToInt32(await command.ExecuteScalarAsync(cancellationToken), CultureInfo.InvariantCulture);
        }
        finally
        {
            if (opened)
            {
                await connection.CloseAsync();
            }
        }
    }
}
