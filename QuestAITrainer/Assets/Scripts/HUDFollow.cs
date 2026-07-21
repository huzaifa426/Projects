using UnityEngine;

// Keeps the HUD canvas floating in front of the user's face.
// Lazily follows the head so the UI is always findable in VR,
// regardless of where the user looks or walks.
public class HUDFollow : MonoBehaviour
{
    public float distance = 2.1f;
    public float heightOffset = -0.25f;   // slightly below eye line
    public float followSpeed = 4f;
    public float rotateSpeed = 5f;

    private Transform cam;

    void LateUpdate()
    {
        if (cam == null)
        {
            if (Camera.main != null) cam = Camera.main.transform;
            else return;
        }

        // target: in front of the head, yaw only (ignore pitch so UI stays level)
        Vector3 flatForward = cam.forward;
        flatForward.y = 0f;
        if (flatForward.sqrMagnitude < 0.001f) flatForward = cam.up; // looking straight down
        flatForward.Normalize();

        Vector3 targetPos = cam.position + flatForward * distance + Vector3.up * heightOffset;
        transform.position = Vector3.Lerp(transform.position, targetPos, Time.deltaTime * followSpeed);

        Quaternion targetRot = Quaternion.LookRotation(transform.position - cam.position);
        targetRot = Quaternion.Euler(0f, targetRot.eulerAngles.y, 0f);
        transform.rotation = Quaternion.Slerp(transform.rotation, targetRot, Time.deltaTime * rotateSpeed);
    }

    // Snap instantly (used at startup so the HUD doesn't drift in from far away)
    public void SnapNow()
    {
        cam = Camera.main != null ? Camera.main.transform : null;
        if (cam == null) return;
        Vector3 flatForward = cam.forward; flatForward.y = 0f; flatForward.Normalize();
        transform.position = cam.position + flatForward * distance + Vector3.up * heightOffset;
        transform.rotation = Quaternion.Euler(0f, Quaternion.LookRotation(flatForward).eulerAngles.y, 0f);
    }
}
