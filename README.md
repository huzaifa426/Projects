# QuestAITrainer — Offline VR AI Training Simulator

A **Meta Quest** virtual-reality training app powered entirely by **local, offline AI** —
no internet, no cloud, no API keys. You speak to an AI tutor in VR; your voice is
transcribed, reasoned over by a local LLM, and spoken back to you, all running on the PC.

The app features three role-play environments, each with a domain-locked AI persona and an
animated on-screen character, plus a gamified XP / levelling system.

---

## ✨ Features

- **Fully local voice pipeline** — Whisper (speech-to-text) → Llama 3 (reasoning) → Piper (text-to-speech)
- **Three training environments**, switchable in-app:
  - 🎓 **Classroom** — *Professor Claude*, a software-engineering tutor. Ask SE questions, or say
    **"start quiz"** to enter a graded quiz mode (bonus XP for correct answers).
  - 💪 **Gym** — *Coach Max*, a fitness coach who gives muscle-specific workouts with sets & reps.
  - 💼 **Office** — *Ms. Taylor*, an HR interviewer running behavioural mock interviews.
- **Animated AI character** with a speech bubble that shows live status (Listening… / Thinking…)
  and types out the reply while the voice plays.
- **Gamification** — XP, levelling, floating "+XP" popups and a LEVEL-UP banner.
- **Hand/controller VR interaction** built on Unity's XR Interaction Toolkit.

---

## 🏗 Architecture

```
[Quest headset]  --HTTP POST /upload (wav + environment)-->  [Node AI Server :3000]
                                                                |
                                     1. FFmpeg  → 16 kHz mono wav
                                     2. Whisper → transcription
                                     3. Llama 3 → reply (persona chosen by environment,
                                                  per-environment conversation memory)
                                     4. Piper   → speech audio
                                                                |
[Quest headset]  <--JSON { text, audio_base64, quizResult }-----
     plays audio, shows text in the character's speech bubble, awards XP
```

---

## 📁 Repository layout

| Path | What it is |
|------|-----------|
| `QuestAITrainer/` | The Unity 2022.3 (URP + XR) VR application |
| `QuestAITrainer/Assets/Scripts/` | Gameplay: `MicRecorder`, `GameStats`, `EnvironmentManager`, `AICharacterController`, HUD/XP scripts |
| `quest-ai-server/` | Node.js/Express server orchestrating the Whisper→Llama→Piper pipeline |
| `SETUP_AND_RUN.md` | Full setup, daily startup, demo checklist and troubleshooting guide |

---

## 🔧 External dependencies (not included in this repo)

These are large third-party binaries/models — install them separately and point the server
config (top of `quest-ai-server/server.js`) at their paths:

| Dependency | Notes |
|-----------|-------|
| [Ollama](https://ollama.com) + `llama3` | `ollama pull llama3` (runs the LLM on port 11434) |
| [whisper.cpp](https://github.com/ggerganov/whisper.cpp) | Build it, download `ggml-base.en.bin` model |
| [Piper](https://github.com/rhasspy/piper) + a voice (`en_US-amy-medium.onnx`) | Local text-to-speech |
| [FFmpeg](https://ffmpeg.org) | Audio format conversion |
| Node.js | `cd quest-ai-server && npm install` |

> The Unity project's build cache (`Library/`, `Temp/`, `Builds/`), `node_modules/`, and the
> `whisper.cpp/` clone are intentionally **git-ignored** — they are regenerated/installed locally.

---

## 🚀 Quick start

1. Install the dependencies above.
2. Start the AI brain and server on the PC:
   ```powershell
   ollama serve
   cd quest-ai-server && node server.js
   ```
3. Open `QuestAITrainer` in Unity, set **AISystem → MicRecorder → Server IP** to your PC's LAN IP,
   and build/deploy to the Quest (Android, IL2CPP, ARM64).
4. Put on the headset, hold the right trigger, and start talking.

See **[SETUP_AND_RUN.md](SETUP_AND_RUN.md)** for the complete guide, demo script, and troubleshooting.

---

## 🎓 About

Built as a Final Year Project — a demonstration of on-device, privacy-preserving conversational
AI in virtual reality, integrating speech recognition, a large language model, and speech
synthesis into an interactive VR experience.
