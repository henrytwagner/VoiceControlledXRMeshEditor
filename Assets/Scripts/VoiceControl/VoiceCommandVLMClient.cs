using System;
using System.Collections;
using System.IO;
using System.Text;
using UnityEngine;
using UnityEngine.Networking;
using UnityEngine.XR;

/// <summary>
/// Bridges microphone/screenshot capture in Unity with the local VLM service (Python).
/// When the service responds with a command payload it forwards it to the VoiceCommandProcessor.
/// </summary>
[AddComponentMenu("Voice Control/Voice Command VLM Client")]
public class VoiceCommandVLMClient : MonoBehaviour
{
    [Header("Service")]
    [Tooltip("HTTP endpoint exposed by vlm.py")]
    public string serviceUrl = "http://localhost:5000/process";

    [Header("Voice Capture")]
    [Tooltip("Minimum RMS volume required to detect speech.")]
    [Range(0.0001f, 1f)]
    public float speechThreshold = 0.05f;

    [Tooltip("Amount of silence (seconds) before sending the captured clip.")]
    [Range(0.2f, 5f)]
    public float silenceTimeout = 1.2f;

    [Tooltip("Size of the RMS sampling window (number of audio samples).")]
    public int sampleWindow = 1024;

    [Tooltip("Total seconds stored in the looping microphone clip.")]
    public int microphoneLoopSeconds = 10;

    [Tooltip("Sample rate used for microphone capture.")]
    public int microphoneSampleRate = 16000;

    [Header("Audio Capture Tweaks")]
    [Tooltip("Amount of audio (in seconds) captured before speech is detected.")]
    [Range(0f, 1f)]
    public float preRollSeconds = 0.25f;

    [Header("Scene Capture")]
    [Tooltip("Camera used to capture the screenshot that accompanies the audio.")]
    public Camera captureCamera;
    public int captureWidth = 1024;
    public int captureHeight = 1024;

    [Header("Dependencies")]
    public VoiceCommandProcessor commandProcessor;

    [Header("Debug")]
    public bool logDebug = true;

    private string microphoneDevice;
    private AudioClip microphoneClip;
    private bool isSpeaking;
    private float silenceTimer;
    private int speechStartSample;
    private Coroutine pendingCaptureCoroutine;
    private ObjectSelector objectSelector;

    [Serializable]
    private class VLMServiceResponse
    {
        public bool success;
        public string error;
        public string raw;
        public string transcript;
        public MeshCommand command;
    }

    [Serializable]
    private class VLMServicePayload
    {
        public string image;
        public string audio;
        public VLMSceneContext context;
    }

    [Serializable]
    private class VLMSceneContext
    {
        public bool xr_active;
        public Vector3Data camera_position;
        public Vector3Data camera_forward;
        public Vector3Data camera_right;
        public Vector3Data camera_up;
        public string camera_name;

        public string selected_object;
        public Vector3Data selected_position;
        public Vector3Data selected_forward;
        public Vector3Data selected_right;
        public Vector3Data selected_up;
        public Vector3Data selected_scale;
        public string selected_mode;
    }

    [Serializable]
    private struct Vector3Data
    {
        public float x;
        public float y;
        public float z;

        public Vector3Data(Vector3 vector)
        {
            x = vector.x;
            y = vector.y;
            z = vector.z;
        }
    }

    private void Awake()
    {
        if (commandProcessor == null)
            commandProcessor = FindAnyObjectByType<VoiceCommandProcessor>();

        if (captureCamera == null)
            captureCamera = Camera.main;

        if (objectSelector == null && commandProcessor != null)
            objectSelector = commandProcessor.objectSelector;

        if (objectSelector == null)
            objectSelector = FindAnyObjectByType<ObjectSelector>();
    }

    private void OnEnable()
    {
        StartMicrophone();
    }

    private void OnDisable()
    {
        StopMicrophone();
    }

    private void StartMicrophone()
    {
        if (Microphone.devices.Length == 0)
        {
            Debug.LogError("[VLM Client] No microphone devices detected. Voice control disabled.");
            enabled = false;
            return;
        }

        microphoneDevice = Microphone.devices[0];
        microphoneClip = Microphone.Start(microphoneDevice, true, microphoneLoopSeconds, microphoneSampleRate);
        if (logDebug)
            Debug.Log($"[VLM Client] Started microphone '{microphoneDevice}' @ {microphoneSampleRate}Hz");
    }

    private void StopMicrophone()
    {
        if (!string.IsNullOrEmpty(microphoneDevice) && Microphone.IsRecording(microphoneDevice))
            Microphone.End(microphoneDevice);

        microphoneClip = null;
        microphoneDevice = null;
    }

