using UdonSharp;
using UnityEngine;
using System;

public class EphemerisSystem : UdonSharpBehaviour
{
    [Header("Output")]
    public EphemSnapshot snapshot;

    [Header("Time -> JD")]
    [Tooltip("Julian Date at T=0. This defines the scenario epoch (UTC~UT1 for now).")]
    public double jd0 = 2460000.5; // example; set per scenario

    [Header("Body solvers")]
    public EarthMoonEphemerisBarycentric posModel;
    public EarthRotationModelSimple rotModel;
    public void Evaluate(double t)
    {
        if (snapshot == null) return;

        snapshot.t = t;
        double jd = jd0 + t / 86400.0;
        snapshot.jd = jd;
        snapshot.mjd = jd - 2400000.5;

        // Position/velocity model
        if (posModel != null)
        {
            posModel.Evaluate(jd,
                out snapshot.sun_rx, out snapshot.sun_ry, out snapshot.sun_rz,
                out snapshot.sun_vx, out snapshot.sun_vy, out snapshot.sun_vz,
                out snapshot.earth_rx, out snapshot.earth_ry, out snapshot.earth_rz,
                out snapshot.earth_vx, out snapshot.earth_vy, out snapshot.earth_vz,
                out snapshot.moon_rx, out snapshot.moon_ry, out snapshot.moon_rz,
                out snapshot.moon_vx, out snapshot.moon_vy, out snapshot.moon_vz);
        }
        else
        {
            snapshot.sun_rx = snapshot.sun_ry = snapshot.sun_rz = 0.0;
            snapshot.sun_vx = snapshot.sun_vy = snapshot.sun_vz = 0.0;
        }

        // Rotation model (needs the states)
        if (rotModel != null)
        {
            rotModel.Evaluate(jd,
                snapshot.earth_rx, snapshot.earth_ry, snapshot.earth_rz,
                snapshot.earth_vx, snapshot.earth_vy, snapshot.earth_vz,
                snapshot.moon_rx, snapshot.moon_ry, snapshot.moon_rz,
                snapshot.moon_vx, snapshot.moon_vy, snapshot.moon_vz,
                out snapshot.earth_omega_x, out snapshot.earth_omega_y, out snapshot.earth_omega_z,
                out snapshot.earth_qx, out snapshot.earth_qy, out snapshot.earth_qz, out snapshot.earth_qw,
                out snapshot.moon_omega_x, out snapshot.moon_omega_y, out snapshot.moon_omega_z,
                out snapshot.moon_qx, out snapshot.moon_qy, out snapshot.moon_qz, out snapshot.moon_qw);
        }
        else
        {
            snapshot.earth_omega_x = snapshot.earth_omega_y = snapshot.earth_omega_z = 0.0;
            snapshot.earth_qx = 0; snapshot.earth_qy = 0; snapshot.earth_qz = 0; snapshot.earth_qw = 1;
            snapshot.moon_omega_x = snapshot.moon_omega_y = snapshot.moon_omega_z = 0.0;
            snapshot.moon_qx = 0; snapshot.moon_qy = 0; snapshot.moon_qz = 0; snapshot.moon_qw = 1;
        }


        // Earth->Moon relative
        double rx = snapshot.earth_rx - snapshot.sun_rx;
        double ry = snapshot.earth_ry - snapshot.sun_ry;
        double rz = snapshot.earth_rz - snapshot.sun_rz;

        double vx = snapshot.earth_vx - snapshot.sun_vx;
        double vy = snapshot.earth_vy - snapshot.sun_vy;
        double vz = snapshot.earth_vz - snapshot.sun_vz;

        // h = r x v in solver ecliptic frame
        double hx = ry*vz - rz*vy;
        double hy = rz*vx - rx*vz;
        double hz = rx*vy - ry*vx;

        // Debug.Log($"Moon h (solver): hx={hx:E3} hy={hy:E3} hz={hz:E3}");


    }



    
}