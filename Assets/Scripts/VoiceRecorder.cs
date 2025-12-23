using UnityEngine;
using System.IO;

public class VoiceRecorder : MonoBehaviour
{
    public static VoiceRecorder Instance;

    [Header("Recording Settings")]
    public float recordingDuration = 2f;  // Recording time in seconds

    private AudioClip recording;
    private string micName;
    private bool isRecording = false;
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

    public void StartRecording()
    {
        if (isRecording || micName == null)
        {
            Debug.LogWarning("Cannot start recording - already recording or no microphone detected");
            return;
        }

        isRecording = true;
        recording = Microphone.Start(micName, false, (int)recordingDuration, 44100);
        Debug.Log("Recording started...");
    }

    public void StopAndSaveRecording()
    {
        if (!isRecording)
        {
            Debug.LogWarning("No recording in progress");
            return;
        }

        int position = Microphone.GetPosition(micName);
        Microphone.End(micName);
        isRecording = false;

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
        
        // Delete oldest file if we exceed the limit
        if (recordingCount > MAX_RECORDINGS)
        {
            DeleteOldestRecording();
        }
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
}