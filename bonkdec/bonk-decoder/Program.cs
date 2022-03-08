using System;
using System.Buffers;
using System.IO;
using NAudio.Wave;

var fileStream = new FileStream(@"C:\dev\zanzarah\Resources\Videos\_i001.bik", FileMode.Open, FileAccess.Read);
using var decoder = new Bonk.Decoder(fileStream);
decoder.ToggleAllAudioTracks(true);

var waveFormat = CreateWaveFormat(decoder.AudioTracks[0]);
using var waveWriter = new WaveFileWriter("out.wav", waveFormat);

while (decoder.MoveNext())
{
    var samples = decoder.Current.AudioSamples;
    var arr = ArrayPool<short>.Shared.Rent(samples.Length);
    samples.CopyTo(arr.AsSpan());
    waveWriter.WriteSamples(arr, 0, samples.Length);
    ArrayPool<short>.Shared.Return(arr);
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
