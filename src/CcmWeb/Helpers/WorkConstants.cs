namespace CcmWeb.Helpers;

using System.Globalization;

/// <summary>
/// Values shared with the database CHECK constraints and the role names.
/// They are added module by module: a list lives here only when a screen actually needs it.
/// </summary>
public static class WorkConstants
{
    public static class Roles
    {
        public const string Manager = "Manager";
        public const string Sales = "Sales";
        public const string Support = "Support";

        public static readonly string[] All = [Manager, Sales, Support];
    }

    /// <summary>Values of <c>vw_CareSchedule.ScheduleStatus</c> (design.md 2.2).</summary>
    public static class ScheduleStatus
    {
        public const string Overdue = "Overdue";
        public const string Today = "Today";
        public const string Upcoming = "Upcoming";
    }

    /// <summary>Values of <c>CUSTOMER.CustomerType</c> (<c>CHK_Customer_Type</c>).</summary>
    public static class CustomerTypes
    {
        public const string Individual = "Individual";
        public const string Business = "Business";

        public static readonly string[] All = [Individual, Business];

        /// <summary>A business has no gender and no date of birth (<c>CHK_Customer_Business</c>).</summary>
        public static bool IsBusiness(string? type) => type == Business;
    }

    /// <summary>Values of <c>CUSTOMER.Gender</c> (<c>CHK_Customer_Gender</c>).</summary>
    public static class CustomerGenders
    {
        public const string Male = "Male";
        public const string Female = "Female";
        public const string Other = "Other";

        public static readonly string[] All = [Male, Female, Other];
    }

    /// <summary>
    /// Values of <c>CUSTOMER.Tier</c> (<c>CHK_Customer_Tier</c>), lowest rank first. Read-only in the
    /// application: <c>trg_Contract_UpdateTier</c> owns the column (AGENTS.md 4).
    /// </summary>
    public static class CustomerTiers
    {
        public const string Standard = "Standard";
        public const string Silver = "Silver";
        public const string Gold = "Gold";
        public const string Diamond = "Diamond";

        public static readonly string[] All = [Standard, Silver, Gold, Diamond];
    }

    /// <summary>
    /// Status of a contract, from <c>CHK_Contract_Status</c>.
    /// </summary>
    public static class ContractStatus
    {
        public const string Pending = "Pending";
        public const string Active = "Active";
        public const string Expired = "Expired";
        public const string Cancelled = "Cancelled";

        public static readonly string[] All = [Pending, Active, Expired, Cancelled];
    }

    /// <summary>Values of <c>CONTACT.Channel</c> (<c>CHK_Contact_Channel</c>).</summary>
    public static class ContactChannels
    {
        public const string Phone = "Phone";
        public const string Email = "Email";
        public const string Zalo = "Zalo";
        public const string InPerson = "InPerson";
        public const string Other = "Other";

        public static readonly string[] All = [Phone, Email, Zalo, InPerson, Other];
    }

    /// <summary>Values of <c>CARE.CareType</c> (<c>CHK_Care_Type</c>).</summary>
    public static class CareTypes
    {
        public const string Renewal = "Renewal";
        public const string Upsell = "Upsell";
        public const string Birthday = "Birthday";
        public const string Survey = "Survey";
        public const string RenewalReminder = "RenewalReminder";
        public const string Other = "Other";

        public static readonly string[] All = [Renewal, Upsell, Birthday, Survey, RenewalReminder, Other];
    }

    /// <summary>Values of <c>FEEDBACK.FeedbackType</c> (<c>CHK_Feedback_Type</c>).</summary>
    public static class FeedbackTypes
    {
        public const string Complaint = "Complaint";
        public const string Suggestion = "Suggestion";
        public const string GeneralFeedback = "GeneralFeedback";
        public const string Compliment = "Compliment";

        public static readonly string[] All = [Complaint, Suggestion, GeneralFeedback, Compliment];
    }

    /// <summary>Values of <c>FEEDBACK.FeedbackStatus</c> (<c>CHK_Feedback_Status</c>).</summary>
    public static class FeedbackStatuses
    {
        public const string New = "New";
        public const string InProgress = "InProgress";
        public const string Resolved = "Resolved";
        public const string Closed = "Closed";

        public static readonly string[] All = [New, InProgress, Resolved, Closed];
    }

    /// <summary>Values of <c>FEEDBACK.Priority</c> (<c>CHK_Feedback_Priority</c>): 1 high, 3 low.</summary>
    public static class FeedbackPriorities
    {
        public const byte High = 1;
        public const byte Medium = 2;
        public const byte Low = 3;

        public static readonly byte[] All = [High, Medium, Low];
    }

    /// <summary>
    /// The report tabs (design.md 6.12) and the roles each one is for. The same lists feed the
    /// <c>[Authorize]</c> attributes of <c>ReportsController</c> and the visibility of the links, so a
    /// tab cannot be offered to a role the server would refuse.
    /// </summary>
    public static class Reports
    {
        public const string TabRevenue = "revenue";
        public const string TabCustomers = "customers";
        public const string TabCare = "care";

        public static readonly string[] RevenueRoles = [Roles.Manager, Roles.Sales];
        public static readonly string[] CustomerRoles = [Roles.Manager, Roles.Sales];
        public static readonly string[] CareRoles = [Roles.Manager, Roles.Support];

