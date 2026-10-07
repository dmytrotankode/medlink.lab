#!/usr/bin/env bash
cd "$(dirname "$0")"
echo "=== MedLink LIS: запуск API та інтерфейсу на http://localhost:5055 ==="
dotnet run --project src/MedLink.LIS.Api --urls http://0.0.0.0:5055
