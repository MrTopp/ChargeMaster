namespace ChargeMaster.Services.Daikin;

/// <summary>
/// Driftlägen för Daikin värmepump.
/// </summary>
public enum DaikinMode
{
    /// <summary>
    /// Automatiskt läge.
    /// </summary>
    Auto = 0,

    /// <summary>
    /// Automatiskt läge (alternativ värde).
    /// </summary>
    AutoAlt = 1,

    /// <summary>
    /// Torkning (avfuktning).
    /// </summary>
    Dry = 2,

    /// <summary>
    /// Kylning.
    /// </summary>
    Cool = 3,

    /// <summary>
    /// Uppvärmning.
    /// </summary>
    Heat = 4,

    /// <summary>
    /// Fläkt.
    /// </summary>
    Fan = 6,

    /// <summary>
    /// Automatiskt läge (tredje variant).
    /// </summary>
    AutoSwing = 7
}
