@echo off
chcp 65001 > nul
echo === MedLink LIS: запуск API та інтерфейсу на http://localhost:5055 ===
dotnet run --project "%~dp0src\MedLink.LIS.Api" --urls http://0.0.0.0:5055
