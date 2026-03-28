@echo off
setlocal

echo Running PipesPuzzle on connected Android device...
dotnet build PipesPuzzle.csproj -t:Run -f net10.0-android

endlocal
