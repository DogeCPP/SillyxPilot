using System;

namespace Vatsim.Xpilot.PluginSdk.Events;

/// <summary>
/// Arguments for the ControllerDeleted event.
/// </summary>
public class ControllerDeletedEventArgs : EventArgs
{
    /// <summary>
    /// Gets the controller's callsign.
    /// </summary>
    public string Callsign { get; }

    /// <summary>
    /// Initializes a new instance of the <see cref="ControllerDeletedEventArgs"/> class.
    /// </summary>
    /// <param name="callsign">The controller's callsign.</param>
    public ControllerDeletedEventArgs(string callsign)
    {
        Callsign = callsign;
    }
}
