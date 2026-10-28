using System.ComponentModel.DataAnnotations;
using System.Globalization;
using CcmWeb.Helpers;

namespace CcmWeb.Models.ViewModels;

/// <summary>One row of the care schedule (<c>vw_CareSchedule</c>).</summary>
public sealed record CareScheduleRow(
    int CareId,
    int CustomerId,
    string CustomerName,
    string? Phone,
    int EmployeeId,
    string EmployeeName,
    string CareType,
    string? Content,
    DateOnly? NextAppointmentDate,
    string ScheduleStatus);

/// <summary>The schedule tab, split into the three groups the view defines.</summary>
public sealed class CareScheduleViewModel
{
    public IReadOnlyList<CareScheduleRow> Rows { get; init; } = [];

    public IReadOnlyList<CareScheduleRow> Overdue =>
        Rows.Where(r => r.ScheduleStatus == WorkConstants.ScheduleStatus.Overdue).ToList();

    public IReadOnlyList<CareScheduleRow> Today =>
        Rows.Where(r => r.ScheduleStatus == WorkConstants.ScheduleStatus.Today).ToList();

    public IReadOnlyList<CareScheduleRow> Upcoming =>
        Rows.Where(r => r.ScheduleStatus == WorkConstants.ScheduleStatus.Upcoming).ToList();

    public int Total => Rows.Count;
}

/// <summary>One row of the care log (<c>CARE</c>).</summary>
public sealed record CareLogRow(
    int CareId,
    DateOnly CareDate,
    int CustomerId,
    string CustomerName,
    int EmployeeId,
    string EmployeeName,
    string CareType,
    string? Content,
    string? Result,
    byte? SatisfactionScore,
    DateOnly? NextAppointmentDate);

/// <summary>
/// The state of the care log. Every value is read from the query string and written back into the
/// sort and pager links; <c>tab=log</c> always travels along so the current page stays open.
/// </summary>
public sealed class CareFilter
{
    public const string SortByCareDate = "CareDate";
    public const string SortByCustomer = "Customer";
    public const string SortByCareType = "CareType";

    public static readonly string[] SortFields =
        [SortByCareDate, SortByCustomer, SortByCareType];

    public string? Q { get; init; }
    public int? Staff { get; init; }
    public string? CareType { get; init; }
    public DateOnly? From { get; init; }
    public DateOnly? To { get; init; }

    public string Sort { get; init; } = SortByCareDate;
    public bool Desc { get; init; } = true;

    public int Page { get; init; } = 1;
    public int PageSize { get; init; } = WorkConstants.Settings.DefaultPageSize;

    public bool HasFilters =>
        !string.IsNullOrWhiteSpace(Q) || Staff is not null || CareType is not null
        || From is not null || To is not null;

    public string SortQuery(string field) =>
        Build(field, Sort == field ? !Desc : field == SortByCareDate, 1, PageSize);

    public string PageQuery(int page) => Build(Sort, Desc, page, PageSize);

    public string PageSizeQuery(int size) => Build(Sort, Desc, 1, size);

    private string Build(string sort, bool desc, int page, int pageSize)
    {
        var parameters = new List<string> { "tab=log" };

        if (!string.IsNullOrWhiteSpace(Q))
        {
            parameters.Add($"q={Uri.EscapeDataString(Q)}");
        }

        if (Staff is { } staff)
        {
            parameters.Add($"staff={staff.ToString(CultureInfo.InvariantCulture)}");
        }

        if (CareType is not null)
        {
            parameters.Add($"type={Uri.EscapeDataString(CareType)}");
        }

        if (From is { } from)
        {
            parameters.Add($"from={from.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture)}");
        }

        if (To is { } to)
        {
            parameters.Add($"to={to.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture)}");
        }

        if (sort != SortByCareDate)
        {
            parameters.Add($"sort={Uri.EscapeDataString(sort)}");
        }

        if (!desc)
        {
            parameters.Add("desc=false");
        }

        if (page > 1)
        {
            parameters.Add($"page={page.ToString(CultureInfo.InvariantCulture)}");
        }

        if (pageSize != WorkConstants.Settings.DefaultPageSize)
        {
            parameters.Add($"pageSize={pageSize.ToString(CultureInfo.InvariantCulture)}");
        }

        return $"?{string.Join('&', parameters)}";
    }
}

