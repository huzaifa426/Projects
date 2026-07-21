# QuestAITrainer — Setup & Run Guide

A local, offline VR AI training app. Voice in → Whisper transcribes → Llama 3 responds
as one of three personas → Piper speaks the reply. Includes XP/leveling with floating
XP popups and a LEVEL UP banner, three switchable environments (classroom / gym / office),
quiz scoring in the classroom, and an animated **AI tutor character** with a speech bubble
that shows what the AI is doing (Listening… / Thinking…) and types out its replies.

The character re-skins per environment: blue **Professor Claude** (classroom), red
**Coach Max** (gym), gray **Ms. Taylor** (office). The HUD floats in front of you and
follows your gaze; at startup the whole room aligns to whichever direction you're facing.

**Everything runs locally on the PC — no internet, no cloud, no API keys.**

---

## 1. Components & Verified Paths

| Component | Location | Purpose |
|-----------|----------|---------|
| AI Server | `C:\Projects\quest-ai-server\server.js` | Orchestrates the pipeline (port 3000) |
| Unity project | `C:\Projects\QuestAITrainer` | The VR app (scene: `Assets/Scenes/MainScene.unity`) |
| Whisper | `C:\Projects\whisper.cpp\build\bin\Release\whisper-cli.exe` | Speech → text |
| Whisper model | `C:\Projects\whisper.cpp\models\ggml-base.en.bin` | English model |
| Ollama | `%LOCALAPPDATA%\Programs\Ollama\ollama.exe` | Runs Llama 3 (port 11434) |
| Piper | `C:\Piper\piper.exe` | Text → speech |
| Piper voice | `C:\Piper\models\en_US-amy-medium.onnx` | Female US voice |
| FFmpeg | `C:\ffmpeg\bin\ffmpeg.exe` | Audio format conversion |

All of the above are confirmed present on this machine. If you move any of them,
update the path constants at the top of `server.js`.

---

## 2. One-Time Setup (already done — for reference / new machines)

1. Install Node deps:
   ```powershell
   cd C:\Projects\quest-ai-server
   npm install
   ```
2. Pull the Llama 3 model (once):
   ```powershell
   ollama pull llama3
   ```
3. In Unity, open `QuestAITrainer`, load `Assets/Scenes/MainScene.unity`.
4. On the **AISystem** GameObject → **MicRecorder** component, set **Server IP** to your
   PC's LAN IP (currently `192.168.1.10`). See §6 to find/verify it.

---

## 3. Daily Startup Sequence (PC side)

Open **three** terminals (or use the batch file in §3.1). Order matters.

**Terminal 1 — Ollama** (the AI brain):
```powershell
ollama serve
```
Leave it running. First response after boot is slow (~30–60 s) while the model loads
into RAM; every response after that is faster.

**Terminal 2 — AI Server**:
```powershell
cd C:\Projects\quest-ai-server
node server.js
```
You should see `🚀 QUEST AI SERVER STARTED`. Verify the health check in a browser or curl:
```
http://localhost:3000/          → shows whisper/piper/ffmpeg all true
```

**Terminal 3 (optional) — reset before a demo**:
```powershell
curl -X POST http://localhost:3000/reset
```
Clears the AI's conversation memory so the quiz/interview starts fresh.

### 3.1 Optional one-click starter

Save this as `C:\Projects\start-servers.bat` and double-click it:
```bat
@echo off
start "Ollama" cmd /k ollama serve
timeout /t 3 >nul
start "AI Server" cmd /k "cd /d C:\Projects\quest-ai-server && node server.js"
echo Both servers launching in separate windows...
```

---

## 4. Running in the Headset (Meta Quest)

The PC and the Quest **must be on the same Wi-Fi network**.

**Option A — Play in Unity (fastest for testing):**
Press ▶ Play in the Unity Editor with a Quest Link cable connected.

**Option B — Build & deploy to the Quest (standalone demo):**
1. `File → Build Settings → Android` → **Switch Platform** (if not already).
2. Connect the Quest via USB, allow USB debugging on the headset.
3. **Build And Run** (or Build an `.apk` and install with `adb install`).
4. Put on the headset. Grant the **microphone** permission when prompted.

---

## 5. Demo Flow / Feature Checklist

Once in the app you should be able to:

