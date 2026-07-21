using UnityEngine;
using System.Collections;
using TMPro;

// Spawns floating "+XP" popups and the LEVEL UP banner in world space,
// anchored near the HUD so they are always in view.
public class XPPopupManager : MonoBehaviour
{
    public static XPPopupManager Instance;

    [Header("Anchors")]
    public Transform hudAnchor;          // the HUD canvas transform

    [Header("Colors")]
    public Color xpColor = new Color(0.35f, 1f, 0.45f);
    public Color bonusColor = new Color(1f, 0.85f, 0.25f);
    public Color levelUpColor = new Color(1f, 0.6f, 0.1f);

    void Awake()
    {
        if (Instance == null) Instance = this;
    }

    public void ShowXP(int amount, bool bonus)
    {
        Spawn("+" + amount + " XP", bonus ? bonusColor : xpColor, 0.55f, 1.4f);
    }

    public void ShowLevelUp(int newLevel)
    {
        Spawn("LEVEL " + newLevel + "!", levelUpColor, 1.4f, 2.6f);
    }

    void Spawn(string text, Color color, float size, float lifetime)
    {
        if (hudAnchor == null) return;

        var go = new GameObject("XPPopup");
        go.transform.position = hudAnchor.position
            + hudAnchor.up * 0.55f
            + hudAnchor.right * Random.Range(-0.08f, 0.08f);
        go.transform.rotation = hudAnchor.rotation;

        var tmp = go.AddComponent<TextMeshPro>();
        tmp.text = text;
        tmp.fontSize = size;
        tmp.color = color;
        tmp.alignment = TextAlignmentOptions.Center;
        tmp.fontStyle = FontStyles.Bold;
        tmp.outlineWidth = 0.15f;
        tmp.outlineColor = new Color32(0, 0, 0, 200);
        var rt = tmp.rectTransform;
        rt.sizeDelta = new Vector2(4f, 1f);

        StartCoroutine(Animate(go.transform, tmp, lifetime));
    }

    IEnumerator Animate(Transform t, TextMeshPro tmp, float lifetime)
    {
        float elapsed = 0f;
        Vector3 start = t.position;
        Vector3 baseScale = t.localScale;

        // punch-in
        t.localScale = baseScale * 0.2f;
        while (elapsed < lifetime)
        {
            elapsed += Time.deltaTime;
            float k = elapsed / lifetime;
            t.position = start + Vector3.up * (0.35f * k);
            float scaleK = Mathf.Clamp01(elapsed / 0.18f);
            t.localScale = baseScale * Mathf.Lerp(0.2f, 1f, EaseOutBack(scaleK));
            var c = tmp.color;
            c.a = 1f - Mathf.SmoothStep(0f, 1f, Mathf.Clamp01((k - 0.55f) / 0.45f));
            tmp.color = c;
            yield return null;
        }
        Destroy(t.gameObject);
    }

    static float EaseOutBack(float x)
    {
        const float c1 = 1.70158f, c3 = c1 + 1f;
        return 1f + c3 * Mathf.Pow(x - 1f, 3f) + c1 * Mathf.Pow(x - 1f, 2f);
    }
}
