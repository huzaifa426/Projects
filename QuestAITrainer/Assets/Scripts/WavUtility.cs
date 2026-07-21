using UnityEngine;
using System;
using System.IO;
public static class WavUtility
{
const int HEADER_SIZE = 44;
public static byte[] FromAudioClip(AudioClip clip)
{
MemoryStream stream = new MemoryStream();
int sampleCount = clip.samples * clip.channels;
// Write WAV header
WriteHeader(stream, clip);
// Write audio data
float[] samples = new float[sampleCount];
clip.GetData(samples, 0);
Int16[] intData = new Int16[sampleCount];
Byte[] bytesData = new Byte[sampleCount * 2];
int rescaleFactor = 32767; // to convert float to Int16
for (int i = 0; i < sampleCount; i++)
{
intData[i] = (short)(samples[i] * rescaleFactor);
Byte[] byteArr = BitConverter.GetBytes(intData[i]);
byteArr.CopyTo(bytesData, i * 2);
}
stream.Write(bytesData, 0, bytesData.Length);
return stream.ToArray();
}
static void WriteHeader(Stream stream, AudioClip clip)
{
int hz = clip.frequency;
int channels = clip.channels;
int samples = clip.samples;
stream.Seek(0, SeekOrigin.Begin);
Byte[] riff = System.Text.Encoding.UTF8.GetBytes("RIFF");
stream.Write(riff, 0, 4);
Byte[] chunkSize = BitConverter.GetBytes(stream.Length - 8);
stream.Write(chunkSize, 0, 4);
Byte[] wave = System.Text.Encoding.UTF8.GetBytes("WAVE");
stream.Write(wave, 0, 4);
Byte[] fmt = System.Text.Encoding.UTF8.GetBytes("fmt ");
stream.Write(fmt, 0, 4);
Byte[] subChunk1 = BitConverter.GetBytes(16);
stream.Write(subChunk1, 0, 4);
UInt16 one = 1;
Byte[] audioFormat = BitConverter.GetBytes(one);
stream.Write(audioFormat, 0, 2);
Byte[] numChannels = BitConverter.GetBytes(channels);
stream.Write(numChannels, 0, 2);
Byte[] sampleRate = BitConverter.GetBytes(hz);
stream.Write(sampleRate, 0, 4);
Byte[] byteRate = BitConverter.GetBytes(hz * channels * 2);
stream.Write(byteRate, 0, 4);
UInt16 blockAlign = (ushort)(channels * 2);
stream.Write(BitConverter.GetBytes(blockAlign), 0, 2);
UInt16 bps = 16;
Byte[] bitsPerSample = BitConverter.GetBytes(bps);
stream.Write(bitsPerSample, 0, 2);
Byte[] datastring = System.Text.Encoding.UTF8.GetBytes("data");
stream.Write(datastring, 0, 4);
Byte[] subChunk2 = BitConverter.GetBytes(samples * channels * 2);
stream.Write(subChunk2, 0, 4);
}
}
