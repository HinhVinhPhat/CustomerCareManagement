using System.Text.Json;
using CcmWeb.Models.ViewModels;

namespace CcmWeb.Helpers;

/// <summary>
/// The rules of a contract that do not need a database call (design.md 6.7, 6.8): which action a
/// status allows, and the JSON shape <c>sp_AddContract</c> expects. Kept here, free of EF, so the
/// controller, the view and the tests all read the same rule instead of repeating a status check.
/// </summary>
public static class ContractRules
{
    /// <summary>"Expiring soon" means a contract whose end date is inside this window (<c>vw_ExpiringContract</c>).</summary>
    public const int ExpiringSoonDays = 30;

    /// <summary>Only a "Pending" contract can be signed (<c>sp_SignContract</c>).</summary>
    public static bool CanSign(string? status) => status == WorkConstants.ContractStatus.Pending;

    /// <summary>An "Active" or "Expired" contract can be renewed (<c>sp_RenewContract</c>).</summary>
    public static bool CanRenew(string? status) =>
        status is WorkConstants.ContractStatus.Active or WorkConstants.ContractStatus.Expired;

    /// <summary>An "Active" or "Pending" contract can be cancelled (<c>sp_CancelContract</c>).</summary>
    public static bool CanCancel(string? status) =>
        status is WorkConstants.ContractStatus.Active or WorkConstants.ContractStatus.Pending;

    /// <summary>No action at all: a cancelled contract is final, and a finished one only expires.</summary>
    public static bool HasAnyAction(string? status) => CanSign(status) || CanRenew(status) || CanCancel(status);

    /// <summary>
    /// Reads the hidden <c>LinesJson</c> field. A hand-written or truncated value is treated as "no
    /// lines" so the user sees the same message as an empty form, instead of an exception. Property
    /// names are matched without case because the same JSON shape is written back for the procedure
    /// as <c>ProductID</c> and posted by the page as <c>ProductId</c>.
    /// </summary>
    public static IReadOnlyList<ContractLineInput> ParseLines(string? json)
    {
        if (string.IsNullOrWhiteSpace(json) || json.Trim() == "[]") return [];

        try
        {
            return JsonSerializer.Deserialize<List<ContractLineInput>>(json, JsonOptions) ?? [];
        }
        catch (JsonException)
        {
            return [];
        }
    }

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    /// <summary>The same product may appear only once in a contract (<c>sp_AddContract</c>, 50009).</summary>
    public static bool HasDuplicateProduct(IEnumerable<ContractLineInput> lines) =>
        lines.Select(line => line.ProductId).Distinct().Count() != lines.Count();

    /// <summary>
    /// The exact shape <c>sp_AddContract</c> reads with <c>OPENJSON ... WITH</c>: an array of objects
    /// whose properties are <c>ProductID</c>, <c>Quantity</c> and <c>UnitPrice</c>. A null unit price
    /// lets the procedure fall back to the catalogue price, so it is sent as null rather than 0.
    /// </summary>
    public static string BuildDetailJson(IEnumerable<ContractLineInput> lines) =>
        JsonSerializer.Serialize(lines.Select(line => new
        {
            ProductID = line.ProductId,
            Quantity = line.Quantity,
            UnitPrice = line.UnitPrice,
        }));
}
