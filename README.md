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

* Each category is mapped to a dedicated practice scene
* Selection is handled through a central CategorySelector.cs script

#### Example: Food Practice Scene

The FoodPractice scene, controlled by FoodSceneController.cs, demonstrates the core interaction loop of the app.

* Guided Interaction
A penguin character and a voice prompt prompts the user with a word (e.g., apple, pizza)
Visual states change depending on the current word and outcome (idle, eating, reacting)
* Audio Prompting
Each word has an associated audio clip
The app plays the pronunciation before enabling the microphone
Ensures users hear the correct pronunciation first
* Speech Input Flow
User taps the mic to begin recording
App waits until the microphone is ready
User speaks the prompted word
User taps again to stop recording
* Feedback & Scoring
Audio is sent to the backend for phoneme analysis
A score (0–100%) is returned
The app determines pass/fail using a threshold (default: 75%)
* Success Feedback
Positive message displayed
Penguin reacts (e.g., eating animation)
Sound effects or particles triggered (confetti, etc.)
Automatically advances to the next word
* Failure Feedback
Encourages retry
Plays corrective audio feedback
Keeps the same word active

### Backend

* Phoneme scoring engine (e.g. Wav2Vec2 / ONNX model)
* Audio processing pipeline
* Scoring API

---

## Project Structure

```
/frontend        # UI and user interaction
/backend         # Speech processing and scoring
/models          # ML / ONNX models
/assets          # Images, audio, penguin animations
```

---

