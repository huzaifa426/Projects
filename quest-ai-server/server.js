const express = require("express");
const multer = require("multer");
const { execSync } = require("child_process");
const fs = require("fs");
const axios = require("axios");
const path = require("path");
const cors = require("cors");

const app = express();

// Configure multer
const storage = multer.diskStorage({
  destination: function (req, file, cb) {
    cb(null, 'uploads/')
  },
  filename: function (req, file, cb) {
    cb(null, Date.now() + '-' + Math.random().toString(36).substring(7) + '.raw')
  }
});

const upload = multer({ storage: storage });

app.use(cors());
app.use(express.json());

// ========================================
// CONFIGURATION - UPDATE THESE PATHS
// ========================================
const WHISPER_PATH = "C:/Projects/whisper.cpp";
const PIPER_PATH = "C:/Piper";
const PIPER_MODEL = "C:/Piper/models/en_US-amy-medium.onnx";
const FFMPEG_PATH = "C:/ffmpeg/bin"; // We'll install this

// ========================================
// HELPER FUNCTION: Convert audio to Whisper format
// ========================================
function convertToWhisperFormat(inputPath) {
  const outputPath = inputPath.replace('.raw', '.wav');
  
  try {
    // Use ffmpeg to convert to 16kHz, mono, 16-bit PCM WAV
    const ffmpegCmd = `"${FFMPEG_PATH}/ffmpeg.exe" -i "${inputPath}" -ar 16000 -ac 1 -sample_fmt s16 "${outputPath}" -y`;
    
    console.log("Converting audio format...");
    execSync(ffmpegCmd, { stdio: 'pipe' });
    console.log("✅ Audio converted successfully");
    
    return outputPath;
  } catch (error) {
    console.error("❌ FFmpeg conversion failed:", error.message);
    throw new Error("Audio conversion failed: " + error.message);
  }
}

// ========================================
// ENDPOINTS
// ========================================

// Health check
app.get("/", (req, res) => {
  res.json({ 
    status: "Server is running", 
    timestamp: new Date(),
    whisper: fs.existsSync(WHISPER_PATH),
    piper: fs.existsSync(PIPER_PATH),
    ffmpeg: fs.existsSync(FFMPEG_PATH)
  });
});

// Test endpoint
app.get("/test", (req, res) => {
  res.json({ message: "Backend is working!" });
});

// Reset conversation memory (call before a fresh demo)
app.post("/reset", (req, res) => {
  for (const key of Object.keys(conversationHistory)) {
    conversationHistory[key] = [];
  }
  classroomQuizMode = false;
  console.log("🔄 Conversation history + quiz mode reset");
  res.json({ message: "Conversation history cleared" });
});

