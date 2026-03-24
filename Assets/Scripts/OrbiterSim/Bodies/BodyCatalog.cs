using UdonSharp;
using UnityEngine;
using System;

public class BodyCatalog : UdonSharpBehaviour
{
    [Header("References")]
    public EphemSnapshot ephem;

    [Header("Body IDs")]
    public byte sunId   = 0;
    public byte earthId = 1;
    public byte moonId  = 2;

    [Header("Constants (m, kg, SI)")]
    public double muSun   = 1.32712440018e20;
    public double muEarth = 3.986004418e14;
    public double muMoon  = 4.9048695e12;

    public double earthMassKg = 5.9722e24;
    public double moonMassKg  = 7.342e22;

    public double earthRadiusM = 6371000.0;
    public double moonRadiusM  = 1737400.0;

    public double earthSOIRadiusM = 9.25e8; // ~925,000 km

    public double moonSOIRadiusM = 6.61e7;

    public double GetMu(byte bodyId)
    {
        if (bodyId == sunId) return muSun;
        if (bodyId == earthId) return muEarth;
        if (bodyId == moonId) return muMoon;
        return 0.0;
    }

    public double GetRadius(byte bodyId)
    {
        if (bodyId == earthId) return earthRadiusM;
        if (bodyId == moonId) return moonRadiusM;
        return 0.0;
    }

    public byte GetParentBodyId(byte bodyId)
    {
        if (bodyId == moonId)  return earthId;
        if (bodyId == earthId) return sunId;

        // Sun or unknown -> no higher parent in current system
        return sunId;
    }
    public double GetSOIRadius(byte bodyId)
    {
        if (bodyId == earthId) return earthSOIRadiusM;
        if (bodyId == moonId)  return moonSOIRadiusM;
        return 0.0;
    }

    // --- State extraction (SSB/Ecliptic inertial) ---
    public void GetBodyPos(byte bodyId, out double x, out double y, out double z)
    {
        x = y = z = 0.0;
        if (ephem == null) return;

        if (bodyId == sunId)
        {
            x = ephem.sun_rx; y = ephem.sun_ry; z = ephem.sun_rz;
        }
        else if (bodyId == earthId)
        {
            x = ephem.earth_rx; y = ephem.earth_ry; z = ephem.earth_rz;
        }
        else if (bodyId == moonId)
        {
            x = ephem.moon_rx; y = ephem.moon_ry; z = ephem.moon_rz;
        }
    }

    public void GetBodyVel(byte bodyId, out double x, out double y, out double z)
    {
        x = y = z = 0.0;
        if (ephem == null) return;

        if (bodyId == sunId)
        {
            x = ephem.sun_vx; y = ephem.sun_vy; z = ephem.sun_vz;
        }
        else if (bodyId == earthId)
        {
            x = ephem.earth_vx; y = ephem.earth_vy; z = ephem.earth_vz;
        }
        else if (bodyId == moonId)
        {
            x = ephem.moon_vx; y = ephem.moon_vy; z = ephem.moon_vz;
        }
    }

    public void GetBodyOmega(byte bodyId, out double ox, out double oy, out double oz)
    {
        ox = oy = oz = 0.0;
        if (ephem == null) return;

        if (bodyId == earthId)
        {
            ox = ephem.earth_omega_x; oy = ephem.earth_omega_y; oz = ephem.earth_omega_z;
        }
        else if (bodyId == moonId)
        {
            ox = ephem.moon_omega_x; oy = ephem.moon_omega_y; oz = ephem.moon_omega_z;
        }
    }

    // Surface/atmosphere velocity at position rRel (body-centered inertial) in SSB frame: v = ω × r
    public void GetSurfaceVelocity(byte bodyId, double rRel_x, double rRel_y, double rRel_z,
        out double vx, out double vy, out double vz)
    {
        double ox, oy, oz;
        GetBodyOmega(bodyId, out ox, out oy, out oz);

        vx = oy * rRel_z - oz * rRel_y;
        vy = oz * rRel_x - ox * rRel_z;
        vz = ox * rRel_y - oy * rRel_x;
    }

    public Quaternion GetBodyFixedToInertial(byte bodyId)
    {
        if (ephem == null) return Quaternion.identity;

        if (bodyId == earthId)
            return new Quaternion(ephem.earth_qx, ephem.earth_qy, ephem.earth_qz, ephem.earth_qw);
        if (bodyId == moonId)
            return new Quaternion(ephem.moon_qx, ephem.moon_qy, ephem.moon_qz, ephem.moon_qw);

        return Quaternion.identity;
    }

    // Relative vector craft->body, in inertial SSB
    public void GetCraftToBodyVector(byte bodyId, CraftStateModel craft, out double dx, out double dy, out double dz)
    {
        dx = dy = dz = 0.0;
        if (craft == null) return;

        double bx, by, bz;
        GetBodyPos(bodyId, out bx, out by, out bz);

        dx = craft.rx - bx;
        dy = craft.ry - by;
        dz = craft.rz - bz;
    }

    public double GetCraftDistanceToBody(byte bodyId, CraftStateModel craft)
    {
        double dx, dy, dz;
        GetCraftToBodyVector(bodyId, craft, out dx, out dy, out dz);
        return Math.Sqrt(dx * dx + dy * dy + dz * dz);
    }


    // Convenience: get heliocentric inertial state of a body
    public void GetBodyState(byte bodyId,
        out double rx, out double ry, out double rz,
        out double vx, out double vy, out double vz)
    {
        GetBodyPos(bodyId, out rx, out ry, out rz);
        GetBodyVel(bodyId, out vx, out vy, out vz);
    }

    // Convert craft heliocentric -> primary-relative
    public void ToPrimaryRelative(byte primaryId, CraftStateModel craft,
        out double rrx, out double rry, out double rrz,
        out double rvx, out double rvy, out double rvz)
    {
        rrx = rry = rrz = 0.0;
        rvx = rvy = rvz = 0.0;
        if (craft == null) return;

        double px, py, pz, pvx, pvy, pvz;
        GetBodyState(primaryId, out px, out py, out pz, out pvx, out pvy, out pvz);

        rrx = craft.rx - px;
        rry = craft.ry - py;
        rrz = craft.rz - pz;

        rvx = craft.vx - pvx;
        rvy = craft.vy - pvy;
        rvz = craft.vz - pvz;
    }

    // Convert primary-relative -> craft heliocentric (compose)
    public void FromPrimaryRelative(byte primaryId,
        double rrx, double rry, double rrz,
        double rvx, double rvy, double rvz,
        CraftStateModel craft)
    {
        if (craft == null) return;

        double px, py, pz, pvx, pvy, pvz;
        GetBodyState(primaryId, out px, out py, out pz, out pvx, out pvy, out pvz);

        craft.rx = px + rrx;
        craft.ry = py + rry;
        craft.rz = pz + rrz;

        craft.vx = pvx + rvx;
        craft.vy = pvy + rvy;
        craft.vz = pvz + rvz;
    }


}
