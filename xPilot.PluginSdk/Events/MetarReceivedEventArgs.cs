using System;

namespace Vatsim.Xpilot.PluginSdk.Events;

/// <summary>
/// Arguments for the MetarReceived event.
/// </summary>
public class MetarReceivedEventArgs : EventArgs
{
    /// <summary>
    /// Gets the full raw METAR.
    /// </summary>
    public string Metar { get; }

    /// <summary>
    /// Initializes a new instance of the <see cref="MetarReceivedEventArgs"/> class.
    /// </summary>
    /// <param name="metar">The full raw METAR.</param>
    public MetarReceivedEventArgs(string metar)
    {
        Metar = metar;
    }
}
