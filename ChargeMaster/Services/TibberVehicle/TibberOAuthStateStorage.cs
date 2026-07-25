namespace ChargeMaster.Services.TibberVehicle;

/// <summary>
/// Lagrar OAuth2 state-parametrar på servern för CSRF-validering.
/// Detta är säkrare än att lagra i sessionStorage som kan gå förlorad vid omdirigering.
/// </summary>
public class TibberOAuthStateStorage
{
    private static readonly Dictionary<string, (string StateParameter, DateTime CreatedAt)> _stateStore = new();
    private static readonly object _lockObj = new();
    private static readonly TimeSpan _stateExpiration = TimeSpan.FromMinutes(10);

    /// <summary>
    /// Sparar en state-parameter på servern.
    /// </summary>
    public static string SaveState(string stateParameter)
    {
        lock (_lockObj)
        {
            // Rensa gamla state-parametrar
            var expired = _stateStore.Where(x => DateTime.UtcNow - x.Value.CreatedAt > _stateExpiration).Select(x => x.Key).ToList();
            foreach (var key in expired)
            {
                _stateStore.Remove(key);
            }

            // Spara ny state
            _stateStore[stateParameter] = (stateParameter, DateTime.UtcNow);
            return stateParameter;
        }
    }

    /// <summary>
    /// Validerar en state-parameter. Tar bort den vid validering för att förhindra replay-attacker.
    /// </summary>
    public static bool ValidateAndRemoveState(string stateParameter)
    {
        if (string.IsNullOrEmpty(stateParameter))
            return false;

        lock (_lockObj)
        {
            if (_stateStore.TryGetValue(stateParameter, out var entry))
            {
                // Kontrollera att den inte är för gammal
                if (DateTime.UtcNow - entry.CreatedAt > _stateExpiration)
                {
                    _stateStore.Remove(stateParameter);
                    return false;
                }

                // Ta bort den för att förhindra replay
                _stateStore.Remove(stateParameter);
                return true;
            }

            return false;
        }
    }
}