        /// <summary>Các tab mà vai trò được mở, theo thứ tự trên thanh tab.</summary>
        public static string[] TabsForRole(string? role)
        {
            var tabs = new List<string>(3);

            if (role is not null && RevenueRoles.Contains(role))
            {
                tabs.Add(TabRevenue);
                tabs.Add(TabCustomers);
            }

            if (role is not null && CareRoles.Contains(role))
            {
                tabs.Add(TabCare);
            }

            return [.. tabs];
        }

        /// <summary>Tab đầu tiên của vai trò, dùng cho <c>/reports</c>.</summary>
        public static string? FirstTabFor(string? role) => TabsForRole(role).FirstOrDefault();
    }

    /// <summary>Con số của trang tổng quan (design.md 6.2).</summary>
    public static class Dashboard
    {
        /// <summary>Số khách hàng trong bảng "Khách hàng hàng đầu".</summary>
        public const int TopCustomerCount = 5;
    }

    /// <summary>Chuông thông báo trên thanh trên cùng (design.md 3.2).</summary>
    public static class Bell
    {
        /// <summary>Số việc tối đa mỗi nhóm; phần còn lại xem ở trang đầy đủ.</summary>
        public const int ItemsPerGroup = 3;
    }

    /// <summary>Ô tìm nhanh khách hàng trên thanh trên cùng.</summary>
    public static class Search
    {
        /// <summary>Số gợi ý tối đa trả về (design.md 3.2).</summary>
        public const int SuggestionCount = 8;

        /// <summary>Gõ ít hơn số ký tự này thì chưa gọi cơ sở dữ liệu.</summary>
        public const int MinKeywordLength = 2;
    }

    /// <summary>
    /// Suggestions for the free-text <c>CUSTOMER.Source</c> column: the five values the sample data
    /// uses. This is business data, so it stays Vietnamese while the code around it is English
    /// (<c>ccm-language</c>); the form accepts anything the user types.
    /// </summary>
    public static readonly string[] CustomerSources =
        ["Website", "Giới thiệu", "Sự kiện", "Facebook", "Điện thoại"];

    /// <summary>
    /// Keys and legal values of the <c>SETTINGS</c> table. They must stay in step with
    /// <c>CHK_Settings_Key</c> and <c>CHK_Settings_Value</c> in <c>database/01_CreateTables.sql</c>:
    /// the database is the authority, this list only lets the application validate before it writes.
    /// </summary>
    public static class Settings
    {
        public const string Language = "Language";
        public const string Theme = "Theme";
        public const string PageSize = "PageSize";

        public static readonly string[] Keys = [Language, Theme, PageSize];

        /// <summary>Rows per page for every list. The database refuses anything outside 1-100.</summary>
        public const int DefaultPageSize = 20;
        public const int MinPageSize = 1;
        public const int MaxPageSize = 100;

        /// <summary>Sizes the pager offers; the URL may ask for any size in 1-100.</summary>
        public static readonly int[] PageSizeOptions = [10, 20, 50];

        public static class Languages
        {
            public const string Vietnamese = "vi";
            public const string English = "en";

            public static readonly string[] All = [Vietnamese, English];
        }

        public static class Themes
        {
            public const string Light = "light";
            public const string Dark = "dark";

            public static readonly string[] All = [Light, Dark];
        }

        /// <summary>
        /// Values <c>CHK_Settings_Value</c> accepts for a key; empty for a key whose value is free
        /// (PageSize is a number, so it has no list).
        /// </summary>
        public static string[] AllowedValues(string? key) => key switch
        {
            Language => Languages.All,
            Theme => Themes.All,
            _ => []
        };

        /// <summary>
        /// The <c>DataType</c> stored with a key. It must match <c>CHK_Settings_Value</c>, which ties
        /// the type to the key: a bare value would let sqlcmd store garbage.
        /// </summary>
        public static string DataTypeOf(string key) => key switch
        {
            PageSize => "Int",
            _ => "String"
        };

        public static bool IsKnownKey(string? key) => Keys.Contains(key);

        public static bool IsValidValue(string? key, string? value) =>
            key == PageSize
                ? IsValidPageSize(value)
                : value is not null && AllowedValues(key).Contains(value);

        /// <summary>
        /// A page size the database accepts and a list can use: digits only (no sign, no space - the
        /// CHECK says the same) within 1-100.
        /// </summary>
        public static bool IsValidPageSize(string? value) =>
            int.TryParse(value, NumberStyles.None, CultureInfo.InvariantCulture, out var size)
            && size is >= MinPageSize and <= MaxPageSize;
    }

    /// <summary>Fallbacks used when neither the cookie, the account nor the database decides.</summary>
    public const string DefaultLanguage = Settings.Languages.Vietnamese;
    public const string DefaultTheme = Settings.Themes.Light;

    /// <summary>
    /// Cookie holding the chosen theme. The chosen language uses the framework's own culture cookie
    /// (<c>CookieRequestCultureProvider</c>), so the built-in provider reads back what we wrote.
    /// </summary>
    public const string ThemeCookieName = ".CcmWeb.Theme";

    /// <summary>Name of the rate-limiting policy applied to the login POST.</summary>
    public const string LoginRateLimitPolicy = "login";
}