/// <summary>The care log tab: the filter, the rows of the current page and the pager facts.</summary>
public sealed class CareLogViewModel
{
    public required CareFilter Filter { get; init; }

    public IReadOnlyList<CareLogRow> Rows { get; init; } = [];

    public IReadOnlyList<EmployeeOption> Staff { get; init; } = [];

    public string? ErrorKey { get; init; }

    public int TotalRows { get; init; }

    public int TotalPages => TotalRows == 0 ? 0 : (int)Math.Ceiling(TotalRows / (double)Filter.PageSize);

    public int FirstRowNumber => TotalRows == 0 ? 0 : ((Filter.Page - 1) * Filter.PageSize) + 1;

    public int LastRowNumber => Math.Min(Filter.Page * Filter.PageSize, TotalRows);

    public bool HasPreviousPage => Filter.Page > 1 && TotalPages > 0;

    public bool HasNextPage => Filter.Page < TotalPages;

    public IReadOnlyList<int> PageNumbers
    {
        get
        {
            if (TotalPages <= 1)
            {
                return [];
            }

            var start = Math.Max(1, Filter.Page - 2);
            var end = Math.Min(TotalPages, start + 4);
            start = Math.Max(1, end - 4);

            return Enumerable.Range(start, end - start + 1).ToList();
        }
    }
}

/// <summary>The /care page: the open tab, the schedule and (on the log tab) the paged log.</summary>
public sealed class CarePageViewModel
{
    public string Tab { get; init; } = "schedule";

    public CareScheduleViewModel Schedule { get; init; } = new();

    public CareLogViewModel? Log { get; init; }

    /// <summary>Set when the schedule could not be read; a failed query is not an empty schedule.</summary>
    public string? ErrorKey { get; init; }

    public bool IsLogTab => Tab == "log";
}

/// <summary>
/// The add-care form (design.md 6.10). The care date is today and is not a field: a care record is
/// written when the care happens. The score is kept only for a Survey and the next appointment may
/// not be before the care date.
/// </summary>
public sealed class CareFormViewModel : IValidatableObject
{
    [Required(ErrorMessage = "Validation_CareCustomerRequired")]
    [Display(Name = "Common_Customer")]
    public int? CustomerId { get; set; }

    [Required(ErrorMessage = "Validation_CareTypeRequired")]
    [Display(Name = "Care_Type")]
    public string? CareType { get; set; }

    [StringLength(CareRules.MaxContentLength, ErrorMessage = "Validation_ContentTooLong")]
    [Display(Name = "Common_Content")]
    public string? Content { get; set; }

    [StringLength(CareRules.MaxResultLength, ErrorMessage = "Validation_ResultTooLong")]
    [Display(Name = "Common_Result")]
    public string? Result { get; set; }

    [Range(1, 5, ErrorMessage = "Validation_SurveyScoreInvalid")]
    [Display(Name = "Care_Score")]
    public byte? SatisfactionScore { get; set; }

    [Display(Name = "Care_NextAppointment")]
    [DataType(DataType.Date)]
    public DateOnly? NextAppointmentDate { get; set; }

    /// <summary>The care date shown on the form (read-only) and used for the server-side rule.</summary>
    public DateOnly CareDate { get; init; } = DateOnly.FromDateTime(DateTime.Today);

    /// <summary>Where to go after a successful save; only a local address is accepted.</summary>
    public string? ReturnUrl { get; set; }

    public IReadOnlyList<CustomerOption> Customers { get; set; } = [];

    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        if (CareType is not null && !WorkConstants.CareTypes.All.Contains(CareType))
        {
            yield return new ValidationResult(
                "Validation_CareTypeInvalid", [nameof(CareType)]);
        }

        if (SatisfactionScore is not null && !CareRules.IsSurvey(CareType))
        {
            yield return new ValidationResult(
                "Validation_SurveyScoreInvalid", [nameof(SatisfactionScore)]);
        }

        if (!CareRules.AppointmentIsValid(CareDate, NextAppointmentDate))
        {
            yield return new ValidationResult(
                "Error_NextAppointmentBeforeCare", [nameof(NextAppointmentDate)]);
        }
    }
}
