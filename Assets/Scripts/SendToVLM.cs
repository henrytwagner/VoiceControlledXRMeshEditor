using UnityEngine;
using UnityEngine.Networking;
using System.Collections;
using System.Text;

public class SendToPython : MonoBehaviour
{

    public string micName;
    AudioClip micClip;
    public float threshold = 0.02f;
    public int sampleWindow = 128;

    bool isSpeaking = false;
    float silenceTimer = 0f;
    public float silenceTimeout = 1.0f;

    void Start()
    {
        micName = Microphone.devices[0];
        micClip = Microphone.Start(micName, true, 10, 16000);
    }

    void Update()
    {
        float volume = GetAveragedVolume();
        if (volume > threshold)
        {
            if (!isSpeaking)
            {
                isSpeaking = true;
                Debug.Log("Start recording");
                StartCoroutine(SendOnVoiceDetected());
            }
            silenceTimer = 0f;
        }
        else if (isSpeaking)
        {
            silenceTimer += Time.deltaTime;
            if (silenceTimer >= silenceTimeout)
            {
                isSpeaking = false;
                Debug.Log("Stop recording.");
            }
        }
    }

    float GetAveragedVolume()
    {
        float[] data = new float[sampleWindow];
        int micPos = Microphone.GetPosition(micName) - sampleWindow + 1;
        if (micPos < 0) return 0;
        micClip.GetData(data, micPos);
        float sum = 0;
        for (int i = 0; i < sampleWindow; i++) sum += data[i] * data[i];
        return Mathf.Sqrt(sum / sampleWindow);
    }

    public byte[] AudioClipToWavBytes(AudioClip clip)
    {

        // 1. Get Audio Data
        float[] samples = new float[clip.samples * clip.channels];
        clip.GetData(samples, 0);

        using (MemoryStream memoryStream = new MemoryStream())
        {
            using (BinaryWriter writer = new BinaryWriter(memoryStream))
            {
                // 2. Prepare WAV Header
                writer.Write(new char[] { 'R', 'I', 'F', 'F' });
                writer.Write(36 + samples.Length * 2); // ChunkSize
                writer.Write(new char[] { 'W', 'A', 'V', 'E' });

                // FMT chunk
                writer.Write(new char[] { 'f', 'm', 't', ' ' });
                writer.Write(16); // Subchunk1Size
                writer.Write((ushort)1); // AudioFormat (1 = PCM)
                writer.Write((ushort)clip.channels);
                writer.Write(clip.frequency); // SampleRate
                writer.Write(clip.frequency * clip.channels * 2); // ByteRate
                writer.Write((ushort)(clip.channels * 2)); // BlockAlign
                writer.Write((ushort)16); // BitsPerSample

                // DATA chunk
                writer.Write(new char[] { 'd', 'a', 't', 'a' });
                writer.Write(samples.Length * 2); // Subchunk2Size

                // 3. Convert Float Data to 16-bit PCM
                foreach (float sample in samples)
                {
                    float clamped = Mathf.Clamp(sample, -1f, 1f);
                    short pcmSample = (short)(clamped * short.MaxValue);
                    writer.Write(pcmSample);
                }

                writer.Flush();
                return memoryStream.ToArray();
            }
        }
    }

    public Texture2D Capture(Camera cam, int width = 1024, int height = 1024)
    {
        // Create a temporary RenderTexture
        RenderTexture rt = new RenderTexture(width, height, 24);
        cam.targetTexture = rt;

        // Render camera view
        cam.Render();

        // Read into Texture2D
        RenderTexture.active = rt;
        Texture2D screenshot = new Texture2D(width, height, TextureFormat.RGB24, false);
        screenshot.ReadPixels(new Rect(0, 0, width, height), 0, 0);
        screenshot.Apply();

        // Cleanup
        cam.targetTexture = null;
        RenderTexture.active = null;
        UnityEngine.Object.Destroy(rt);

        return screenshot;
    }

    IEnumerator SendOnVoiceDetected()
    {
        // Wait until user stops talking
        while (isSpeaking) yield return null;

        Texture2D screenshot = Capture(Camera.main);
        byte[] imageBytes = screenshot.EncodeToPNG();

        string imageBase64 = System.Convert.ToBase64String(imageBytes);
        string audioBase64 = System.Convert.ToBase64String(AudioClipToWavBytes(micClip));

        var payload = new
        {
            image = imageBase64,
            audio = audioBase64
        };

        string json = JsonUtility.ToJson(payload);
        byte[] bodyRaw = Encoding.UTF8.GetBytes(json);

        using (UnityWebRequest req = new UnityWebRequest("http://localhost:5000/process", "POST"))
        {
            req.uploadHandler = new UploadHandlerRaw(bodyRaw);
            req.downloadHandler = new DownloadHandlerBuffer();
            req.SetRequestHeader("Content-Type", "application/json");

            yield return req.SendWebRequest();

            if (req.result != UnityWebRequest.Result.Success)
                Debug.LogError(req.error);
            else
                Debug.Log("Response: " + req.downloadHandler.text);
        }
    }
}
