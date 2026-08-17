@echo off
setlocal
cd /d "%~dp0"

set "OPENCV_DIR=D:\opencv-4.5.3\opencv-4.5.3\build\install"

echo [1/3] Checking build tools...
where cmake >nul 2>nul
if errorlevel 1 goto :cmake_missing
where dotnet >nul 2>nul
if errorlevel 1 goto :dotnet_missing
if not exist "%OPENCV_DIR%\include" goto :opencv_missing
if not exist "%OPENCV_DIR%\bin\opencv_core453.dll" goto :opencv_missing

echo [2/3] Configuring Visual Studio 2022 x64 build...
cmake -S . -B .\build -G "Visual Studio 17 2022" -A x64 -DOPENCV_INSTALL_DIR="%OPENCV_DIR%"
if errorlevel 1 goto :failed

echo [3/3] Building Release...
cmake --build .\build --config Release
if errorlevel 1 goto :failed

echo.
echo Build succeeded.
echo Application: %~dp0bin\Release\DatasetManager.App.exe
echo.
pause
exit /b 0

:cmake_missing
echo ERROR: CMake was not found. Install CMake and add it to PATH.
goto :failed

:dotnet_missing
echo ERROR: dotnet was not found. Install the .NET 8 SDK.
goto :failed

:opencv_missing
echo ERROR: OpenCV 4.5.3 Release files were not found:
echo %OPENCV_DIR%
goto :failed

:failed
echo.
echo Build failed. Review the error message above.
pause
exit /b 1
