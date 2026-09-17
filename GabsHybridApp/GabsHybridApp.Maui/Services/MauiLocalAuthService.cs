using System.Diagnostics;
using System.Net.Http.Json;
using System.Security.Claims;
using GabsHybridApp.Shared.Models;
using GabsHybridApp.Shared.Services;
using GabsHybridApp.Shared.States;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.Maui.Storage;

namespace GabsHybridApp.Maui.Services;

// The service must be public and non-abstract
public sealed class MauiLocalAuthService : IAuthService
{
    // simple local “cookie” keys
    private const string SignedInKey = "auth_ok";
    private const string NameKey = "auth_name";
    private const string IdKey = "auth_id";
    private const string RolesKey = "auth_roles";
    private const string IsStationModeKey = "is_station_mode";
    private const string StationDeviceIdKey = "station_device_id";

    // Start with an anonymous user, this is the safe initial state.
    private ClaimsPrincipal _user = new ClaimsPrincipal(new ClaimsIdentity());
    private readonly UserService _users;
    private readonly INetworkService _networkService;
    private readonly HttpClient _http;

    // Flag to ensure the disk read only happens once
    private bool _isStateLoaded = false;

    public ClaimsPrincipal CurrentUser
    {
        get
        {
            if (!_isStateLoaded)
            {
                _user = Preferences.Default.Get(SignedInKey, false)
                    ? BuildPrincipalFromPrefs()
                    : new ClaimsPrincipal(new ClaimsIdentity());
                _isStateLoaded = true;
            }
            return _user;
        }
    }

    public MauiLocalAuthService(UserService users, INetworkService networkService, HttpClient? http = null)
    {
        _users = users;
        _networkService = networkService;
        if (http != null)
        {
            _http = http;
        }
        else
        {
            var handler = new HttpClientHandler();
#pragma warning disable CA1416 // ServerCertificateCustomValidationCallback is only invoked on MAUI/Desktop platforms
            handler.ServerCertificateCustomValidationCallback = (sender, cert, chain, sslPolicyErrors) => true;
#pragma warning restore CA1416
            _http = new HttpClient(handler)
            {
                Timeout = TimeSpan.FromSeconds(10)
            };
        }
    }

    public async Task<bool> SignInAsync(string username, string password, string? returnUrl = null)
    {
        if (string.IsNullOrWhiteSpace(username) || string.IsNullOrWhiteSpace(password))
            return false;

        // 1. If online, attempt online verification to sync/refresh credentials and append to local SQLite
        if (_networkService.IsOnline)
        {
            try
            {
                using var cts = new CancellationTokenSource(TimeSpan.FromMilliseconds(2500));
                var baseUrl = StorageConstants.AppWebUrl.TrimEnd('/');
                var loginUrl = $"{baseUrl}/api/auth/mobile-login";
                var devId = Preferences.Default.Get(StationDeviceIdKey, string.Empty);
                var req = new MobileLoginRequest(username.Trim(), password, devId);

                var response = await _http.PostAsJsonAsync(loginUrl, req, cts.Token);
                if (response.IsSuccessStatusCode)
                {
                    var userDto = await response.Content.ReadFromJsonAsync<MobileLoginResponse>(cancellationToken: cts.Token);
                    if (userDto != null && userDto.IsActive)
                    {
                        var account = new UserAccount
                        {
                            Id = userDto.Id,
                            Username = userDto.Username,
                            PasswordHash = userDto.PasswordHash,
                            PasswordSalt = userDto.PasswordSalt,
                            Roles = userDto.Roles,
                            IsActive = userDto.IsActive,
                            ServerSalt = userDto.ServerSalt,
                            CreatedOn = userDto.CreatedOn,
                            LastLogin = DateTime.Now
                        };
                        _users.UpsertSyncedUsers(new[] { account });
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[AUTH] Online mobile-login unreachable/offline, continuing with local verification: {ex.Message}");
            }
        }

        // 2. Perform local verification against local SQLite database
        var u = _users.Authenticate(username, password);
        if (u is null) return false;

        // persist minimal identity to disk
        Preferences.Default.Set(SignedInKey, true);
        Preferences.Default.Set(NameKey, username);
        Preferences.Default.Set(IdKey, u.Id.ToString());
        Preferences.Default.Set(RolesKey, u.Roles ?? string.Empty);
        Preferences.Default.Set(IsStationModeKey, false);

        // Update the in-memory cache instantly
        _user = BuildPrincipalFromPrefs();
        // Since we signed in, the state is definitely loaded.
        _isStateLoaded = true;
        return true;
    }

    public Task<bool> SignInStationModeAsync(string deviceId, string pin)
    {
        if (string.IsNullOrWhiteSpace(deviceId) || !StorageConstants.IsValidStationPin(pin))
            return Task.FromResult(false);

        var cleanDevId = deviceId.Trim();
        Preferences.Default.Set(SignedInKey, true);
        Preferences.Default.Set(NameKey, cleanDevId);
        Preferences.Default.Set(IdKey, cleanDevId);
        Preferences.Default.Set(RolesKey, "StationOperator,user");
        Preferences.Default.Set(IsStationModeKey, true);
        Preferences.Default.Set(StationDeviceIdKey, cleanDevId);

        _user = BuildPrincipalFromPrefs();
        _isStateLoaded = true;
        return Task.FromResult(true);
    }

    public Task SignOutAsync()
    {
        // Remove persistence from disk
        Preferences.Default.Remove(SignedInKey);
        Preferences.Default.Remove(NameKey);
        Preferences.Default.Remove(IdKey);
        Preferences.Default.Remove(RolesKey);
        Preferences.Default.Remove(IsStationModeKey);

        // Update the in-memory cache instantly
        _user = new ClaimsPrincipal(new ClaimsIdentity());
        _isStateLoaded = true;
        return Task.CompletedTask;
    }

    public Task<AuthenticationState> GetAuthenticationStateAsync()
    {
        return Task.FromResult(new AuthenticationState(CurrentUser));
    }

    // Helper method to build the ClaimsPrincipal from storage
    private static ClaimsPrincipal BuildPrincipalFromPrefs()
    {
        var name = Preferences.Default.Get(NameKey, string.Empty);
        var id = Preferences.Default.Get(IdKey, string.Empty);
        var roles = Preferences.Default.Get(RolesKey, string.Empty);
        var isStation = Preferences.Default.Get(IsStationModeKey, false);

        var claims = new List<Claim>();
        if (!string.IsNullOrEmpty(name)) claims.Add(new Claim(ClaimTypes.Name, name));
        if (!string.IsNullOrEmpty(id)) claims.Add(new Claim(ClaimTypes.NameIdentifier, id));

        foreach (var r in roles.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
            claims.Add(new Claim(ClaimTypes.Role, r));

        if (isStation)
        {
            claims.Add(new Claim("station_mode", "true"));
        }

        return new ClaimsPrincipal(new ClaimsIdentity(claims, isStation ? "MauiStation" : "MauiLocal"));
    }
}