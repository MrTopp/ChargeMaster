namespace ChargeMaster.Services.TibberPulse;

/// <summary>
/// Realtidsmätdata från Tibber Pulse.
/// Alla fält speglar GraphQL-schemats liveMeasurement-objekt.
/// </summary>
public class TibberLiveMeasurement
{
    /// <summary>Tidsstämpel för mätningen.</summary>
    public DateTimeOffset Timestamp { get; init; }

    /// <summary>Aktuell effekt i Watt.</summary>
    public double Power { get; init; }

    /// <summary>Senaste mätarställning för förbrukning i kWh.</summary>
    public double? LastMeterConsumption { get; init; }

    /// <summary>Ackumulerad förbrukning sedan midnatt i kWh.</summary>
    public double AccumulatedConsumption { get; init; }

    /// <summary>Ackumulerad produktion sedan midnatt i kWh.</summary>
    public double AccumulatedProduction { get; init; }

    /// <summary>Ackumulerad förbrukning sedan innevarande heltimme i kWh.</summary>
    public double AccumulatedConsumptionLastHour { get; init; }

    /// <summary>Ackumulerad produktion sedan innevarande heltimme i kWh.</summary>
    public double AccumulatedProductionLastHour { get; init; }

    /// <summary>Ackumulerad kostnad sedan midnatt.</summary>
    public double? AccumulatedCost { get; init; }

    /// <summary>Ackumulerad produktion/belöning sedan midnatt.</summary>
    public double? AccumulatedReward { get; init; }

    /// <summary>Valutakod, t.ex. "SEK".</summary>
    public string? Currency { get; init; }

    /// <summary>Lägsta uppmätta effekt sedan midnatt i Watt.</summary>
    public double MinPower { get; init; }

    /// <summary>Genomsnittlig effekt sedan midnatt i Watt.</summary>
    public double AveragePower { get; init; }

    /// <summary>Högsta uppmätta effekt sedan midnatt i Watt.</summary>
    public double MaxPower { get; init; }

    /// <summary>Aktuell produktionseffekt i Watt (solpanel etc.).</summary>
    public double? PowerProduction { get; init; }

    /// <summary>Aktuell reaktiv effekt i VAr.</summary>
    public double? PowerReactive { get; init; }

    /// <summary>Aktuell reaktiv produktionseffekt i VAr.</summary>
    public double? PowerProductionReactive { get; init; }

    /// <summary>Lägsta produktionseffekt sedan midnatt i Watt.</summary>
    public double? MinPowerProduction { get; init; }

    /// <summary>Högsta produktionseffekt sedan midnatt i Watt.</summary>
    public double? MaxPowerProduction { get; init; }

    /// <summary>Senaste mätarställning för produktion i kWh.</summary>
    public double? LastMeterProduction { get; init; }

    /// <summary>Effektfaktor (power factor).</summary>
    public double? PowerFactor { get; init; }

    /// <summary>Spänning fas 1 i Volt.</summary>
    public double? VoltagePhase1 { get; init; }

    /// <summary>Spänning fas 2 i Volt.</summary>
    public double? VoltagePhase2 { get; init; }

    /// <summary>Spänning fas 3 i Volt.</summary>
    public double? VoltagePhase3 { get; init; }

    /// <summary>Ström fas L1 i Ampere.</summary>
    public double? CurrentL1 { get; init; }

    /// <summary>Ström fas L2 i Ampere.</summary>
    public double? CurrentL2 { get; init; }

    /// <summary>Ström fas L3 i Ampere.</summary>
    public double? CurrentL3 { get; init; }

    /// <summary>Signalstyrka för Tibber Pulse i dBm.</summary>
    public int? SignalStrength { get; init; }
}
