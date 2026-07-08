using System;

namespace Vatsim.Xpilot.PluginSdk.Events;

/// <summary>
/// Arguments for the AircraftDeleted event.
/// </summary>
public class AircraftDeletedEventArgs : EventArgs
{
    /// <summary>
    /// Gets the aircraft callsign.
    /// </summary>
    public string Callsign { get; }

    /// <summary>
    /// Initializes a new instance of the <see cref="AircraftDeletedEventArgs"/> class.
    /// </summary>
    /// <param name="callsign">The aircraft callsign.</param>
    public AircraftDeletedEventArgs(string callsign)
    {
        Callsign = callsign;
    }
}
