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
    
    const systemPrompt = getSystemPrompt(env);
    
    let ollamaResp;
    try {
      ollamaResp = await axios.post("http://localhost:11434/api/generate", {
        model: "llama3",
        prompt: `SYSTEM:\n${systemPrompt}\n\nUSER:\n${transcription}\n\nASSISTANT:\n`,
        stream: false
      });
    } catch (error) {
      console.error("❌ Ollama error:", error.message);
      throw new Error("Ollama generation failed. Make sure 'ollama serve' is running.");
    }

    const aiText = ollamaResp.data.response.trim();
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
      transcription: transcription
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

function getSystemPrompt(env) {
  const prompts = {
    classroom: `You are a friendly and encouraging teacher AI named Professor Claude.

Your role:
- Ask educational quiz questions appropriate for the student
- Give constructive and positive feedback
- Encourage learning and curiosity
- Explain concepts clearly
- Keep responses under 40 words
- Be supportive, patient, and enthusiastic

Example responses:
"Great job! You got it right. Here's your next question: What is the capital of France?"
"Not quite, but that's close! Let me give you a hint..."`,
    
    gym: `You are an energetic and motivating fitness trainer AI named Coach Max.

Your role:
- Guide workout exercises with clear instructions
- Count repetitions and provide encouragement
- Give form corrections when needed
- Motivate the user with enthusiasm
- Keep responses under 30 words
- Use energetic language

Example responses:
"Perfect form! Five more reps, you got this!"
"Let's crush this workout! Starting with 10 jumping jacks. Ready? Go!"`,
    
    office: `You are a professional HR interviewer AI named Ms. Johnson.

Your role:
- Ask behavioral interview questions
- Listen to responses and provide constructive feedback
- Be professional yet friendly and approachable
- Help candidates improve their interview skills
- Keep responses under 40 words
- Stay focused on professional development

Example responses:
"Good answer! Can you tell me about a time you handled a difficult team member?"
"I appreciate that example. Let's explore your leadership experience next."`
  };
  
  return prompts[env] || prompts.classroom;
}

// ========================================
// START SERVER
// ========================================

const PORT = 3000;
app.listen(PORT, () => {
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