- [ ] App launches in the **Classroom**, room aligned to where you're facing; **Professor Claude** (blue robot) stands ahead with a speech bubble.
- [ ] The HUD (stats top-right with XP bar, status line bottom, buttons) floats in front of you and follows your gaze.
- [ ] **Hold the RIGHT TRIGGER** to talk — the character leans in, bubble shows **"🎤 Listening..."**.
- [ ] Release → bubble shows **"💭 Thinking..."**, then the reply **types out** in the bubble while the AI speaks and its mouth animates.
- [ ] A **"+5 XP"** popup floats up after each interaction; the XP bar fills.
- [ ] Correct classroom answer → gold **"+20 XP"** popup and correct count rises.
- [ ] Enough XP → orange **"LEVEL 2!"** banner.
- [ ] Point the controller ray at **SWITCH MODE** and pull trigger → Gym: red **Coach Max**, new room.
- [ ] Switch again → Office: gray **Ms. Taylor** behind the interview desk.
- [ ] **RESET** button zeroes stats for a clean demo.

**2-minute pitch to evaluators:**
1. "Fully local AI training system — no internet, no cloud, no API keys."
2. Show classroom; answer a question correctly → point out the XP jump.
3. "Whisper for speech recognition, Llama 3 for reasoning, Piper for speech — all on the PC."
4. SWITCH MODE → gym coach → SWITCH again → HR interviewer.
5. Show the PC server console printing the 3 processing steps live.
6. "Modular — a new environment is just a new prompt."

---

## 6. Finding / Fixing your PC IP

If the Quest can't reach the server ("ERROR: Cannot reach server – Check IP!"):

1. Find your PC's LAN IP:
   ```powershell
   ipconfig | findstr /i "IPv4"
   ```
2. In Unity, **AISystem → MicRecorder → Server IP**, set it to that address.
3. Make sure Windows Firewall allows Node.js on a **Private** network (allow it when
   first prompted, or add an inbound rule for TCP port 3000).

---

## 7. Troubleshooting

| Symptom | Cause / Fix |
|---------|-------------|
| Browser at `127.0.0.1:8080/mcp` shows `Not Acceptable: Client must accept text/event-stream` | **Not an error.** That's the Unity MCP editor server; a plain browser GET can't speak the protocol. Ignore it. It only matters to MCP tooling, not the app. |
| First AI reply takes 30–60 s | Llama 3 cold-loading into RAM. Normal. Send one throwaway request before demoing to warm it up. |
| "Ollama generation failed" | `ollama serve` isn't running, or `llama3` isn't pulled. Run `ollama pull llama3`. |
| "No speech detected" | Mic permission denied in headset, or you released the trigger too fast. Hold it while speaking. |
| Whisper/Piper/ffmpeg = false on health check | A path moved — fix the constants at the top of `server.js`. |
| Environment room is empty / invisible | The three env objects are positioned relative to the XR rig. If you move the rig, re-run the scene setup or reposition `ClassroomEnv/GymEnv/OfficeEnv`. |
| Quiz never awards bonus XP | Only the **classroom** persona emits `[CORRECT]`/`[INCORRECT]` markers; gym/office intentionally don't. Make sure you're in classroom mode. |

---

## 8. How the Pieces Talk (for your report)

```
[Quest headset]  --HTTP POST /upload (wav + env)-->  [Node AI Server :3000]
                                                        |
                             1. FFmpeg → 16kHz mono wav |
                             2. Whisper → transcription |
                             3. Ollama/Llama3 → reply   |  (persona chosen by `env`,
                                + quiz marker detection  |   conversation history kept
                             4. Piper → speech wav       |   per environment)
                                                        |
[Quest headset]  <--JSON (text, audio_base64, quizResult)--
   plays audio, shows text, awards XP
```

The personas live in `SYSTEM_PROMPTS` in `server.js` — each is locked to its domain:
- **classroom** — Professor Claude, **software engineering tutor**. Default mode answers
  your SE questions (OOP, data structures, databases, testing, agile...). Say **"start quiz"**
  to enter quiz mode (SE questions only, judged with `[CORRECT]`/`[INCORRECT]` for bonus XP);
  say **"stop quiz"** to go back to tutoring. Quiz mode is a server-side switch
  (`classroomQuizMode`) triggered by your spoken command — the AI never starts quizzing on its own.
- **gym** — Coach Max, fitness only. Ask "what should I do for back today?" and you get
  concrete exercises with sets and reps for that muscle group.
- **office** — Ms. Taylor, HR/behavioral interview questions only (strengths, teamwork,
  conflict...), with one line of feedback after each of your answers.

Adding a 4th environment = add a prompt to that dict + a room object + one array entry
in `EnvironmentManager.cs`. Nothing else changes.
