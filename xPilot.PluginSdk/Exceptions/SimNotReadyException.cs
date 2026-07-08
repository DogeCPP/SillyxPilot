using System;

namespace Vatsim.Xpilot.PluginSdk.Exceptions;

/// <summary>
/// The exception that is thrown when a request is made to connect to the network when xPilot does not have an active connection to the simulator.
/// </summary>
public class SimNotReadyException : Exception
{
    /// <summary>
    /// Initializes a new instance of the <see cref="SimNotReadyException"/> class.
    /// </summary>
    public SimNotReadyException()
        : base("The simulator is not ready.")
    {
    }
}
