# EC463 Team 29: Voice-to-Text Speech Therapy App

<p align="center">
<img src="./images/Team 29.jpg" width="50%">
</p>
<p align="center">
Denalda Gashi, Isaac Williams, Yuhao Liang, Zonggyu Zhang, Aarush Duvvuri, Jacky Chen 
</p>

## Team links
- [Team Google Drive](https://drive.google.com/drive/folders/1Ac-iV2D9KQEg-vvfJxMU1BYvoQYRQEl0?usp=drive_link)

## Course links
- [ECE Senior Design Piazza Site](https://piazza.com/bu/fall2025/ec463/home)
- [Blackboard](http://learn.bu.edu/)

## Testing ground:
(TODO: add other branches here)

First Semester App: https://github.com/jackychen071404/EC463_Team_29_V-to-T/tree/WhisperTiny

Voice Algorithm Sandbox App: https://github.com/daarush/SpeechTherapyAppCore

Final App: https://github.com/jackychen071404/EC463_Team_29_V-to-T/tree/Feature-Demo-2

## Optional features links
- [Jira](https://voicetotext.atlassian.net/jira/software/projects/KAN/list?jql=project%20%3D%20KAN%20ORDER%20BY%20assignee%20ASC%2C%20cf%5B10019%5D%20ASC)

## Project Description
Our project aims to develop an algorithm that will be used in speech therapy to compare pronunciation of words from speech therapists and the pronunciation spoken by children to help the children’s vocalizations to be more similar to the model diction.

## Project Technical Information
**Backend pipeline:**
1. `VoiceRecorder` captures 3 seconds of microphone audio at 44,100 Hz and writes a WAV file to persistent storage (rolling 5-file buffer).
2. `Wav2VecManager` loads the WAV, runs it through a Wav2Vec2 ONNX model on the device GPU (Unity Inference Engine 2.5.0), and CTC-decodes the output into an ARPA phoneme sequence.
3. `PhonemeConverter` looks up the target word in the CMU Pronouncing Dictionary to get the ground-truth phoneme sequence.
4. `PhonemeScoringEngine` computes a weighted similarity score: phoneme edit distance (70%), vowel sequence match (15%), and length ratio penalty (15%). A scale factor and initial-consonant bonus adjust the raw score to a 0–100 range.
5. If the score clears the scene threshold (70–75%), the word is marked as passed, an unlock or progression event fires, and the next word loads.

**Practice categories:**

| Category | Words | Pass threshold |
| --- | --- | --- |
| Food | apple, pizza, cookie, banana | 75% |
| Outfit | hat, shirt, pants, shoes | 70% |
| Numbers | floating bubble grid | 70% |

**Key scripts:**

| Script | Role |
| --- | --- |
| `Wav2VecONNX.cs` | Audio preprocessing (resample → 16 kHz, normalize, pad) + GPU inference + CTC decode |
| `Wav2VecManager.cs` | Singleton; model init, GPU warm-up on startup, async score dispatch |
| `VoiceRecorder.cs` | Singleton; microphone lifecycle, WAV I/O, 5-file rolling buffer |
| `PhonemeConverter.cs` | CMU dictionary parser; word → ARPA phoneme array |
| `PhonemeScoringEngine.cs` | Weighted Levenshtein + vowel + length scoring → 0–100 |
| `BackendConfig.cs` | All tuneable constants (durations, weights, thresholds, padding) |
| `FoodSceneController.cs` | Two-tap mic UI; penguin sprite swaps; confetti on final word |
| `OutfitSceneController.cs` | Toggle-listen UI; auto-stop at 3 s; calls `GameManager.Unlock()` on pass |
| `PopupManager.cs` | Numbers flow: bubble click → record → score → pop bubble |
| `GameManager.cs` | Singleton; session-scoped unlock state and word index |

**Known limitations:**
- APK is ~422 MB due to bundled ONNX model weights; quantization needed for distribution.
- First inference on cold launch has 1–3 s latency while GPU shaders compile (warm-up mitigates but doesn't fully eliminate this).
- Only CMU dictionary words can be scored; proper nouns and non-English words have no ground truth.
- Initial consonants words have trouble scoring (migitated via adding scoring leniency to those words)

## Project Visualization

This figure provides a high-level overview of the app's backend logic and main process flow.

<p align="center">
<img src="./images/Screenshot 2026-04-07 170144.png" alt="App Flowchart" width="100%">
</p>
<p align="center">
Figure 1: Flowchart of the backend pipeline
</p>
