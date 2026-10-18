namespace CcmWeb;

/// <summary>
/// Marker type for the shared resource files. With <c>ResourcesPath = "Resources"</c>,
/// <c>IStringLocalizer&lt;SharedResource&gt;</c> reads <c>Resources/SharedResource.resx</c>
/// (Vietnamese - the default, so a forgotten key shows Vietnamese and never English) and
/// <c>Resources/SharedResource.en.resx</c> (English). See the `ccm-language` skill.
/// </summary>
public sealed class SharedResource
{
}
