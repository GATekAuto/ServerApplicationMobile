#nullable enable

using MapKit;
using Microsoft.Maui.Maps;
using Microsoft.Maui.Maps.Handlers;
using Microsoft.Maui.Maps.Platform;
using ServerApplicationMobile.Controls;
using MauiMap = Microsoft.Maui.Maps.IMap;

namespace ServerApplicationMobile.Handlers;

public sealed class ClusteredMapHandler : MapHandler
{
    // Keep enough range to show the continental US without allowing MapKit's
    // most extreme all-world camera layout on physical iOS devices.
    private const double MaximumCameraDistanceMeters = 12_000_000;

    private static readonly IPropertyMapper<MauiMap, IMapHandler> ClusteredMapper =
        new PropertyMapper<MauiMap, IMapHandler>(MapHandler.Mapper)
        {
            [nameof(MauiMap.Pins)] = MapClusteredPins
        };

    public ClusteredMapHandler()
        : base(ClusteredMapper, MapHandler.CommandMapper)
    {
    }

    protected override void ConnectHandler(MauiMKMapView platformView)
    {
        base.ConnectHandler(platformView);

        // Use MAUI/MapKit's stock annotation view pipeline on iOS. Installing a
        // custom view callback here previously allowed MapKit's reuse container
        // to send MKClusterAnnotation selectors to an MKPointAnnotation and abort
        // the process while zooming. No iOS annotation is opted into clustering.
        platformView.SetCameraZoomRange(
            new MKMapCameraZoomRange(100, MaximumCameraDistanceMeters),
            false);
    }

    private static void MapClusteredPins(IMapHandler handler, MauiMap map)
    {
        if (map is ClusteredMap { IsReplacingPins: true })
            return;

        MapHandler.MapPins(handler, map);
    }
}
