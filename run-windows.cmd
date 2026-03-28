@echo off
setlocal

echo Running PipesPuzzle on Windows...
dotnet build PipesPuzzle.csproj -t:Run -f net10.0-windows10.0.19041.0

endlocal
