# Speech Therapy App

An intuitive speech therapy application that helps users practice pronunciation through guided prompts and phoneme-based scoring.

---

## Overview

This app presents users with words in certain categories through a friendly penguin character on the frontend. Users speak the prompted word upon voocal prompting and a button press. The backend analyzes their pronunciation using phoneme scoring to provide feedback.

---

## Features

* Interactive and simple frontend with a penguin guide
* Microphone voice recording for user speech input
* Word prompts for categories such as food or clothing
* Phoneme-based scoring system
* Real-time score feedback and grade to proceed

---

## Tech Stack

### Frontend

The frontend is built in Unity and is responsible for the start screen, word category selection, and guiding the speech practice experience through a character-driven interface.

#### Category Selection

The app begins with a start screen, and from there  a category selection screen where users choose what type of words they want to practice (e.g., Food, Colors, Numbers). It is a modular and expandable approach to creating categories of words to practice. 

* Each category is mapped to a dedicated practice scene.
* Selection is handled through a central CategorySelector.cs script.

#### Example: Food Practice Scene

The FoodPractice scene, controlled by FoodSceneController.cs, demonstrates the core interaction loop of the app.

* Guided Interaction
A penguin character and a voice prompt prompts the user with a word (e.g., apple, pizza).
Visual states change depending on the current word and outcome (idle, eating, reacting).
* Audio Prompting
Each word has an associated audio clip.
The app plays the pronunciation before enabling the microphone.
Ensures users hear the correct pronunciation first.
* Speech Input Flow
User taps the mic to begin recording.
App waits until the microphone is ready.
User speaks the prompted word.
User taps again to stop recording.
* Feedback & Scoring
Audio is sent to the backend for phoneme analysis.
A score (0–100%) is returned.
The app determines pass/fail using a threshold (default: 75%).
* Success Feedback
Positive message displayed.
Penguin reacts (e.g., eating animation).
Sound effects or particles triggered (confetti, etc.).
Automatically advances to the next word.
* Failure Feedback
Encourages retry.
Plays corrective audio feedback.
Keeps the same word active.

### Backend

The backend handles audio recording, phoneme extraction, and pronunciation scoring. It is fully integrated within Unity and combines signal processing, a machine learning model, and a custom scoring algorithm. It is completely offline as per the client's requirements.

#### Voice Recording (VoiceRecorder.cs)

Responsible for capturing and saving user speech input.

* Uses Unity’s Microphone API, thus has built-in cross platform usability.
* Handles microphone initialization and device selection.
* Records audio clips into  .wav files trimmed to the actual spoken length.
* Stores files locally for processing, with automatic cleanup of old recordings.

#### Phoneme Conversion (PhonemeConverter.cs)

Converts target words into phoneme sequences for comparison.

* Uses the CMU Pronouncing Dictionary containing phonemes breakdowns of words.
* Translates ARPAbet phonemes into a simplified internal vocabulary.
* Outputs phoneme sequences in a format compatible with the scoring engine.

Example:

"cow" → "k aow"

#### Phoneme Scoring (PhonemeScoringEngine.cs)

This is the core logic for evaluating pronunciation accuracy. It compares spoken phonemes vs target phonemes and produces a similarity score (0–100%)

##### Scoring Components:
* Edit Distance Similarity
Token-based Levenshtein distance on phonemes.
* Vowel Similarity
Ensures vowel accuracy is weighted appropriately.
* Length Penalty
Penalizes overly short or long pronunciations.

##### Smart Matching:

Partial credit for similar sounds (e.g., b ↔ p, d ↔ t).
Grouped vowel similarity (e.g., ee, i).

#### Phoneme Extraction via ML (Wav2VecONNX.cs)

Uses a pretrained Wav2Vec2 ONNX model to convert audio into phoneme sequences.

##### Pipeline:
* Load recorded .wav file.
* Convert to mono audio.
* Resample to expected frequency.
* Normalize audio.
* Add slight silence padding (improves model stability).
* Run inference using Unity’s Inference Engine (GPU).

Output:
Sequence of phoneme tokens decoded using CTC (Connectionist Temporal Classification).

## CMU Pronouncing Dictionary (cmudict)

The CMU Pronouncing Dictionary is used as the foundation for converting words into phoneme sequences for scoring.

* A widely used pronunciation dictionary for North American English
* Maps words to their phonetic representations using **ARPAbet**
* Provides standardized phoneme breakdowns for thousands of words

* Supplies the **target phoneme sequence** for each prompted word
* Enables consistent comparison between:

  * Expected pronunciation (dictionary)
  * Actual pronunciation (model output)

### Processing Pipeline

1. A word is selected (e.g., *cow*)
2. The dictionary returns its phoneme sequence
3. ARPAbet phonemes are converted into a simplified internal format
4. Output is passed to the scoring engine

* Ensures linguistic consistency across all words
* Lightweight and fully offline
* Easily extendable with custom word entries if needed

## ONNX Model: Wav2Vec2 LJSpeech Gruut

This project uses a pretrained **Wav2Vec2-based ONNX model** for phoneme-level speech recognition.

### Overview

Wav2Vec2 LJSpeech Gruut is an automatic speech recognition model based on the **wav2vec 2.0 architecture**.

* Fine-tuned on the **LJSpeech Phoneme dataset**
* Designed to predict **phoneme sequences instead of words**
* Outputs phonemes using an **IPA-based vocabulary (gruut)**

**Example output:**

```
["h", "ɛ", "l", "ˈoʊ", "w", "ˈɚ", "l", "d"]
```

---

### ⚙️ Role in the App

* Converts recorded user speech into phoneme sequences
* Serves as the **bridge between raw audio and scoring logic**
* Enables pronunciation evaluation at a fine-grained phonetic level

---

### 🔄 Inference Pipeline

1. Input `.wav` audio from the recorder
2. Preprocessing:

   * Convert to mono
   * Resample to expected frequency
   * Normalize audio
   * Add silence padding
3. Run inference using Unity’s Inference Engine (GPU)
4. Decode output using **CTC (Connectionist Temporal Classification)**
5. Produce phoneme sequence for scoring

---

### (Model)[https://huggingface.co/ct-vikramanantha/phoneme-scorer-v2-wav2vec2] Details

* Architecture: **Wav2Vec2-Base**
* Framework: **PyTorch (HuggingFace)**
* Training Dataset: **LJSpeech Phonemes**
* Training Environment:

  * Google Cloud VM
  * NVIDIA Tesla A100 GPU
* Training metrics tracked via **TensorBoard**
https://huggingface.co/ct-vikramanantha/phoneme-scorer-v2-wav2vec2
---

* Optimized for **phoneme recognition instead of full transcription**
* Fully offline inference (no API calls required)
* ONNX format enables efficient deployment within Unity
* Works seamlessly with the custom phoneme scoring pipeline

---

## Project Structure

```
/frontend        # UI and user interaction
/backend         # Speech processing and scoring
/models          # ML / ONNX models
/assets          # Images, audio, penguin animations
```

---

