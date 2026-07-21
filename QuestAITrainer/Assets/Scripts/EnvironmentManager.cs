using UnityEngine;
using TMPro;

public class EnvironmentManager : MonoBehaviour
{
    public static EnvironmentManager Instance;

    [Header("References")]
    public MicRecorder micRecorder;
    public TextMeshProUGUI environmentLabel;

    [Header("Environment Backgrounds")]
    public GameObject classroomEnvironment;
    public GameObject gymEnvironment;
    public GameObject officeEnvironment;

    [Header("AI Character")]
    public AICharacterController character;
    public Transform[] characterAnchors;   // per-environment standing spot (classroom, gym, office)

    private string[] environments = { "classroom", "gym", "office" };
    private string[] displayNames = { "Classroom - Teacher AI", "Gym - Fitness Coach", "Office - HR Interviewer" };
    private string[] personaNames = { "Professor Claude", "Coach Max", "Ms. Taylor" };
    private string[] personaGreetings = {
        "Ask me anything about software engineering —\nor say \"Start quiz\" to test yourself for XP!",
        "Ask me for a workout! Try:\n\"What should I do for back today?\"",
        "Welcome! Say hello when you're ready\nto begin your HR interview practice."
    };
    private Color[] personaColors = {
        new Color(0.25f, 0.55f, 1f),    // classroom blue
        new Color(1f, 0.30f, 0.25f),    // gym red
        new Color(0.55f, 0.55f, 0.62f)  // office gray
    };
    private int currentIndex = 0;

    void Awake()
    {
        if (Instance == null) Instance = this;
    }

    void Start()
    {
        SetEnvironment(0);
    }

    // Call this from a button to cycle environments
    public void NextEnvironment()
    {
        currentIndex = (currentIndex + 1) % environments.Length;
        SetEnvironment(currentIndex);
    }

    public void SetEnvironment(int index)
    {
        currentIndex = index;

        // Update the environment sent to AI backend
        if (micRecorder != null)
            micRecorder.environment = environments[index];

        // Update label
        if (environmentLabel != null)
            environmentLabel.text = displayNames[index];

        // Show/hide 3D backgrounds
        if (classroomEnvironment != null) classroomEnvironment.SetActive(index == 0);
        if (gymEnvironment != null) gymEnvironment.SetActive(index == 1);
        if (officeEnvironment != null) officeEnvironment.SetActive(index == 2);

        // Move + re-skin the AI character for this persona
        if (character != null)
        {
            if (characterAnchors != null && index < characterAnchors.Length)
                character.MoveToAnchor(characterAnchors[index]);
            character.SetPersona(personaNames[index], personaColors[index], personaGreetings[index]);
        }

        Debug.Log("Switched to: " + environments[index]);
    }
}
