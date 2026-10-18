using System;
using System.Collections.Generic;

namespace CcmWeb.Models.Entities;

public partial class Feedback
{
    public int FeedbackId { get; set; }

    public int CustomerId { get; set; }

    public int? EmployeeId { get; set; }

    public DateTime CreatedTime { get; set; }

    public string FeedbackType { get; set; } = null!;

    public string? Content { get; set; }

    public byte Priority { get; set; }

    public string FeedbackStatus { get; set; } = null!;

    public DateOnly? DueDate { get; set; }

    public DateOnly? ResolvedDate { get; set; }

    public string? Note { get; set; }

    public virtual Customer Customer { get; set; } = null!;

    public virtual Employee? Employee { get; set; }
}
