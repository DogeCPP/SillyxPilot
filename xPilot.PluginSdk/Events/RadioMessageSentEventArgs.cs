using System;

namespace Vatsim.Xpilot.PluginSdk.Events;

/// <summary>
/// Arguments for the RadioMessageSent event.
/// </summary>
public class RadioMessageSentEventArgs : EventArgs
{
    /// <summary>
    /// Gets the message contents.
    /// </summary>
    public string Message { get; }

    /// <summary>
    /// Initializes a new instance of the <see cref="RadioMessageSentEventArgs"/> class.
    /// </summary>
    /// <param name="message">The message contents.</param>
    public RadioMessageSentEventArgs(string message)
    {
        Message = message;
    }
}
