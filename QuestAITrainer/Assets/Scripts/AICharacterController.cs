using UnityEngine;
using System.Collections;
using TMPro;

// Drives the AI tutor character: procedural animation states +
// the speech bubble above its head. States are set by MicRecorder
// as the voice pipeline progresses.
public class AICharacterController : MonoBehaviour
{
    public enum AIState { Idle, Listening, Thinking, Speaking }

    [Header("Body Parts (assigned by scene setup)")]
    public Transform head;
    public Transform leftEye;
    public Transform rightEye;
    public Transform mouth;
    public Transform antennaTip;
    public Transform leftArm;
    public Transform rightArm;
    public Renderer[] accentRenderers;   // parts tinted with the persona color

    [Header("Speech Bubble")]
    public CanvasGroup bubbleGroup;
    public TextMeshProUGUI bubbleText;
    public TextMeshProUGUI nameText;

    [Header("Voice")]
    public AudioSource voiceSource;      // MicRecorder's AudioSource (AI voice playback)

    [Header("Tuning")]
    public float typewriterCharsPerSecond = 45f;
    public float idleBubbleDelay = 5f;

    public AIState State { get; private set; } = AIState.Idle;

    private Transform cam;
    private float stateTime;
    private float blinkTimer;
    private Vector3 headBasePos;
    private Vector3 mouthBaseScale;
    private Vector3 eyeBaseScale;
    private Coroutine typing;
    private Coroutine bubbleFade;
    private string idleHint = "Hold the trigger and talk to me!";

    void Start()
    {
        if (Camera.main != null) cam = Camera.main.transform;
        if (head != null) headBasePos = head.localPosition;
        if (mouth != null) mouthBaseScale = mouth.localScale;
        if (leftEye != null) eyeBaseScale = leftEye.localScale;
        blinkTimer = Random.Range(2f, 4f);
        SetIdle();
    }

    void Update()
    {
        stateTime += Time.deltaTime;

        FaceUser();
        AnimateBlink();

        switch (State)
        {
            case AIState.Idle:
                Bob(0.02f, 1.2f);
                SwingArms(4f, 0.6f);
                break;

            case AIState.Listening:
                Bob(0.012f, 2.2f);
                PulseAntenna(6f, 0.35f);
                SetEyeScale(1.25f);
                break;

            case AIState.Thinking:
                Bob(0.008f, 1f);
                TiltHead(8f, 0.9f);
                PulseAntenna(2.5f, 0.5f);
                break;

            case AIState.Speaking:
                Bob(0.015f, 1.6f);
                AnimateMouth();
                NodHead(3f, 2.4f);
                SwingArms(9f, 1.6f);
                // Return to idle automatically once the voice clip finishes
                if (voiceSource != null && !voiceSource.isPlaying && stateTime > 0.8f)
                    SetIdle();
                break;
        }
    }

    // ---------- public API (called by MicRecorder / EnvironmentManager) ----------

    public void SetIdle()
    {
        EnterState(AIState.Idle);
        ShowBubble(idleHint, instant: true, autoFade: true);
    }

    public void SetListening()
    {
        EnterState(AIState.Listening);
        ShowBubble("🎤 Listening...", instant: true, autoFade: false);
    }

    public void SetThinking()
    {
        EnterState(AIState.Thinking);
        ShowBubble("💭 Thinking...", instant: true, autoFade: false);
    }

    public void Speak(string text)
    {
        EnterState(AIState.Speaking);
        ShowBubble(text, instant: false, autoFade: true);
    }

    public void ShowError(string message)
    {
        EnterState(AIState.Idle);
        ShowBubble(message, instant: true, autoFade: true);
    }

    // Persona switching (called by EnvironmentManager)
    public void SetPersona(string personaName, Color accent, string greeting)
    {
        if (nameText != null)
        {
            nameText.text = personaName;
            nameText.color = Color.Lerp(accent, Color.white, 0.45f);
        }
        foreach (var r in accentRenderers)
            if (r != null) r.material.color = accent;
        idleHint = greeting;
        SetIdle();
    }

    public void MoveToAnchor(Transform anchor)
    {
        if (anchor == null) return;
        transform.position = anchor.position;
        transform.rotation = anchor.rotation;
    }

    // ---------- internals ----------

    void EnterState(AIState s)
    {
        State = s;
        stateTime = 0f;
        if (head != null) head.localRotation = Quaternion.identity;
        if (mouth != null) mouth.localScale = mouthBaseScale;
        SetEyeScale(1f);
    }

