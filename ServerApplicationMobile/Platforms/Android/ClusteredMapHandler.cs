#nullable enable

using Android.Gms.Maps;
using Android.Gms.Maps.Model;
using Com.Google.Maps.Android.Clustering;
using Com.Google.Maps.Android.Clustering.Algo;
using Microsoft.Maui.Controls.Maps;
using Microsoft.Maui.Maps;
using Microsoft.Maui.Maps.Handlers;
using ServerApplicationMobile.Controls;
using static Com.Google.Maps.Android.Clustering.ClusterManager;
using MauiMap = Microsoft.Maui.Maps.IMap;

namespace ServerApplicationMobile.Handlers;

public sealed class ClusteredMapHandler : MapHandler
{
    private static readonly IPropertyMapper<MauiMap, IMapHandler> ClusteredMapper =
        new PropertyMapper<MauiMap, IMapHandler>(MapHandler.Mapper)
        {
            [nameof(MauiMap.Pins)] = MapClusteredPins
        };

    private GoogleMap? _nativeMap;
    private ClusterManager? _clusterManager;
    private ClusterClickListener? _clickListener;
    private MapReadyCallback? _mapReadyCallback;

    public ClusteredMapHandler()
        : base(ClusteredMapper, MapHandler.CommandMapper)
    {
    }

    protected override void ConnectHandler(MapView platformView)
    {
        base.ConnectHandler(platformView);
        _mapReadyCallback = new MapReadyCallback(this);
        platformView.GetMapAsync(_mapReadyCallback);
    }

    protected override void DisconnectHandler(MapView platformView)
    {
        if (_nativeMap != null)
        {
            _nativeMap.SetOnCameraIdleListener(null);
            _nativeMap.SetOnMarkerClickListener(null);
        }

        _clusterManager?.ClearItems();
        _clusterManager?.Dispose();
        _clickListener?.Dispose();
        _mapReadyCallback?.Dispose();
        _clusterManager = null;
        _clickListener = null;
        _mapReadyCallback = null;
        _nativeMap = null;

        base.DisconnectHandler(platformView);
    }

    private static void MapClusteredPins(IMapHandler handler, MauiMap map)
    {
        if (map is ClusteredMap { IsReplacingPins: true })
            return;

        ((ClusteredMapHandler)handler).UpdateClusters();
    }

    private void OnMapReady(GoogleMap nativeMap)
    {
        _nativeMap = nativeMap;
        _clusterManager = new ClusterManager(Context, nativeMap)
        {
            Algorithm = new NonHierarchicalDistanceBasedAlgorithm()
        };
        _clickListener = new ClusterClickListener(this);

        _clusterManager.SetOnClusterItemClickListener(_clickListener);
        _clusterManager.SetOnClusterClickListener(_clickListener);
        nativeMap.SetOnCameraIdleListener(_clusterManager);
        nativeMap.SetOnMarkerClickListener(_clusterManager);

        UpdateClusters();
    }

    private void UpdateClusters()
    {
        if (_clusterManager == null || VirtualView == null)
            return;

        _clusterManager.ClearItems();
        foreach (var pin in VirtualView.Pins.OfType<Pin>())
            _clusterManager.AddItem(new CustomerClusterItem(pin));

        _clusterManager.Cluster();
    }

    private void OpenPin(CustomerClusterItem item)
    {
        item.Pin.SendMarkerClick();
    }

    private void ZoomToCluster(ICluster cluster)
    {
        if (_nativeMap == null)
            return;

        var bounds = new LatLngBounds.Builder();
        foreach (var item in cluster.Items.OfType<CustomerClusterItem>())
            bounds.Include(item.Position);

        try
        {
            _nativeMap.AnimateCamera(CameraUpdateFactory.NewLatLngBounds(bounds.Build(), 120));
        }
        catch (Java.Lang.IllegalStateException)
        {
            // The map can briefly have no measured size while a tab is changing.
        }
    }

    private sealed class MapReadyCallback(ClusteredMapHandler owner)
        : Java.Lang.Object, IOnMapReadyCallback
    {
        private readonly WeakReference<ClusteredMapHandler> _owner = new(owner);

        public void OnMapReady(GoogleMap googleMap)
        {
            if (_owner.TryGetTarget(out var handler))
                handler.OnMapReady(googleMap);
        }
    }

    private sealed class ClusterClickListener(ClusteredMapHandler owner)
        : Java.Lang.Object, IOnClusterItemClickListener, IOnClusterClickListener
    {
        private readonly WeakReference<ClusteredMapHandler> _owner = new(owner);

        public bool OnClusterItemClick(Java.Lang.Object? nativeItem)
        {
            if (nativeItem is CustomerClusterItem item && _owner.TryGetTarget(out var handler))
                handler.OpenPin(item);

            return true;
        }

        public bool OnClusterClick(ICluster? cluster)
        {
            if (cluster != null && _owner.TryGetTarget(out var handler))
                handler.ZoomToCluster(cluster);

            return true;
        }
    }

    private sealed class CustomerClusterItem(Pin pin) : Java.Lang.Object, IClusterItem
    {
        public Pin Pin { get; } = pin;
        public LatLng Position { get; } = new(pin.Location.Latitude, pin.Location.Longitude);
        public string Title => Pin.Label ?? string.Empty;
        public string Snippet => Pin.Address ?? string.Empty;
        public Java.Lang.Float ZIndex => (Java.Lang.Float)1.0f;
    }
}
