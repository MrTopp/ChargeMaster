# Tibber Vehicle OAuth2 Service

This service replaces the deprecated `VWService` and provides integration with Tibber's vehicle API using OAuth2 authentication.

## Overview

The Tibber Vehicle Service handles:
- OAuth2 authentication flow (Authorization Code flow)
- Token storage and refresh management
- API calls to retrieve vehicle status
- Vehicle charging and climatization control

## Configuration

### appsettings.json

Add your Tibber OAuth credentials to `appsettings.json`:

```json
{
  "Tibber": {
	"OAuth": {
	  "ClientId": "YOUR_TIBBER_CLIENT_ID",
	  "ClientSecret": "YOUR_TIBBER_CLIENT_SECRET",
	  "AuthorizeUrl": "https://thewall.tibber.com/connect/authorize",
	  "TokenUrl": "https://api.tibber.com/oauth/token",
	  "ApiBaseUrl": "https://api.tibber.com",
	  "RedirectUri": "http://localhost:5000/tibber-callback",
	  "Scope": "data-api-vehicles-read"
	}
  }
}
```

**Important**: 
- For development, store sensitive values in `appsettings.Development.json`
- For production, use environment variables to override configuration
- Update `RedirectUri` to your production URL (e.g., `https://chargemaster.example.com/tibber-callback`)

## OAuth2 Flow

### 1. Login Page (`/tibber-login`)
User clicks "Connect to Tibber" button, which:
1. Generates a random state parameter (for CSRF protection)
2. Saves state to browser session storage
3. Redirects to Tibber's authorization endpoint

### 2. Tibber Authorization
User logs in and authorizes ChargeMaster to access their vehicle data.

### 3. Callback Page (`/tibber-callback`)
Tibber redirects back with authorization code:
1. Validates state parameter (CSRF check)
2. Calls backend API `/api/tibber/callback` with authorization code
3. Backend exchanges code for access and refresh tokens
4. Tokens are stored securely in a local file

### 4. Token Storage

Tokens are stored in `/tmp` directory:

- **All environments**: `/tmp/.tibber-tokens`
- **Format**: JSON
- **Temporary**: Tokens are cleared on system reboot

⚠️ **Note**: This is a temporary solution. More secure storage (encrypted database, secure vault, etc.) will be implemented later.

## Token Refresh

Tokens are automatically refreshed when needed:
1. `TibberOAuthService.GetValidAccessTokenAsync()` checks token expiration
2. If expired (with 1-minute buffer), automatically calls `RefreshTokenAsync()`
3. Refresh token is used to obtain new access token
4. New tokens are saved to storage

## API Endpoints

### Vehicle Status
```
GET /api/tibber/status
```
Returns current vehicle status (battery level, charging status, etc.)

### Vehicle List
```
GET /api/tibber/vehicles
```
Returns list of registered vehicles.

### Control Commands
```
POST /api/tibber/start-charging
POST /api/tibber/stop-charging
POST /api/tibber/start-climatization
POST /api/tibber/stop-climatization
```

### Logout
```
POST /api/tibber/logout
```
Removes stored tokens (requires re-authentication).

## Service API (Replacing VWService)

The `TibberVehicleService` provides the same interface as `VWService`:

```csharp
// Get vehicle status
var status = await vehicleService.GetStatusAsync();

// Get list of vehicles
var vehicles = await vehicleService.GetVehiclesAsync();

// Control commands
await vehicleService.StartChargingAsync();
await vehicleService.StopChargingAsync();
await vehicleService.StartClimatizationAsync();
await vehicleService.StopClimatizationAsync();

// Subscribe to status updates
vehicleService.VehicleStatusRetrieved += (s, e) => {
	logger.LogInformation("Battery: {Battery}%", e.Status?.BatteryLevel);
};
```

## Error Handling

Authentication failures are logged and returned as `null`:
- If access token is invalid or expired, the service attempts to refresh it
- If refresh fails, `GetValidAccessTokenAsync()` returns `null`
- The UI can detect this and redirect to the login page for re-authentication

## Security Considerations

1. **State Parameter**: CSRF protection using random state parameter
2. **Client Secret**: Never exposed to client; only used in backend API calls
3. **Token Expiration**: Tokens expire after a set time (typically 1 hour)
4. **File Permissions**: Production token file should have restrictive permissions (600)
5. **HTTPS**: Always use HTTPS in production for OAuth2 redirect URIs

## Development vs Production

### Development
- Tokens stored in `.tibber-tokens` in project root
- OAuth redirect URI: `http://localhost:5000/tibber-callback`
- Secrets in `appsettings.Development.json` (not committed)

### Production
- Tokens stored in `/var/lib/chargemaster/.tibber-tokens` (or custom via `CHARGEMASTER_DATA_DIR`)
- OAuth redirect URI: Your production domain
- Secrets provided via environment variables
- Application runs as `chargemasterapp` user with restricted permissions

## Testing

See `ChargeMaster.xUnit` project for exploratory tests with the actual Tibber API.

**Note**: Integration tests should only be run when explicitly requested. They require valid Tibber credentials.

## Replacing VWService

To completely replace VWService:
1. Stop using `VWService` in workers and components
2. Inject `TibberVehicleService` instead
3. The API is nearly identical, but returns `TibberVehicleStatus` instead of `VWStatus`
4. Update any event handler references from `VWStatusRetrieved` to `VehicleStatusRetrieved`

## References

- [Tibber API Documentation](https://developer.tibber.com/)
- [OAuth2 Authorization Code Flow](https://oauth.net/2/grant-types/authorization-code/)
