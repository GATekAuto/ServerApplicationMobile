#nullable enable

using Foundation;
using MapKit;
using Microsoft.Maui.Maps;
using Microsoft.Maui.Maps.Handlers;
using Microsoft.Maui.Maps.Platform;
using ObjCRuntime;
using ServerApplicationMobile.Controls;
using UIKit;
using MauiMap = Microsoft.Maui.Maps.IMap;

namespace ServerApplicationMobile.Handlers;

public sealed class ClusteredMapHandler : MapHandler
{
    private static readonly IPropertyMapper<MauiMap, IMapHandler> ClusteredMapper =
        new PropertyMapper<MauiMap, IMapHandler>(MapHandler.Mapper)
        {
            [nameof(MauiMap.Pins)] = MapClusteredPins
        };

    public ClusteredMapHandler()
        : base(ClusteredMapper, MapHandler.CommandMapper)
    {
    }

    protected override MauiMKMapView CreatePlatformView() => new ClusteredMauiMapView(this);

    private static void MapClusteredPins(IMapHandler handler, MauiMap map)
    {
        if (map is ClusteredMap { IsReplacingPins: true })
            return;

        MapHandler.MapPins(handler, map);
    }
}

internal sealed class ClusteredMauiMapView : MauiMKMapView
{
    private const string CustomerReuseIdentifier = "customer-pin";
    // A continental view still shows every customer cluster, while avoiding
    // MapKit's unstable all-world annotation layout on physical devices.
    private const double MaximumCameraDistanceMeters = 12_000_000;
    public ClusteredMauiMapView(IMapHandler handler)
        : base(handler)
    {
        // Native MapKit clustering on iOS 26 can leave its annotation container
        // treating an MKPointAnnotation as an MKClusterAnnotation while the map is
        // zooming. Keep view reuse and collision decluttering, but do not opt these
        // customer annotations into that unstable native cluster path.
        Register(typeof(MKMarkerAnnotationView), CustomerReuseIdentifier);
        GetViewForAnnotation = CreateAnnotationView;
        SetCameraZoomRange(
            new MKMapCameraZoomRange(100, MaximumCameraDistanceMeters),
            false);
    }

    private static MKAnnotationView CreateAnnotationView(MKMapView mapView, IMKAnnotation annotation)
    {
        if (Runtime.GetNSObject(annotation.Handle) is MKUserLocation)
            return null!;

        // Defensive only: no customer view receives a clustering identifier, so
        // MapKit should no longer create cluster annotations for this map.
        if (annotation is MKClusterAnnotation)
            return null!;

        var pinView = (MKMarkerAnnotationView)mapView.DequeueReusableAnnotation(
            CustomerReuseIdentifier,
            annotation);

        pinView.CanShowCallout = true;
        pinView.ClusteringIdentifier = null;
        pinView.DisplayPriority = MKFeatureDisplayPriority.DefaultLow;
        pinView.RightCalloutAccessoryView ??= new UIView();
        return pinView;
    }
}
