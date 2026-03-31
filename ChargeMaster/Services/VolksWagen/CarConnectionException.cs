namespace ChargeMaster.Services.VolksWagen;

/// <summary>
/// Represents errors that occur when a connection to a car cannot be established or is lost.
/// </summary>
/// <param name="message">The message that describes the error.</param>
public class CarConnectionException(string message, Exception? innerException = null)
    : Exception(message, innerException);
