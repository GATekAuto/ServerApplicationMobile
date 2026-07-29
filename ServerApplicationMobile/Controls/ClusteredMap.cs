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
}