    private void Update()
    {
        if (microphoneClip == null || captureCamera == null)
            return;

        float volume = GetRmsVolume();
        if (volume >= speechThreshold)
        {
            if (!isSpeaking)
            {
                isSpeaking = true;
                silenceTimer = 0f;
                speechStartSample = CalculateSpeechStartSample();
                if (logDebug)
                    Debug.Log("[VLM Client] Speech detected, recording segment…");

                if (pendingCaptureCoroutine != null)
                    StopCoroutine(pendingCaptureCoroutine);
                pendingCaptureCoroutine = StartCoroutine(SendSegmentWhenSilence());
            }
            else
            {
                silenceTimer = 0f;
            }
        }
        else if (isSpeaking)
        {
            silenceTimer += Time.deltaTime;
            if (silenceTimer >= silenceTimeout)
            {
                isSpeaking = false;
                if (logDebug)
                    Debug.Log("[VLM Client] Silence detected, finalizing segment.");
            }
        }
    }

    private float GetRmsVolume()
    {
        if (microphoneClip == null || string.IsNullOrEmpty(microphoneDevice))
            return 0f;

        float[] samples = new float[sampleWindow];
        int micPosition = Microphone.GetPosition(microphoneDevice) - sampleWindow + 1;
        if (micPosition < 0)
            return 0f;

        microphoneClip.GetData(samples, micPosition);
        double sum = 0;
        for (int i = 0; i < samples.Length; i++)
            sum += samples[i] * samples[i];
        return Mathf.Sqrt((float)(sum / samples.Length));
    }

    private int CalculateSpeechStartSample()
    {
        if (microphoneClip == null || string.IsNullOrEmpty(microphoneDevice))
            return 0;

        int currentPosition = Microphone.GetPosition(microphoneDevice);
        int preRollSamples = Mathf.RoundToInt(preRollSeconds * microphoneClip.frequency);

        preRollSamples = Mathf.Clamp(preRollSamples, 0, microphoneClip.samples - 1);

        int startSample = currentPosition - preRollSamples;
        if (startSample < 0)
            startSample += microphoneClip.samples;

        return startSample;
    }

    private IEnumerator SendSegmentWhenSilence()
    {
        while (isSpeaking)
            yield return null;

        if (microphoneClip == null)
            yield break;

        int endSample = Microphone.GetPosition(microphoneDevice);
        int sampleCount = endSample - speechStartSample;
        if (sampleCount <= 0)
            sampleCount += microphoneClip.samples;

        if (sampleCount <= 0)
        {
            Debug.LogWarning("[VLM Client] No audio samples captured, skipping.");
            yield break;
        }

        float[] samples = new float[sampleCount * microphoneClip.channels];
        if (endSample > speechStartSample)
        {
            microphoneClip.GetData(samples, speechStartSample);
        }
        else
        {
            int firstPartSamples = microphoneClip.samples - speechStartSample;
            float[] firstBuffer = new float[firstPartSamples * microphoneClip.channels];
            microphoneClip.GetData(firstBuffer, speechStartSample);

            float[] secondBuffer = new float[endSample * microphoneClip.channels];
            if (endSample > 0)
                microphoneClip.GetData(secondBuffer, 0);

            Array.Copy(firstBuffer, samples, firstBuffer.Length);
            Array.Copy(secondBuffer, 0, samples, firstBuffer.Length, secondBuffer.Length);
        }

        AudioClip trimmedClip = AudioClip.Create("VLM_TempClip", sampleCount, microphoneClip.channels, microphoneClip.frequency, false);
        trimmedClip.SetData(samples, 0);

        Texture2D screenshot = CaptureCamera(captureCamera, captureWidth, captureHeight);
        string imageBase64 = Convert.ToBase64String(screenshot.EncodeToPNG());
        string audioBase64 = Convert.ToBase64String(AudioClipToWavBytes(trimmedClip));

        if (logDebug)
            Debug.Log($"[VLM Client] Captured segment ({sampleCount} samples). Sending to service…");

        yield return SendRequest(imageBase64, audioBase64);

        Destroy(screenshot);
        Destroy(trimmedClip);
        pendingCaptureCoroutine = null;
    }

