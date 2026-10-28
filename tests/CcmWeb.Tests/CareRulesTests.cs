using CcmWeb.Helpers;
using CcmWeb.Models.ViewModels;

namespace CcmWeb.Tests;

/// <summary>
/// The pure rules of the care screens: a satisfaction score belongs to a Survey and the next
/// appointment may not be before the care date. The database repeats both; these tests cover the
/// copy the form and the service use before the row is sent.
/// </summary>
public class CareRulesTests
{
    [Theory]
    [InlineData(WorkConstants.CareTypes.Survey, (byte)4, (byte)4)]
    [InlineData(WorkConstants.CareTypes.Survey, null, null)]
    [InlineData(WorkConstants.CareTypes.Renewal, (byte)4, null)]
    [InlineData(WorkConstants.CareTypes.Other, (byte)1, null)]
    public void A_score_is_kept_only_for_a_survey(string careType, byte? score, byte? expected) =>
        Assert.Equal(expected, CareRules.NormaliseScore(careType, score));

    [Theory]
    [InlineData(WorkConstants.CareTypes.Survey, (byte)1, true)]
    [InlineData(WorkConstants.CareTypes.Survey, (byte)5, true)]
    [InlineData(WorkConstants.CareTypes.Survey, null, true)]
    [InlineData(WorkConstants.CareTypes.Survey, (byte)0, false)]
    [InlineData(WorkConstants.CareTypes.Survey, (byte)6, false)]
    [InlineData(WorkConstants.CareTypes.Upsell, (byte)5, false)]
    [InlineData(WorkConstants.CareTypes.Upsell, null, true)]
    public void The_score_range_and_type_match_the_check_constraint(
        string careType, byte? score, bool valid) =>
        Assert.Equal(valid, CareRules.ScoreIsValid(careType, score));

    [Fact]
    public void An_empty_next_appointment_is_allowed()
    {
        var today = new DateOnly(2026, 10, 20);

        Assert.True(CareRules.AppointmentIsValid(today, null));
    }

    [Fact]
    public void A_next_appointment_before_the_care_date_is_rejected()
    {
        var today = new DateOnly(2026, 10, 20);

        Assert.True(CareRules.AppointmentIsValid(today, today));
        Assert.True(CareRules.AppointmentIsValid(today, today.AddDays(1)));
        Assert.False(CareRules.AppointmentIsValid(today, today.AddDays(-1)));
    }

    [Fact]
    public void A_care_form_with_a_score_on_another_type_is_invalid()
    {
        var model = new CareFormViewModel
        {
            CustomerId = 1,
            CareType = WorkConstants.CareTypes.Renewal,
            SatisfactionScore = 3
        };

        var results = new List<System.ComponentModel.DataAnnotations.ValidationResult>();
        var valid = System.ComponentModel.DataAnnotations.Validator.TryValidateObject(
            model, new System.ComponentModel.DataAnnotations.ValidationContext(model), results, true);

        Assert.False(valid);
        Assert.Contains(results, r => r.ErrorMessage == "Validation_SurveyScoreInvalid");
    }

    [Fact]
    public void A_care_form_rejects_a_next_appointment_before_today()
    {
        var model = new CareFormViewModel
        {
            CustomerId = 1,
            CareType = WorkConstants.CareTypes.Survey,
            SatisfactionScore = 5,
            NextAppointmentDate = DateOnly.FromDateTime(DateTime.Today).AddDays(-1)
        };

        var results = new List<System.ComponentModel.DataAnnotations.ValidationResult>();
        var valid = System.ComponentModel.DataAnnotations.Validator.TryValidateObject(
            model, new System.ComponentModel.DataAnnotations.ValidationContext(model), results, true);

        Assert.False(valid);
        Assert.Contains(results, r => r.ErrorMessage == "Error_NextAppointmentBeforeCare");
    }
}
