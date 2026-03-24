using System.Runtime.InteropServices;

namespace ChargeMaster.Workers;

/// <summary>
/// Event args för systemlast-data.
/// </summary>
public class SystemLoadEventArgs(double load1, double load5, double load15, int runningProcesses, int totalProcesses) : EventArgs
{
    /// <summary>
    /// Genomsnittlig last över senaste 1 minut.
    /// </summary>
    public double Load1 { get; } = load1;

    /// <summary>
    /// Genomsnittlig last över senaste 5 minuter.
    /// </summary>
    public double Load5 { get; } = load5;

    /// <summary>
    /// Genomsnittlig last över senaste 15 minuter.
    /// </summary>
    public double Load15 { get; } = load15;

    /// <summary>
    /// Antal processer som för närvarande körs.
    /// </summary>
    public int RunningProcesses { get; } = runningProcesses;

    /// <summary>
    /// Totalt antal processer i systemet.
    /// </summary>
    public int TotalProcesses { get; } = totalProcesses;
}

/// <summary>
/// Worker som övervakar systemlasten på Linux-maskiner.
/// Läser från /proc/loadavg var 10:e sekund och publicerar event, 
/// men loggar endast vid uppstart och sedan var 15:e minut.
/// </summary>
public class LinuxWorker(ILogger<LinuxWorker> logger) : BackgroundService
{
    private const int CheckIntervalSeconds = 10; // Läs och posta event var 10:e sekund
    private const int LogIntervalSeconds = 900; // Logga var 15:e minut (900 sekunder)
    private const string LoadavgPath = "/proc/loadavg";
    private DateTime _lastLogTime = DateTime.MinValue;

    /// <summary>
    /// Event som höjs när ny systemlast-data läses in.
    /// </summary>
    public event EventHandler<SystemLoadEventArgs>? SystemLoadUpdated;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        // Kör endast på Linux
        if (!RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
        {
            logger.LogInformation("LinuxWorker: Inte en Linux-maskin, avslutar");
            return;
        }

        logger.LogInformation("LinuxWorker: Startar systemlast-övervakning");

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await Task.Delay(TimeSpan.FromSeconds(CheckIntervalSeconds), stoppingToken);

                var systemLoad = await ReadSystemLoadAsync();
                if (systemLoad != null)
                {
                    // Logga endast vid uppstart eller om LogIntervalSeconds har passerat
                    var now = DateTime.UtcNow;
                    if (_lastLogTime == DateTime.MinValue || (now - _lastLogTime).TotalSeconds >= LogIntervalSeconds)
                    {
                        logger.LogInformation(
                            "Systemlast - 1min: {Load1}, 5min: {Load5}, 15min: {Load15} | Processer: {Running}/{Total}",
                            systemLoad.Load1.ToString("F2"),
                            systemLoad.Load5.ToString("F2"),
                            systemLoad.Load15.ToString("F2"),
                            systemLoad.RunningProcesses,
                            systemLoad.TotalProcesses);

                        _lastLogTime = now;
                    }

                    // Publicera event var gång (inte beroende på loggintervallet)
                    SystemLoadUpdated?.Invoke(this, new SystemLoadEventArgs(
                        systemLoad.Load1,
                        systemLoad.Load5,
                        systemLoad.Load15,
                        systemLoad.RunningProcesses,
                        systemLoad.TotalProcesses));
                }
            }
            catch (OperationCanceledException)
            {
                logger.LogInformation("LinuxWorker avslutas");
                break;
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Fel vid läsning av systemlast");
            }
        }
    }

    /// <summary>
    /// Läser systemlasten från /proc/loadavg.
    /// </summary>
    /// <returns>Systemlastdata eller null om läsningen misslyckas</returns>
    private static async Task<SystemLoad?> ReadSystemLoadAsync()
    {
        try
        {
            if (!File.Exists(LoadavgPath))
            {
                return null;
            }

            var content = await File.ReadAllTextAsync(LoadavgPath);
            var parts = content.Split(' ', StringSplitOptions.RemoveEmptyEntries);

            if (parts.Length < 5)
            {
                return null;
            }

            // Format: 1.25 1.20 1.15 2/150 1234
            if (!double.TryParse(parts[0], out var load1) ||
                !double.TryParse(parts[1], out var load5) ||
                !double.TryParse(parts[2], out var load15))
            {
                return null;
            }

            var processInfo = parts[3].Split('/');
            int.TryParse(processInfo[0], out var runningProcesses);
            int.TryParse(processInfo[1], out var totalProcesses);

            return new SystemLoad
            {
                Load1 = load1,
                Load5 = load5,
                Load15 = load15,
                RunningProcesses = runningProcesses,
                TotalProcesses = totalProcesses
            };
        }
        catch
        {
            return null;
        }
    }

    /// <summary>
    /// Representerar systemlastdata från /proc/loadavg.
    /// </summary>
    private class SystemLoad
    {
        /// <summary>
        /// Genomsnittlig last över senaste 1 minut.
        /// </summary>
        public double Load1 { get; set; }

        /// <summary>
        /// Genomsnittlig last över senaste 5 minuter.
        /// </summary>
        public double Load5 { get; set; }

        /// <summary>
        /// Genomsnittlig last över senaste 15 minuter.
        /// </summary>
        public double Load15 { get; set; }

        /// <summary>
        /// Antal processer som för närvarande körs.
        /// </summary>
        public int RunningProcesses { get; set; }

        /// <summary>
        /// Totalt antal processer i systemet.
        /// </summary>
        public int TotalProcesses { get; set; }
    }
}
