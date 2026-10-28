using System.ComponentModel.DataAnnotations;
using System.Globalization;
using CcmWeb.Helpers;

namespace CcmWeb.Models.ViewModels;

/// <summary>One row of the feedback list.</summary>
public sealed record FeedbackRow(
    int FeedbackId,
    int CustomerId,
    string CustomerName,
    string FeedbackType,
    string? Content,
    byte Priority,
    string FeedbackStatus,
    int? EmployeeId,
    string? EmployeeName,
    DateTime CreatedTime,
    DateOnly? DueDate,
    bool IsOverdue);

/// <summary>The three numbers of the counter row (design.md 6.11).</summary>
public sealed record FeedbackCounters(int New, int InProgress, int Overdue);

/// <summary>
/// The state of the feedback list. No status means "open", which is exactly what
/// <c>vw_OpenFeedback</c> returns; Resolved and Closed are read from <c>FEEDBACK</c> because the view
/// deliberately carries open items only.
/// </summary>
public sealed class FeedbackFilter
{
    public const string SortByPriority = "Priority";
    public const string SortByDueDate = "DueDate";
    public const string SortByCreatedTime = "CreatedTime";
    public const string SortByCustomer = "Customer";

    public static readonly string[] SortFields =
        [SortByPriority, SortByDueDate, SortByCreatedTime, SortByCustomer];

    public string? Q { get; init; }

    /// <summary>One of <c>CHK_Feedback_Status</c>; null = open (New and InProgress).</summary>
    public string? Status { get; init; }

    public byte? Priority { get; init; }

    public string? Type { get; init; }

    /// <summary>Only the open items whose due date has passed.</summary>
    public bool Overdue { get; init; }

    public string Sort { get; init; } = SortByPriority;
    public bool Desc { get; init; }
    public int Page { get; init; } = 1;
    public int PageSize { get; init; } = WorkConstants.Settings.DefaultPageSize;

    public bool HasFilters =>
        !string.IsNullOrWhiteSpace(Q) || Status is not null || Priority is not null
        || Type is not null || Overdue;

    public string SortQuery(string field) =>
        Build(field, Sort == field && !Desc, 1, PageSize);

    public string PageQuery(int page) => Build(Sort, Desc, page, PageSize);

    public string PageSizeQuery(int size) => Build(Sort, Desc, 1, size);

    private string Build(string sort, bool desc, int page, int pageSize)
    {
        var parameters = new List<string>();

        if (!string.IsNullOrWhiteSpace(Q))
        {
            parameters.Add($"q={Uri.EscapeDataString(Q)}");
        }

        if (Status is not null)
        {
            parameters.Add($"status={Uri.EscapeDataString(Status)}");
        }

        if (Priority is { } priority)
        {
            parameters.Add($"priority={priority.ToString(CultureInfo.InvariantCulture)}");
        }

        if (Type is not null)
        {
            parameters.Add($"type={Uri.EscapeDataString(Type)}");
        }

        if (Overdue)
        {
            parameters.Add("overdue=true");
        }

        if (sort != SortByPriority)
        {
            parameters.Add($"sort={Uri.EscapeDataString(sort)}");
        }

        if (desc)
        {
            parameters.Add("desc=true");
        }

        if (page > 1)
        {
            parameters.Add($"page={page.ToString(CultureInfo.InvariantCulture)}");
        }

        if (pageSize != WorkConstants.Settings.DefaultPageSize)
        {
            parameters.Add($"pageSize={pageSize.ToString(CultureInfo.InvariantCulture)}");
        }

        return parameters.Count == 0 ? string.Empty : $"?{string.Join('&', parameters)}";
    }
}

/// <summary>The feedback list page: counters, filter, rows and pager facts.</summary>
public sealed class FeedbackListViewModel
{
    public required FeedbackFilter Filter { get; init; }

    public IReadOnlyList<FeedbackRow> Rows { get; init; } = [];

    public IReadOnlyList<EmployeeOption> Staff { get; init; } = [];

    public FeedbackCounters Counters { get; init; } = new(0, 0, 0);

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

/// <summary>
/// The new-feedback form (design.md 6.11). A new item starts as <c>New</c> with no assignee and the
/// server clock as its created time; those are not form fields.
/// </summary>
public sealed class FeedbackCreateViewModel : IValidatableObject
{
    [Required(ErrorMessage = "Validation_FeedbackCustomerRequired")]
    [Display(Name = "Common_Customer")]
    public int? CustomerId { get; set; }

    [Required(ErrorMessage = "Validation_FeedbackTypeRequired")]
    [Display(Name = "Feedback_Type")]
    public string? FeedbackType { get; set; }

    [StringLength(FeedbackRules.MaxContentLength, ErrorMessage = "Validation_FeedbackContentTooLong")]
    [Display(Name = "Feedback_Content")]
    public string? Content { get; set; }

    [Display(Name = "Feedback_Priority")]
    public byte Priority { get; set; } = WorkConstants.FeedbackPriorities.Medium;

    [Display(Name = "Feedback_DueDate")]
    [DataType(DataType.Date)]
    public DateOnly? DueDate { get; set; }

    public IReadOnlyList<CustomerOption> Customers { get; set; } = [];

    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        if (FeedbackType is not null && !WorkConstants.FeedbackTypes.All.Contains(FeedbackType))
        {
            yield return new ValidationResult(
                "Validation_FeedbackTypeInvalid", [nameof(FeedbackType)]);
        }

        if (!WorkConstants.FeedbackPriorities.All.Contains(Priority))
        {
            yield return new ValidationResult(
                "Validation_FeedbackPriorityInvalid", [nameof(Priority)]);
        }

        if (DueDate is { } due && due < DateOnly.FromDateTime(DateTime.Today))
        {
            yield return new ValidationResult(
                "Validation_FeedbackDueDateInvalid", [nameof(DueDate)]);
        }
    }
}

/// <summary>The feedback detail page (design.md 6.11): the facts, the assign and status actions.</summary>
public sealed class FeedbackDetailViewModel
{
    public int FeedbackId { get; init; }
    public int CustomerId { get; init; }
    public string CustomerName { get; init; } = string.Empty;
    public string? CustomerPhone { get; init; }
    public string? CustomerEmail { get; init; }
    public int? EmployeeId { get; init; }
    public string? EmployeeName { get; init; }
    public DateTime CreatedTime { get; init; }
    public string FeedbackType { get; init; } = string.Empty;
    public string? Content { get; init; }
    public byte Priority { get; init; }
    public string FeedbackStatus { get; init; } = string.Empty;
    public DateOnly? DueDate { get; init; }
    public DateOnly? ResolvedDate { get; init; }
    public string? Note { get; init; }

    /// <summary>Open and past its due date; the same rule <c>vw_OpenFeedback.IsOverdue</c> uses.</summary>
    public bool IsOverdue { get; init; }

    public IReadOnlyList<EmployeeOption> Staff { get; init; } = [];
}
