using System;

namespace Vatsim.Xpilot.PluginSdk.Events;

/// <summary>
/// Arguments for the ControllerFrequencyChanged event.
/// </summary>
public class ControllerFrequencyChangedEventArgs : EventArgs
{
    /// <summary>
    /// Gets the controller's callsign.
    /// </summary>
    public string Callsign { get; }

    /// <summary>
    /// Gets the controller's new primary frequency, in Hz.
    /// Example: <c>123725000</c> represents 123.725 MHz.
    /// </summary>
    public int NewFrequency { get; }

    /// <summary>
    /// Initializes a new instance of the <see cref="ControllerFrequencyChangedEventArgs"/> class.
    /// </summary>
    /// <param name="callsign">The controller's callsign.</param>
    /// <param name="newFrequency">The controller's new primary frequency, in Hz.</param>
    public ControllerFrequencyChangedEventArgs(string callsign, int newFrequency)
    {
        Callsign = callsign;
        NewFrequency = newFrequency;
    }
}
