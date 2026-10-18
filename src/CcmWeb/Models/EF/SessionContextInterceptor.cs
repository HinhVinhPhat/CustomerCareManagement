using System.Data.Common;
using CcmWeb.Helpers;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace CcmWeb.Models.EF;

/// <summary>
/// The audit triggers read the acting employee from <c>SESSION_CONTEXT('EmployeeID')</c>, so every
/// connection EF opens has to set it. Without this the AUDIT_LOG rows record a NULL actor.
/// Background work has no signed-in user and correctly leaves it NULL.
/// </summary>
public sealed class SessionContextInterceptor(IHttpContextAccessor accessor) : DbConnectionInterceptor
{
    public override void ConnectionOpened(DbConnection connection, ConnectionEndEventData eventData) =>
        SetActorAsync(connection, CancellationToken.None).GetAwaiter().GetResult();

    public override Task ConnectionOpenedAsync(DbConnection connection, ConnectionEndEventData eventData,
        CancellationToken cancellationToken = default) =>
        SetActorAsync(connection, cancellationToken);

    private async Task SetActorAsync(DbConnection connection, CancellationToken cancellationToken)
    {
        var employeeID = accessor.HttpContext?.User.GetEmployeeID();
        if (employeeID is null)
        {
            return;
        }

        await using var command = connection.CreateCommand();
        command.CommandText = "EXEC sp_set_session_context N'EmployeeID', @employeeID";

        var parameter = command.CreateParameter();
        parameter.ParameterName = "@employeeID";
        parameter.Value = employeeID.Value;
        command.Parameters.Add(parameter);

        await command.ExecuteNonQueryAsync(cancellationToken);
    }
}
