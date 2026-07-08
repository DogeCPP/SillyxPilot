using System;

namespace Vatsim.Xpilot.PluginSdk.Exceptions;

/// <summary>
/// The exception that is thrown when neither COM radio is configured for transmit or the avionics power is off,
/// and a request is made to transmit a radio message.
/// </summary>
public class NotTransmittingException : Exception
{
    /// <summary>
    /// Initializes a new instance of the <see cref="NotTransmittingException"/> class.
    /// </summary>
    public NotTransmittingException()
        : base("Not currently transmitting on any frequency.")
    {
    }
}
