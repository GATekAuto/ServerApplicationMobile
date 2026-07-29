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
    private const string ClusterReuseIdentifier = "customer-cluster";
    private static readonly NSString CustomerClusterIdentifier = new("customer-locations");

    public ClusteredMauiMapView(IMapHandler handler)
        : base(handler)
    {
        GetViewForAnnotation = CreateAnnotationView;
        DidSelectAnnotationView += OnAnnotationSelected;
    }

    private static MKAnnotationView CreateAnnotationView(MKMapView mapView, IMKAnnotation annotation)
    {
        if (Runtime.GetNSObject(annotation.Handle) is MKUserLocation)
            return null!;

        if (annotation is MKClusterAnnotation cluster)
        {
            var clusterView = mapView.DequeueReusableAnnotation(ClusterReuseIdentifier)
                as MKMarkerAnnotationView
                ?? new MKMarkerAnnotationView(annotation, ClusterReuseIdentifier);

            clusterView.Annotation = annotation;
            clusterView.CanShowCallout = false;
            clusterView.MarkerTintColor = UIColor.SystemBlue;
            clusterView.GlyphText = cluster.MemberAnnotations.Length.ToString();
            return clusterView;
        }

        var pinView = mapView.DequeueReusableAnnotation(CustomerReuseIdentifier)
            as MKMarkerAnnotationView
            ?? new MKMarkerAnnotationView(annotation, CustomerReuseIdentifier);

        pinView.Annotation = annotation;
        pinView.CanShowCallout = true;
        pinView.ClusteringIdentifier = CustomerClusterIdentifier;
        pinView.RightCalloutAccessoryView ??= new UIView();
        return pinView;
    }

    private void OnAnnotationSelected(object? sender, MKAnnotationViewEventArgs e)
    {
        if (e.View.Annotation is not MKClusterAnnotation cluster)
            return;

        ShowAnnotations(cluster.MemberAnnotations, true);
        DeselectAnnotation(cluster, false);
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
            DidSelectAnnotationView -= OnAnnotationSelected;

        base.Dispose(disposing);
    }
}
