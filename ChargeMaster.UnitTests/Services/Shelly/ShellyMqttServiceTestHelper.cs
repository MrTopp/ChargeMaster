using ChargeMaster.Services.Shelly;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Moq;
using MQTTnet;

namespace ChargeMaster.UnitTests.Services.Shelly;

/// <summary>
/// Helper class for creating properly configured test dependencies for ShellyMqttService tests.
/// Centralizes mock creation to reduce duplication and ensure consistency across tests.
/// </summary>
public static class ShellyMqttServiceTestHelper
{
    /// <summary>
    /// Creates a mock IServiceScopeFactory suitable for unit testing.
    /// </summary>
    /// <returns>A mock IServiceScopeFactory instance</returns>
    /// <exception cref="InvalidOperationException">Thrown if the mock cannot be properly configured</exception>
    public static Mock<IServiceScopeFactory> CreateMockServiceScopeFactory()
    {
        var mockServiceScopeFactory = new Mock<IServiceScopeFactory>();

        // Verification: Ensure the mock was created successfully
        if (mockServiceScopeFactory == null)
        {
            throw new InvalidOperationException("Failed to create mock IServiceScopeFactory instance");
        }

        return mockServiceScopeFactory;
    }

    /// <summary>
    /// Creates a mock ILogger suitable for verification in tests.
    /// </summary>
    /// <returns>A mock ILogger for ShellyMqttService</returns>
    /// <exception cref="InvalidOperationException">Thrown if the mock cannot be properly configured</exception>
    public static Mock<ILogger<ShellyMqttService>> CreateMockLogger()
    {
        var mockLogger = new Mock<ILogger<ShellyMqttService>>();

        // Verification: Ensure the mock was created successfully
        if (mockLogger == null)
        {
            throw new InvalidOperationException("Failed to create mock ILogger<ShellyMqttService> instance");
        }

        return mockLogger;
    }

    /// <summary>
    /// Creates a mock IMqttClient suitable for unit testing.
    /// The mock returns false for IsConnected by default.
    /// </summary>
    /// <returns>A mock IMqttClient instance</returns>
    /// <exception cref="InvalidOperationException">Thrown if the mock cannot be properly configured</exception>
    public static Mock<IMqttClient> CreateMockMqttClient()
    {
        var mockMqttClient = new Mock<IMqttClient>();

        // Configure default behavior: not connected
        mockMqttClient.Setup(c => c.IsConnected).Returns(false);

        // Verification: Ensure the mock was created successfully
        if (mockMqttClient == null)
        {
            throw new InvalidOperationException("Failed to create mock IMqttClient instance");
        }

        return mockMqttClient;
    }

    /// <summary>
    /// Creates a ShellyMqttService instance with all dependencies mocked.
    /// This is the recommended way to create a service instance for unit tests.
    /// </summary>
    /// <param name="serviceScopeFactory">Optional custom service scope factory. If null, a mock is used.</param>
    /// <param name="logger">Optional custom logger. If null, a mock is used.</param>
    /// <param name="mqttClient">Optional custom MQTT client. If null, a mock is used.</param>
    /// <returns>A configured ShellyMqttService instance with all dependencies properly injected</returns>
    /// <exception cref="InvalidOperationException">Thrown if the service cannot be created or any dependency is invalid</exception>
    public static ShellyMqttService CreateServiceWithMocks(
        Mock<IServiceScopeFactory>? serviceScopeFactory = null,
        Mock<ILogger<ShellyMqttService>>? logger = null,
        Mock<IMqttClient>? mqttClient = null)
    {
        var serviceScopeFactoryToUse = serviceScopeFactory ?? CreateMockServiceScopeFactory();
        var loggerToUse = logger ?? CreateMockLogger();
        var mqttClientToUse = mqttClient ?? CreateMockMqttClient();

        // Verification: Ensure all dependencies are properly created before service instantiation
        if (serviceScopeFactoryToUse == null)
            throw new InvalidOperationException("Service scope factory mock cannot be null");
        if (serviceScopeFactoryToUse.Object == null)
            throw new InvalidOperationException("Service scope factory mock object cannot be null");
        if (loggerToUse == null)
            throw new InvalidOperationException("Logger mock cannot be null");
        if (loggerToUse.Object == null)
            throw new InvalidOperationException("Logger mock object cannot be null");
        if (mqttClientToUse == null)
            throw new InvalidOperationException("MQTT client mock cannot be null");
        if (mqttClientToUse.Object == null)
            throw new InvalidOperationException("MQTT client mock object cannot be null");

        ShellyMqttService service;
        try
        {
            service = new ShellyMqttService(
                serviceScopeFactoryToUse.Object,
                loggerToUse.Object,
                mqttClientToUse.Object);
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException(
                "Failed to create ShellyMqttService instance with provided mocks. " +
                "Ensure all dependencies are properly configured.",
                ex);
        }

        // Verification: Ensure service was created successfully
        if (service == null)
        {
            throw new InvalidOperationException("ShellyMqttService instance creation resulted in null");
        }

        return service;
    }

