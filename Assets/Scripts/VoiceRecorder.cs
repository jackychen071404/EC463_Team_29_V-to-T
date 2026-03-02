using UnityEngine;
using System.Collections;
using System.IO;

public class VoiceRecorder : MonoBehaviour
{
    public static VoiceRecorder Instance;

    [Header("Recording Settings")]
    public float recordingDuration = 2f;  // Recording time in seconds

    [Header("Microphone Stability")]
    [SerializeField] private float androidSettleDelaySeconds = 0.2f;
    [SerializeField] private float nonAndroidSettleDelaySeconds = 0.05f;
    [SerializeField] private float micStartTimeoutSeconds = 1.5f;

    private AudioClip recording;
    private string micName;
    private bool isRecording = false;
    private bool isPreparingMic = false;
    private float nextStartAllowedAt = 0f;
    private int recordingCount = 0;
    private const int MAX_RECORDINGS = 5;

    void Awake()
    {
        // Singleton pattern
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
        }
        else
        {
            Destroy(gameObject);
            return;
        }

        InitializeMicrophone();
    }

    private void InitializeMicrophone()
    {
        if (Microphone.devices.Length > 0)
        {
            micName = Microphone.devices[0];
            Debug.Log("Using microphone: " + micName);
            LoadRecordingCount();
        }
        else
        {
            Debug.LogError("No microphone detected!");
        }
    }

    public bool StartRecording()
    {
        if (micName == null)
        {
            Debug.LogWarning("Cannot start recording - no microphone detected");
            return false;
        }

        if (isRecording || isPreparingMic)
        {
            Debug.LogWarning("Cannot start recording - microphone is already active or preparing");
            return false;
        }

        StartCoroutine(StartRecordingRoutine());
        return true;
    }

    public bool StopAndSaveRecording()
    {
        if (!isRecording)
        {
            Debug.LogWarning("No recording in progress");
            return false;
        }

        int position = Microphone.GetPosition(micName);
        if (Microphone.IsRecording(micName))
            Microphone.End(micName);

        isRecording = false;
        nextStartAllowedAt = Time.unscaledTime + GetSettleDelay();

        if (recording == null)
        {
            Debug.LogError("Recording clip is null after stopping microphone.");
            return false;
        }

        if (position <= 0)
        {
            Debug.LogWarning("Recording contained no samples. Skipping WAV save.");
            recording = null;
            return false;
        }

        // Trim the audio clip to actual recorded length
        float[] samples = new float[position * recording.channels];
        recording.GetData(samples, 0);
        
        AudioClip trimmedClip = AudioClip.Create("TrimmedRecording", position, recording.channels, recording.frequency, false);
        trimmedClip.SetData(samples, 0);

        Debug.Log("Recording stopped and saved.");

        // Increment count and create unique filename
        recordingCount++;
        string filename = $"VoiceRecording_{recordingCount:D2}.wav";
        
        SaveWav(filename, trimmedClip);
        recording = null;
        
        // Delete oldest file if we exceed the limit
        if (recordingCount > MAX_RECORDINGS)
        {
            DeleteOldestRecording();
        }

        return true;
    }

    private IEnumerator StartRecordingRoutine()
    {
        isPreparingMic = true;

        if (Microphone.IsRecording(micName))
            Microphone.End(micName);

        while (Time.unscaledTime < nextStartAllowedAt)
            yield return null;

        int targetFrequency = 44100;
        recording = Microphone.Start(micName, false, Mathf.CeilToInt(recordingDuration), targetFrequency);

        float timeoutAt = Time.unscaledTime + Mathf.Max(0.1f, micStartTimeoutSeconds);
        while (Microphone.GetPosition(micName) <= 0)
        {
            if (Time.unscaledTime >= timeoutAt)
            {
                Debug.LogError("Microphone failed to initialize in time. Releasing session.");
                if (Microphone.IsRecording(micName))
                    Microphone.End(micName);

                recording = null;
                isPreparingMic = false;
                isRecording = false;
                nextStartAllowedAt = Time.unscaledTime + GetSettleDelay();
                yield break;
            }

            yield return null;
        }

        isRecording = true;
        isPreparingMic = false;
        Debug.Log("Recording started...");
    }

    private float GetSettleDelay()
    {
        return Application.platform == RuntimePlatform.Android
            ? Mathf.Max(0f, androidSettleDelaySeconds)
            : Mathf.Max(0f, nonAndroidSettleDelaySeconds);
    }

    private void LoadRecordingCount()
    {
        string directory = Application.persistentDataPath;
        
        if (!Directory.Exists(directory))
            return;
            
        // Find all existing voice recordings
        string[] existingFiles = Directory.GetFiles(directory, "VoiceRecording_*.wav");
        
        if (existingFiles.Length > 0)
        {
            // Find the highest number
            int maxNumber = 0;
            foreach (string file in existingFiles)
            {
                string filename = Path.GetFileNameWithoutExtension(file);
                string numberPart = filename.Replace("VoiceRecording_", "");
                
                if (int.TryParse(numberPart, out int number))
                {
                    if (number > maxNumber)
                        maxNumber = number;
                }
            }
            recordingCount = maxNumber;
            Debug.Log($"Found {existingFiles.Length} existing recordings. Starting from #{recordingCount + 1}");
        }
    }

    private void DeleteOldestRecording()
    {
        int oldestNumber = recordingCount - MAX_RECORDINGS;
        string oldestFile = $"VoiceRecording_{oldestNumber:D2}.wav";
        string oldestPath = Path.Combine(Application.persistentDataPath, oldestFile);
        
        if (File.Exists(oldestPath))
        {
            File.Delete(oldestPath);
            Debug.Log($"Deleted oldest recording: {oldestFile}");
        }
    }

    private void SaveWav(string filename, AudioClip clip)
    {
        var samples = new float[clip.samples * clip.channels];
        clip.GetData(samples, 0);

        byte[] wavData = ConvertToWav(samples, clip.channels, clip.frequency);
        string filePath = Path.Combine(Application.persistentDataPath, filename);
        File.WriteAllBytes(filePath, wavData);

        Debug.Log($"Saved recording to: {filePath}");
        Debug.Log($"Total recordings: {Mathf.Min(recordingCount, MAX_RECORDINGS)} of {MAX_RECORDINGS}");
    }

    private byte[] ConvertToWav(float[] samples, int channels, int sampleRate)
    {
        using (MemoryStream stream = new MemoryStream())
        using (BinaryWriter writer = new BinaryWriter(stream))
        {
            int byteRate = sampleRate * channels * 2;
            writer.Write(System.Text.Encoding.ASCII.GetBytes("RIFF"));
            writer.Write(36 + samples.Length * 2);
            writer.Write(System.Text.Encoding.ASCII.GetBytes("WAVEfmt "));
            writer.Write(16);
            writer.Write((short)1);
            writer.Write((short)channels);
            writer.Write(sampleRate);
            writer.Write(byteRate);
            writer.Write((short)(channels * 2));
            writer.Write((short)16);
            writer.Write(System.Text.Encoding.ASCII.GetBytes("data"));
            writer.Write(samples.Length * 2);

            foreach (float sample in samples)
            {
                short val = (short)(Mathf.Clamp(sample, -1f, 1f) * short.MaxValue);
                writer.Write(val);
            }

            return stream.ToArray();
        }
    }

    // Public method to get the path of the latest recording
    public string GetLatestRecordingPath()
    {
        string filename = $"VoiceRecording_{recordingCount:D2}.wav";
        return Path.Combine(Application.persistentDataPath, filename);
    }

    public bool IsCurrentlyRecording()
    {
        return isRecording;
    }

    private void OnDisable()
    {
        ForceReleaseMicrophone();
    }

    private void OnDestroy()
    {
        ForceReleaseMicrophone();
    }

    private void ForceReleaseMicrophone()
    {
        if (!string.IsNullOrEmpty(micName) && Microphone.IsRecording(micName))
            Microphone.End(micName);

        isRecording = false;
        isPreparingMic = false;
    }
}