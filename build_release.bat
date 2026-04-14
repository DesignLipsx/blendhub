@echo off
setlocal

set PROJECT_PATH=%~dp0BlendHub
set OUTPUT_PATH=%PROJECT_PATH%\bin\Release\net8.0-windows10.0.19041.0\win-x64\publish

echo ==========================================
echo    Clean & Build BlendHub Production
echo ==========================================

echo [1/4] Cleaning old build artifacts...
if exist "%PROJECT_PATH%\bin" rd /s /q "%PROJECT_PATH%\bin"
if exist "%PROJECT_PATH%\obj" rd /s /q "%PROJECT_PATH%\obj"

echo [2/4] Publishing project (win-x64)...
dotnet publish "%PROJECT_PATH%\BlendHub.csproj" -c Release -r win-x64 --self-contained true -p:WindowsAppSDKSelfContained=true -p:PublishReadyToRun=false
if %errorlevel% neq 0 (
    echo.
    echo ERROR: Build failed! Check the errors above.
    pause
    exit /b %errorlevel%
)

echo [3/4] Synchronizing assets...
if not exist "%OUTPUT_PATH%\Assets" mkdir "%OUTPUT_PATH%\Assets"
xcopy "%PROJECT_PATH%\Assets" "%OUTPUT_PATH%\Assets" /E /I /Y /Q

echo [4/4] Opening production folder...
explorer "%OUTPUT_PATH%"

echo.
echo ==========================================
echo    Success! Double-click BlendHub.exe.
echo ==========================================
pause
