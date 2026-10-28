using System.Text.RegularExpressions;
using System.Xml.Linq;
using CcmWeb.Helpers;

namespace CcmWeb.Tests;

/// <summary>
/// The resource files are the only place a visible string may live (ccm-language). These tests keep
/// the two languages in step and catch a key that is used in the code or a view but never written
/// down, which would otherwise show the key itself on screen.
/// </summary>
public class LocalizationTests
{
    private static readonly string Root = TestDatabase.RepositoryRoot();

    private static readonly string NeutralResx =
        Path.Combine(Root, "src", "CcmWeb", "Resources", "SharedResource.resx");

    private static readonly string EnglishResx =
        Path.Combine(Root, "src", "CcmWeb", "Resources", "SharedResource.en.resx");

    // Every resource key of this project starts with one of these, so a key can be recognised in a
    // .cs or .cshtml file without parsing Razor.
    private static readonly string[] Prefixes =
    [
        "App", "Common", "Nav", "Topbar", "Setting", "Settings", "Language", "Theme", "Role",
        "Login", "Validation", "Error", "Bell", "Dashboard", "Reports", "Search"
    ];

    private static readonly Regex KeyInSource = new(
        "\"((?:" + string.Join("|", Prefixes) + ")_[A-Za-z0-9_]+)\"",
        RegexOptions.Compiled);

    [Fact]
    public void The_two_languages_carry_exactly_the_same_keys() =>
        Assert.Equal(
            Keys(NeutralResx).OrderBy(key => key, StringComparer.Ordinal),
            Keys(EnglishResx).OrderBy(key => key, StringComparer.Ordinal));

    [Fact]
    public void No_message_is_left_empty()
    {
        foreach (var file in new[] { NeutralResx, EnglishResx })
        {
            foreach (var (key, value) in Entries(file))
            {
                Assert.False(string.IsNullOrWhiteSpace(value), $"{Path.GetFileName(file)}: {key} is empty");
            }
        }
    }

    [Fact]
    public void The_default_file_is_vietnamese_so_a_missing_key_falls_back_to_it()
    {
        Assert.Equal("Hệ thống chăm sóc khách hàng", Value(NeutralResx, "App_Name"));
        Assert.Equal("Customer Care Management", Value(EnglishResx, "App_Name"));
    }

    [Fact]
    public void Every_key_used_in_the_code_or_a_view_exists()
    {
        var known = Keys(NeutralResx).ToHashSet(StringComparer.Ordinal);
        var missing = UsedKeys()
            .Where(key => !known.Contains(key))
            .OrderBy(key => key, StringComparer.Ordinal)
            .ToList();

        Assert.True(missing.Count == 0,
            $"These keys are used but not defined: {string.Join(", ", missing)}");
    }

    [Fact]
    public void Every_coded_value_has_a_label_in_both_languages()
    {
        var labels = WorkConstants.Settings.Languages.All.Select(value => $"Language_{value}")
            .Concat(WorkConstants.Settings.Themes.All.Select(value => $"Theme_{value}"))
            .Concat(WorkConstants.Roles.All.Select(value => $"Role_{value}"))
            .Concat(WorkConstants.Settings.Keys.Select(value => $"Setting_{value}"))
            .ToList();

        foreach (var file in new[] { NeutralResx, EnglishResx })
        {
            var known = Keys(file).ToHashSet(StringComparer.Ordinal);
            var withoutLabel = labels.Where(label => !known.Contains(label)).ToList();

            Assert.True(withoutLabel.Count == 0,
                $"{Path.GetFileName(file)} has no label for: {string.Join(", ", withoutLabel)}");
        }
    }

    [Fact]
    public void Every_sql_error_a_user_can_meet_is_translated_to_a_key_that_exists()
    {
        var known = Keys(NeutralResx).ToHashSet(StringComparer.Ordinal);

        var keys = Enumerable.Range(50001, 15).Select(Utils.ProcedureErrorKey)
            .Concat(
            [
                Utils.TranslateSqlNumber(547, "CHECK constraint \"CHK_Customer_Business\""),
                Utils.TranslateSqlNumber(547, "CHECK constraint \"CHK_Customer_Contact\""),
                Utils.TranslateSqlNumber(547, "CHECK constraint \"CHK_Feedback_Assignee\""),
                Utils.TranslateSqlNumber(547, "CHECK constraint \"CHK_Care_SatisfactionScore\""),
                Utils.TranslateSqlNumber(547, "CHECK constraint \"CHK_Care_NextAppointment\""),
                Utils.TranslateSqlNumber(547, "CHECK constraint \"CHK_Settings_Key\""),
                Utils.TranslateSqlNumber(547, "CHECK constraint \"CHK_Settings_Value\""),
                Utils.TranslateSqlNumber(547, "REFERENCE constraint \"FK_Contract_Customer\""),
                Utils.TranslateSqlNumber(547, "CHECK constraint \"CHK_Other\""),
                Utils.TranslateSqlNumber(2601, "unique index 'UX_Customer_Email'"),
                Utils.TranslateSqlNumber(2627, "UNIQUE KEY constraint 'UQ_Settings_Scope'"),
                Utils.TranslateSqlNumber(2627, "UNIQUE KEY constraint 'UX_Employee_DepartmentManager'"),
                Utils.TranslateSqlNumber(2627, "UNIQUE KEY constraint 'UQ_Other'")
            ])
            .OfType<string>()
            .Distinct()
            .ToList();

        var unknown = keys.Where(key => !known.Contains(key)).ToList();

        Assert.True(unknown.Count == 0,
            $"These error keys have no resource: {string.Join(", ", unknown)}");
    }

    private static IEnumerable<(string Key, string Value)> Entries(string file) =>
        XDocument.Load(file).Root!
            .Elements("data")
            .Select(element => (
                element.Attribute("name")!.Value,
                element.Element("value")?.Value ?? string.Empty));

    private static IEnumerable<string> Keys(string file) => Entries(file).Select(entry => entry.Key);

    private static string Value(string file, string key) =>
        Entries(file).Single(entry => entry.Key == key).Value;

    /// <summary>Every key-looking literal in the application source, views included.</summary>
    private static IEnumerable<string> UsedKeys()
    {
        var separator = Path.DirectorySeparatorChar.ToString();

        return Directory
            .EnumerateFiles(Path.Combine(Root, "src", "CcmWeb"), "*.*", SearchOption.AllDirectories)
            .Where(path => path.EndsWith(".cs", StringComparison.OrdinalIgnoreCase)
                        || path.EndsWith(".cshtml", StringComparison.OrdinalIgnoreCase))
            .Where(path => !path.Contains($"{separator}obj{separator}")
                        && !path.Contains($"{separator}bin{separator}"))
            .SelectMany(path => KeyInSource.Matches(File.ReadAllText(path))
                .Select(match => match.Groups[1].Value));
    }
}
