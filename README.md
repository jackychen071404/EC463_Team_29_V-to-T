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

🧭 Category Selection

The app begins with a category selection screen where users choose what type of words they want to practice (e.g., Food, Colors, Numbers).

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

