dotnet publish MapEditorLauncher/MapEditorLauncher.csproj -c Release -p:PublishSingleFile=true -p:PublishTrimmed=false -p:IncludeNativeLibrariesForSelfExtract=true -p:CopyOutputSymbolsToPublishDirectory=false -p:DisableWinExeOutputInference=true -r win-x64 --self-contained=false