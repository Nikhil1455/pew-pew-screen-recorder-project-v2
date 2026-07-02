using System.IO.Pipes;
using NAudio.CoreAudioApi;
using NAudio.Wave;
using NAudio.Wave.SampleProviders;

namespace PewPewRecorder.Services;

public sealed class AudioPipeCapture : IDisposable
{
    public const int SampleRate = 48000;
    public const int Channels = 2;
    public const string PipeName = "PewPewRecorderAudio";

    private WasapiLoopbackCapture? _loopback;
    private WasapiCapture? _mic;
    private BufferedWaveProvider? _loopbackBuffer;
    private BufferedWaveProvider? _micBuffer;
    private CancellationTokenSource? _cts;
    private Task? _pumpTask;
    private bool _disposed;

    public void Start(string loopbackDeviceId, string micDeviceId)
    {
        Stop();

        _cts = new CancellationTokenSource();

        using var enumerator = new MMDeviceEnumerator();
        var loopbackDevice = enumerator.GetDevice(loopbackDeviceId);
        var micDevice = enumerator.GetDevice(micDeviceId);

        _loopback = new WasapiLoopbackCapture(loopbackDevice);
        _mic = new WasapiCapture(micDevice);

        _loopbackBuffer = CreateBuffer(_loopback.WaveFormat);
        _micBuffer = CreateBuffer(_mic.WaveFormat);

        _loopback.DataAvailable += (_, e) => _loopbackBuffer.AddSamples(e.Buffer, 0, e.BytesRecorded);
        _mic.DataAvailable += (_, e) => _micBuffer.AddSamples(e.Buffer, 0, e.BytesRecorded);

        var loopbackSource = ToStereo(Resample(new WaveToSampleProvider(_loopbackBuffer), SampleRate));
        var micSource = ToStereo(Resample(new WaveToSampleProvider(_micBuffer), SampleRate));

        var mixer = new MixingSampleProvider([loopbackSource, micSource])
        {
            ReadFully = true,
        };

        var pipeServer = new NamedPipeServerStream(
            PipeName,
            PipeDirection.Out,
            1,
            PipeTransmissionMode.Byte,
            PipeOptions.Asynchronous);

        _pumpTask = Task.Run(async () =>
        {
            await pipeServer.WaitForConnectionAsync(_cts.Token);

            var pcm = new SampleToWaveProvider16(mixer);
            await using var stream = pipeServer;

            try
            {
                var buffer = new byte[pcm.WaveFormat.AverageBytesPerSecond / 10];
                while (!_cts.Token.IsCancellationRequested)
                {
                    var read = pcm.Read(buffer, 0, buffer.Length);
                    if (read == 0)
                    {
                        await Task.Delay(5, _cts.Token);
                        continue;
                    }

                    await stream.WriteAsync(buffer.AsMemory(0, read), _cts.Token);
                }
            }
            catch (OperationCanceledException)
            {
                // Expected on stop.
            }
        }, _cts.Token);

        _loopback.StartRecording();
        _mic.StartRecording();
    }

    public void Stop()
    {
        _cts?.Cancel();

        try { _pumpTask?.Wait(TimeSpan.FromSeconds(2)); } catch { }

        _loopback?.StopRecording();
        _mic?.StopRecording();
        _loopback?.Dispose();
        _mic?.Dispose();

        _loopback = null;
        _mic = null;
        _pumpTask = null;
        _cts?.Dispose();
        _cts = null;
    }

    private static BufferedWaveProvider CreateBuffer(WaveFormat format) =>
        new(format) { DiscardOnBufferOverflow = true, BufferDuration = TimeSpan.FromSeconds(2) };

    private static ISampleProvider Resample(ISampleProvider source, int targetRate) =>
        source.WaveFormat.SampleRate == targetRate
            ? source
            : new WdlResamplingSampleProvider(source, targetRate);

    private static ISampleProvider ToStereo(ISampleProvider source) =>
        source.WaveFormat.Channels == 2
            ? source
            : new MonoToStereoSampleProvider(source);

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        Stop();
    }
}
