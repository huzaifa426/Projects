const { execSync } = require("child_process");

const cmd = '"C:/Projects/whisper.cpp/build/bin/Release/whisper-cli.exe" -m "C:/Projects/whisper.cpp/models/ggml-base.en.bin" -f "C:/Projects/Recording.wav"';

console.log("Running:", cmd);
const output = execSync(cmd, { encoding: 'utf8' });
console.log(output);