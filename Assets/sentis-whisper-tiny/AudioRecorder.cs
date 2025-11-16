using UnityEngine;

public class AudioRecorder : MonoBehaviour
{
    [Header("Recording Settings")]
    public int sampleRate = 16000;
    public float recordingDuration = 30f; // Whisper expects 30 seconds

    public delegate void OnRecordingComplete(float[] audioData);
    public event OnRecordingComplete RecordingComplete;

    bool isRecording = false;
    string recordingDevice;
    AudioClip recordingClip;

    AudioSource audioSource;

    void Start()
    {
        audioSource = GetComponent<AudioSource>();
        if (audioSource == null)
            audioSource = gameObject.AddComponent<AudioSource>();
        // Get default microphone
        if (Microphone.devices.Length > 0)
        {
            recordingDevice = Microphone.devices[0];
            Debug.Log($"Using microphone: {recordingDevice}");
        }
        else
        {
            Debug.LogError("No microphone found!");
        }
    }

    void Update()
    {
        if (Input.GetKeyDown(KeyCode.Space))
        {
            if (!isRecording)
                StartRecording();
            else
                StopRecording();
        }
    }

    public void StartRecording()
    {
        if (isRecording) return;
        
        Debug.Log("Starting recording...");
        isRecording = true;
        recordingClip = Microphone.Start(recordingDevice, false, (int)recordingDuration, sampleRate);
    }

    public void StopRecording()
    {
        if (!isRecording) return;
        
        Debug.Log("Stopping recording...");
        isRecording = false;
        
        int micPosition = Microphone.GetPosition(recordingDevice);
        Microphone.End(recordingDevice);
        
        // Pass the actual recording length to the coroutine
        StartCoroutine(ProcessRecordingAfterDelay(micPosition));
    }

    private System.Collections.IEnumerator ProcessRecordingAfterDelay(int actualSamples)
    {
        yield return null; // Wait one frame
        
        if (recordingClip == null)
        {
            Debug.LogError("Recording clip is null!");
            yield break;
        }

        // Whisper expects exactly 30 seconds of audio at 16kHz = 480,000 samples
        const int WHISPER_SAMPLE_COUNT = 480000;
        
        // Get the actual recorded samples
        float[] recordedData = new float[actualSamples];
        recordingClip.GetData(recordedData, 0);
        
        // Create a properly sized array for Whisper
        float[] audioData = new float[WHISPER_SAMPLE_COUNT];
        
        if (actualSamples < WHISPER_SAMPLE_COUNT)
        {
            // Copy recorded data and pad with zeros
            System.Array.Copy(recordedData, audioData, actualSamples);
            Debug.Log($"Recording padded: {actualSamples} samples -> {WHISPER_SAMPLE_COUNT} samples");
        }
        else
        {
            // Truncate to 30 seconds if longer
            System.Array.Copy(recordedData, audioData, WHISPER_SAMPLE_COUNT);
            Debug.Log($"Recording truncated: {actualSamples} samples -> {WHISPER_SAMPLE_COUNT} samples");
        }
        
        try
        {
            Debug.Log($"Recording complete: {audioData.Length} samples (16kHz, 30s)");
            Debug.Log($"About to invoke RecordingComplete. Listeners: {RecordingComplete?.GetInvocationList().Length ?? 0}");
            RecordingComplete?.Invoke(audioData);
            Debug.Log("RecordingComplete invoked successfully");
        }
        catch (System.Exception e)
        {
            Debug.LogError($"Error processing audio data: {e.Message}\n{e.StackTrace}");
        }
        // Create an AudioClip from the recorded data
        AudioClip playbackClip = AudioClip.Create(
            "Playback",                     // name
            audioData.Length,               // number of samples
            1,                              // number of channels
            sampleRate,                     // sample rate
            false                           // streaming
        );

        // Copy data into the clip
        playbackClip.SetData(audioData, 0);

        // Play through the AudioSource
        audioSource.clip = playbackClip;
        audioSource.Play();

        Debug.Log("Playing back recorded audio...");
    }

    public bool IsRecording => isRecording;
}