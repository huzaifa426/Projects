const { execSync } = require("child_process");

// Your audio file
const input = "C:/Projects/test.wav";
const output = "C:/Projects/test-converted.wav";

const cmd = `"C:/ffmpeg/bin/ffmpeg.exe" -i "${input}" -ar 16000 -ac 1 -sample_fmt s16 "${output}" -y`;

console.log("Converting audio...");
execSync(cmd);
console.log("Done! Check:", output);

// Now test with Whisper
const whisperCmd = `"C:/Projects/whisper.cpp/build/bin/Release/whisper-cli.exe" -m "C:/Projects/whisper.cpp/models/ggml-base.en.bin" -f "${output}"`;

console.log("\nTesting with Whisper...");
const result = execSync(whisperCmd, { encoding: 'utf8' });
console.log(result);