using System;

namespace Vatsim.Xpilot.PluginSdk.Events;

/// <summary>
/// Arguments for the RadioMessageReceived event.
/// </summary>
public class RadioMessageReceivedEventArgs : EventArgs
{
    /// <summary>
    /// Gets the list of frequencies the message was transmitted on, in Hz.
    /// Example: <c>123725000</c> represents 123.725 MHz.
    /// </summary>
    public int[] Frequencies { get; }

    /// <summary>
    /// Gets the sender's callsign.
    /// </summary>
    public string From { get; }

    /// <summary>
    /// Gets the message contents.
    /// </summary>
    public string Message { get; }

    /// <summary>
    /// Initializes a new instance of the <see cref="RadioMessageReceivedEventArgs"/> class.
    /// </summary>
    /// <param name="frequencies">The list of frequencies the message was transmitted on, in Hz.</param>
    /// <param name="from">The sender's callsign.</param>
    /// <param name="message">The message contents.</param>
    public RadioMessageReceivedEventArgs(int[] frequencies, string from, string message)
    {
        Frequencies = frequencies;
        From = from;
        Message = message;
    }
}
