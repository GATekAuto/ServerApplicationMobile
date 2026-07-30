using System.Collections.Concurrent;
using System.Text.Json;
using Microsoft.Maui.Controls.Maps;
using Microsoft.Maui.Devices.Sensors;
using Microsoft.Maui.Maps;
using ServerApplicationMobile.Services;

namespace ServerApplicationMobile;

public partial class MapPage : ContentPage
{
    private const int MaxConcurrentGeocoders = 4;
    private const int ProgressiveRenderInterval = 25;
    private const string LocationCacheFileName = "customer-map-locations.json";

    private readonly DatabaseService _databaseService;
    private readonly CustomerDataService _customerDataService;
    private readonly AuthenticationService _authenticationService;
    private readonly Dictionary<Pin, LocatedCustomerGroup> _pinGroups = new();
    private readonly List<LocatedCustomerGroup> _locatedGroups = new();
    private readonly HashSet<string> _renderedLocationAddresses =
        new(StringComparer.OrdinalIgnoreCase);
    private readonly ConcurrentDictionary<string, LocatedCustomerGroup> _newLocatedGroups =
        new(StringComparer.OrdinalIgnoreCase);
    private Task _loadTask;
    private int _resolvedLocationCount;
    private int _totalLocationCount;
    private int _renderedCustomerCount;
    private int _renderedLocationCount;
    private bool _mapLoaded;
    private bool _locationsLoaded;

    public MapPage(
        DatabaseService databaseService,
        CustomerDataService customerDataService,
        AuthenticationService authenticationService)
    {
        InitializeComponent();
        _databaseService = databaseService;
        _customerDataService = customerDataService;
        _authenticationService = authenticationService;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();

        await EnableUserLocationAsync();

        EnsureInitialMapRegion();

        if (_loadTask == null)
            _loadTask = LoadLocationsAsync();

        try
        {
            await _loadTask;
        }
        catch (Exception ex)
        {
            _loadTask = null;
            System.Diagnostics.Debug.WriteLine($"MapPage: Unable to load locations: {ex.Message}");
        }
    }
    private async Task LoadLocationsAsync()
    {
        var customers = await _customerDataService.GetCustomersAsync();

        var customerGroups = customers
            .Select(customer => new { Customer = customer, Query = BuildGeocodeQuery(customer) })
            .Where(item => !string.IsNullOrWhiteSpace(item.Query))
            .GroupBy(item => item.Query, StringComparer.OrdinalIgnoreCase)
            .Select(group => new CustomerLocationRequest(
                group.Key,
                group.Select(item => item.Customer).ToList()))
            .ToList();

        _totalLocationCount = customerGroups.Count;

        var cache = new ConcurrentDictionary<string, CachedLocation>(
            await LoadLocationCacheAsync(),
            StringComparer.OrdinalIgnoreCase);

        var pending = new List<CustomerLocationRequest>();
        foreach (var request in customerGroups)
        {
            if (cache.TryGetValue(request.Query, out var cached))
            {
                _locatedGroups.Add(new LocatedCustomerGroup(
                    new Location(cached.Latitude, cached.Longitude),
                    request.Query,
                    request.Customers));
                _resolvedLocationCount++;
            }
            else
            {
                pending.Add(request);
            }
        }
        RenderVisiblePins();
        UpdateStatusLabel();

        await Parallel.ForEachAsync(
            pending,
            new ParallelOptions { MaxDegreeOfParallelism = MaxConcurrentGeocoders },
            async (request, _) =>
            {
                try
                {
                    var locations = await Geocoding.Default.GetLocationsAsync(request.Query);
                    var location = locations?.FirstOrDefault();
                    if (location != null)
                    {
                        cache[request.Query] = new CachedLocation(location.Latitude, location.Longitude);
                        _newLocatedGroups[request.Query] = new LocatedCustomerGroup(
                            new Location(location.Latitude, location.Longitude),
                            request.Query,
                            request.Customers);
                    }
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"MapPage: Geocoding failed for {request.Query}: {ex.Message}");
                }

                var completed = Interlocked.Increment(ref _resolvedLocationCount);
                if (completed % ProgressiveRenderInterval == 0 || completed == _totalLocationCount)
                {
                    await MainThread.InvokeOnMainThreadAsync(() =>
                    {
                        RenderVisiblePins();
                        UpdateStatusLabel();
                    });
                }
            });

        _locatedGroups.AddRange(_newLocatedGroups.Values);
        _newLocatedGroups.Clear();
        await SaveLocationCacheAsync(cache);
        _locationsLoaded = true;