    /// <summary>
    /// Creates a ShellyMqttService instance using the parameterless constructor.
    /// Useful for testing default initialization behavior.
    /// </summary>
    /// <returns>A new ShellyMqttService instance with default initialization</returns>
    /// <exception cref="InvalidOperationException">Thrown if the service cannot be created</exception>
    public static ShellyMqttService CreateServiceWithDefaults()
    {
        ShellyMqttService service;
        try
        {
            service = new ShellyMqttService();
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException(
                "Failed to create ShellyMqttService instance with parameterless constructor",
                ex);
        }

        // Verification: Ensure service was created successfully
        if (service == null)
        {
            throw new InvalidOperationException("ShellyMqttService instance creation resulted in null");
        }

        return service;
    }

    /// <summary>
    /// Creates an event handler collection for testing temperature change events.
    /// </summary>
    /// <returns>A new list for collecting ShellyTemperatureChangedEventArgs</returns>
    public static List<ShellyTemperatureChangedEventArgs> CreateEventCollection()
    {
        return new List<ShellyTemperatureChangedEventArgs>();
    }

    /// <summary>
    /// Helper method to configure a mock service scope factory with a scope and service provider.
    /// </summary>
    /// <param name="mockServiceScopeFactory">The mock factory to configure</param>
    /// <param name="mockServiceProvider">The mock service provider to use</param>
    /// <returns>The configured mock service scope factory</returns>
    /// <exception cref="ArgumentNullException">Thrown if any parameter is null</exception>
    public static Mock<IServiceScopeFactory> ConfigureServiceScopeFactory(
        Mock<IServiceScopeFactory> mockServiceScopeFactory,
        Mock<IServiceProvider> mockServiceProvider)
    {
        if (mockServiceScopeFactory == null)
            throw new ArgumentNullException(nameof(mockServiceScopeFactory));
        if (mockServiceProvider == null)
            throw new ArgumentNullException(nameof(mockServiceProvider));

        var mockServiceScope = new Mock<IServiceScope>();
        mockServiceScope.Setup(s => s.ServiceProvider).Returns(mockServiceProvider.Object);
        mockServiceScopeFactory.Setup(f => f.CreateScope()).Returns(mockServiceScope.Object);

        return mockServiceScopeFactory;
    }

    /// <summary>
    /// Helper method to set up an MQTT client mock to throw a connection exception.
    /// Useful for testing connection failure scenarios.
    /// </summary>
    /// <param name="mockMqttClient">The mock MQTT client to configure</param>
    /// <param name="exception">The exception to throw on connect attempt</param>
    /// <returns>The configured mock MQTT client</returns>
    /// <exception cref="ArgumentNullException">Thrown if any parameter is null</exception>
    public static Mock<IMqttClient> ConfigureMqttClientToFailConnection(
        Mock<IMqttClient> mockMqttClient,
        Exception exception)
    {
        if (mockMqttClient == null)
            throw new ArgumentNullException(nameof(mockMqttClient));
        if (exception == null)
            throw new ArgumentNullException(nameof(exception));

        mockMqttClient.Setup(c => c.ConnectAsync(It.IsAny<MqttClientOptions>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(exception);

        return mockMqttClient;
    }

    /// <summary>
    /// Helper method to set up an MQTT client mock as connected.
    /// Useful for testing scenarios where the client is already connected.
    /// </summary>
    /// <param name="mockMqttClient">The mock MQTT client to configure</param>
    /// <returns>The configured mock MQTT client</returns>
    /// <exception cref="ArgumentNullException">Thrown if parameter is null</exception>
    public static Mock<IMqttClient> ConfigureMqttClientAsConnected(Mock<IMqttClient> mockMqttClient)
    {
        if (mockMqttClient == null)
            throw new ArgumentNullException(nameof(mockMqttClient));

        mockMqttClient.Setup(c => c.IsConnected).Returns(true);

        return mockMqttClient;
    }
}
