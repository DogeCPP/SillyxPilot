using System;

namespace Vatsim.Xpilot.PluginSdk.Events;

/// <summary>
/// Arguments for the AircraftUpdated event.
/// </summary>
public class AircraftUpdatedEventArgs : EventArgs
{
    /// <summary>
    /// Gets the aircraft callsign.
    /// </summary>
    public string Callsign { get; }

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
    /// Initializes a new instance of the <see cref="AircraftUpdatedEventArgs"/> class.
    /// </summary>
    /// <param name="callsign">The aircraft callsign.</param>
    /// <param name="lat">The aircraft latitude.</param>
    /// <param name="lon">The aircraft longitude.</param>
    /// <param name="alt">The aircraft altitude.</param>
    /// <param name="pressureAlt">The aircraft's pressure altitude.</param>
    /// <param name="pitch">The aircraft pitch.</param>
    /// <param name="bank">The aircraft bank.</param>
    /// <param name="heading">The aircraft heading.</param>
    /// <param name="speed">The aircraft ground speed.</param>
    public AircraftUpdatedEventArgs(string callsign, double lat, double lon, double alt,
        double pressureAlt, double pitch, double bank, double heading, double speed)
    {
        Callsign = callsign;
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
