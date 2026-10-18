using System;
using System.Collections.Generic;
using CcmWeb.Models.Entities;
using Microsoft.EntityFrameworkCore;

namespace CcmWeb.Models.EF;

public partial class ApplicationDbContext : DbContext
{
    public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
        : base(options)
    {
    }

    public virtual DbSet<Account> Accounts { get; set; }

    public virtual DbSet<AuditLog> AuditLogs { get; set; }

    public virtual DbSet<Care> Cares { get; set; }

    public virtual DbSet<Contact> Contacts { get; set; }

    public virtual DbSet<Contract> Contracts { get; set; }

    public virtual DbSet<ContractDetail> ContractDetails { get; set; }

    public virtual DbSet<Customer> Customers { get; set; }

    public virtual DbSet<Department> Departments { get; set; }

    public virtual DbSet<Employee> Employees { get; set; }

    public virtual DbSet<Feedback> Feedbacks { get; set; }

    public virtual DbSet<ProductService> ProductServices { get; set; }

    public virtual DbSet<Setting> Settings { get; set; }

    public virtual DbSet<VwCareSchedule> VwCareSchedules { get; set; }

    public virtual DbSet<VwContractDetail> VwContractDetails { get; set; }

    public virtual DbSet<VwContractInfo> VwContractInfos { get; set; }

    public virtual DbSet<VwCustomerRevenue> VwCustomerRevenues { get; set; }

    public virtual DbSet<VwEffectiveSetting> VwEffectiveSettings { get; set; }

    public virtual DbSet<VwExpiringContract> VwExpiringContracts { get; set; }

    public virtual DbSet<VwInteractionHistory> VwInteractionHistories { get; set; }

    public virtual DbSet<VwOpenFeedback> VwOpenFeedbacks { get; set; }

