using System.Net.WebSockets;
using System.Text;
using System.Text.Json;

using Microsoft.Extensions.Options;

namespace ChargeMaster.Services.TibberPulse;

/// <summary>
/// Tjänst för att prenumerera på realtidsdata från Tibber Pulse via GraphQL-subscription och WebSocket.
/// Använder protokollet graphql-transport-ws mot Tibbers API.
/// </summary>
public class TibberPulseService(
    IOptions<TibberPulseOptions> options,
    ILogger<TibberPulseService> logger)
{
    private static readonly Uri SubscriptionEndpoint =
        new("wss://api.tibber.com/v1-beta/gql/subscriptions");

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    /// <summary>
    /// Senast mottagna mätdata från Tibber Pulse.
    /// </summary>
    public TibberLiveMeasurement? LastMeasurement { get; private set; }

    /// <summary>
    /// Event som höjs när ny mätdata tas emot från Tibber Pulse.
    /// </summary>
    public event EventHandler<TibberMeasurementEventArgs>? MeasurementReceived;

    /// <summary>
    /// Ansluter till Tibber API och prenumererar på strömmande mätdata.
    /// Blockerar tills <paramref name="cancellationToken"/> avbryts eller ett fel inträffar.
    /// </summary>
    public async Task SubscribeAsync(CancellationToken cancellationToken)
    {
        var opts = options.Value;

        using var ws = new ClientWebSocket();
        ws.Options.AddSubProtocol("graphql-transport-ws");
        ws.Options.SetRequestHeader("Authorization", $"Bearer {opts.ApiToken}");

        logger.LogInformation("Ansluter till Tibber API...");
        await ws.ConnectAsync(SubscriptionEndpoint, cancellationToken);
        logger.LogInformation("WebSocket-anslutning upprättad");

        // Skicka connection_init med token i payload
        await SendAsync(ws, new
        {
            type = "connection_init",
            payload = new { token = opts.ApiToken }
        }, cancellationToken);

        // Vänta på connection_ack
        var ackMessage = await ReceiveMessageAsync(ws, cancellationToken);
        var ackType = GetMessageType(ackMessage);
        if (ackType != "connection_ack")
        {
            throw new InvalidOperationException(
                $"Oväntat svar från Tibber API: '{ackType}'. Förväntade 'connection_ack'. Fullständigt svar: {ackMessage}");
        }
        logger.LogInformation("Tibber API anslutning bekräftad (connection_ack)");

        // Skicka subscribe med GraphQL-query
        await SendAsync(ws, new
        {
            id = "1",
            type = "subscribe",
            payload = new { query = BuildSubscriptionQuery(opts.HomeId) }
        }, cancellationToken);

        logger.LogInformation("Tibber Pulse-prenumeration startad för HomeId: {HomeId}", opts.HomeId);

        // Mottagningsloop tills avbruten eller fel
        while (!cancellationToken.IsCancellationRequested && ws.State == WebSocketState.Open)
        {
            var message = await ReceiveMessageAsync(ws, cancellationToken);
            await HandleMessageAsync(ws, message, cancellationToken);
        }
    }

    private async Task HandleMessageAsync(ClientWebSocket ws, string message, CancellationToken cancellationToken)
    {
        using var doc = JsonDocument.Parse(message);
        var root = doc.RootElement;

        if (!root.TryGetProperty("type", out var typeElement))
        {
            logger.LogWarning("Mottog meddelande utan 'type'-fält: {Message}", message);
            return;
        }

        switch (typeElement.GetString())
        {
            case "next":
                HandleNext(root);
                break;

            case "ping":
                await SendAsync(ws, new { type = "pong" }, cancellationToken);
                break;

            case "error":
                logger.LogError("Tibber API subscription-fel: {Message}", message);
                throw new InvalidOperationException($"Tibber API subscription-fel: {message}");

            case "complete":
                logger.LogInformation("Tibber API subscription avslutad av servern");
                break;

            case "connection_keepalive":
                // Äldre protokollsversion, ignorera
                break;

            default:
                logger.LogDebug("Okänd meddelandetyp från Tibber API: {Message}", message);
                break;
        }
    }

    private void HandleNext(JsonElement root)
    {
        try
        {
            var measurementElement = root
                .GetProperty("payload")
                .GetProperty("data")
                .GetProperty("liveMeasurement");

            var measurement = measurementElement.Deserialize<TibberLiveMeasurement>(JsonOptions);
            if (measurement == null)
            {
                logger.LogWarning("Kunde inte deserialisera mätdata från Tibber API");
                return;
            }

            LastMeasurement = measurement;
            MeasurementReceived?.Invoke(this, new TibberMeasurementEventArgs(measurement));
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Fel vid hantering av 'next'-meddelande från Tibber API");
        }
    }

    private static string GetMessageType(string message)
    {
        using var doc = JsonDocument.Parse(message);
        return doc.RootElement.TryGetProperty("type", out var type)
            ? type.GetString() ?? string.Empty
            : string.Empty;
    }

    private static async Task SendAsync(ClientWebSocket ws, object payload, CancellationToken cancellationToken)
    {
        var json = JsonSerializer.Serialize(payload);
        var bytes = Encoding.UTF8.GetBytes(json);
        await ws.SendAsync(new ArraySegment<byte>(bytes), WebSocketMessageType.Text, true, cancellationToken);
    }

    private static async Task<string> ReceiveMessageAsync(ClientWebSocket ws, CancellationToken cancellationToken)
    {
        var buffer = new byte[8192];
        using var ms = new MemoryStream();
        WebSocketReceiveResult result;

        do
        {
            result = await ws.ReceiveAsync(new ArraySegment<byte>(buffer), cancellationToken);

            if (result.MessageType == WebSocketMessageType.Close)
            {
                throw new InvalidOperationException(
                    $"WebSocket-anslutningen stängdes av servern: {result.CloseStatusDescription}");
            }

            ms.Write(buffer, 0, result.Count);
        }
        while (!result.EndOfMessage);

        return Encoding.UTF8.GetString(ms.ToArray());
    }

    private static string BuildSubscriptionQuery(string homeId) =>
        $$"""
        subscription {
          liveMeasurement(homeId: "{{homeId}}") {
            timestamp
            power
            lastMeterConsumption
            accumulatedConsumption
            accumulatedProduction
            accumulatedConsumptionLastHour
            accumulatedProductionLastHour
            accumulatedCost
            accumulatedReward
            currency
            minPower
            averagePower
            maxPower
            powerProduction
            powerReactive
            powerProductionReactive
            minPowerProduction
            maxPowerProduction
            lastMeterProduction
            powerFactor
            voltagePhase1
            voltagePhase2
            voltagePhase3
            currentL1
            currentL2
            currentL3
            signalStrength
          }
        }
        """;
}
