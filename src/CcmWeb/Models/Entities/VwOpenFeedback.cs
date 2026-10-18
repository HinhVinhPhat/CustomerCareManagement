using System;
using System.Collections.Generic;

namespace CcmWeb.Models.Entities;

public partial class VwOpenFeedback
{
    public int FeedbackId { get; set; }

    public int CustomerId { get; set; }

    public string CustomerName { get; set; } = null!;

    public string FeedbackType { get; set; } = null!;

    public string? Content { get; set; }

    public byte Priority { get; set; }

    public string PriorityName { get; set; } = null!;

    public string FeedbackStatus { get; set; } = null!;

    public int? EmployeeId { get; set; }

    public string? FullName { get; set; }

    public DateTime CreatedTime { get; set; }

    public DateOnly? DueDate { get; set; }

    public int IsOverdue { get; set; }
}
