using CcmWeb.Helpers;
using CcmWeb.Models.EF;
using CcmWeb.Models.Entities;
using CcmWeb.Models.ViewModels;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;

namespace CcmWeb.Services;

/// <summary>
/// The feedback list and detail. "Open" is exactly what <c>vw_OpenFeedback</c> says it is; the
/// resolved and closed filter reads the table directly because the view does not carry those rows.
/// Every status change and assignment is a procedure call, and the service never writes
/// <c>ResolvedDate</c> or <c>AUDIT_LOG</c>.
/// </summary>
public sealed class FeedbackService(ApplicationDbContext db, ILogger<FeedbackService> logger) : IFeedbackService
{
    public async Task<FeedbackSearchResult> SearchAsync(
        FeedbackFilter filter, CancellationToken ct = default)
    {
        if (filter.Status is WorkConstants.FeedbackStatuses.Resolved or WorkConstants.FeedbackStatuses.Closed)
        {
            return await SearchClosedAsync(filter, ct);
        }

        // Open: New and InProgress, the definition the view owns.
        var query = db.VwOpenFeedbacks.AsNoTracking();

        if (filter.Status is not null)
        {
            query = query.Where(v => v.FeedbackStatus == filter.Status);
        }

        if (!string.IsNullOrWhiteSpace(filter.Q))
        {
            var q = filter.Q.Trim();
            query = query.Where(v => v.CustomerName.Contains(q));
        }

        if (filter.Priority is { } priority)
        {
            query = query.Where(v => v.Priority == priority);
        }

        if (filter.Type is not null)
        {
            query = query.Where(v => v.FeedbackType == filter.Type);
        }

        if (filter.Overdue)
        {
            query = query.Where(v => v.IsOverdue == 1);
        }

        query = (filter.Sort, filter.Desc) switch
        {
            (FeedbackFilter.SortByDueDate, false) => query
                .OrderBy(v => v.DueDate == null).ThenBy(v => v.DueDate)
                .ThenBy(v => v.Priority).ThenByDescending(v => v.CreatedTime),
            (FeedbackFilter.SortByDueDate, true) => query
                .OrderByDescending(v => v.DueDate)
                .ThenBy(v => v.Priority).ThenByDescending(v => v.CreatedTime),
            (FeedbackFilter.SortByCreatedTime, false) => query
                .OrderBy(v => v.CreatedTime).ThenBy(v => v.Priority),
            (FeedbackFilter.SortByCreatedTime, true) => query
                .OrderByDescending(v => v.CreatedTime).ThenBy(v => v.Priority),
            (FeedbackFilter.SortByCustomer, false) => query
                .OrderBy(v => v.CustomerName).ThenBy(v => v.Priority)
                .ThenBy(v => v.DueDate == null).ThenBy(v => v.DueDate),
            (FeedbackFilter.SortByCustomer, true) => query
                .OrderByDescending(v => v.CustomerName).ThenBy(v => v.Priority)
                .ThenBy(v => v.DueDate == null).ThenBy(v => v.DueDate),
            (_, true) => query
                .OrderByDescending(v => v.Priority)
                .ThenBy(v => v.DueDate == null).ThenBy(v => v.DueDate)
                .ThenByDescending(v => v.CreatedTime),
            _ => query
                .OrderBy(v => v.Priority)
                .ThenBy(v => v.DueDate == null).ThenBy(v => v.DueDate)
                .ThenByDescending(v => v.CreatedTime),
        };

        var totalRows = await query.CountAsync(ct);

        var rows = await query
            .Skip((filter.Page - 1) * filter.PageSize)
            .Take(filter.PageSize)
            .Select(v => new FeedbackRow(
                v.FeedbackId,
                v.CustomerId,
                v.CustomerName,
                v.FeedbackType,
                v.Content,
                v.Priority,
                v.FeedbackStatus,
                v.EmployeeId,
                v.FullName,
                v.CreatedTime,
                v.DueDate,
                v.IsOverdue == 1))
            .ToListAsync(ct);

        return new FeedbackSearchResult(rows, totalRows);
    }

