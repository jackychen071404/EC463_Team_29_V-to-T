using UnityEngine;
using System.IO;

public class VoiceRecorder : MonoBehaviour
{
    private AudioClip recording;
    private string micName;
    private bool isRecording = false;
    
    // ADDED: Track recording count
    private int recordingCount = 0;
    private const int MAX_RECORDINGS = 15; // ADDED: Maximum number of recordings to keep

    void Start()
    {
        if (Microphone.devices.Length > 0)
        {
            micName = Microphone.devices[0];
            Debug.Log("Using microphone: " + micName);
        }
        else
        {
            Debug.LogError("No microphone detected!");
        }
        
        // ADDED: Load the current recording count from saved files
        LoadRecordingCount();
    }

    void Update()
    {
        if (Input.GetKeyDown(KeyCode.R))
            StartRecording();

        if (Input.GetKeyDown(KeyCode.S))
            StopRecording();
    }

    public void StartRecording()
    {
        if (isRecording || micName == null) return;

        isRecording = true;
        recording = Microphone.Start(micName, false, 30, 44100);
        Debug.Log("Recording started...");
    }

    public void StopRecording()
    {
        if (!isRecording) return;

        Microphone.End(micName);
        isRecording = false;
        Debug.Log("Recording stopped.");

        // CHANGED: Increment count and create unique filename
        recordingCount++;
        string filename = $"VoiceRecording_{recordingCount:D2}.wav"; // D2 = 2 digits (01, 02, etc.)
        
        SaveWav(filename, recording);
        
        // ADDED: Delete oldest file if we exceed the limit
        if (recordingCount > MAX_RECORDINGS)
        {
            DeleteOldestRecording();
        }
    }

    // ADDED: Method to find and load existing recording count
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

    // ADDED: Method to delete the oldest recording
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
        var samples = new float[clip.samples];
        clip.GetData(samples, 0);

        byte[] wavData = ConvertToWav(samples, clip.channels, clip.frequency);
        string filePath = Path.Combine(Application.persistentDataPath, filename);
        File.WriteAllBytes(filePath, wavData);

        Debug.Log($"Saved recording to: {filePath}");
        Debug.Log($"Total recordings: {Mathf.Min(recordingCount, MAX_RECORDINGS)} of {MAX_RECORDINGS}"); // ADDED: Show count
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
}