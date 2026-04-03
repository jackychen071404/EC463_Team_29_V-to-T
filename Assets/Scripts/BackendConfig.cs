public static class BackendConfig
{
    public static class Voice
    {
        public const float DefaultRecordingDurationSeconds = 3f;
        public const float AndroidSettleDelaySeconds = 0.2f;
        public const float NonAndroidSettleDelaySeconds = AndroidSettleDelaySeconds;
        public const float MicStartTimeoutSeconds = 1.5f;
        public const float MinMicStartTimeoutSeconds = 0.1f;
        public const int SampleRateHz = 44100;
        public const int MaxRecordings = 5;
        public const string RecordingPrefix = "VoiceRecording_";
        public const string RecordingExtension = ".wav";
        public const string RecordingNumberFormat = "D2";
        public const string TrimmedClipName = "TrimmedRecording";
        public static string RecordingSearchPattern => $"{RecordingPrefix}*{RecordingExtension}";

        public static string FormatRecordingFileName(int recordingNumber)
        {
            return $"{RecordingPrefix}{recordingNumber.ToString(RecordingNumberFormat)}{RecordingExtension}";
        }
    }

    public static class Ml
    {
        public const string CmuDictResourceName = "cmudict";
        public const int DefaultWarmUpSampleCount = 16000;
        public const int DefaultExpectedSampleRate = 16000;
        public const bool DefaultNormalizeAudio = true;
        public const float LeadingSilencePaddingSeconds = 0.10f;
        public const int BlankTokenId = 42;
        public const float ScoreErrorValue = -1f;
    }

    public static class Processing
    {
        public const float CorrectScoreThreshold = 70f;
    }

    public static class Cmu
    {
        public const string CommentPrefix = ";;;";
        public const char LineSeparator = '\n';
        public const string WordPhonemeSeparator = "  ";
        public const char PhonemeSeparator = ' ';
    }

    public static class Scoring
    {
        public const float MainWeight = 0.70f;
        public const float VowelWeight = 0.15f;
        public const float LengthWeight = 0.15f;

        // TODO: Change these afterwards based on testing and tuning
        public const float ScoreScale = 120f;
        public const float MinLengthRatio = 0.5f;
        public const float MaxLengthRatio = 1.8f;
        public const float OutOfRangeLengthPenalty = 0.4f;
        public const float RatioPenaltyFactor = 0.5f;

        public static readonly string[] Vowels =
        {
            "a","aw","ay","e","ee","i", "I", "o","oau","oh","oi","oo","or","u","uoh","E"
        };

        public const float InitialConsonantBonus = 15f;
    }
}