    private async Task<FeedbackSearchResult> SearchClosedAsync(
        FeedbackFilter filter, CancellationToken ct)
    {
        var query = db.Feedbacks.AsNoTracking()
            .Where(f => f.FeedbackStatus == filter.Status);

        if (!string.IsNullOrWhiteSpace(filter.Q))
        {
            var q = filter.Q.Trim();
            query = query.Where(f => f.Customer.CustomerName.Contains(q));
        }

        if (filter.Priority is { } priority)
        {
            query = query.Where(f => f.Priority == priority);
        }

        if (filter.Type is not null)
        {
            query = query.Where(f => f.FeedbackType == filter.Type);
        }

        query = (filter.Sort, filter.Desc) switch
        {
            (FeedbackFilter.SortByDueDate, false) => query
                .OrderBy(f => f.DueDate == null).ThenBy(f => f.DueDate)
                .ThenBy(f => f.Priority).ThenByDescending(f => f.CreatedTime),
            (FeedbackFilter.SortByDueDate, true) => query
                .OrderByDescending(f => f.DueDate)
                .ThenBy(f => f.Priority).ThenByDescending(f => f.CreatedTime),
            (FeedbackFilter.SortByCreatedTime, false) => query
                .OrderBy(f => f.CreatedTime).ThenBy(f => f.Priority),
            (FeedbackFilter.SortByCreatedTime, true) => query
                .OrderByDescending(f => f.CreatedTime).ThenBy(f => f.Priority),
            (FeedbackFilter.SortByCustomer, false) => query
                .OrderBy(f => f.Customer.CustomerName).ThenBy(f => f.Priority)
                .ThenBy(f => f.DueDate == null).ThenBy(f => f.DueDate),
            (FeedbackFilter.SortByCustomer, true) => query
                .OrderByDescending(f => f.Customer.CustomerName).ThenBy(f => f.Priority)
                .ThenBy(f => f.DueDate == null).ThenBy(f => f.DueDate),
            (_, true) => query
                .OrderByDescending(f => f.Priority)
                .ThenBy(f => f.DueDate == null).ThenBy(f => f.DueDate)
                .ThenByDescending(f => f.CreatedTime),
            _ => query
                .OrderBy(f => f.Priority)
                .ThenBy(f => f.DueDate == null).ThenBy(f => f.DueDate)
                .ThenByDescending(f => f.CreatedTime),
        };

        var totalRows = await query.CountAsync(ct);

        var rows = await query
            .Skip((filter.Page - 1) * filter.PageSize)
            .Take(filter.PageSize)
            .Select(f => new FeedbackRow(
                f.FeedbackId,
                f.CustomerId,
                f.Customer.CustomerName,
                f.FeedbackType,
                f.Content,
                f.Priority,
                f.FeedbackStatus,
                f.EmployeeId,
                f.Employee != null ? f.Employee.FullName : null,
                f.CreatedTime,
                f.DueDate,
                // Resolved and Closed are never "overdue" in the sense of the counter cards.
                false))
            .ToListAsync(ct);

        return new FeedbackSearchResult(rows, totalRows);
    }

    public async Task<FeedbackCounters> GetCountersAsync(CancellationToken ct = default)
    {
        var open = db.VwOpenFeedbacks.AsNoTracking();

        var grouped = await open
            .GroupBy(v => v.FeedbackStatus)
            .Select(g => new { Status = g.Key, Count = g.Count() })
            .ToListAsync(ct);

        var overdue = await open.CountAsync(v => v.IsOverdue == 1, ct);

        return new FeedbackCounters(
            grouped.FirstOrDefault(g => g.Status == WorkConstants.FeedbackStatuses.New)?.Count ?? 0,
            grouped.FirstOrDefault(g => g.Status == WorkConstants.FeedbackStatuses.InProgress)?.Count ?? 0,
            overdue);
    }

    public async Task<FeedbackDetailViewModel?> GetDetailAsync(
        int feedbackId, CancellationToken ct = default)
    {
        var feedback = await db.Feedbacks
            .AsNoTracking()
            .Include(f => f.Customer)
            .Include(f => f.Employee)
            .FirstOrDefaultAsync(f => f.FeedbackId == feedbackId, ct);

        if (feedback is null)
        {
            return null;
        }

        var today = DateOnly.FromDateTime(DateTime.Today);
        var open = feedback.FeedbackStatus is WorkConstants.FeedbackStatuses.New
            or WorkConstants.FeedbackStatuses.InProgress;

        return new FeedbackDetailViewModel
        {
            FeedbackId = feedback.FeedbackId,
            CustomerId = feedback.CustomerId,
            CustomerName = feedback.Customer.CustomerName,
            CustomerPhone = feedback.Customer.Phone,
            CustomerEmail = feedback.Customer.Email,
            EmployeeId = feedback.EmployeeId,
            EmployeeName = feedback.Employee?.FullName,
            CreatedTime = feedback.CreatedTime,
            FeedbackType = feedback.FeedbackType,
            Content = feedback.Content,
            Priority = feedback.Priority,
            FeedbackStatus = feedback.FeedbackStatus,
            DueDate = feedback.DueDate,
            ResolvedDate = feedback.ResolvedDate,
            Note = feedback.Note,
            IsOverdue = open && feedback.DueDate is { } due && due < today,
            Staff = await GetStaffAsync(ct)
        };
    }

