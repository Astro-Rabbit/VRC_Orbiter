using UdonSharp;
using System;
using UnityEngine;
using VRC.SDKBase;
using VRC.Udon;

public class MFDDockingPage : MFDPage
{
    [Header("References")]
    public GuidanceNavContactsState contacts;

    [Header("Scale Options (meters full scale)")]
    public float[] lateralScaleOptionsM = new float[] { 0.1f, 0.3f, 1.0f, 3.0f, 10.0f };
    public int lateralScaleIndex = 2;

    [Header("Display Data")]
    public bool hasTarget = false;
    public bool portSelected = false;
    public double range;
    public double closure;
    public double speed;
    public double offsetX;
    public double offsetY;
    public float angleX;
    public float angleY;
    public float roll;

    private float currentLateralScaleM = 1.0f;

    void Update()
    {
        hasTarget = contacts.fullStationIndex0 >= 0;
        portSelected = contacts.dockValid0;

        if (!hasTarget) {
            return;
        }

        Vector3 relVel = new Vector3(
            (float)contacts.dvx_E0,
            (float)contacts.dvy_E0,
            (float)contacts.dvz_E0
        );
        speed = Math.Sqrt(relVel.x*relVel.x + relVel.y*relVel.y + relVel.z*relVel.z);

        if (!portSelected) {
            range = contacts.range_m[contacts.fullStationIndex0];
            return;
        }

        //UpdateScaleSelection();

        double errX = contacts.dockErr_px_B0;
        double errY = contacts.dockErr_py_B0;
        double errZ = contacts.dockErr_pz_B0;

        // Port-to-port range, not craft-center to station-root range.
        range = Math.Sqrt(errX * errX + errY * errY + errZ * errZ);
        closure = Vector3.Dot(relVel, contacts.qTargetPortInB0 * Vector3.forward);

        double errMag = Math.Sqrt(errX*errX + errY*errY);
        double visualMag = Math.Log10(errMag) + 2;
        if (errMag < 0.1) {
            visualMag = errMag * 10; // Go linear for the inner ring
        }
        offsetX = 0.25 * errX / errMag * visualMag;
        offsetY = 0.25 * errY / errMag * visualMag;

        /*
        // Lateral docking error shown in target-port frame projected on the page.
        if (currentLateralScaleM > 1e-6f) {
            offsetX = errX / currentLateralScaleM;
            offsetY = errY / currentLateralScaleM;
        } else {
            offsetX = 0.0;
            offsetY = 0.0;
        }
        */

        // Clamp visual offset so the cue stays on-screen.
        double offsetMag = Math.Sqrt(offsetX*offsetX + offsetY*offsetY);
        if (offsetMag > 1.0) {
            offsetX /= offsetMag;
            offsetY /= offsetMag;
        }

        Quaternion rotErr = contacts.qDockErr0 * Quaternion.AngleAxis(180f, Vector3.up);
        Vector3 towardsPort = rotErr * Vector3.forward;
        Vector3 angPlanar = Vector3.ProjectOnPlane(towardsPort, Vector3.forward);

        if (angPlanar.sqrMagnitude > 1e-8f) {
            Vector3 angDir = angPlanar.normalized;
            Quaternion pointToRot = Quaternion.FromToRotation(Vector3.forward, towardsPort);
            float angMag = Quaternion.Angle(Quaternion.identity, pointToRot);

            angleX = angDir.x * angMag / 20f;
            angleY = angDir.y * angMag / 20f;

            Quaternion rollRot = Quaternion.Inverse(pointToRot) * rotErr;
            float rollAngle;
            Vector3 rollAxis;
            rollRot.ToAngleAxis(out rollAngle, out rollAxis);
            roll = (float)(Math.PI / 180.0) * (rollAxis.z < 0f ? 360f - rollAngle : rollAngle);
        } else {
            angleX = 0f;
            angleY = 0f;

            float rollAngle;
            Vector3 rollAxis;
            rotErr.ToAngleAxis(out rollAngle, out rollAxis);
            roll = (float)(Math.PI / 180.0) * (rollAxis.z < 0f ? 360f - rollAngle : rollAngle);
        }
    }

    private void UpdateScaleSelection()
    {
        if (lateralScaleOptionsM == null || lateralScaleOptionsM.Length == 0) {
            currentLateralScaleM = 1.0f;
            lateralScaleIndex = 0;
            return;
        }

        if (lateralScaleIndex < 0) lateralScaleIndex = 0;
        if (lateralScaleIndex >= lateralScaleOptionsM.Length) lateralScaleIndex = lateralScaleOptionsM.Length - 1;

        currentLateralScaleM = lateralScaleOptionsM[lateralScaleIndex];
        if (currentLateralScaleM <= 0f) currentLateralScaleM = 1.0f;
    }

