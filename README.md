# PewPew Recorder

Efficient Windows screen recorder with GPU capture and hardware H.264 encoding.

## Repository note

The GitHub repository intentionally does **not** store `PewPewRecorder/ffmpeg/ffmpeg.exe`.
That file is larger than GitHub's 100 MB file limit, so FFmpeg is downloaded locally by
`build.ps1` when needed and then copied into the published app output.

## Features

- **1080p** and **720p** resolution
- **24 / 30 / 60 fps** frame rates
- **System audio** + **microphone** mixed into one track
- Auto-detects best encoder: NVIDIA NVENC, AMD AMF, Intel QSV, or software x264
- Outputs MP4 (H.264 + AAC)

## Run the app

**Do not run `build.ps1` to launch the recorder** — that is only for building.

After building, run:

```text
PewPewRecorder\bin\Release\net8.0-windows\win-x64\publish\PewPewRecorder.exe
```

`build.ps1` also creates `Run Recorder.bat` in the same `publish` folder if you want a simple launcher shortcut.

## Build

Requires .NET 8 SDK.

```powershell
.\build.ps1
```

This downloads FFmpeg and publishes to:

```
PewPewRecorder\bin\Release\net8.0-windows\win-x64\publish\
```

The downloaded local FFmpeg binary is stored at:

```
PewPewRecorder\ffmpeg\ffmpeg.exe
```

That local file is ignored by Git and should not be committed to the repository.

## Usage

1. Pick resolution and frame rate
2. Choose output folder (defaults to `Videos\PewPewRecorder`)
3. Select system audio and microphone devices
4. Click **Start Recording** — click **Stop** when done

Recordings are saved as `Recording_yyyyMMdd_HHmmss.mp4`.

## Troubleshooting

- **CMD flashes and nothing happens**: You may be running `build.ps1` instead of `PewPewRecorder.exe`. Use the `publish` folder exe.
- **FFmpeg not found**: Re-run `.\build.ps1` so `publish\ffmpeg\ffmpeg.exe` exists.
- **GitHub rejects the push because of file size**: Make sure `PewPewRecorder/ffmpeg/ffmpeg.exe`, `bin/`, and `obj/` are not tracked by Git before pushing.
- **App crashes**: Check `%LOCALAPPDATA%\PewPewRecorder\error.log` for details.

## Requirements

- Windows 10 or later
- .NET 8 SDK for building
- FFmpeg is downloaded by `build.ps1` when needed
