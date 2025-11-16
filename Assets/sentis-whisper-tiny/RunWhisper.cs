using System;
using System.IO;
using System.Text;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Unity.Sentis;

public class RunWhisper : MonoBehaviour
{
    [Header("Models")]
    public ModelAsset audioDecoderModel;
    public ModelAsset audioEncoderModel;
    public ModelAsset logMelSpectroModel;

    [Header("Vocab")]
    public TextAsset vocabAsset;

    public AudioRecorder audioRecorder;

    IWorker decoder, encoder, spectro;
    Model decoderModelRef;
    string[] tokens;

    const int maxTokens = 100;

    // Special tokens
    const int START_OF_TRANSCRIPT = 50258;
    const int ENGLISH = 50259;
    const int TRANSCRIBE = 50359;
    const int END_OF_TEXT = 50257;

    void Start()
{
    try
    {
        // Load vocab
        if (vocabAsset == null)
        {
            Debug.LogError("vocabAsset not assigned!");
            return;
        }

        // Parse vocab.json manually (it's a flat object)
        var vocabDict = new Dictionary<string, int>();
        try
        {
            // Remove outer braces and split by commas (simple parsing)
            string json = vocabAsset.text.Trim();
            json = json.Substring(1, json.Length - 2); // Remove { }
            
            var matches = System.Text.RegularExpressions.Regex.Matches(json, "\"([^\"]*?)\":\\s*(\\d+)");
            foreach (System.Text.RegularExpressions.Match match in matches)
            {
                string token = match.Groups[1].Value;
                int id = int.Parse(match.Groups[2].Value);
                vocabDict[token] = id;
            }
        }
        catch (System.Exception e)
        {
            Debug.LogError($"Error parsing vocab: {e.Message}");
            return;
        }

        if (vocabDict.Count == 0)
        {
            Debug.LogError("Failed to parse vocab JSON!");
            return;
        }

        // Find the max token ID to size the array
        int maxTokenId = 0;
        foreach (var kv in vocabDict)
        {
            if (kv.Value > maxTokenId)
                maxTokenId = kv.Value;
        }

        tokens = new string[maxTokenId + 1];
        foreach (var kv in vocabDict)
        {
            tokens[kv.Value] = kv.Key;
        }

        // Add special tokens manually if missing
        if (tokens.Length > START_OF_TRANSCRIPT && tokens[START_OF_TRANSCRIPT] == null)
            tokens[START_OF_TRANSCRIPT] = "<|startoftranscript|>";
        if (tokens.Length > ENGLISH && tokens[ENGLISH] == null)
            tokens[ENGLISH] = "<|en|>";
        if (tokens.Length > TRANSCRIBE && tokens[TRANSCRIBE] == null)
            tokens[TRANSCRIBE] = "<|transcribe|>";
        if (tokens.Length > END_OF_TEXT && tokens[END_OF_TEXT] == null)
            tokens[END_OF_TEXT] = "<|endoftext|>";

        Debug.Log($"Loaded {tokens.Length} tokens from vocab");

        // Load models
        spectro = WorkerFactory.CreateWorker(BackendType.GPUCompute, ModelLoader.Load(logMelSpectroModel));
        encoder = WorkerFactory.CreateWorker(BackendType.GPUCompute, ModelLoader.Load(audioEncoderModel));
        
        // Load decoder and inspect its inputs/outputs
        decoderModelRef = ModelLoader.Load(audioDecoderModel);
        Debug.Log("=== DECODER MODEL INPUTS ===");
        foreach (var input in decoderModelRef.inputs)
        {
            Debug.Log($"Input name: {input.name}, shape: {input.shape}");
        }
        Debug.Log("=== DECODER MODEL OUTPUTS ===");
        foreach (var output in decoderModelRef.outputs)
        {
            Debug.Log($"Output name: {output.name}");
        }
        
        decoder = WorkerFactory.CreateWorker(BackendType.GPUCompute, decoderModelRef);

        // Subscribe to recording events
        if (audioRecorder != null)
        {
            audioRecorder.RecordingComplete += StartTranscription;
            Debug.Log("AudioRecorder subscribed");
        }
        else
        {
            Debug.LogError("AudioRecorder not assigned!");
        }
    }
    catch (System.Exception e)
    {
        Debug.LogError($"Error in Start: {e.Message}\n{e.StackTrace}");
    }
}

void StartTranscription(float[] audioData)
{
    TensorFloat encodedAudio = null;
    
    try
    {
        Debug.Log($"Starting transcription of {audioData.Length} samples...");

        // Run spectrogram
        TensorFloat spectroOut;
        using (var audioTensor = new TensorFloat(new TensorShape(1, audioData.Length), audioData))
        {
            spectro.Execute(audioTensor);
            spectroOut = spectro.PeekOutput() as TensorFloat;
            Debug.Log($"[1] Spectrogram output shape: {spectroOut.shape}");
        }

        // Run encoder
        encoder.Execute(spectroOut);
        var encodedAudioTemp = encoder.PeekOutput() as TensorFloat;
        Debug.Log($"[2] Encoder output shape: {encodedAudioTemp.shape}");
        
        // Create a copy of the encoded audio to keep it alive
        encodedAudio = TensorFloat.AllocZeros(encodedAudioTemp.shape);
        encodedAudioTemp.CompleteOperationsAndDownload();
        for (int idx = 0; idx < encodedAudioTemp.count; idx++)
        {
            encodedAudio[idx] = encodedAudioTemp[idx];
        }
        
        // Clean up spectro output after encoder is done
        spectroOut.Dispose();

        // Initialize output tokens
        var outputTokens = new int[maxTokens];
        outputTokens[0] = START_OF_TRANSCRIPT;
        outputTokens[1] = ENGLISH;
        outputTokens[2] = TRANSCRIBE;
        
        Debug.Log($"[3] Initial tokens: [{START_OF_TRANSCRIPT}, {ENGLISH}, {TRANSCRIBE}]");
        
        int currentToken = 3;

        // Get the actual input names from the model
        string tokensInputName = decoderModelRef.inputs[0].name;
        string encodedAudioInputName = decoderModelRef.inputs.Count > 1 ? decoderModelRef.inputs[1].name : decoderModelRef.inputs[0].name;

        Debug.Log($"[4] Decoder input names: tokens='{tokensInputName}', encoded_audio='{encodedAudioInputName}'");
        Debug.Log($"[4b] Decoder has {decoderModelRef.outputs.Count} outputs:");

        // Run decoder loop - process tokens one at a time
        for (int i = currentToken; i < maxTokens; i++)
        {
            // Only pass the tokens generated so far
            int[] currentTokens = new int[i];
            System.Array.Copy(outputTokens, currentTokens, i);
            
            using var tokensTensor = new TensorInt(new TensorShape(1, i), currentTokens);

            // Use the actual input names from the model
            var inputs = new Dictionary<string, Tensor>();
            inputs[tokensInputName] = tokensTensor;
            inputs[encodedAudioInputName] = encodedAudio;

            decoder.Execute(inputs);

            // Inspect ALL outputs on first iteration
            if (i == currentToken)
            {
                Debug.Log($"[5] Inspecting all decoder outputs:");
                for (int outIdx = 0; outIdx < decoderModelRef.outputs.Count; outIdx++)
                {
                    string outputName = decoderModelRef.outputs[outIdx].name;
                    var testOut = decoder.PeekOutput(outputName);
                    string outputType = testOut.GetType().Name;
                    Debug.Log($"    Output {outIdx}: name='{outputName}', type={outputType}, shape={testOut.shape}");
                }
            }

            // Try to find an output with shape (1, seq_len, vocab_size) where vocab_size > 50000
            Tensor logitsTensor = null;
            string logitsName = "";
            
            for (int outIdx = 0; outIdx < decoderModelRef.outputs.Count; outIdx++)
            {
                string outputName = decoderModelRef.outputs[outIdx].name;
                var testOut = decoder.PeekOutput(outputName);
                
                // Look for 3D tensor with last dimension being vocab size (should be ~51000)
                if (testOut.shape.rank == 3 && testOut.shape[-1] > 1000)
                {
                    logitsTensor = testOut;
                    logitsName = outputName;
                    break;
                }
                
                // Also try 2D tensor (batch, vocab_size) - some models output this
                if (testOut.shape.rank == 2 && testOut.shape[-1] > 1000)
                {
                    logitsTensor = testOut;
                    logitsName = outputName;
                    break;
                }
            }

            if (logitsTensor == null)
            {
                Debug.LogError("Cannot find logits output with expected shape!");
                Debug.LogError("Expected: (1, seq_len, ~51000) or (1, ~51000)");
                return;
            }

            var outTensor = logitsTensor as TensorFloat;
            outTensor.CompleteOperationsAndDownload();

            if (i == currentToken)
            {
                Debug.Log($"[6] Using output '{logitsName}' with shape: {outTensor.shape}");
            }

            // Handle different output shapes
            int vocabSize = outTensor.shape[-1];
            int lastTokenIndex = 0;
            
            if (outTensor.shape.rank == 3)
            {
                // Shape: (batch, seq_len, vocab_size)
                int sequenceLength = outTensor.shape[-2];
                lastTokenIndex = (sequenceLength - 1) * vocabSize;
            }
            else if (outTensor.shape.rank == 2)
            {
                // Shape: (batch, vocab_size) - already at last token
                lastTokenIndex = 0;
            }
            
            int nextToken = 0;
            float maxVal = float.MinValue;

            // Get logits for the last position only
            for (int t = 0; t < vocabSize; t++)
            {
                float val = outTensor[lastTokenIndex + t];
                if (val > maxVal)
                {
                    maxVal = val;
                    nextToken = t;
                }
            }

            outputTokens[i] = nextToken;
            currentToken = i;

            // Debug first few tokens with more detail
            if (i < currentToken + 3)
            {
                string tokenStr = nextToken < tokens.Length && tokens[nextToken] != null ? tokens[nextToken] : "NULL";
                Debug.Log($"[7] Token {i}: ID={nextToken}, MaxLogit={maxVal:F2}, Text='{tokenStr}'");
                
                // Show top 5 tokens for first iteration
                if (i == 3)
                {
                    var topTokens = new List<(int id, float val)>();
                    for (int t = 0; t < Math.Min(vocabSize, tokens.Length); t++)
                    {
                        topTokens.Add((t, outTensor[lastTokenIndex + t]));
                    }
                    topTokens.Sort((a, b) => b.val.CompareTo(a.val));
                    
                    Debug.Log("    Top 10 predicted tokens:");
                    for (int t = 0; t < 10 && t < topTokens.Count; t++)
                    {
                        int tid = topTokens[t].id;
                        string tstr = tid < tokens.Length && tokens[tid] != null ? tokens[tid] : "NULL";
                        Debug.Log($"      {t+1}. ID={tid}, Logit={topTokens[t].val:F2}, Text='{tstr}'");
                    }
                }
            }

            if (nextToken == END_OF_TEXT)
            {
                Debug.Log($"[8] END_OF_TEXT token found at position {i}");
                break;
            }
        }

        // Convert token IDs to string
        StringBuilder outputBuilder = new StringBuilder();
        
        for (int i = 3; i <= currentToken; i++) // Skip the special tokens at start
        {
            int tokenId = outputTokens[i];
            if (tokenId < tokens.Length && tokens[tokenId] != null)
            {
                string token = tokens[tokenId];
                if (token == "<|endoftext|>")
                    break;
                // Clean up Whisper token formatting
                token = token.Replace("Ġ", " "); // Whisper uses Ġ for spaces
                token = token.Replace("Ċ", "\n"); // Newlines
                outputBuilder.Append(token);
            }
        }

        string transcription = outputBuilder.ToString().Trim();
        
        // Print transcription prominently
        Debug.Log("==========================================");
        Debug.Log("TRANSCRIPTION RESULT:");
        Debug.Log(transcription);
        Debug.Log("==========================================");
    }
    catch (System.Exception e)
    {
        Debug.LogError($"Transcription error: {e.Message}\n{e.StackTrace}");
    }
    finally
    {
        // Always clean up encoded audio even if there's an error
        encodedAudio?.Dispose();
    }
}
    private void OnDestroy()
    {
        decoder?.Dispose();
        encoder?.Dispose();
        spectro?.Dispose();
    }
}