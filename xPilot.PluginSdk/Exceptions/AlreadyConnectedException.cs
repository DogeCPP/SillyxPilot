using System;

namespace Vatsim.Xpilot.PluginSdk.Exceptions;

/// <summary>
/// The exception that is thrown when a connection request is made while xPilot is already connected to the network.
/// </summary>
public class AlreadyConnectedException : Exception
{
    /// <summary>
    /// Initializes a new instance of the <see cref="AlreadyConnectedException"/> class.
    /// </summary>
    public AlreadyConnectedException()
        : base("Already connected to the network.")
    {
    }
}