    public override void OnButton(MFD display, ButtonSide side, int num)
    {
        if (side == ButtonSide.Bottom && num == 2) {
            display.SetPage((byte)MFDPageID.Menu);
            return;
        }

        /*
        if (side == ButtonSide.Left) {
            // L4 = scale down
            if (num == 3 && lateralScaleIndex > 0) {
                lateralScaleIndex--;
                return;
            }

            // L5 = scale up
            if (num == 4 && lateralScaleIndex < lateralScaleOptionsM.Length - 1) {
                lateralScaleIndex++;
                return;
            }
        }
        */
    }

    public override void DrawDisplay(MFD display)
    {
        if (!hasTarget) {
            display.ClearGraphics();
            display.ClearText();

            string msg = "NO TARGET SELECTED";
            display.DrawText(msg, 10, 24 - msg.Length / 2, Color.green);

            display.DrawText("MENU", MFD.TEXT_ROWS - 1, MFD.TEXT_COLUMNS / 2 - 2, Color.white);
            return;
        }

        float size = 0.75f;
        float iconSize = 0.25f;
        float arrowSize = 0.05f;
        Vector2 center = Vector2.zero;

        display.ClearGraphics();

        Color targetColor = Color.white * 0.2f;
        display.DrawLine(center - new Vector2(size, 0), center + new Vector2(size, 0), targetColor);
        display.DrawLine(center - new Vector2(0, size), center + new Vector2(0, size), targetColor);
        display.DrawConic(center, size * 0.25f, 0f, 0f, targetColor);
        display.DrawConic(center, size * 0.5f, 0f, 0f, targetColor);
        display.DrawConic(center, size * 0.75f, 0f, 0f, targetColor);
        display.DrawConic(center, size, 0f, 0f, targetColor);

        if (portSelected) {
            Vector2 offsetPos = size * new Vector2((float)offsetX, -(float)offsetY);
            display.DrawLine(center + offsetPos - new Vector2(iconSize, 0), center + offsetPos + new Vector2(iconSize, 0), Color.green);
            display.DrawLine(center + offsetPos - new Vector2(0, iconSize), center + offsetPos + new Vector2(0, iconSize), Color.green);

            Vector2 angPos = size * new Vector2(angleX, angleY);
            display.DrawLine(center + angPos - new Vector2(iconSize, iconSize), center + angPos + new Vector2(iconSize, iconSize), Color.white);
            display.DrawLine(center + angPos - new Vector2(iconSize, -iconSize), center + angPos + new Vector2(iconSize, -iconSize), Color.white);

            float s = (float)Math.Sin(roll);
            float c = (float)Math.Cos(roll);

            Vector2 arrowPoint = center + Rotate(new Vector2(0, size), s, c);
            Vector2 arrowCorner1 = center + Rotate(new Vector2(-0.5f * arrowSize, size - arrowSize), s, c);
            Vector2 arrowCorner2 = center + Rotate(new Vector2(0.5f * arrowSize, size - arrowSize), s, c);

            Color arrowColor = Color.white;
            display.DrawLine(arrowCorner1, arrowCorner2, arrowColor);
            display.DrawLine(arrowPoint, arrowCorner1, arrowColor);
            display.DrawLine(arrowPoint, arrowCorner2, arrowColor);
        }

        display.ClearText();
        display.DrawText(MFD.FormatNumber("RNG", range), 2, 2, Color.green);
        if (portSelected) {
            display.DrawText(MFD.FormatNumber("CLS", closure), 2, 20, Color.green);
        }
        display.DrawText(MFD.FormatNumber("SPD", speed), 2, 36, Color.green);
        //display.DrawText(MFD.FormatNumber("SCL", currentLateralScaleM), 2, 36, Color.green);

        display.DrawText("MENU", MFD.TEXT_ROWS - 1, MFD.TEXT_COLUMNS / 2 - 2, Color.white);
        //display.DrawText("SCL-", 17, 2, Color.white);
        //display.DrawText("SCL+", 20, 2, Color.white);
    }

    private static Vector2 Rotate(Vector2 v, float s, float c)
    {
        return new Vector2(c * v.x - s * v.y, c * v.y + s * v.x);
    }
}