// Main AI endpoint
app.post("/upload", upload.single("audio"), async (req, res) => {
  let convertedFilePath = null;
  
  try {
    const env = req.body.env || "classroom";
    const rawFilePath = req.file.path;
    
    console.log("\n" + "=".repeat(50));
    console.log("📥 NEW REQUEST");
    console.log("=".repeat(50));
    console.log("📁 Raw audio file:", rawFilePath);
    console.log("🎓 Environment:", env);

    // Convert audio to Whisper-compatible format
    convertedFilePath = convertToWhisperFormat(rawFilePath);
    console.log("📁 Converted file:", convertedFilePath);

    // 1. TRANSCRIBE WITH WHISPER
    console.log("\n🎤 Step 1: Transcribing audio...");
    
    const whisperCmd = `"${WHISPER_PATH}/build/bin/Release/whisper-cli.exe" -m "${WHISPER_PATH}/models/ggml-base.en.bin" -f "${convertedFilePath}"`;
    
    console.log("Running:", whisperCmd);
    
    let whisperOutput;
    try {
      whisperOutput = execSync(whisperCmd, { encoding: 'utf8', stdio: 'pipe' });
    } catch (error) {
      console.error("❌ Whisper execution error:", error.message);
      throw new Error("Whisper transcription failed: " + error.message);
    }
    
    // DEBUG: Print full whisper output
    console.log("\n--- FULL WHISPER OUTPUT ---");
    console.log(whisperOutput);
    console.log("--- END WHISPER OUTPUT ---\n");
    
    // Extract transcription
    let transcription = "";
    
    // Method 1: Look for lines with timestamps
    const timestampLines = whisperOutput.split('\n').filter(line => 
      line.includes('-->') && !line.includes('whisper_')
    );
    
    if (timestampLines.length > 0) {
      transcription = timestampLines
        .map(line => line.replace(/\[[\d:\.]+\s*-->\s*[\d:\.]+\]/g, '').trim())
        .filter(text => text.length > 0)
        .join(' ')
        .trim();
    }
    
    // Method 2: Look for lines with ]
    if (!transcription || transcription.length < 3) {
      const bracketLines = whisperOutput.split('\n').filter(line => 
        line.includes(']') && !line.includes('whisper_')
      );
      transcription = bracketLines
        .map(line => line.split(']').pop()?.trim())
        .filter(text => text && text.length > 0)
        .join(' ')
        .trim();
    }
    
    console.log("📝 Extracted Transcription:", transcription);
    console.log("📏 Transcription Length:", transcription.length);

    if (!transcription || transcription.length < 3) {
      throw new Error("No speech detected or transcription too short");
    }

    // 2. GENERATE AI RESPONSE WITH OLLAMA
    console.log("\n🤖 Step 2: Generating AI response...");
    
    updateQuizMode(env, transcription);
    const systemPrompt = getSystemPrompt(env);

    let ollamaResp;
    try {
      ollamaResp = await axios.post("http://localhost:11434/api/generate", {
        model: "llama3",
        prompt: buildConversationPrompt(env, systemPrompt, transcription),
        stream: false
      });
    } catch (error) {
      console.error("❌ Ollama error:", error.message);
      throw new Error("Ollama generation failed. Make sure 'ollama serve' is running.");
    }

    let aiText = ollamaResp.data.response.trim();

    // Quiz scoring: detect and strip the [CORRECT]/[INCORRECT] markers
    // (stripped BEFORE TTS so Piper doesn't read them aloud)
    let quizResult = "";
    if (aiText.includes("[CORRECT]")) {
      quizResult = "correct";
    } else if (aiText.includes("[INCORRECT]")) {
      quizResult = "incorrect";
    }
    aiText = aiText.replace(/\[CORRECT\]/g, "").replace(/\[INCORRECT\]/g, "").trim();

    // Remember this exchange so the AI keeps quiz/interview context
    rememberTurn(env, transcription, (quizResult ? `[${quizResult.toUpperCase()}] ` : "") + aiText);

    if (quizResult) console.log("🎯 Quiz result:", quizResult);
    console.log("💬 AI Response:", aiText.substring(0, 100) + (aiText.length > 100 ? "..." : ""));

    // 3. CONVERT TO SPEECH WITH PIPER
    console.log("\n🔊 Step 3: Converting to speech...");
    
    const inputTextPath = path.join(__dirname, "input.txt");
    const outputAudioPath = path.join(__dirname, "output.wav");
    
    fs.writeFileSync(inputTextPath, aiText);
    
    const piperCmd = `type "${inputTextPath}" | "${PIPER_PATH}/piper.exe" --model "${PIPER_MODEL}" --output_file "${outputAudioPath}"`;
    
    console.log("Running Piper...");
    
    try {
      execSync(piperCmd, { stdio: 'pipe' });
    } catch (error) {
      console.error("❌ Piper error:", error.message);
      throw new Error("Piper TTS failed: " + error.message);
    }

    const audioBase64 = fs.readFileSync(outputAudioPath).toString("base64");
    
    console.log("✅ Request completed successfully!");
    console.log("=".repeat(50) + "\n");

    res.json({
      text: aiText,
      audio_base64: audioBase64,
      transcription: transcription,
      quizResult: quizResult
    });

  } catch (err) {
    console.error("\n❌ ERROR:", err.message);
    console.error(err.stack);
    
    res.status(500).json({ 
      error: err.message,
      details: err.stack
    });
    
  } finally {
    // Cleanup
    try {
      if (req.file && req.file.path && fs.existsSync(req.file.path)) {
        fs.unlinkSync(req.file.path);
      }
      if (convertedFilePath && fs.existsSync(convertedFilePath)) {
        fs.unlinkSync(convertedFilePath);
      }
      const inputTextPath = path.join(__dirname, "input.txt");
      if (fs.existsSync(inputTextPath)) {
        fs.unlinkSync(inputTextPath);
      }
    } catch (cleanupErr) {
      console.error("Cleanup error:", cleanupErr.message);
    }
  }
});

// ========================================
// AI PROMPTS FOR DIFFERENT ENVIRONMENTS
// ========================================

