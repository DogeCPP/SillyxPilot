using System;

namespace Vatsim.Xpilot.PluginSdk.Events;

/// <summary>
/// Arguments for the NetworkConnected event.
/// </summary>
public class NetworkConnectedEventArgs : EventArgs
{
    /// <summary>
    /// Gets the user's CID.
    /// </summary>
    public string Cid { get; }

    /// <summary>
    /// Gets the callsign specified for the connection.
    /// </summary>
    public string Callsign { get; }

    /// <summary>
    /// Gets the aircraft type code specified for the connection.
    /// </summary>
    public string TypeCode { get; }

    /// <summary>
    /// Gets the SELCAL code specified for the connection, if any.
    /// </summary>
    public string SelcalCode { get; }

    /// <summary>
    /// Gets a value indicating whether the connection is in observer mode.
    /// </summary>
    public bool ObserverMode { get; }

    /// <summary>
    /// Initializes a new instance of the <see cref="NetworkConnectedEventArgs"/> class.
    /// </summary>
    /// <param name="cid">The user's CID.</param>
    /// <param name="callsign">The callsign specified for the connection.</param>
    /// <param name="typeCode">The aircraft type code specified for the connection.</param>
    /// <param name="selcalCode">The SELCAL code specified for the connection, if any.</param>
    /// <param name="observerMode">Whether or not the connection is in observer mode.</param>
    public NetworkConnectedEventArgs(string cid, string callsign, string typeCode, string selcalCode, bool observerMode)
    {
        Cid = cid;
        Callsign = callsign;
        TypeCode = typeCode;
        SelcalCode = selcalCode;
        ObserverMode = observerMode;
    }
}
