using UnityEngine;
using System.Collections;
using Unity.XR.CoreUtils;

// On Quest, the user's facing direction at app start comes from their
// Guardian recenter — NOT from the scene. This script rotates the world
// content (environments + AI character) around the rig so the room's
// "front" is always where the user is actually looking at startup.
public class WorldAligner : MonoBehaviour
{
    public XROrigin xrOrigin;
    public Transform[] contentRoots;     // ClassroomEnv, GymEnv, OfficeEnv, AICharacter
    public HUDFollow hud;

    IEnumerator Start()
    {
        if (xrOrigin != null)
            xrOrigin.RequestedTrackingOriginMode = XROrigin.TrackingOriginMode.Floor;

        // wait for head tracking to deliver a real pose
        Transform cam = xrOrigin != null && xrOrigin.Camera != null
            ? xrOrigin.Camera.transform : (Camera.main != null ? Camera.main.transform : null);
        float waited = 0f;
        while (cam != null && cam.localPosition.sqrMagnitude < 0.0001f && waited < 3f)
        {
            waited += Time.deltaTime;
            yield return null;
        }
        yield return null; // one extra frame for a settled pose

        Align();
    }

    public void Align()
    {
        Transform cam = xrOrigin != null && xrOrigin.Camera != null
            ? xrOrigin.Camera.transform : (Camera.main != null ? Camera.main.transform : null);
        if (cam == null || xrOrigin == null) return;

        Vector3 pivot = xrOrigin.transform.position;

        // yaw the user is actually facing vs the yaw the content was authored for (+Z)
        Vector3 f = cam.forward; f.y = 0f;
        if (f.sqrMagnitude < 0.001f) return;
        float userYaw = Quaternion.LookRotation(f).eulerAngles.y;

        foreach (var root in contentRoots)
        {
            if (root == null) continue;
            float authoredYaw = root.eulerAngles.y;
            root.RotateAround(pivot, Vector3.up, userYaw - authoredYaw);
        }

        if (hud != null) hud.SnapNow();
        Debug.Log("WorldAligner: content aligned to user yaw " + userYaw.ToString("F0"));
    }
}
