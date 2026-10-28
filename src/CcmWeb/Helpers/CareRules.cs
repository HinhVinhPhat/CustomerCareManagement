namespace CcmWeb.Helpers;

/// <summary>
/// The rules of a <c>CARE</c> record that the form and the service share (design.md 6.10). They mirror
/// <c>CHK_Care_Type</c>, <c>CHK_Care_SatisfactionScore</c> and <c>CHK_Care_NextAppointment</c>; the
/// database remains the authority, this class only stops a bad value before it is sent.
/// </summary>
public static class CareRules
{
    /// <summary>Length of <c>CARE.Content</c>.</summary>
    public const int MaxContentLength = 1000;

    /// <summary>Length of <c>CARE.Result</c>.</summary>
    public const int MaxResultLength = 200;

    public static bool IsSurvey(string? careType) => careType == WorkConstants.CareTypes.Survey;

    /// <summary>
    /// A satisfaction score belongs to a Survey only. Any other type sends NULL instead of letting
    /// <c>CHK_Care_SatisfactionScore</c> reject the row.
    /// </summary>
    public static byte? NormaliseScore(string? careType, byte? score) =>
        IsSurvey(careType) ? score : null;

    /// <summary>Null is allowed; otherwise it must be 1-5 and the type must be Survey.</summary>
    public static bool ScoreIsValid(string? careType, byte? score) =>
        score is null || (IsSurvey(careType) && score is >= 1 and <= 5);

    /// <summary>The next appointment may be empty, but never before the care date.</summary>
    public static bool AppointmentIsValid(DateOnly careDate, DateOnly? nextAppointmentDate) =>
        nextAppointmentDate is null || nextAppointmentDate >= careDate;
}
