using System;
using System.Collections.Generic;

namespace CcmWeb.Models.Entities;

public partial class Employee
{
    public int EmployeeId { get; set; }

    public string FullName { get; set; } = null!;

    public string Email { get; set; } = null!;

    public string Phone { get; set; } = null!;

    public string Gender { get; set; } = null!;

    public string Position { get; set; } = null!;

    public int DepartmentId { get; set; }

    public virtual Account? Account { get; set; }

    public virtual ICollection<AuditLog> AuditLogs { get; set; } = new List<AuditLog>();

    public virtual ICollection<Care> Cares { get; set; } = new List<Care>();

    public virtual ICollection<Contact> Contacts { get; set; } = new List<Contact>();

    public virtual ICollection<Contract> Contracts { get; set; } = new List<Contract>();

    public virtual ICollection<Customer> Customers { get; set; } = new List<Customer>();

    public virtual Department Department { get; set; } = null!;

    public virtual ICollection<Feedback> Feedbacks { get; set; } = new List<Feedback>();

    public virtual ICollection<Setting> Settings { get; set; } = new List<Setting>();
}
