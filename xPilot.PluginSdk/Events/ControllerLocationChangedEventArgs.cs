using System;

namespace Vatsim.Xpilot.PluginSdk.Events;

/// <summary>
/// Arguments for the ControllerLocationChanged event.
/// </summary>
public class ControllerLocationChangedEventArgs : EventArgs
{
    /// <summary>
    /// Gets the controller's callsign.
    /// </summary>
    public string Callsign { get; }

    /// <summary>
    /// Gets the controller's new latitude.
    /// </summary>
    public double NewLatitude { get; }

    /// <summary>
    /// Gets the controller's new longitude.
    /// </summary>
    public double NewLongitude { get; }

    /// <summary>
    /// Initializes a new instance of the <see cref="ControllerLocationChangedEventArgs"/> class.
    /// </summary>
    /// <param name="callsign">The controller's callsign.</param>
    /// <param name="newLatitude">The controller's new latitude.</param>
    /// <param name="newLongitude">The controller's new longitude.</param>
    public ControllerLocationChangedEventArgs(string callsign, double newLatitude, double newLongitude)
    {
        Callsign = callsign;
        NewLatitude = newLatitude;
        NewLongitude = newLongitude;
    }
}