const SYSTEM_PROMPTS = {
  // Classroom has two modes: tutor (default) and quiz (user says "start quiz").
  classroomTutor: `You are Professor Claude, a friendly software engineering teacher in a virtual classroom.
Your role:
- Answer the student's software engineering questions clearly and accurately
  (programming, OOP, data structures, algorithms, databases, testing, agile, web development, software design)
- If the question is not about software engineering, answer briefly then steer back to software engineering
- Do NOT quiz the student or ask them questions unless they ask for a quiz
- If they mention wanting a quiz or test, tell them: say "start quiz" to begin
- Keep responses under 50 words
- Be clear and encouraging`,

  classroomQuiz: `You are Professor Claude, a software engineering teacher running a quiz in a virtual classroom.
Strict rules:
- Ask ONE software engineering quiz question at a time (programming concepts, OOP, data structures,
  algorithms, databases, testing, agile, web development). NEVER general knowledge.
- When the student answers your question: if CORRECT start your response with "[CORRECT]",
  if wrong start with "[INCORRECT]". Then give one line of feedback and ask the next question.
- If the student just asked to start the quiz, greet briefly and ask the first question (no marker).
- If the student asks a question instead of answering, answer it briefly, then repeat your quiz question.
- Keep responses under 40 words.
Example: "[CORRECT] Right! A stack is LIFO. Next question: what does SQL stand for?"`,

  gym: `You are Coach Max, an energetic personal trainer in a virtual gym.
Strict rules:
- ONLY discuss fitness: workouts, exercises, muscle groups, form, sets and reps, stretching, basic nutrition, motivation
- When asked what to train or how to train a specific muscle (like "what should I do for back today"),
  give 3-4 specific exercises WITH sets and reps (example: "Deadlifts 3x8, Lat pulldowns 3x12...")
- Answer every exercise and fitness question directly and practically
- If asked about anything not fitness related, redirect to training in one short line
- Keep responses under 50 words
- Be high-energy like a real personal trainer`,

  office: `You are Ms. Taylor, a professional HR interviewer conducting a mock job interview in an office.
Strict rules:
- Ask ONE classic HR/behavioral interview question at a time (tell me about yourself, strengths,
  weaknesses, teamwork, handling conflict, career goals, why should we hire you, describe a challenge you overcame)
- ONLY HR/behavioral questions - never technical questions, never general knowledge
- After the candidate answers, give one line of constructive feedback on their answer, then ask the next HR question
- If the candidate greets you, welcome them warmly and ask the first question
- Keep responses under 50 words
- Be professional and friendly`
};

// ========================================
// QUIZ MODE (classroom only, keyword-switched)
// ========================================
// Deterministic server-side state: the LLM never decides when quiz
// mode starts/stops - the user's spoken command does.
let classroomQuizMode = false;

function updateQuizMode(env, transcription) {
  if (env !== "classroom") return;
  const t = transcription.toLowerCase();
  if (/\b(start|begin)\s+(the\s+)?quiz\b/.test(t) || /\bquiz\s+me\b/.test(t)) {
    classroomQuizMode = true;
    console.log("📝 Quiz mode: ON");
  } else if (/\b(stop|end|exit|finish)\s+(the\s+)?quiz\b/.test(t)) {
    classroomQuizMode = false;
    console.log("📝 Quiz mode: OFF");
  }
}

function getSystemPrompt(env) {
  if (env === "classroom") {
    return classroomQuizMode ? SYSTEM_PROMPTS.classroomQuiz : SYSTEM_PROMPTS.classroomTutor;
  }
  return SYSTEM_PROMPTS[env] || SYSTEM_PROMPTS.classroomTutor;
}

// ========================================
// CONVERSATION MEMORY (per environment)
// ========================================
// Keeps the last few exchanges so the classroom AI remembers
// which quiz question it asked when judging the answer.
const conversationHistory = { classroom: [], gym: [], office: [] };
const MAX_HISTORY_TURNS = 6; // user+assistant pairs kept

function buildConversationPrompt(env, systemPrompt, userMessage) {
  const history = conversationHistory[env] || [];
  let prompt = `SYSTEM:\n${systemPrompt}\n\n`;
  for (const turn of history) {
    prompt += `USER:\n${turn.user}\n\nASSISTANT:\n${turn.assistant}\n\n`;
  }
  prompt += `USER:\n${userMessage}\n\nASSISTANT:\n`;
  return prompt;
}

function rememberTurn(env, user, assistant) {
  if (!conversationHistory[env]) conversationHistory[env] = [];
  conversationHistory[env].push({ user, assistant });
  while (conversationHistory[env].length > MAX_HISTORY_TURNS) {
    conversationHistory[env].shift();
  }
}

// ========================================
// START SERVER
// ========================================

const PORT = 3000;
app.listen(PORT, '0.0.0.0', () => {
  console.log("\n" + "=".repeat(60));
  console.log("🚀 QUEST AI SERVER STARTED");
  console.log("=".repeat(60));
  console.log(`📡 Server URL: http://localhost:${PORT}`);
  console.log(`🌐 Network URL: http://YOUR_PC_IP:${PORT}`);
  console.log("\n📋 Configuration:");
  console.log(`   Whisper Path: ${WHISPER_PATH}`);
  console.log(`   Piper Path: ${PIPER_PATH}`);
  console.log(`   Piper Model: ${PIPER_MODEL}`);
  console.log(`   FFmpeg Path: ${FFMPEG_PATH}`);
  console.log("\n💡 Test endpoints:");
  console.log(`   GET  http://localhost:${PORT}/`);
  console.log(`   GET  http://localhost:${PORT}/test`);
  console.log(`   POST http://localhost:${PORT}/upload`);
  console.log("\n⚠️  Requirements:");
  console.log("   1. Ollama is running (ollama serve)");
  console.log("   2. FFmpeg is installed");
  console.log("   Check: http://localhost:11434");
  console.log("=".repeat(60) + "\n");
});