    public async Task<int> CreateAsync(
        FeedbackCreateViewModel form, CancellationToken ct = default)
    {
        var customerId = form.CustomerId!.Value;
        await LookupQueries.EnsureActiveCustomerAsync(db, customerId, ct);

        if (!WorkConstants.FeedbackPriorities.All.Contains(form.Priority))
        {
            throw new BusinessException("Validation_FeedbackPriorityInvalid");
        }

        var today = DateOnly.FromDateTime(DateTime.Today);

        if (form.DueDate is { } due && due < today)
        {
            throw new BusinessException("Validation_FeedbackDueDateInvalid");
        }

        var feedback = new Feedback
        {
            CustomerId = customerId,
            // A new item is unassigned; only vw_OpenFeedback/CHK_Feedback_Assignee define what may follow.
            EmployeeId = null,
            CreatedTime = DateTime.Now,
            FeedbackType = form.FeedbackType!,
            Content = string.IsNullOrWhiteSpace(form.Content) ? null : form.Content.Trim(),
            Priority = form.Priority,
            FeedbackStatus = WorkConstants.FeedbackStatuses.New,
            DueDate = form.DueDate,
            Note = null
        };

        db.Feedbacks.Add(feedback);

        try
        {
            await db.SaveChangesAsync(ct);
        }
        catch (DbUpdateException ex)
        {
            logger.LogWarning(ex, "Creating a feedback item failed.");
            throw new BusinessException(Utils.TranslateSqlError(ex) ?? "Error_Unexpected", inner: ex);
        }

        return feedback.FeedbackId;
    }

    public async Task AssignAsync(int feedbackId, int employeeId, CancellationToken ct = default)
    {
        try
        {
            await db.Database.ExecuteSqlRawAsync(
                "EXEC dbo.sp_AssignFeedback @FeedbackID = {0}, @EmployeeID = {1}",
                [feedbackId, employeeId],
                ct);
        }
        catch (SqlException ex)
        {
            logger.LogWarning(ex, "sp_AssignFeedback failed for feedback {FeedbackID}.", feedbackId);
            throw new BusinessException(Utils.TranslateSqlError(ex) ?? "Error_Unexpected", inner: ex);
        }
    }

    public async Task UpdateStatusAsync(
        int feedbackId, string status, string? note, CancellationToken ct = default)
    {
        if (!FeedbackRules.IsValidStatus(status))
        {
            throw new BusinessException("Error_FeedbackStatusInvalid");
        }

        var current = await db.Feedbacks
            .AsNoTracking()
            .Where(f => f.FeedbackId == feedbackId)
            .Select(f => new { f.Note })
            .FirstOrDefaultAsync(ct)
            ?? throw new BusinessException("Error_FeedbackNotFound");

        // The procedure replaces Note; the rule of "add a note" is to append, so compose it here.
        var newNote = string.IsNullOrWhiteSpace(note)
            ? null
            : FeedbackRules.AppendNote(current.Note, note);

        await ExecuteUpdateStatusAsync(feedbackId, status, newNote, ct);
    }

    public async Task AppendNoteAsync(int feedbackId, string note, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(note))
        {
            throw new BusinessException("Validation_FeedbackNoteRequired");
        }

        var current = await db.Feedbacks
            .AsNoTracking()
            .Where(f => f.FeedbackId == feedbackId)
            .Select(f => new { f.FeedbackStatus, f.Note })
            .FirstOrDefaultAsync(ct)
            ?? throw new BusinessException("Error_FeedbackNotFound");

        // Passing the current status changes no state; the procedure still validates the row and a
        // note-only action never touches ResolvedDate (the trigger only reacts to a status change).
        var newNote = FeedbackRules.AppendNote(current.Note, note);

        await ExecuteUpdateStatusAsync(feedbackId, current.FeedbackStatus, newNote, ct);
    }

    private async Task ExecuteUpdateStatusAsync(
        int feedbackId, string status, string? note, CancellationToken ct)
    {
        try
        {
            // A typed parameter, because an untyped DBNull cannot be mapped by EF (and the procedure
            // needs NULL - not an empty string - to leave the existing note alone).
            var noteParameter = new SqlParameter("@Note", System.Data.SqlDbType.NVarChar, FeedbackRules.MaxNoteLength)
            {
                Value = (object?)note ?? DBNull.Value
            };

            await db.Database.ExecuteSqlRawAsync(
                "EXEC dbo.sp_UpdateFeedbackStatus @FeedbackID = @FeedbackID, @Status = @Status, @Note = @Note",
                [
                    new SqlParameter("@FeedbackID", feedbackId),
                    new SqlParameter("@Status", status),
                    noteParameter
                ],
                ct);
        }
        catch (SqlException ex)
        {
            logger.LogWarning(ex, "sp_UpdateFeedbackStatus failed for feedback {FeedbackID}.", feedbackId);
            throw new BusinessException(Utils.TranslateSqlError(ex) ?? "Error_Unexpected", inner: ex);
        }
    }

    public async Task<IReadOnlyList<CustomerOption>> GetCustomersAsync(CancellationToken ct = default) =>
        await LookupQueries.ActiveCustomersAsync(db, ct);

    public async Task<IReadOnlyList<EmployeeOption>> GetStaffAsync(CancellationToken ct = default) =>
        await LookupQueries.StaffAsync(db, ct);
}
