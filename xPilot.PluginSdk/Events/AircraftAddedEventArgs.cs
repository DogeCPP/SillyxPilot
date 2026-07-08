using System;

namespace Vatsim.Xpilot.PluginSdk.Events;

/// <summary>
/// Arguments for the AircraftAdded event.
/// </summary>
public class AircraftAddedEventArgs : EventArgs
{
    /// <summary>
    /// Gets the aircraft callsign.
    /// </summary>
    public string Callsign { get; }

    /// <summary>
    /// Gets the aircraft type code.
    /// </summary>
    public string TypeCode { get; }

    /// <summary>
    /// Gets the aircraft latitude.
    /// </summary>
    public double Latitude { get; }

    /// <summary>
    /// Gets the aircraft longitude.
    /// </summary>
    public double Longitude { get; }

    /// <summary>
    /// Gets the aircraft altitude.
    /// </summary>
    public double Altitude { get; }

    /// <summary>
    /// Gets the aircraft's pressure altitude.
    /// </summary>
    public double PressureAltitude { get; }

    /// <summary>
    /// Gets the aircraft pitch.
    /// </summary>
    public double Pitch { get; }

    /// <summary>
    /// Gets the aircraft bank.
    /// </summary>
    public double Bank { get; }

    /// <summary>
    /// Gets the aircraft heading.
    /// </summary>
    public double Heading { get; }

    /// <summary>
    /// Gets the aircraft ground speed.
    /// </summary>
    public double Speed { get; }

    /// <summary>
    /// Initializes a new instance of the <see cref="AircraftAddedEventArgs"/> class.
    /// </summary>
    /// <param name="callsign">The aircraft callsign.</param>
    /// <param name="typeCode">The aircraft type code.</param>
    /// <param name="lat">The aircraft latitude.</param>
    /// <param name="lon">The aircraft longitude.</param>
    /// <param name="alt">The aircraft altitude.</param>
    /// <param name="pressureAlt">The aircraft's pressure altitude.</param>
    /// <param name="pitch">The aircraft pitch.</param>
    /// <param name="bank">The aircraft bank.</param>
    /// <param name="heading">The aircraft heading.</param>
    /// <param name="speed">The aircraft ground speed.</param>
    public AircraftAddedEventArgs(string callsign, string typeCode, double lat, double lon, double alt,
        double pressureAlt, double pitch, double bank, double heading, double speed)
    {
        Callsign = callsign;
        TypeCode = typeCode;
        Latitude = lat;
        Longitude = lon;
        Altitude = alt;
        PressureAltitude = pressureAlt;
        Pitch = pitch;
        Bank = bank;
        Heading = heading;
        Speed = speed;
    }
}
