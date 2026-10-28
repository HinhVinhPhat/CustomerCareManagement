using System.ComponentModel.DataAnnotations;
using CcmWeb.Helpers;

namespace CcmWeb.Models.ViewModels;

/// <summary>
/// The add / edit customer form (design.md 6.4). The strings in the attributes and in the
/// cross-field rules are resource KEYS: the text the user reads is looked up in the language they
/// chose, so this file stays English while the screen is Vietnamese by default.
///
/// The form has no Tier field on purpose: <c>trg_Contract_UpdateTier</c> owns that column, so the
/// application never writes it (AGENTS.md 4). A business customer has no gender and no date of
/// birth, which is why the service clears both instead of trusting what was posted.
/// </summary>
public sealed class CustomerFormViewModel : IValidatableObject
{
    /// <summary>Null on the add form, the customer being edited otherwise.</summary>
    public int? CustomerId { get; set; }

    [Required(ErrorMessage = "Validation_CustomerNameRequired")]
    [StringLength(100, ErrorMessage = "Validation_CustomerNameTooLong")]
    [Display(Name = "Customer_Field_Name")]
    public string CustomerName { get; set; } = string.Empty;

    [Display(Name = "Customer_Field_Type")]
    public string CustomerType { get; set; } = WorkConstants.CustomerTypes.Individual;

    [EmailAddress(ErrorMessage = "Validation_CustomerEmailFormat")]
    [StringLength(100, ErrorMessage = "Validation_CustomerEmailTooLong")]
    [Display(Name = "Customer_Field_Email")]
    public string? Email { get; set; }

    [StringLength(15, ErrorMessage = "Validation_CustomerPhoneTooLong")]
    [Display(Name = "Customer_Field_Phone")]
    public string? Phone { get; set; }

    [Display(Name = "Customer_Field_Gender")]
    public string? Gender { get; set; }

    [Display(Name = "Customer_Field_DateOfBirth")]
    [DataType(DataType.Date)]
    public DateOnly? DateOfBirth { get; set; }

    [StringLength(255, ErrorMessage = "Validation_CustomerAddressTooLong")]
    [Display(Name = "Customer_Field_Address")]
    public string? Address { get; set; }

    [StringLength(50, ErrorMessage = "Validation_CustomerSourceTooLong")]
    [Display(Name = "Customer_Field_Source")]
    public string? Source { get; set; }

    [Display(Name = "Customer_Field_Owner")]
    public int? OwnerEmployeeId { get; set; }

    /// <summary>The employees the owner dropdown offers.</summary>
    public IReadOnlyList<EmployeeOption> Owners { get; set; } = [];

    /// <summary>Suggestions for the free-text source field (business data, so it stays Vietnamese).</summary>
    public IReadOnlyList<string> SourceSuggestions { get; } = WorkConstants.CustomerSources;

    public bool IsEdit => CustomerId is not null;

    public bool IsBusiness => WorkConstants.CustomerTypes.IsBusiness(CustomerType);

    /// <summary>
    /// The rules the database also enforces (<c>CHK_Customer_Contact</c>, <c>CHK_Customer_Phone</c>,
    /// <c>CHK_Customer_DateOfBirth</c>). They are checked here so the message appears next to the
    /// field that has to change, in the user's language, before anything is written.
    /// </summary>
    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        if (!WorkConstants.CustomerTypes.All.Contains(CustomerType))
        {
            yield return new ValidationResult("Validation_CustomerTypeInvalid", [nameof(CustomerType)]);
        }

        if (!string.IsNullOrWhiteSpace(Gender) && !WorkConstants.CustomerGenders.All.Contains(Gender))
        {
            yield return new ValidationResult("Validation_CustomerGenderInvalid", [nameof(Gender)]);
        }

        // At least an e-mail or a phone number: a customer nobody can reach is not a customer.
        if (string.IsNullOrWhiteSpace(Email) && string.IsNullOrWhiteSpace(Phone))
        {
            yield return new ValidationResult("Error_CustomerNeedsContact", [nameof(Email)]);
        }

        if (!string.IsNullOrWhiteSpace(Phone)
            && (Phone.Length is < 9 or > 11 || Phone.Any(character => !char.IsAsciiDigit(character))))
        {
            yield return new ValidationResult("Error_CustomerPhoneInvalid", [nameof(Phone)]);
        }

        // DateOfBirth < CreatedDate in the database, so a date of birth from today on is refused.
        if (DateOfBirth is { } dateOfBirth && dateOfBirth >= DateOnly.FromDateTime(DateTime.Today))
        {
            yield return new ValidationResult("Error_CustomerBirthDateInvalid", [nameof(DateOfBirth)]);
        }

        if (OwnerEmployeeId is { } owner && Owners.Count > 0 && Owners.All(o => o.EmployeeId != owner))
        {
            yield return new ValidationResult("Validation_CustomerOwnerInvalid", [nameof(OwnerEmployeeId)]);
        }
    }
}
