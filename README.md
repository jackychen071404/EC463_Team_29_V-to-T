# Voice-to-Text Pronunciation Scoring System  
**BU EC463 Senior Design — Team 29**

---

## Introduction

This project is a **Unity-based speech therapy application** designed to evaluate how closely a user’s spoken word matches a target word.

The system captures audio input, processes it using a **phoneme-based speech recognition model (Wav2Vec)**, and generates a **numerical pronunciation score (0–100)**.

The goal is to provide immediate, structured, and motivating feedback for speech therapy applications.

---

## System Pipeline

**User Interface**  
→ **Audio Capture Module**  
→ **Speech Recognition** (Wav2Vec Phoneme Model)  
→ **Scoring Engine** (phoneme comparison + weighted metrics)  
→ **Feedback Display**

---

## Folder Structure

### Assets/
Core Unity application logic:
- Scenes  
- C# Scripts (UI, Recorder, Scoring Engine)  
- Audio resources and model integrations  

### Packages/
Unity package dependencies and manifest files.

### ProjectSettings/
Unity configuration files, including the required Unity version (`ProjectVersion.txt`).

---

## Installation

### Prerequisites
- Git  
- Unity Hub  
- Unity version listed in `ProjectSettings/ProjectVersion.txt`

### Clone the Project

```bash
git clone -b speech-therapy-app https://github.com/jackychen071404/EC463_Team_29_V-to-T.git
```

### Open in Unity

1. Open **Unity Hub**  
2. Click **Add project from disk**  
3. Select the cloned folder  
4. Install the required Unity version if prompted  
5. Open the main scene in `Assets/Scenes`  
6. Press **Play**
