using System;

namespace Vatsim.Xpilot.PluginSdk.Events;

/// <summary>
/// Arguments for the PrivateMessageSent event.
/// </summary>
public class PrivateMessageSentEventArgs : EventArgs
{
    /// <summary>
    /// Gets the recipient's callsign.
    /// </summary>
    public string To { get; }

    /// <summary>
    /// Gets the message contents.
    /// </summary>
    public string Message { get; }

    /// <summary>
    /// Initializes a new instance of the <see cref="PrivateMessageSentEventArgs"/> class.
    /// </summary>
    /// <param name="to">The recipient's callsign.</param>
    /// <param name="message">The message contents.</param>
    public PrivateMessageSentEventArgs(string to, string message)
    {
        To = to;
        Message = message;
    }
}
