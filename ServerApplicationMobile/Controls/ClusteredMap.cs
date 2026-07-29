using Microsoft.Maui.Controls.Maps;
using MauiMapControl = Microsoft.Maui.Controls.Maps.Map;
using MauiMap = Microsoft.Maui.Maps.IMap;

namespace ServerApplicationMobile.Controls;

public sealed class ClusteredMap : MauiMapControl
{
    internal bool IsReplacingPins { get; private set; }

    public void ReplacePins(IEnumerable<Pin> pins)
    {
        IsReplacingPins = true;

        try
        {
            Pins.Clear();
            foreach (var pin in pins)
                Pins.Add(pin);
        }
        finally
        {
            IsReplacingPins = false;
        }

        Handler?.UpdateValue(nameof(MauiMap.Pins));
    }

    public void AppendPins(IEnumerable<Pin> pins)
    {
        var additions = pins as IReadOnlyCollection<Pin> ?? pins.ToList();
        if (additions.Count == 0)
            return;

        IsReplacingPins = true;

        try
        {
            foreach (var pin in additions)
                Pins.Add(pin);
        }
        finally
        {
            IsReplacingPins = false;
        }

        Handler?.UpdateValue(nameof(MauiMap.Pins));
    }

    public void RefreshPins() => Handler?.UpdateValue(nameof(MauiMap.Pins));
}