    public virtual DbSet<VwSystemSetting> VwSystemSettings { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Account>(entity =>
        {
            entity.HasKey(e => e.AccountId).HasName("PK__ACCOUNT__349DA58650CB7B64");

            entity.ToTable("ACCOUNT");

            entity.HasIndex(e => e.Username, "UQ__ACCOUNT__536C85E4DC43749E").IsUnique();

            entity.HasIndex(e => e.EmployeeId, "UQ__ACCOUNT__7AD04FF0EAF276F5").IsUnique();

            entity.Property(e => e.AccountId).HasColumnName("AccountID");
            entity.Property(e => e.EmployeeId).HasColumnName("EmployeeID");
            entity.Property(e => e.IsActive).HasDefaultValue(true);
            entity.Property(e => e.PasswordHash)
                .HasMaxLength(255)
                .IsUnicode(false);
            entity.Property(e => e.Role).HasMaxLength(30);
            entity.Property(e => e.Username).HasMaxLength(50);

            entity.HasOne(d => d.Employee).WithOne(p => p.Account)
                .HasForeignKey<Account>(d => d.EmployeeId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_Account_Employee");
        });

        modelBuilder.Entity<AuditLog>(entity =>
        {
            entity.HasKey(e => e.LogId).HasName("PK__AUDIT_LO__5E5499A816E6D2F3");

            entity.ToTable("AUDIT_LOG");

            entity.HasIndex(e => new { e.TableName, e.RecordId }, "IDX_AuditLog_Table_Record");

            entity.Property(e => e.LogId).HasColumnName("LogID");
            entity.Property(e => e.Action).HasMaxLength(30);
            entity.Property(e => e.Detail).HasMaxLength(500);
            entity.Property(e => e.EmployeeId).HasColumnName("EmployeeID");
            entity.Property(e => e.LogTime)
                .HasPrecision(0)
                .HasDefaultValueSql("(sysdatetime())");
            entity.Property(e => e.RecordId).HasColumnName("RecordID");
            entity.Property(e => e.TableName).HasMaxLength(50);

            entity.HasOne(d => d.Employee).WithMany(p => p.AuditLogs)
                .HasForeignKey(d => d.EmployeeId)
                .HasConstraintName("FK_AuditLog_Employee");
        });

        modelBuilder.Entity<Care>(entity =>
        {
            entity.HasKey(e => e.CareId).HasName("PK__CARE__544993E78D7209BC");

            entity.ToTable("CARE");

            entity.HasIndex(e => new { e.CustomerId, e.CareDate }, "IDX_Care_CustomerID_CareDate");

            entity.HasIndex(e => e.EmployeeId, "IDX_Care_EmployeeID");

            entity.HasIndex(e => e.NextAppointmentDate, "IDX_Care_NextAppointmentDate");

            entity.Property(e => e.CareId).HasColumnName("CareID");
            entity.Property(e => e.CareDate).HasDefaultValueSql("(getdate())");
            entity.Property(e => e.CareType).HasMaxLength(80);
            entity.Property(e => e.Content).HasMaxLength(1000);
            entity.Property(e => e.CustomerId).HasColumnName("CustomerID");
            entity.Property(e => e.EmployeeId).HasColumnName("EmployeeID");
            entity.Property(e => e.Result).HasMaxLength(200);

            entity.HasOne(d => d.Customer).WithMany(p => p.Cares)
                .HasForeignKey(d => d.CustomerId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_Care_Customer");

            entity.HasOne(d => d.Employee).WithMany(p => p.Cares)
                .HasForeignKey(d => d.EmployeeId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_Care_Employee");
        });

        modelBuilder.Entity<Contact>(entity =>
        {
            entity.HasKey(e => e.ContactId).HasName("PK__CONTACT__5C6625BBF94A059A");

            entity.ToTable("CONTACT");

            entity.HasIndex(e => new { e.CustomerId, e.ContactTime }, "IDX_Contact_CustomerID_ContactTime");

            entity.HasIndex(e => e.EmployeeId, "IDX_Contact_EmployeeID");

            entity.Property(e => e.ContactId).HasColumnName("ContactID");
            entity.Property(e => e.Channel).HasMaxLength(50);
            entity.Property(e => e.ContactTime)
                .HasDefaultValueSql("(getdate())")
                .HasColumnType("datetime");
            entity.Property(e => e.Content).HasMaxLength(1000);
            entity.Property(e => e.CustomerId).HasColumnName("CustomerID");
            entity.Property(e => e.EmployeeId).HasColumnName("EmployeeID");
            entity.Property(e => e.Result).HasMaxLength(200);

            entity.HasOne(d => d.Customer).WithMany(p => p.Contacts)
                .HasForeignKey(d => d.CustomerId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_Contact_Customer");

            entity.HasOne(d => d.Employee).WithMany(p => p.Contacts)
                .HasForeignKey(d => d.EmployeeId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_Contact_Employee");
        });

        modelBuilder.Entity<Contract>(entity =>
        {
            entity.HasKey(e => e.ContractId).HasName("PK__CONTRACT__C90D3409268B277D");

            entity.ToTable("CONTRACT", tb =>
                {
                    tb.HasTrigger("trg_Contract_AuditLog");
                    tb.HasTrigger("trg_Contract_UpdateTier");
                });

            entity.HasIndex(e => new { e.CustomerId, e.Status }, "IDX_Contract_CustomerID_Status");

            entity.HasIndex(e => e.EmployeeId, "IDX_Contract_EmployeeID");

            entity.HasIndex(e => e.SignDate, "IDX_Contract_SignDate");

            entity.HasIndex(e => new { e.Status, e.EndDate }, "IDX_Contract_Status_EndDate");

            entity.Property(e => e.ContractId).HasColumnName("ContractID");
            entity.Property(e => e.CustomerId).HasColumnName("CustomerID");
            entity.Property(e => e.EmployeeId).HasColumnName("EmployeeID");
            entity.Property(e => e.Note).HasMaxLength(500);
            entity.Property(e => e.SignDate).HasDefaultValueSql("(getdate())");
            entity.Property(e => e.Status)
                .HasMaxLength(30)
                .HasDefaultValue("Active");
            entity.Property(e => e.TotalValue).HasColumnType("decimal(18, 2)");

            entity.HasOne(d => d.Customer).WithMany(p => p.Contracts)
                .HasForeignKey(d => d.CustomerId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_Contract_Customer");

            entity.HasOne(d => d.Employee).WithMany(p => p.Contracts)
                .HasForeignKey(d => d.EmployeeId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_Contract_Employee");
        });

        modelBuilder.Entity<ContractDetail>(entity =>
        {
            entity.HasKey(e => new { e.ContractId, e.ProductId }).HasName("PK_ContractDetail");

            entity.ToTable("CONTRACT_DETAIL", tb => tb.HasTrigger("trg_ContractDetail_UpdateTotal"));

            entity.HasIndex(e => e.ProductId, "IDX_ContractDetail_ProductID");

            entity.Property(e => e.ContractId).HasColumnName("ContractID");
            entity.Property(e => e.ProductId).HasColumnName("ProductID");
            entity.Property(e => e.Quantity).HasDefaultValue(1);
            entity.Property(e => e.UnitPrice).HasColumnType("decimal(18, 2)");

            entity.HasOne(d => d.Contract).WithMany(p => p.ContractDetails)
                .HasForeignKey(d => d.ContractId)
                .HasConstraintName("FK_ContractDetail_Contract");

            entity.HasOne(d => d.Product).WithMany(p => p.ContractDetails)
                .HasForeignKey(d => d.ProductId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_ContractDetail_Product");
        });

        modelBuilder.Entity<Customer>(entity =>
        {
            entity.HasKey(e => e.CustomerId).HasName("PK__CUSTOMER__A4AE64B8BBB182D3");

            entity.ToTable("CUSTOMER", tb => tb.HasTrigger("trg_Customer_PreventDelete"));

            entity.HasIndex(e => e.OwnerEmployeeId, "IDX_Customer_Owner");

            entity.HasIndex(e => e.Email, "UX_Customer_Email")
                .IsUnique()
                .HasFilter("([Email] IS NOT NULL)");

            entity.Property(e => e.CustomerId).HasColumnName("CustomerID");
            entity.Property(e => e.Address).HasMaxLength(255);
            entity.Property(e => e.CreatedDate).HasDefaultValueSql("(getdate())");
            entity.Property(e => e.CustomerName).HasMaxLength(100);
            entity.Property(e => e.CustomerType)
                .HasMaxLength(20)
                .HasDefaultValue("Individual");
            entity.Property(e => e.Email).HasMaxLength(100);
            entity.Property(e => e.Gender).HasMaxLength(10);
            entity.Property(e => e.IsActive).HasDefaultValue(true);
            entity.Property(e => e.OwnerEmployeeId).HasColumnName("OwnerEmployeeID");
            entity.Property(e => e.Phone).HasMaxLength(15);
            entity.Property(e => e.Source).HasMaxLength(50);
            entity.Property(e => e.Tier)
                .HasMaxLength(50)
                .HasDefaultValue("Standard");

            entity.HasOne(d => d.OwnerEmployee).WithMany(p => p.Customers)
                .HasForeignKey(d => d.OwnerEmployeeId)
                .HasConstraintName("FK_Customer_Owner");
        });

        modelBuilder.Entity<Department>(entity =>
        {
            entity.HasKey(e => e.DepartmentId).HasName("PK__DEPARTME__B2079BCD6784CF8B");

            entity.ToTable("DEPARTMENT");

            entity.HasIndex(e => e.DepartmentName, "UQ__DEPARTME__D949CC342DF31CF5").IsUnique();

            entity.Property(e => e.DepartmentId).HasColumnName("DepartmentID");
            entity.Property(e => e.DepartmentName).HasMaxLength(100);
        });

        modelBuilder.Entity<Employee>(entity =>
        {
            entity.HasKey(e => e.EmployeeId).HasName("PK__EMPLOYEE__7AD04FF1292F82FC");

            entity.ToTable("EMPLOYEE");

            entity.HasIndex(e => e.DepartmentId, "IDX_Employee_DepartmentID");

            entity.HasIndex(e => e.Phone, "UQ__EMPLOYEE__5C7E359E8C66F46E").IsUnique();

            entity.HasIndex(e => e.Email, "UQ__EMPLOYEE__A9D105346812C20E").IsUnique();

            entity.HasIndex(e => e.DepartmentId, "UX_Employee_DepartmentManager")
                .IsUnique()
                .HasFilter("([Position]=N'Manager')");

            entity.Property(e => e.EmployeeId).HasColumnName("EmployeeID");
            entity.Property(e => e.DepartmentId).HasColumnName("DepartmentID");
            entity.Property(e => e.Email).HasMaxLength(100);
            entity.Property(e => e.FullName).HasMaxLength(100);
            entity.Property(e => e.Gender).HasMaxLength(10);
            entity.Property(e => e.Phone).HasMaxLength(15);
            entity.Property(e => e.Position).HasMaxLength(50);

            entity.HasOne(d => d.Department).WithOne(p => p.Employee)
                .HasForeignKey<Employee>(d => d.DepartmentId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_Employee_Department");
        });

        modelBuilder.Entity<Feedback>(entity =>
        {
            entity.HasKey(e => e.FeedbackId).HasName("PK__FEEDBACK__6A4BEDF6DDABB00D");

            entity.ToTable("FEEDBACK", tb =>
                {
                    tb.HasTrigger("trg_Feedback_AuditLog");
                    tb.HasTrigger("trg_Feedback_FillResolvedDate");
                });

            entity.HasIndex(e => e.CustomerId, "IDX_Feedback_CustomerID");

            entity.HasIndex(e => e.EmployeeId, "IDX_Feedback_EmployeeID");

            entity.HasIndex(e => e.FeedbackStatus, "IDX_Feedback_Status");

            entity.Property(e => e.FeedbackId).HasColumnName("FeedbackID");
            entity.Property(e => e.Content).HasMaxLength(2000);
            entity.Property(e => e.CreatedTime)
                .HasDefaultValueSql("(getdate())")
                .HasColumnType("datetime");
            entity.Property(e => e.CustomerId).HasColumnName("CustomerID");
            entity.Property(e => e.EmployeeId).HasColumnName("EmployeeID");
            entity.Property(e => e.FeedbackStatus)
                .HasMaxLength(50)
                .HasDefaultValue("New");
            entity.Property(e => e.FeedbackType).HasMaxLength(50);
            entity.Property(e => e.Note).HasMaxLength(500);
            entity.Property(e => e.Priority).HasDefaultValue((byte)2);

            entity.HasOne(d => d.Customer).WithMany(p => p.Feedbacks)
                .HasForeignKey(d => d.CustomerId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_Feedback_Customer");

            entity.HasOne(d => d.Employee).WithMany(p => p.Feedbacks)
                .HasForeignKey(d => d.EmployeeId)
                .HasConstraintName("FK_Feedback_Employee");
        });

        modelBuilder.Entity<ProductService>(entity =>
        {
            entity.HasKey(e => e.ProductId).HasName("PK__PRODUCT___B40CC6ED36398D74");

            entity.ToTable("PRODUCT_SERVICE");

            entity.HasIndex(e => e.ProductName, "UQ__PRODUCT___DD5A978A8E84BE20").IsUnique();

            entity.Property(e => e.ProductId).HasColumnName("ProductID");
            entity.Property(e => e.Description).HasMaxLength(500);
            entity.Property(e => e.ProductName).HasMaxLength(150);
            entity.Property(e => e.ProductType).HasMaxLength(50);
            entity.Property(e => e.UnitPrice).HasColumnType("decimal(18, 2)");
        });

        modelBuilder.Entity<Setting>(entity =>
        {
            entity.HasKey(e => e.SettingId).HasName("PK__SETTINGS__54372AFD499937FA");

            entity.ToTable("SETTINGS");

            entity.HasIndex(e => new { e.EmployeeId, e.SettingKey }, "UQ_Settings_Scope").IsUnique();

            entity.Property(e => e.SettingId).HasColumnName("SettingID");
            entity.Property(e => e.Category)
                .HasMaxLength(30)
                .IsUnicode(false);
            entity.Property(e => e.DataType)
                .HasMaxLength(20)
                .IsUnicode(false);
            entity.Property(e => e.Description).HasMaxLength(300);
            entity.Property(e => e.EmployeeId).HasColumnName("EmployeeID");
            entity.Property(e => e.SettingKey).HasMaxLength(50);
            entity.Property(e => e.SettingValue).HasMaxLength(100);
            entity.Property(e => e.UpdatedDate)
                .HasDefaultValueSql("(sysdatetime())")
                .HasColumnType("datetime");

            entity.HasOne(d => d.Employee).WithMany(p => p.Settings)
                .HasForeignKey(d => d.EmployeeId)
                .HasConstraintName("FK_Settings_Employee");
        });

        modelBuilder.Entity<VwCareSchedule>(entity =>
        {
            entity
                .HasNoKey()
                .ToView("vw_CareSchedule");

            entity.Property(e => e.CareId).HasColumnName("CareID");
            entity.Property(e => e.CareType).HasMaxLength(80);
            entity.Property(e => e.Content).HasMaxLength(1000);
            entity.Property(e => e.CustomerId).HasColumnName("CustomerID");
            entity.Property(e => e.CustomerName).HasMaxLength(100);
            entity.Property(e => e.EmployeeId).HasColumnName("EmployeeID");
            entity.Property(e => e.FullName).HasMaxLength(100);
            entity.Property(e => e.Phone).HasMaxLength(15);
            entity.Property(e => e.ScheduleStatus).HasMaxLength(8);
        });

        modelBuilder.Entity<VwContractDetail>(entity =>
        {
            entity
                .HasNoKey()
                .ToView("vw_ContractDetail");

            entity.Property(e => e.ContractId).HasColumnName("ContractID");
            entity.Property(e => e.LineTotal).HasColumnType("decimal(29, 2)");
            entity.Property(e => e.ProductId).HasColumnName("ProductID");
            entity.Property(e => e.ProductName).HasMaxLength(150);
            entity.Property(e => e.ProductType).HasMaxLength(50);
            entity.Property(e => e.UnitPrice).HasColumnType("decimal(18, 2)");
        });

        modelBuilder.Entity<VwContractInfo>(entity =>
        {
            entity
                .HasNoKey()
                .ToView("vw_ContractInfo");

            entity.Property(e => e.ContractId).HasColumnName("ContractID");
            entity.Property(e => e.CustomerId).HasColumnName("CustomerID");
            entity.Property(e => e.CustomerName).HasMaxLength(100);
            entity.Property(e => e.DepartmentName).HasMaxLength(100);
            entity.Property(e => e.EmployeeId).HasColumnName("EmployeeID");
            entity.Property(e => e.FullName).HasMaxLength(100);
            entity.Property(e => e.Note).HasMaxLength(500);
            entity.Property(e => e.Status).HasMaxLength(30);
            entity.Property(e => e.TotalValue).HasColumnType("decimal(18, 2)");
        });

        modelBuilder.Entity<VwCustomerRevenue>(entity =>
        {
            entity
                .HasNoKey()
                .ToView("vw_CustomerRevenue");

            entity.Property(e => e.CustomerId).HasColumnName("CustomerID");
            entity.Property(e => e.CustomerName).HasMaxLength(100);
            entity.Property(e => e.CustomerType).HasMaxLength(20);
            entity.Property(e => e.OwnerEmployeeId).HasColumnName("OwnerEmployeeID");
            entity.Property(e => e.Tier).HasMaxLength(50);
            entity.Property(e => e.TotalValue).HasColumnType("decimal(38, 2)");
        });

        modelBuilder.Entity<VwEffectiveSetting>(entity =>
        {
            entity
                .HasNoKey()
                .ToView("vw_EffectiveSettings");

            entity.Property(e => e.Category)
                .HasMaxLength(30)
                .IsUnicode(false);
            entity.Property(e => e.DataType)
                .HasMaxLength(20)
                .IsUnicode(false);
            entity.Property(e => e.DepartmentId).HasColumnName("DepartmentID");
            entity.Property(e => e.Description).HasMaxLength(300);
            entity.Property(e => e.EmployeeId).HasColumnName("EmployeeID");
            entity.Property(e => e.FullName).HasMaxLength(100);
            entity.Property(e => e.SettingKey).HasMaxLength(50);
            entity.Property(e => e.SettingValue).HasMaxLength(100);
            entity.Property(e => e.UpdatedDate).HasColumnType("datetime");
        });

        modelBuilder.Entity<VwExpiringContract>(entity =>
        {
            entity
                .HasNoKey()
                .ToView("vw_ExpiringContract");

            entity.Property(e => e.ContractId).HasColumnName("ContractID");
            entity.Property(e => e.CustomerId).HasColumnName("CustomerID");
            entity.Property(e => e.CustomerName).HasMaxLength(100);
            entity.Property(e => e.EmployeeId).HasColumnName("EmployeeID");
            entity.Property(e => e.FullName).HasMaxLength(100);
            entity.Property(e => e.Phone).HasMaxLength(15);
            entity.Property(e => e.TotalValue).HasColumnType("decimal(18, 2)");
        });

        modelBuilder.Entity<VwInteractionHistory>(entity =>
        {
            entity
                .HasNoKey()
                .ToView("vw_InteractionHistory");

            entity.Property(e => e.Category).HasMaxLength(8);
            entity.Property(e => e.Content).HasMaxLength(2000);
            entity.Property(e => e.CustomerId).HasColumnName("CustomerID");
            entity.Property(e => e.EmployeeId).HasColumnName("EmployeeID");
            entity.Property(e => e.FullName).HasMaxLength(100);
            entity.Property(e => e.LogTime).HasColumnType("datetime");
            entity.Property(e => e.Result).HasMaxLength(200);
            entity.Property(e => e.SubType).HasMaxLength(80);
        });

        modelBuilder.Entity<VwOpenFeedback>(entity =>
        {
            entity
                .HasNoKey()
                .ToView("vw_OpenFeedback");

            entity.Property(e => e.Content).HasMaxLength(2000);
            entity.Property(e => e.CreatedTime).HasColumnType("datetime");
            entity.Property(e => e.CustomerId).HasColumnName("CustomerID");
            entity.Property(e => e.CustomerName).HasMaxLength(100);
            entity.Property(e => e.EmployeeId).HasColumnName("EmployeeID");
            entity.Property(e => e.FeedbackId).HasColumnName("FeedbackID");
            entity.Property(e => e.FeedbackStatus).HasMaxLength(50);
            entity.Property(e => e.FeedbackType).HasMaxLength(50);
            entity.Property(e => e.FullName).HasMaxLength(100);
            entity.Property(e => e.PriorityName).HasMaxLength(6);
        });

        modelBuilder.Entity<VwSystemSetting>(entity =>
        {
            entity
                .HasNoKey()
                .ToView("vw_SystemSettings");

            entity.Property(e => e.Category)
                .HasMaxLength(30)
                .IsUnicode(false);
            entity.Property(e => e.DataType)
                .HasMaxLength(20)
                .IsUnicode(false);
            entity.Property(e => e.Description).HasMaxLength(300);
            entity.Property(e => e.SettingKey).HasMaxLength(50);
            entity.Property(e => e.SettingValue).HasMaxLength(100);
            entity.Property(e => e.UpdatedDate).HasColumnType("datetime");
        });

        OnModelCreatingPartial(modelBuilder);
    }

    partial void OnModelCreatingPartial(ModelBuilder modelBuilder);
}
