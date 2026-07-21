using UnityEngine;
using UnityEngine.Networking;
using System.Collections;
using System.IO;
using System;
using UnityEngine.XR;
using TMPro;

public class MicRecorder : MonoBehaviour
{
    [Header("Server Configuration")]
    public string serverIP = "192.168.1.10";
    public int serverPort = 3000;
    public string environment = "classroom";

    [Header("Recording Settings")]
    public int recordingLength = 10;
    public int sampleRate = 16000;

    [Header("UI References")]
    public TextMeshProUGUI statusText;
    public TextMeshProUGUI transcriptionText;
    public TextMeshProUGUI responseText;

    [Header("AI Character")]
    public AICharacterController aiCharacter;

    private AudioClip recordedClip;
    private string microphoneDevice;
    private bool isRecording = false;
    private bool wasButtonPressed = false;

    private InputDevice rightController;

    void Start()
    {
        // Request microphone permission
        if (!Application.HasUserAuthorization(UserAuthorization.Microphone))
        {
            Application.RequestUserAuthorization(UserAuthorization.Microphone);
        }

        // Get microphone
        if (Microphone.devices.Length > 0)
        {
            microphoneDevice = Microphone.devices[0];
            UpdateStatus("Ready! Press RIGHT TRIGGER to record");
            Debug.Log("Microphone found: " + microphoneDevice);
        }
        else
        {
            UpdateStatus("ERROR: No microphone!");
            Debug.LogError("No microphone found!");
        }

        // Get right controller
        var rightHandDevices = new System.Collections.Generic.List<InputDevice>();
        InputDevices.GetDevicesAtXRNode(XRNode.RightHand, rightHandDevices);
        if (rightHandDevices.Count > 0)
        {
            rightController = rightHandDevices[0];
            Debug.Log("Right controller found!");
        }
    }

    void Update()
    {
        // Try to get controller if not found yet
        if (!rightController.isValid)
        {
            var devices = new System.Collections.Generic.List<InputDevice>();
            InputDevices.GetDevicesAtXRNode(XRNode.RightHand, devices);
            if (devices.Count > 0)
                rightController = devices[0];
        }

        // Check trigger button
        bool triggerPressed = false;
        rightController.TryGetFeatureValue(
            CommonUsages.triggerButton, out triggerPressed);

        // Start recording on press
        if (triggerPressed && !wasButtonPressed && !isRecording)
        {
            Debug.Log("TRIGGER PRESSED - Starting recording!");
            StartRecording();
        }

        // Stop recording on release
        if (!triggerPressed && wasButtonPressed && isRecording)
        {
            Debug.Log("TRIGGER RELEASED - Stopping recording!");
            StopRecording();
        }

        wasButtonPressed = triggerPressed;
    }

    public void StartRecording()
    {
        Debug.Log("START RECORDING CALLED");
        if (isRecording) return;
        if (string.IsNullOrEmpty(microphoneDevice))
        {
            UpdateStatus("ERROR: No microphone!");
            return;
        }

        UpdateStatus("Recording... Release trigger to stop");
        if (transcriptionText != null) transcriptionText.text = "";
        if (responseText != null) responseText.text = "";
        if (aiCharacter != null) aiCharacter.SetListening();

        recordedClip = Microphone.Start(
            microphoneDevice, false, recordingLength, sampleRate);
        isRecording = true;
        Debug.Log("Recording started!");
    }

    public void StopRecording()
    {
        Debug.Log("STOP RECORDING CALLED");
        if (!isRecording) return;

        Microphone.End(microphoneDevice);
        isRecording = false;
        UpdateStatus("Processing... Please wait");
        if (aiCharacter != null) aiCharacter.SetThinking();
        StartCoroutine(SendAudioToServer());
    }

    IEnumerator SendAudioToServer()
    {
        byte[] wavData = WavUtility.FromAudioClip(recordedClip);
        string url = $"http://{serverIP.Trim()}:{serverPort}/upload";
        Debug.Log("URL IS: " + url);

        WWWForm form = new WWWForm();
        form.AddBinaryData("audio", wavData, "recording.wav", "audio/wav");
        form.AddField("env", environment);

        UpdateStatus("Sending to AI...");

        using (UnityWebRequest www = UnityWebRequest.Post(url, form))
        {
            yield return www.SendWebRequest();

            if (www.result == UnityWebRequest.Result.Success)
            {
                Debug.Log("Server response: " + www.downloadHandler.text);
                try
                {
                    ServerResponse response = JsonUtility.FromJson
                        <ServerResponse>(www.downloadHandler.text);
                    if (transcriptionText != null)
                        transcriptionText.text = "You: " + response.transcription;
                    if (responseText != null)
                        responseText.text = "AI: " + response.text;

                    // Award XP: bonus for correct quiz answers, otherwise participation
                    if (GameStats.Instance != null)
                    {
                        if (response.quizResult == "correct")
                        {
                            GameStats.Instance.AddCorrectAnswer(); // +20 XP bonus
                            UpdateStatus("✅ Correct answer! +20 XP");
                        }
                        else
                        {
                            GameStats.Instance.AddParticipation(); // +5 XP
                        }
                    }
                    UpdateStatus("Done! Press trigger to record again");
                    if (aiCharacter != null) aiCharacter.Speak(response.text);
                    StartCoroutine(PlayAudioResponse(response.audio_base64));
                }
                catch (Exception e)
                {
                    Debug.LogError("Parse error: " + e.Message);
                    UpdateStatus("ERROR: Bad server response");
                    if (aiCharacter != null) aiCharacter.ShowError("Hmm, I received a garbled reply. Try again!");
                }
            }
            else
            {
                Debug.LogError("Server error: " + www.error);
                UpdateStatus("ERROR: Cannot reach server - Check IP!");
                if (aiCharacter != null) aiCharacter.ShowError("I can't reach my brain (the PC server).\nCheck Wi-Fi and the server IP.");
            }
        }
    }

    IEnumerator PlayAudioResponse(string base64Audio)
    {
        if (string.IsNullOrEmpty(base64Audio)) yield break;

        byte[] audioBytes = Convert.FromBase64String(base64Audio);
        string tempPath = Path.Combine(
            Application.persistentDataPath, "ai_response.wav");
        File.WriteAllBytes(tempPath, audioBytes);

        using (UnityWebRequest www =
            UnityWebRequestMultimedia.GetAudioClip(
                "file://" + tempPath, AudioType.WAV))
        {
            yield return www.SendWebRequest();
            if (www.result == UnityWebRequest.Result.Success)
            {
                AudioClip clip = DownloadHandlerAudioClip.GetContent(www);
                AudioSource audioSource = GetComponent<AudioSource>();
                if (audioSource == null)
                    audioSource = gameObject.AddComponent<AudioSource>();
                audioSource.clip = clip;
                audioSource.Play();
                UpdateStatus("AI Speaking...");
                yield return new WaitForSeconds(clip.length);
                UpdateStatus("Ready! Press RIGHT TRIGGER to record");
            }
        }
    }

    void UpdateStatus(string message)
    {
        if (statusText != null)
            statusText.text = message;
        Debug.Log("Status: " + message);
    }

    [System.Serializable]
    public class ServerResponse
    {
        public string text;
        public string audio_base64;
        public string transcription;
        public string quizResult;
    }
}