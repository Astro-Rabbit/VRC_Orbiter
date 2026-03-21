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


    public double MissionTimeToJD(double t)
    {
        return jd0 + t / 86400.0;
    }

    public void EvaluateAtTime(double t,
        out double jd,
        out double sun_rx, out double sun_ry, out double sun_rz,
        out double sun_vx, out double sun_vy, out double sun_vz,
        out double earth_rx, out double earth_ry, out double earth_rz,
        out double earth_vx, out double earth_vy, out double earth_vz,
        out double moon_rx, out double moon_ry, out double moon_rz,
        out double moon_vx, out double moon_vy, out double moon_vz,
        out double earth_omega_x, out double earth_omega_y, out double earth_omega_z,
        out float earth_qx, out float earth_qy, out float earth_qz, out float earth_qw,
        out double moon_omega_x, out double moon_omega_y, out double moon_omega_z,
        out float moon_qx, out float moon_qy, out float moon_qz, out float moon_qw)
    {
        jd = MissionTimeToJD(t);

        if (posModel != null)
        {
            posModel.Evaluate(jd,
                out sun_rx, out sun_ry, out sun_rz,
                out sun_vx, out sun_vy, out sun_vz,
                out earth_rx, out earth_ry, out earth_rz,
                out earth_vx, out earth_vy, out earth_vz,
                out moon_rx, out moon_ry, out moon_rz,
                out moon_vx, out moon_vy, out moon_vz);
        }
        else
        {
            sun_rx = sun_ry = sun_rz = 0.0;
            sun_vx = sun_vy = sun_vz = 0.0;

            earth_rx = earth_ry = earth_rz = 0.0;
            earth_vx = earth_vy = earth_vz = 0.0;

            moon_rx = moon_ry = moon_rz = 0.0;
            moon_vx = moon_vy = moon_vz = 0.0;
        }

        if (rotModel != null)
        {
            rotModel.Evaluate(jd,
                earth_rx, earth_ry, earth_rz,
                earth_vx, earth_vy, earth_vz,
                moon_rx, moon_ry, moon_rz,
                moon_vx, moon_vy, moon_vz,
                out earth_omega_x, out earth_omega_y, out earth_omega_z,
                out earth_qx, out earth_qy, out earth_qz, out earth_qw,
                out moon_omega_x, out moon_omega_y, out moon_omega_z,
                out moon_qx, out moon_qy, out moon_qz, out moon_qw);
        }
        else
        {
            earth_omega_x = earth_omega_y = earth_omega_z = 0.0;
            earth_qx = 0f; earth_qy = 0f; earth_qz = 0f; earth_qw = 1f;

            moon_omega_x = moon_omega_y = moon_omega_z = 0.0;
            moon_qx = 0f; moon_qy = 0f; moon_qz = 0f; moon_qw = 1f;
        }
    }

    public void SampleBodyStateAtTime(byte bodyId, double t,
        out double rx, out double ry, out double rz,
        out double vx, out double vy, out double vz,
        out double ox, out double oy, out double oz,
        out Quaternion qPF2E)
    {
        rx = ry = rz = 0.0;
        vx = vy = vz = 0.0;
        ox = oy = oz = 0.0;
        qPF2E = Quaternion.identity;

        double jd;

        double sun_rx, sun_ry, sun_rz;
        double sun_vx, sun_vy, sun_vz;
        double earth_rx, earth_ry, earth_rz;
        double earth_vx, earth_vy, earth_vz;
        double moon_rx, moon_ry, moon_rz;
        double moon_vx, moon_vy, moon_vz;
        double earth_omega_x, earth_omega_y, earth_omega_z;
        float earth_qx, earth_qy, earth_qz, earth_qw;
        double moon_omega_x, moon_omega_y, moon_omega_z;
        float moon_qx, moon_qy, moon_qz, moon_qw;

        EvaluateAtTime(t,
            out jd,
            out sun_rx, out sun_ry, out sun_rz,
            out sun_vx, out sun_vy, out sun_vz,
            out earth_rx, out earth_ry, out earth_rz,
            out earth_vx, out earth_vy, out earth_vz,
            out moon_rx, out moon_ry, out moon_rz,
            out moon_vx, out moon_vy, out moon_vz,
            out earth_omega_x, out earth_omega_y, out earth_omega_z,
            out earth_qx, out earth_qy, out earth_qz, out earth_qw,
            out moon_omega_x, out moon_omega_y, out moon_omega_z,
            out moon_qx, out moon_qy, out moon_qz, out moon_qw);

        if (bodyId == 0)
        {
            rx = sun_rx; ry = sun_ry; rz = sun_rz;
            vx = sun_vx; vy = sun_vy; vz = sun_vz;
            qPF2E = Quaternion.identity;
            return;
        }

        if (bodyId == 1)
        {
            rx = earth_rx; ry = earth_ry; rz = earth_rz;
            vx = earth_vx; vy = earth_vy; vz = earth_vz;
            ox = earth_omega_x; oy = earth_omega_y; oz = earth_omega_z;
            qPF2E = new Quaternion(earth_qx, earth_qy, earth_qz, earth_qw);
            return;
        }

        if (bodyId == 2)
        {
            rx = moon_rx; ry = moon_ry; rz = moon_rz;
            vx = moon_vx; vy = moon_vy; vz = moon_vz;
            ox = moon_omega_x; oy = moon_omega_y; oz = moon_omega_z;
            qPF2E = new Quaternion(moon_qx, moon_qy, moon_qz, moon_qw);
            return;
        }
    }

    
}