    private IEnumerator SendRequest(string imageBase64, string audioBase64)
    {
        VLMSceneContext context = BuildSceneContext();

        VLMServicePayload payload = new VLMServicePayload
        {
            image = imageBase64,
            audio = audioBase64,
            context = context
        };

        string json = JsonUtility.ToJson(payload);
        byte[] body = Encoding.UTF8.GetBytes(json);

        using UnityWebRequest request = new UnityWebRequest(serviceUrl, UnityWebRequest.kHttpVerbPOST);
        request.uploadHandler = new UploadHandlerRaw(body);
        request.downloadHandler = new DownloadHandlerBuffer();
        request.SetRequestHeader("Content-Type", "application/json");

        yield return request.SendWebRequest();

        if (request.result != UnityWebRequest.Result.Success)
        {
            Debug.LogError($"[VLM Client] Request failed: {request.error}");
            yield break;
        }

        string responseJson = request.downloadHandler.text;
        if (logDebug)
            Debug.Log($"[VLM Client] Response: {responseJson}");

        VLMServiceResponse response = null;
        try
        {
            response = JsonUtility.FromJson<VLMServiceResponse>(responseJson);
        }
        catch (Exception ex)
        {
            Debug.LogError($"[VLM Client] Failed to parse response JSON: {ex.Message}");
        }

        if (response == null)
            yield break;

        if (!response.success)
        {
            Debug.LogError($"[VLM Client] Service reported error: {response.error}\nRaw: {response.raw}");
            yield break;
        }

        if (response.command == null)
        {
            Debug.LogWarning("[VLM Client] No command returned.");
            yield break;
        }

        if (commandProcessor == null)
        {
            Debug.LogError("[VLM Client] No VoiceCommandProcessor assigned.");
            yield break;
        }

        var result = commandProcessor.ProcessCommand(response.command);
        if (result.success)
            Debug.Log($"[VLM Client] ✓ Command executed: {result.message}");
        else
            Debug.LogError($"[VLM Client] ✗ Command failed: {result.message}");
    }

    private VLMSceneContext BuildSceneContext()
    {
        Camera cam = captureCamera != null ? captureCamera : Camera.main;

        VLMSceneContext context = new VLMSceneContext
        {
            xr_active = XRSettings.isDeviceActive
        };

        if (cam != null)
        {
            Transform ct = cam.transform;
            context.camera_name = cam.name;
            context.camera_position = new Vector3Data(ct.position);
            context.camera_forward = new Vector3Data(ct.forward.normalized);
            context.camera_right = new Vector3Data(ct.right.normalized);
            context.camera_up = new Vector3Data(ct.up.normalized);
        }

        Transform selected = GetSelectedTransform();
        if (selected != null)
        {
            context.selected_object = selected.name;
            context.selected_position = new Vector3Data(selected.position);
            context.selected_forward = new Vector3Data(selected.forward.normalized);
            context.selected_right = new Vector3Data(selected.right.normalized);
            context.selected_up = new Vector3Data(selected.up.normalized);
            context.selected_scale = new Vector3Data(selected.lossyScale);

            EditableMesh mesh = selected.GetComponent<EditableMesh>();
            if (mesh != null)
                context.selected_mode = mesh.mode.ToString();
        }

        return context;
    }

    private Transform GetSelectedTransform()
    {
        if (objectSelector != null)
            return objectSelector.GetCurrentSelection();

        if (commandProcessor != null && commandProcessor.objectSelector != null)
            return commandProcessor.objectSelector.GetCurrentSelection();

        return null;
    }

    private static Texture2D CaptureCamera(Camera cam, int width, int height)
    {
        RenderTexture rt = new RenderTexture(width, height, 24);
        RenderTexture previousTarget = cam.targetTexture;

        cam.targetTexture = rt;
        cam.Render();

        RenderTexture previousActive = RenderTexture.active;
        RenderTexture.active = rt;

        Texture2D texture = new Texture2D(width, height, TextureFormat.RGB24, false);
        texture.ReadPixels(new Rect(0, 0, width, height), 0, 0);
        texture.Apply();

        cam.targetTexture = previousTarget;
        RenderTexture.active = previousActive;
        rt.Release();
        UnityEngine.Object.Destroy(rt);

        return texture;
    }

    private byte[] AudioClipToWavBytes(AudioClip clip)
    {
        float[] samples = new float[clip.samples * clip.channels];
        clip.GetData(samples, 0);

        using MemoryStream memoryStream = new MemoryStream();
        using BinaryWriter writer = new BinaryWriter(memoryStream);
        writer.Write("RIFF".ToCharArray());
        writer.Write(36 + samples.Length * 2);
        writer.Write("WAVE".ToCharArray());
        writer.Write("fmt ".ToCharArray());
        writer.Write(16);
        writer.Write((ushort)1);
        writer.Write((ushort)clip.channels);
        writer.Write(clip.frequency);
        writer.Write(clip.frequency * clip.channels * 2);
        writer.Write((ushort)(clip.channels * 2));
        writer.Write((ushort)16);
        writer.Write("data".ToCharArray());
        writer.Write(samples.Length * 2);

        foreach (float sample in samples)
        {
            float clamped = Mathf.Clamp(sample, -1f, 1f);
            short pcm = (short)Mathf.RoundToInt(clamped * short.MaxValue);
            writer.Write(pcm);
        }

        writer.Flush();
        return memoryStream.ToArray();
    }
}

