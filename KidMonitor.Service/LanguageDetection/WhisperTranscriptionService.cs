using KidMonitor.Core.Configuration;
using Microsoft.Extensions.Options;
using NAudio.Wave;
using System.Text;
using Whisper.net;
using Whisper.net.Ggml;

namespace KidMonitor.Service.LanguageDetection;

/// <summary>
/// Captures system audio (WASAPI loopback) for a fixed window and transcribes
/// it using Whisper.net (whisper.cpp wrapper).
///
/// Must run on a low-priority background thread; do not call from the hot path.
/// </summary>
public sealed class WhisperTranscriptionService : IAsyncDisposable
{
    // Whisper expects 16 kHz mono float32 PCM.
    private static readonly WaveFormat WhisperFormat = new(16_000, 16, 1);

    private readonly IOptionsMonitor<MonitoringOptions> _options;
    private readonly ILogger<WhisperTranscriptionService> _logger;

    private WhisperFactory? _factory;
    private string _loadedModelPath = string.Empty;
    private readonly SemaphoreSlim _modelLock = new(1, 1);

    public WhisperTranscriptionService(
        IOptionsMonitor<MonitoringOptions> options,
        ILogger<WhisperTranscriptionService> logger)
    {
        _options = options;
        _logger = logger;
    }

    /// <summary>
    /// Captures <paramref name="window"/> of system audio via WASAPI loopback,
    /// resamples to 16 kHz mono, and returns the Whisper transcript.
    /// Returns an empty string when the model is unavailable or audio is silent.
    /// </summary>
    public async Task<string> CaptureAndTranscribeAsync(TimeSpan window, CancellationToken ct)
    {
        var modelPath = _options.CurrentValue.LanguageDetection.ModelPath;

        var factory = await EnsureModelLoadedAsync(modelPath, ct);
        if (factory is null)
            return string.Empty;

        using var audioStream = await CaptureAudioAsync(window, ct);
        if (audioStream.Length < 1024) // likely silence / no audio device
            return string.Empty;

        audioStream.Position = 0;

        var transcript = new StringBuilder();

        using var processor = factory.CreateBuilder()
            .WithLanguage("auto")
            .Build();

        await foreach (var segment in processor.ProcessAsync(audioStream, ct))
        {
            transcript.Append(segment.Text).Append(' ');
        }

        return transcript.ToString().Trim();
    }

    // ── Model loading ──────────────────────────────────────────────────────────

    private async Task<WhisperFactory?> EnsureModelLoadedAsync(string modelPath, CancellationToken ct)
    {
        if (_factory is not null && _loadedModelPath == modelPath)
            return _factory;

        await _modelLock.WaitAsync(ct);
        try
        {
            if (_factory is not null && _loadedModelPath == modelPath)
                return _factory;

            if (!File.Exists(modelPath))
            {
                _logger.LogWarning(
                    "Whisper model not found at '{Path}'. " +
                    "Download a GGML model file (e.g. ggml-base.bin) to that path to enable audio transcription.",
                    modelPath);
                return null;
            }

            _logger.LogInformation("Loading Whisper model from '{Path}'.", modelPath);
            _factory?.Dispose();
            _factory = WhisperFactory.FromPath(modelPath);
            _loadedModelPath = modelPath;
            _logger.LogInformation("Whisper model loaded.");
            return _factory;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to load Whisper model from '{Path}'.", modelPath);
            return null;
        }
        finally
        {
            _modelLock.Release();
        }
    }

    // ── Audio capture ──────────────────────────────────────────────────────────

    /// <summary>
    /// Captures system audio output via WASAPI loopback for the given duration.
    /// Returns a MemoryStream containing a WAV file resampled to 16 kHz mono 16-bit
    /// suitable for passing directly to Whisper.net's ProcessAsync.
    /// </summary>
    private static async Task<MemoryStream> CaptureAudioAsync(TimeSpan window, CancellationToken ct)
    {
        using var capture = new WasapiLoopbackCapture();
        var rawBuffer = new MemoryStream();

        capture.DataAvailable += (_, e) =>
        {
            if (e.BytesRecorded > 0)
                rawBuffer.Write(e.Buffer, 0, e.BytesRecorded);
        };

        capture.StartRecording();
        try
        {
            await Task.Delay(window, ct);
        }
        finally
        {
            capture.StopRecording();
        }

        // Resample captured audio to 16 kHz mono and write as WAV.
        var output = new MemoryStream();
        if (rawBuffer.Length > 0)
        {
            rawBuffer.Position = 0;
            using var rawSource = new RawSourceWaveStream(rawBuffer, capture.WaveFormat);
            using var mfResampler = new MediaFoundationResampler(rawSource, WhisperFormat)
            {
                ResamplerQuality = 60,
            };
            WaveFileWriter.WriteWavFileToStream(output, mfResampler);
            output.Position = 0;
        }

        return output;
    }

    public async ValueTask DisposeAsync()
    {
        _factory?.Dispose();
        _modelLock.Dispose();
        await ValueTask.CompletedTask;
    }
}
