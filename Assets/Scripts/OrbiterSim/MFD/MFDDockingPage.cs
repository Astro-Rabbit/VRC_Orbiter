using UdonSharp;
using System;
using UnityEngine;
using VRC.SDKBase;
using VRC.Udon;

public class MFDDockingPage : MFDPage
{
    [Header("References")]
    public GuidanceNavContactsState contacts;

    [Header("Display Data")]
    public bool portSelected = false;
    public double range;
    public double closure;
    public double offsetX;
    public double offsetY;
    public float angleX;
    public float angleY;
    public float roll;

    void Update()
    {
        if (contacts.fullStationIndex0 < 0 || !contacts.dockValid0) {
            portSelected = false;
            return;
        }
        portSelected = true;

        range = contacts.range_m[contacts.fullStationIndex0];
        Vector3 relVel = new Vector3((float)contacts.dvx_E0, (float)contacts.dvy_E0, (float)contacts.dvz_E0);
        closure = Vector3.Dot(relVel, contacts.qTargetPortInB0 * Vector3.forward);

        double errX = contacts.dockErr_px_B0;
        double errY = contacts.dockErr_py_B0;
        double errZ = contacts.dockErr_pz_B0;
        double errMag = Math.Sqrt(errX*errX + errY*errY);
        double logErrMag = Math.Log10(errMag);
        if (logErrMag < 1) {
            logErrMag = errMag / 10;
        }
        offsetX = 0.25 * errX * logErrMag / errMag;
        offsetY = 0.25 * errY * logErrMag / errMag;

        Quaternion rotErr = contacts.qDockErr0 * Quaternion.AngleAxis(180, Vector3.up);
        Vector3 towardsPort = rotErr * Vector3.forward;
        Vector3 angDir = Vector3.ProjectOnPlane(towardsPort, Vector3.forward).normalized;
        Quaternion pointToRot = Quaternion.FromToRotation(Vector3.forward, towardsPort);
        float angMag = Quaternion.Angle(Quaternion.identity, pointToRot);
        angleX = angDir.x * angMag / 20;
        angleY = angDir.y * angMag / 20;

        Quaternion rollRot = Quaternion.Inverse(pointToRot) * rotErr;
        float rollAngle;
        Vector3 rollAxis;
        rollRot.ToAngleAxis(out rollAngle, out rollAxis);
        roll = (float)(Math.PI / 180) * (rollAxis.z < 0 ? 360 - rollAngle : rollAngle);
    }

    public override void OnButton(MFD display, ButtonSide side, int num)
    {
        if (side == ButtonSide.Bottom && num == 2) {
            display.SetPage((byte)MFDPageID.Menu);
        }
    }

    public override void DrawDisplay(MFD display)
    {
        if (!portSelected) {
            display.ClearGraphics();
            display.ClearText();

            string msg = "NO TARGET SELECTED";
            display.DrawText(msg, 10, 24 - msg.Length/2, Color.green);

            display.DrawText("MENU", MFD.TEXT_ROWS - 1, MFD.TEXT_COLUMNS / 2 - 2, Color.white);
            return;
        }

        float size = 0.75f;
        float iconSize = 0.25f;
        float arrowSize = 0.05f;
        Vector2 center = Vector2.zero;

        display.ClearGraphics();

        // Draw target lines
        Color targetColor = Color.white * 0.2f;
        display.DrawLine(center - new Vector2(size, 0), center + new Vector2(size, 0), targetColor);
        display.DrawLine(center - new Vector2(0, size), center + new Vector2(0, size), targetColor);
        display.DrawConic(center, size * 0.25f, 0f, 0f, targetColor);
        display.DrawConic(center, size * 0.5f, 0f, 0f, targetColor);
        display.DrawConic(center, size * 0.75f, 0f, 0f, targetColor);
        display.DrawConic(center, size, 0f, 0f, targetColor);

        // Draw target position cross
        Vector2 offsetPos = size * new Vector2((float)offsetX, (float)offsetY);
        display.DrawLine(center + offsetPos - new Vector2(iconSize, 0), center + offsetPos + new Vector2(iconSize, 0), Color.green);
        display.DrawLine(center + offsetPos - new Vector2(0, iconSize), center + offsetPos + new Vector2(0, iconSize), Color.green);

        // Draw port alignment X
        Vector2 angPos = size * new Vector2(angleX, angleY);
        display.DrawLine(center + angPos - new Vector2(iconSize, iconSize), center + angPos + new Vector2(iconSize, iconSize), Color.white);
        display.DrawLine(center + angPos - new Vector2(iconSize, -iconSize), center + angPos + new Vector2(iconSize, -iconSize), Color.white);

        // Draw roll arrow
        float s = (float)Math.Sin(roll);
        float c = (float)Math.Cos(roll);

        Vector2 arrowPoint = center + rotate(new Vector2(0, size), s, c);
        Vector2 arrowCorner1 = center + rotate(new Vector2(-0.5f * arrowSize, size - arrowSize), s, c);
        Vector2 arrowCorner2 = center + rotate(new Vector2(0.5f * arrowSize, size - arrowSize), s, c);

        Color arrowColor = Color.white;
        display.DrawLine(arrowCorner1, arrowCorner2, arrowColor);
        display.DrawLine(arrowPoint, arrowCorner1, arrowColor);
        display.DrawLine(arrowPoint, arrowCorner2, arrowColor);

        display.ClearText();
        display.DrawText(MFD.FormatNumber("RNG", range), 2, 2, Color.green);
        display.DrawText(MFD.FormatNumber("CLS", closure), 2, 36, Color.green);
        display.DrawText("MENU", MFD.TEXT_ROWS - 1, MFD.TEXT_COLUMNS / 2 - 2, Color.white);
    }

    private static Vector2 rotate(Vector2 v, float s, float c)
    {
        return new Vector2(c*v.x - s*v.y, c*v.y + s*v.x);
    }
}