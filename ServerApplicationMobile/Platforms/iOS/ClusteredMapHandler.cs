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
    private static readonly NSString CustomerClusterIdentifier = new("customer-locations");

    public ClusteredMauiMapView(IMapHandler handler)
        : base(handler)
    {
        // Register the two view types before any annotations are added. Using
        // MapKit's dedicated cluster reuse queue prevents an ordinary point
        // annotation view from being recycled as a cluster view.
        Register(typeof(MKMarkerAnnotationView), CustomerReuseIdentifier);
        Register(
            typeof(MKMarkerAnnotationView),
            MKMapViewDefault.ClusterAnnotationViewReuseIdentifier.ToString());
        GetViewForAnnotation = CreateAnnotationView;
        DidSelectAnnotationView += OnAnnotationSelected;
        SetCameraZoomRange(
            new MKMapCameraZoomRange(100, MaximumCameraDistanceMeters),
            false);
    }

    private static MKAnnotationView CreateAnnotationView(MKMapView mapView, IMKAnnotation annotation)
    {
        if (Runtime.GetNSObject(annotation.Handle) is MKUserLocation)
            return null!;

        // Returning null lets MapKit create and manage MKClusterAnnotation views
        // through its reserved cluster reuse identifier. The previous manual
        // assignment could leave MapKit associating a recycled cluster view with
        // an MKPointAnnotation and crash on the native memberAnnotations selector.
        if (annotation is MKClusterAnnotation)
            return null!;

        var pinView = (MKMarkerAnnotationView)mapView.DequeueReusableAnnotation(
            CustomerReuseIdentifier,
            annotation);

        pinView.CanShowCallout = true;
        pinView.ClusteringIdentifier = CustomerClusterIdentifier;
        pinView.RightCalloutAccessoryView ??= new UIView();
        return pinView;
    }

    private void OnAnnotationSelected(object? sender, MKAnnotationViewEventArgs e)
    {
        if (e.View.Annotation is not MKClusterAnnotation cluster)
            return;

        try
        {
            ShowAnnotations(cluster.MemberAnnotations, true);
            DeselectAnnotation(cluster, false);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Unable to expand map cluster: {ex.Message}");
        }
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
            DidSelectAnnotationView -= OnAnnotationSelected;

        base.Dispose(disposing);
    }
}
