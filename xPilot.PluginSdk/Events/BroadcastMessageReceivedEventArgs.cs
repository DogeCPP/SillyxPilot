using System;

namespace Vatsim.Xpilot.PluginSdk.Events;

/// <summary>
/// Arguments for the BroadcastMessageReceived event.
/// </summary>
public class BroadcastMessageReceivedEventArgs : EventArgs
{
    /// <summary>
    /// Gets the sender's callsign.
    /// </summary>
    public string From { get; }

    /// <summary>
    /// Gets the message contents.
    /// </summary>
    public string Message { get; }

    /// <summary>
    /// Initializes a new instance of the <see cref="BroadcastMessageReceivedEventArgs"/> class.
    /// </summary>
    /// <param name="from">The sender's callsign.</param>
    /// <param name="message">The message contents.</param>
    public BroadcastMessageReceivedEventArgs(string from, string message)
    {
        From = from;
        Message = message;
    }
}
