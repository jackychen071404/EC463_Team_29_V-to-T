using UnityEngine;
using System.Collections;
using System.IO;

public class VoiceRecorder : MonoBehaviour
{
    private const string LogSource = "VoiceRecorder.cs";
    public static VoiceRecorder Instance;

    [Header("Recording Settings")]
    public float recordingDuration = BackendConfig.Voice.DefaultRecordingDurationSeconds;  // Recording time in seconds

    [Header("Microphone Stability")]
    [SerializeField] private float androidSettleDelaySeconds = BackendConfig.Voice.AndroidSettleDelaySeconds;
    [SerializeField] private float nonAndroidSettleDelaySeconds = BackendConfig.Voice.NonAndroidSettleDelaySeconds;
    [SerializeField] private float micStartTimeoutSeconds = BackendConfig.Voice.MicStartTimeoutSeconds;

    private AudioClip recording;
    private string micName;
    private bool isRecording = false;
    private bool isPreparingMic = false;
    private float nextStartAllowedAt = 0f;
    private int recordingCount = 0;

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
            BackendLogger.Info(LogSource, "MicrophoneInitialized", $"selectedDevice={micName}, deviceCount={Microphone.devices.Length}");
            LoadRecordingCount();
        }
        else
        {
            BackendLogger.Error(LogSource, "MicrophoneMissing", "No microphone devices detected by Unity");
        }
    }

    public bool StartRecording()
    {
        if (micName == null)
        {
            BackendLogger.Warn(LogSource, "StartRecordingRejected", "reason=no_mic_device");
            return false;
        }

        if (isRecording || isPreparingMic)
        {
            BackendLogger.Warn(LogSource, "StartRecordingRejected", $"reason=mic_busy, isRecording={isRecording}, isPreparingMic={isPreparingMic}");
            return false;
        }

        StartCoroutine(StartRecordingRoutine());
        return true;
    }

    public bool StopAndSaveRecording()
    {
        if (!isRecording)
        {
            BackendLogger.Warn(LogSource, "StopRecordingRejected", "reason=no_active_recording");
            return false;
        }

        int position = Microphone.GetPosition(micName);
        if (Microphone.IsRecording(micName))
            Microphone.End(micName);

        isRecording = false;
        nextStartAllowedAt = Time.unscaledTime + GetSettleDelay();

        if (recording == null)
        {
            BackendLogger.Error(LogSource, "RecordingClipMissing", "recording clip became null after microphone stop");
            return false;
        }

        if (position <= 0)
        {
            BackendLogger.Warn(LogSource, "RecordingEmpty", $"micPosition={position}, action=skip_save");
            recording = null;
            return false;
        }

        // Trim the audio clip to actual recorded length
        float[] samples = new float[position * recording.channels];
        recording.GetData(samples, 0);
        
        AudioClip trimmedClip = AudioClip.Create(BackendConfig.Voice.TrimmedClipName, position, recording.channels, recording.frequency, false);
        trimmedClip.SetData(samples, 0);

        BackendLogger.Info(LogSource, "RecordingStopped", $"capturedSamples={position}, channels={recording.channels}, frequency={recording.frequency}");

        // Increment count and create unique filename
        recordingCount++;
        string filename = BackendConfig.Voice.FormatRecordingFileName(recordingCount);
        
        SaveWav(filename, trimmedClip);
        recording = null;
        
        // Delete oldest file if we exceed the limit
        if (recordingCount > BackendConfig.Voice.MaxRecordings)
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

        int targetFrequency = BackendConfig.Voice.SampleRateHz;
        recording = Microphone.Start(micName, false, Mathf.CeilToInt(recordingDuration), targetFrequency);
        BackendLogger.Verbose(true, LogSource, "RecordingStartRequested", $"device={micName}, durationSec={recordingDuration:F2}, sampleRateHz={targetFrequency}");

        float timeoutAt = Time.unscaledTime + Mathf.Max(BackendConfig.Voice.MinMicStartTimeoutSeconds, micStartTimeoutSeconds);
        while (Microphone.GetPosition(micName) <= 0)
        {
            if (Time.unscaledTime >= timeoutAt)
            {
                BackendLogger.Error(LogSource, "MicrophoneStartTimeout", $"timeoutSec={micStartTimeoutSeconds:F2}, settleDelaySec={GetSettleDelay():F2}");
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
        BackendLogger.Info(LogSource, "RecordingStarted", $"device={micName}, settleDelaySec={GetSettleDelay():F2}");
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
        string[] existingFiles = Directory.GetFiles(directory, BackendConfig.Voice.RecordingSearchPattern);
        
        if (existingFiles.Length > 0)
        {
            // Find the highest number
            int maxNumber = 0;
            foreach (string file in existingFiles)
            {
                string filename = Path.GetFileNameWithoutExtension(file);
                string numberPart = filename.Replace(BackendConfig.Voice.RecordingPrefix, "");
                
                if (int.TryParse(numberPart, out int number))
                {
                    if (number > maxNumber)
                        maxNumber = number;
                }
            }
            recordingCount = maxNumber;
            BackendLogger.Info(LogSource, "RecordingIndexLoaded", $"existingFiles={existingFiles.Length}, nextRecordingIndex={recordingCount + 1}");
        }
    }

    private void DeleteOldestRecording()
    {
        int oldestNumber = recordingCount - BackendConfig.Voice.MaxRecordings;
        string oldestFile = BackendConfig.Voice.FormatRecordingFileName(oldestNumber);
        string oldestPath = Path.Combine(Application.persistentDataPath, oldestFile);
        
        if (File.Exists(oldestPath))
        {
            File.Delete(oldestPath);
            BackendLogger.Info(LogSource, "RecordingDeleted", $"fileName={oldestFile}, fullPath={oldestPath}");
        }
    }

    private void SaveWav(string filename, AudioClip clip)
    {
        var samples = new float[clip.samples * clip.channels];
        clip.GetData(samples, 0);

        byte[] wavData = ConvertToWav(samples, clip.channels, clip.frequency);
        string filePath = Path.Combine(Application.persistentDataPath, filename);
        File.WriteAllBytes(filePath, wavData);

        BackendLogger.Info(LogSource, "RecordingSaved", $"fileName={filename}, fullPath={filePath}, bytes={wavData.Length}, channels={clip.channels}, sampleRateHz={clip.frequency}");
        BackendLogger.Verbose(true, LogSource, "RecordingRetentionStatus", $"retained={Mathf.Min(recordingCount, BackendConfig.Voice.MaxRecordings)}, max={BackendConfig.Voice.MaxRecordings}");
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
        string filename = BackendConfig.Voice.FormatRecordingFileName(recordingCount);
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