namespace Supertext.OrchardCore.Translation.Services;

/// <summary>Per-request state: the Translate page creates localizations itself and translates them explicitly.</summary>
public sealed class SupertextScope
{
    public bool SuppressAutomaticTranslation { get; set; }

    /// <summary>The version report of this request is already scheduled (see <c>SupertextReporting.OnRequestAsync</c>).</summary>
    public bool ReportScheduled { get; set; }
}
