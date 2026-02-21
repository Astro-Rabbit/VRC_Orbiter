#ifndef Helpers_CGINC_INCLUDED
#define Helpers_CGINC_INCLUDED

#include "UnityCG.cginc"

// Shared small, fast hash to [0,1)
inline float Hash12(float2 p)
{
    float3 p3 = frac(float3(p.xyx) * 0.1031);
    p3 += dot(p3, p3.yzx + 33.33);
    return frac((p3.x + p3.y) * p3.z);
}

// Kasten & Young (1989)-style air mass fit
inline float AirMassKastenYoung(float mu) // mu = cos(zenith) = dot(dir, up)
{
    mu = saturate(mu);
    float z = degrees(acos(mu)); // zenith angle in degrees
    float denom = mu + 0.50572 * pow(max(96.07995 - z, 1e-3), -1.6364);
    return 1.0 / max(denom, 1e-3);
}

inline float3 TransmittanceRGB(float3 betaRGB, float airmass)
{
    return exp(-betaRGB * airmass);
}

inline float PhaseRayleigh(float mu)
{
    return (3.0 / (16.0 * UNITY_PI)) * (1.0 + mu * mu);
}

inline float PhaseHG(float mu, float g)
{
    float g2 = g * g;
    float denom = pow(max(1.0 + g2 - 2.0 * g * mu, 1e-4), 1.5);
    return (1.0 - g2) / (4.0 * UNITY_PI * denom);
}

#endif