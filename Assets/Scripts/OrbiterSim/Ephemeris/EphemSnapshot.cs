using UdonSharp;
using UnityEngine;

public class EphemSnapshot : UdonSharpBehaviour
{
    [Header("Time")]
    public double t;      // mission seconds (from SimClock)
    public double jd;     // Julian Date (UTC~UT1 assumption for now)
    public double mjd;    // Modified Julian Date

    [Header("Sun (SSB/Ecliptic inertial)")]
    public double sun_rx, sun_ry, sun_rz;
    public double sun_vx, sun_vy, sun_vz;

    [Header("Earth (SSB/Ecliptic inertial)")]
    public double earth_rx, earth_ry, earth_rz;
    public double earth_vx, earth_vy, earth_vz;

    // Earth rotation state in inertial
    public double earth_omega_x, earth_omega_y, earth_omega_z;   // rad/s in inertial
    public float  earth_qx, earth_qy, earth_qz, earth_qw;        // body-fixed -> inertial rotation

    [Header("Moon (SSB/Ecliptic inertial)")]
    public double moon_rx, moon_ry, moon_rz;
    public double moon_vx, moon_vy, moon_vz;

    // Moon rotation state in inertial
    public double moon_omega_x, moon_omega_y, moon_omega_z;      // rad/s in inertial
    public float  moon_qx, moon_qy, moon_qz, moon_qw;            // body-fixed -> inertial rotation
}
