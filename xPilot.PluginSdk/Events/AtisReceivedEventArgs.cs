using System;
using System.Collections.Generic;

namespace Vatsim.Xpilot.PluginSdk.Events;

/// <summary>
/// Arguments for the AtisReceived event.
/// </summary>
public class AtisReceivedEventArgs : EventArgs
{
    /// <summary>
    /// Gets the sender's callsign.
    /// </summary>
    public string From { get; }

    /// <summary>
    /// Gets the ATIS lines.
    /// </summary>
    public List<string> Lines { get; }

    /// <summary>
    /// Initializes a new instance of the <see cref="AtisReceivedEventArgs"/> class.
    /// </summary>
    /// <param name="from">The sender's callsign.</param>
    /// <param name="lines">The ATIS lines.</param>
    public AtisReceivedEventArgs(string from, List<string> lines)
    {
        From = from;
        Lines = lines;
    }
}