        RenderVisiblePins();
        UpdateStatusLabel();
    }

    private void RenderVisiblePins()
    {
        var locations = _locatedGroups
            .Concat(_newLocatedGroups.Values)
            .Where(group => !_renderedLocationAddresses.Contains(group.Address))
            .OrderBy(group => group.Address, StringComparer.OrdinalIgnoreCase)
            .ToList();

        if (locations.Count == 0)
            return;
        var pins = new List<Pin>(locations.Count);

        foreach (var locationGroup in locations)
        {
            if (!IsValidMapCoordinate(locationGroup.Location))
                continue;

            var customers = locationGroup.Customers;
            var pin = new Pin
            {
                Label = customers.Count == 1
                    ? customers[0].CustomerName
                    : $"{customers.Count} customers",
                Address = locationGroup.Address,
                Location = locationGroup.Location
            };

            pin.MarkerClicked += OnPinClicked;
            pins.Add(pin);
            _pinGroups[pin] = locationGroup;
            _renderedLocationAddresses.Add(locationGroup.Address);
            _renderedCustomerCount += customers.Count;
            _renderedLocationCount++;
        }

        CustomerMapView.AppendPins(pins);
    }

    private static bool IsValidMapCoordinate(Location location) =>
        location != null &&
        double.IsFinite(location.Latitude) &&
        double.IsFinite(location.Longitude) &&
        location.Latitude is >= -85 and <= 85 &&
        location.Longitude is >= -180 and <= 180;

    private void OnMapLoaded(object sender, EventArgs e)
    {
        _mapLoaded = true;
        EnsureInitialMapRegion();
        CustomerMapView.RefreshPins();
        RenderVisiblePins();
        UpdateStatusLabel();
    }

    private bool _initialRegionSet;

    private void EnsureInitialMapRegion()
    {
        if (!_mapLoaded || _initialRegionSet)
            return;

        _initialRegionSet = true;

        CustomerMapView.MoveToRegion(
            MapSpan.FromCenterAndRadius(
                new Location(39.8283, -98.5795),
                Distance.FromMiles(1_800)));
    }

    private async Task EnableUserLocationAsync()
    {
        var permission =
            await Permissions.CheckStatusAsync<Permissions.LocationWhenInUse>();

        if (permission != PermissionStatus.Granted)
        {
            permission =
                await Permissions.RequestAsync<Permissions.LocationWhenInUse>();
        }

        CustomerMapView.IsShowingUser =
            permission == PermissionStatus.Granted;
    }
    private void UpdateStatusLabel()
    {
        if (!_locationsLoaded)
        {
            return;
        }
    }

    private async void OnPinClicked(object sender, PinClickedEventArgs e)
    {
        e.HideInfoWindow = true;

        if (sender is not Pin pin || !_pinGroups.TryGetValue(pin, out var locationGroup))
            return;

        var customers = locationGroup.Customers;

        Customer selectedCustomer;
        if (customers.Count == 1)
        {
            selectedCustomer = customers[0];
        }
        else
        {
            var choices = customers
                .Select(customer => $"{customer.CustomerName} ({customer.OEM})")
                .ToArray();
            var choice = await DisplayActionSheet("Customers at this location", "Cancel", null, choices);
            var selectedIndex = Array.IndexOf(choices, choice);
            if (selectedIndex < 0)
                return;

            selectedCustomer = customers[selectedIndex];
        }

        await Navigation.PushAsync(new CustomerDetailPage(
            selectedCustomer,
            _databaseService,
            _authenticationService));
    }

    private static string BuildGeocodeQuery(Customer customer)
    {
        var postalCode = string.IsNullOrWhiteSpace(customer.Zip)
            ? customer.ShippingZip
            : customer.Zip;

        if (!string.IsNullOrWhiteSpace(postalCode))
        {
            return string.Join(", ", new[] { postalCode.Trim(), customer.Country?.Trim() }
                .Where(value => !string.IsNullOrWhiteSpace(value)));
        }

        return string.Join(", ", new[]
        {
            customer.Address1?.Trim(),
            customer.City?.Trim(),
            customer.State?.Trim(),
            customer.Country?.Trim()
        }.Where(value => !string.IsNullOrWhiteSpace(value)));
    }

    private static async Task<Dictionary<string, CachedLocation>> LoadLocationCacheAsync()
    {
        var path = Path.Combine(FileSystem.AppDataDirectory, LocationCacheFileName);
        if (!File.Exists(path))
            return new Dictionary<string, CachedLocation>(StringComparer.OrdinalIgnoreCase);

        try
        {
            var json = await File.ReadAllTextAsync(path);
            return JsonSerializer.Deserialize<Dictionary<string, CachedLocation>>(json)
                ?? new Dictionary<string, CachedLocation>(StringComparer.OrdinalIgnoreCase);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"MapPage: Unable to read location cache: {ex.Message}");
            return new Dictionary<string, CachedLocation>(StringComparer.OrdinalIgnoreCase);
        }
    }

    private static async Task SaveLocationCacheAsync(
        IEnumerable<KeyValuePair<string, CachedLocation>> locations)
    {
        try
        {
            var path = Path.Combine(FileSystem.AppDataDirectory, LocationCacheFileName);
            var cache = locations.ToDictionary(item => item.Key, item => item.Value, StringComparer.OrdinalIgnoreCase);
            await File.WriteAllTextAsync(path, JsonSerializer.Serialize(cache));
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"MapPage: Unable to save location cache: {ex.Message}");
        }
    }

    private sealed record CustomerLocationRequest(string Query, List<Customer> Customers);
    private sealed record LocatedCustomerGroup(Location Location, string Address, List<Customer> Customers);
    private sealed record CachedLocation(double Latitude, double Longitude);
}