    void ShowBubble(string text, bool instant, bool autoFade)
    {
        if (bubbleGroup == null || bubbleText == null) return;

        if (typing != null) StopCoroutine(typing);
        if (bubbleFade != null) StopCoroutine(bubbleFade);
        bubbleGroup.alpha = 1f;

        if (instant)
        {
            bubbleText.text = text;
            if (autoFade) bubbleFade = StartCoroutine(FadeBubbleAfter(idleBubbleDelay));
        }
        else
        {
            typing = StartCoroutine(TypeText(text, autoFade));
        }
    }

    IEnumerator TypeText(string text, bool autoFade)
    {
        bubbleText.text = "";
        float perChar = 1f / Mathf.Max(1f, typewriterCharsPerSecond);
        for (int i = 0; i < text.Length; i++)
        {
            bubbleText.text = text.Substring(0, i + 1);
            yield return new WaitForSeconds(perChar);
        }
        if (autoFade) bubbleFade = StartCoroutine(FadeBubbleAfter(idleBubbleDelay + 3f));
    }

    IEnumerator FadeBubbleAfter(float delay)
    {
        yield return new WaitForSeconds(delay);
        float t = 0f;
        while (t < 1f)
        {
            t += Time.deltaTime * 2f;
            bubbleGroup.alpha = Mathf.Lerp(1f, 0.35f, t);
            yield return null;
        }
    }

    void FaceUser()
    {
        if (cam == null) { if (Camera.main != null) cam = Camera.main.transform; return; }
        Vector3 to = cam.position - transform.position;
        to.y = 0f;
        if (to.sqrMagnitude < 0.01f) return;
        Quaternion target = Quaternion.LookRotation(to);
        transform.rotation = Quaternion.Slerp(transform.rotation, target, Time.deltaTime * 3f);
    }

    void Bob(float amplitude, float speed)
    {
        if (head == null) return;
        head.localPosition = headBasePos + Vector3.up * Mathf.Sin(Time.time * speed) * amplitude;
    }

    void TiltHead(float degrees, float speed)
    {
        if (head == null) return;
        head.localRotation = Quaternion.Euler(0, 0, Mathf.Sin(Time.time * speed) * degrees);
    }

    void NodHead(float degrees, float speed)
    {
        if (head == null) return;
        head.localRotation = Quaternion.Euler(Mathf.Sin(Time.time * speed) * degrees, 0, 0);
    }

    void AnimateMouth()
    {
        if (mouth == null) return;
        float open = Mathf.Abs(Mathf.Sin(Time.time * 11f)) * 0.8f + 0.4f;
        mouth.localScale = new Vector3(mouthBaseScale.x, mouthBaseScale.y * (0.5f + open), mouthBaseScale.z);
    }

    void PulseAntenna(float speed, float amount)
    {
        if (antennaTip == null) return;
        float s = 1f + Mathf.Sin(Time.time * speed) * amount * 0.5f;
        antennaTip.localScale = Vector3.one * 0.08f * s;
    }

    void SwingArms(float degrees, float speed)
    {
        float a = Mathf.Sin(Time.time * speed) * degrees;
        if (leftArm != null) leftArm.localRotation = Quaternion.Euler(0, 0, 12f + a);
        if (rightArm != null) rightArm.localRotation = Quaternion.Euler(0, 0, -12f - a);
    }

    void SetEyeScale(float s)
    {
        if (leftEye != null) leftEye.localScale = eyeBaseScale * s;
        if (rightEye != null) rightEye.localScale = eyeBaseScale * s;
    }

    void AnimateBlink()
    {
        blinkTimer -= Time.deltaTime;
        if (blinkTimer <= 0f)
        {
            StartCoroutine(Blink());
            blinkTimer = Random.Range(2.5f, 5f);
        }
    }

    IEnumerator Blink()
    {
        if (leftEye == null || rightEye == null) yield break;
        Vector3 closed = new Vector3(eyeBaseScale.x, eyeBaseScale.y * 0.1f, eyeBaseScale.z);
        leftEye.localScale = closed; rightEye.localScale = closed;
        yield return new WaitForSeconds(0.1f);
        if (State != AIState.Listening)
        {
            leftEye.localScale = eyeBaseScale; rightEye.localScale = eyeBaseScale;
        }
    }
}
