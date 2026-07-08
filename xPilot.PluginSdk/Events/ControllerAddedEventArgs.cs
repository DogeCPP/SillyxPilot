using System;

namespace Vatsim.Xpilot.PluginSdk.Events;

/// <summary>
/// Arguments for the ControllerAdded event.
/// </summary>
public class ControllerAddedEventArgs : EventArgs
{
    /// <summary>
    /// Gets the controller's callsign.
    /// </summary>
    public string Callsign { get; }

    /// <summary>
    /// Gets the controller's primary frequency, in Hz.
    /// Example: <c>123725000</c> represents 123.725 MHz.
    /// </summary>
    public int Frequency { get; }

    /// <summary>
    /// Gets the controller's latitude.
    /// </summary>
    public double Latitude { get; }

    /// <summary>
    /// Gets the controller's longitude.
    /// </summary>
    public double Longitude { get; }

    /// <summary>
    /// Initializes a new instance of the <see cref="ControllerAddedEventArgs"/> class.
    /// </summary>
    /// <param name="callsign">The controller's callsign.</param>
    /// <param name="frequency">The controller's primary frequency, in Hz.</param>
    /// <param name="latitude">The controller's latitude.</param>
    /// <param name="longitude">The controller's longitude.</param>
    public ControllerAddedEventArgs(string callsign, int frequency, double latitude, double longitude)
    {
        Callsign = callsign;
        Frequency = frequency;
        Latitude = latitude;
        Longitude = longitude;
    }
}
