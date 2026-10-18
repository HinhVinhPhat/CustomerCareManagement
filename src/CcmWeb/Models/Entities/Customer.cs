using System;
using System.Collections.Generic;

namespace CcmWeb.Models.Entities;

public partial class Customer
{
    public int CustomerId { get; set; }

    public string CustomerName { get; set; } = null!;

    public string CustomerType { get; set; } = null!;

    public string? Email { get; set; }

    public string? Phone { get; set; }

    public string? Gender { get; set; }

    public DateOnly? DateOfBirth { get; set; }

    public string? Address { get; set; }

    public string? Source { get; set; }

    public DateOnly CreatedDate { get; set; }

    public string Tier { get; set; } = null!;

    public int? OwnerEmployeeId { get; set; }

    public bool IsActive { get; set; }

    public virtual ICollection<Care> Cares { get; set; } = new List<Care>();

    public virtual ICollection<Contact> Contacts { get; set; } = new List<Contact>();

    public virtual ICollection<Contract> Contracts { get; set; } = new List<Contract>();

    public virtual ICollection<Feedback> Feedbacks { get; set; } = new List<Feedback>();

    public virtual Employee? OwnerEmployee { get; set; }
}
