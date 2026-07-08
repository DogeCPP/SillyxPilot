using System;

namespace Vatsim.Xpilot.PluginSdk.Events;

/// <summary>
/// Arguments for the SelcalAlertReceived event.
/// </summary>
public class SelcalAlertReceivedEventArgs : EventArgs
{
    /// <summary>
    /// Gets the frequencies on which the SELCAL alert was received, in Hz.
    /// Example: <c>123725000</c> represents 123.725 MHz.
    /// </summary>
    public int[] Frequencies { get; }

    /// <summary>
    /// Gets the callsign for the controller that sent the SELCAL alert.
    /// </summary>
    public string From { get; }

    /// <summary>
    /// Initializes a new instance of the <see cref="SelcalAlertReceivedEventArgs"/> class.
    /// </summary>
    /// <param name="frequencies">The frequencies on which the alert was received, in Hz.</param>
    /// <param name="from">The callsign for the controller that sent the SELCAL alert.</param>
    public SelcalAlertReceivedEventArgs(int[] frequencies, string from)
    {
        Frequencies = frequencies;
        From = from;
    }
}
