## Development Guide

### Build
```bash
dotnet build src/LisperFlow/LisperFlow.csproj
```

### Run
```bash
dotnet run --project src/LisperFlow/LisperFlow.csproj
```

### Debug
Open the workspace in Visual Studio or VS Code:
- `.vscode/launch.json` contains a default launch profile
- `.vscode/tasks.json` contains build tasks

### Logs
Logs are written to:
- `logs/lisperflow-*.log`

### Notes
This app is a WPF `WinExe` application targeting `net8.0-windows`.
