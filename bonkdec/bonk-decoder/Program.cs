using System;
using System.Buffers;
using System.IO;
using NAudio.Wave;

var fileStream = new FileStream(@"C:\dev\zanzarah\Resources\Videos\_i001c.bik", FileMode.Open, FileAccess.Read);
using var decoder = new Bonk.Decoder(fileStream);
decoder.ToggleAllAudioTracks(true);

var waveFormat = CreateWaveFormat(decoder.AudioTracks[0]);
using var waveWriter = new WaveFileWriter("out.wav", waveFormat);
Directory.CreateDirectory("out");

int i = 0;
while (decoder.MoveNext())
{
    var samples = decoder.Current.AudioSamples;
    var arr = ArrayPool<short>.Shared.Rent(samples.Length);
    samples.CopyTo(arr.AsSpan());
    waveWriter.WriteSamples(arr, 0, samples.Length);
    ArrayPool<short>.Shared.Return(arr);

    using var bw = new BinaryWriter(new FileStream($"out/my{i++}.pgm", FileMode.Create, FileAccess.Write));
    bw.Write(System.Text.Encoding.ASCII.GetBytes($"P5\n{decoder.FrameWidth} {decoder.FrameHeight}\n255\n"));
    bw.Write(decoder.Current.YPlane);
}

WaveFormat CreateWaveFormat(Bonk.AudioTrackInfo audioTrackInfo)
{
    int channels = (int)audioTrackInfo.ChannelCount;
    int sampleRate = (int)audioTrackInfo.Frequency;
    int bitsPerSample = 16;

    int blockAlign = (channels * (bitsPerSample / 8));
    int averageBytesPerSecond = sampleRate * blockAlign;
    return WaveFormat.CreateCustomFormat(WaveFormatEncoding.Pcm, sampleRate, channels, averageBytesPerSecond, blockAlign, bitsPerSample);
}
