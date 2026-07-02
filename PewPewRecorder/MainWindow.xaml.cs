using System.IO;
using System.Windows;
using System.Windows.Threading;
using Microsoft.Win32;
using PewPewRecorder.Models;
using PewPewRecorder.Services;

namespace PewPewRecorder;

public partial class MainWindow : Window
{
    private readonly FfmpegRecorder _recorder = new();
    private readonly DispatcherTimer _timer = new();
    private bool _isRecording;
    private DateTime _recordingStart;
    private string? _currentOutputPath;

    public MainWindow()
    {
        InitializeComponent();

        _timer.Interval = TimeSpan.FromSeconds(1);
        _timer.Tick += (_, _) => UpdateTimer();

        _recorder.StatusChanged += (_, msg) => Dispatcher.Invoke(() => StatusLabel.Text = msg);
        _recorder.Exited += (_, code) => Dispatcher.Invoke(() =>
        {
            if (!_isRecording) return;
            OnRecordingStopped(code != 0 ? $"Recording stopped (FFmpeg code {code})." : null);
        });

        Loaded += async (_, _) =>
        {
            try
            {
                await InitializeAsync();
            }
            catch (Exception ex)
            {
                StatusLabel.Text = ex.Message;
                MessageBox.Show(ex.Message, "Startup failed", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        };
        Closed += (_, _) =>
        {
            _timer.Stop();
            _recorder.Dispose();
        };
    }

    private async Task InitializeAsync()
    {
        foreach (var (label, _, _) in ResolutionPresets.Options)
            ResolutionCombo.Items.Add(label);
        ResolutionCombo.SelectedIndex = 0;

        foreach (var fps in FpsPresets.Options)
            FpsCombo.Items.Add($"{fps} fps");
        FpsCombo.SelectedIndex = 1;

        var videosDir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.MyVideos),
            "PewPewRecorder");
        Directory.CreateDirectory(videosDir);
        OutputPathBox.Text = videosDir;

        if (!FfmpegLocator.IsAvailable())
        {
            EncoderLabel.Text = "FFmpeg not found";
            EncoderLabel.Foreground = System.Windows.Media.Brushes.OrangeRed;
            StatusLabel.Text = "Place ffmpeg.exe in the ffmpeg\\ folder next to this app.";
            RecordButton.IsEnabled = false;
            return;
        }

        StatusLabel.Text = "Detecting encoder and audio devices…";

        var encoderTask = HardwareEncoderDetector.DetectAsync();
        var devicesTask = AudioDeviceEnumerator.ListDevicesAsync();
        await Task.WhenAll(encoderTask, devicesTask);

        var encoder = encoderTask.Result;
        EncoderLabel.Text = encoder.Label;

        var devices = devicesTask.Result;
        var loopback = devices.Where(d => d.IsLoopback).ToList();
        var inputs = devices.Where(d => !d.IsLoopback).ToList();

        foreach (var d in loopback)
            SystemAudioCombo.Items.Add(d);
        foreach (var d in inputs)
            MicCombo.Items.Add(d);

        if (SystemAudioCombo.Items.Count > 0)
            SystemAudioCombo.SelectedIndex = 0;
        if (MicCombo.Items.Count > 0)
            MicCombo.SelectedIndex = 0;

        if (SystemAudioCombo.Items.Count == 0 || MicCombo.Items.Count == 0)
        {
            StatusLabel.Text = "No audio devices found. Check Windows sound settings.";
            RecordButton.IsEnabled = false;
        }
        else
        {
            StatusLabel.Text = "Ready";
        }
    }

    private void BrowseButton_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new OpenFolderDialog
        {
            Title = "Choose output folder",
            InitialDirectory = OutputPathBox.Text,
        };

        if (dialog.ShowDialog() == true)
            OutputPathBox.Text = dialog.FolderName;
    }

    private async void RecordButton_Click(object sender, RoutedEventArgs e)
    {
        if (_recorder.IsRecording)
        {
            RecordButton.IsEnabled = false;
            await _recorder.StopAsync();
            return;
        }

        if (SystemAudioCombo.SelectedItem is not AudioDevice systemAudio ||
            MicCombo.SelectedItem is not AudioDevice mic)
        {
            MessageBox.Show("Select both a system audio and microphone device.", "PewPew Recorder",
                MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        var resIndex = ResolutionCombo.SelectedIndex;
        if (resIndex < 0) resIndex = 0;
        var (_, width, height) = ResolutionPresets.Options[resIndex];

        var fpsText = FpsCombo.SelectedItem?.ToString() ?? "30 fps";
        var fps = int.Parse(fpsText.Split(' ')[0]);

        var settings = new RecordingSettings
        {
            Width = width,
            Height = height,
            Fps = fps,
            OutputDirectory = OutputPathBox.Text,
            SystemAudioDeviceId = systemAudio.DeviceId,
            MicrophoneDeviceId = mic.DeviceId,
        };

        try
        {
            SetControlsEnabled(false);
            RecordButton.Content = "■ Stop Recording";
            RecordButton.Background = new System.Windows.Media.SolidColorBrush(
                System.Windows.Media.Color.FromRgb(0xF3, 0x8B, 0xA8));
            TimerLabel.Visibility = Visibility.Visible;

            var encoder = await HardwareEncoderDetector.DetectAsync();
            EncoderLabel.Text = encoder.Label;
            _currentOutputPath = await _recorder.StartAsync(settings, encoder);

            _isRecording = true;
            _recordingStart = DateTime.Now;
            _timer.Start();
            UpdateTimer();
        }
        catch (Exception ex)
        {
            MessageBox.Show(ex.Message, "Recording failed", MessageBoxButton.OK, MessageBoxImage.Error);
            OnRecordingStopped();
        }
    }

    private void OnRecordingStopped(string? statusOverride = null)
    {
        _isRecording = false;
        _timer.Stop();
        TimerLabel.Text = "00:00:00";
        TimerLabel.Visibility = Visibility.Collapsed;
        RecordButton.Content = "● Start Recording";
        RecordButton.Background = new System.Windows.Media.SolidColorBrush(
            System.Windows.Media.Color.FromRgb(0xA6, 0xE3, 0xA1));
        RecordButton.IsEnabled = true;
        SetControlsEnabled(true);

        if (statusOverride is not null)
            StatusLabel.Text = statusOverride;
        else if (_currentOutputPath is not null && File.Exists(_currentOutputPath))
            StatusLabel.Text = $"Saved: {_currentOutputPath}";
    }

    private void SetControlsEnabled(bool enabled)
    {
        ResolutionCombo.IsEnabled = enabled;
        FpsCombo.IsEnabled = enabled;
        BrowseButton.IsEnabled = enabled;
        SystemAudioCombo.IsEnabled = enabled;
        MicCombo.IsEnabled = enabled;
    }

    private void UpdateTimer()
    {
        if (!_isRecording) return;
        var elapsed = DateTime.Now - _recordingStart;
        TimerLabel.Text = elapsed.ToString(@"hh\:mm\:ss");
    }
}
