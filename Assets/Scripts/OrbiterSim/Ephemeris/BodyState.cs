using System;

[Serializable]
public struct BodyState
{
    public Double3 r; // meters, ECI (Unity axes)
    public Double3 v; // m/s,  ECI
}
