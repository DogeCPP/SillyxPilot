using System;

namespace Vatsim.Xpilot.PluginSdk.Exceptions;

/// <summary>
/// The exception that is thrown when xPilot is not connected to the network when a request is made which requires a connection.
/// </summary>
public class NotConnectedException : Exception
{
    /// <summary>
    /// Initializes a new instance of the <see cref="NotConnectedException"/> class.
    /// </summary>
    public NotConnectedException()
        : base("Not connected to the network.")
    {
    }
}
