@echo off
setlocal
cd /d "%~dp0"

set "OPENCV_DIR=D:\opencv-4.5.3\opencv-4.5.3\build\install"
set "OUTPUT_DIR=%~dp0bin\Standalone"
set "VSWHERE=%ProgramFiles(x86)%\Microsoft Visual Studio\Installer\vswhere.exe"

echo [1/6] Checking build tools...
where cmake >nul 2>nul
if errorlevel 1 goto :cmake_missing
where dotnet >nul 2>nul
if errorlevel 1 goto :dotnet_missing
if not exist "%OPENCV_DIR%\include" goto :opencv_missing
if not exist "%OPENCV_DIR%\bin\opencv_core453.dll" goto :opencv_missing
if not exist "%OPENCV_DIR%\bin\opencv_imgcodecs453.dll" goto :opencv_missing
if not exist "%OPENCV_DIR%\bin\opencv_imgproc453.dll" goto :opencv_missing
if not exist "%VSWHERE%" goto :redist_missing
set "VS_RESULT=%TEMP%\dataset-manager-vs-install.txt"
"%VSWHERE%" -latest -products * -requires Microsoft.VisualStudio.Component.VC.Tools.x86.x64 -property installationPath > "%VS_RESULT%"
if errorlevel 1 goto :redist_missing
set /p VS_INSTALL=<"%VS_RESULT%"
del /q "%VS_RESULT%" >nul 2>nul
if not defined VS_INSTALL goto :redist_missing
for /d %%D in ("%VS_INSTALL%\VC\Redist\MSVC\*") do if exist "%%~fD\x64\Microsoft.VC143.CRT\vcruntime140.dll" set "VC_REDIST_DIR=%%~fD\x64\Microsoft.VC143.CRT"
if not defined VC_REDIST_DIR goto :redist_missing
if not exist "%VC_REDIST_DIR%\vcruntime140.dll" goto :redist_missing

echo [2/6] Configuring native x64 Release build...
cmake -S . -B .\build -G "Visual Studio 17 2022" -A x64 -DOPENCV_INSTALL_DIR="%OPENCV_DIR%"
if errorlevel 1 goto :failed

echo [3/6] Building C++ and WPF projects...
cmake --build .\build --config Release
if errorlevel 1 goto :failed

echo [4/6] Publishing self-contained Windows x64 application...
if exist "%OUTPUT_DIR%" rmdir /s /q "%OUTPUT_DIR%"
dotnet publish .\src\DatasetManager.App\DatasetManager.App.csproj --configuration Release --runtime win-x64 --self-contained true --output "%OUTPUT_DIR%" -p:DebugType=None -p:DebugSymbols=false
if errorlevel 1 goto :failed

echo [5/6] Copying C++ and OpenCV runtime DLLs...
copy /y ".\bin\Release\DatasetManager.OpenCvNative.dll" "%OUTPUT_DIR%\" >nul
if errorlevel 1 goto :failed
copy /y "%OPENCV_DIR%\bin\opencv_core453.dll" "%OUTPUT_DIR%\" >nul
if errorlevel 1 goto :failed
copy /y "%OPENCV_DIR%\bin\opencv_imgcodecs453.dll" "%OUTPUT_DIR%\" >nul
if errorlevel 1 goto :failed
copy /y "%OPENCV_DIR%\bin\opencv_imgproc453.dll" "%OUTPUT_DIR%\" >nul
if errorlevel 1 goto :failed

echo [6/6] Copying Visual C++ x64 runtime DLLs...
copy /y "%VC_REDIST_DIR%\*.dll" "%OUTPUT_DIR%\" >nul
if errorlevel 1 goto :failed

echo.
echo Standalone publish succeeded.
echo Copy the entire folder to another Windows x64 computer:
echo %OUTPUT_DIR%
echo Application: %OUTPUT_DIR%\DatasetManager.App.exe
echo.
pause
exit /b 0

:cmake_missing
echo ERROR: CMake was not found. Install CMake and add it to PATH.
goto :failed

:dotnet_missing
echo ERROR: dotnet was not found. Install the .NET 8 SDK on this build computer.
goto :failed

:opencv_missing
echo ERROR: Required OpenCV 4.5.3 files were not found:
echo %OPENCV_DIR%
goto :failed

:redist_missing
echo ERROR: Visual C++ x64 redistributable DLLs were not found.
echo Repair the Visual Studio 2022 Desktop development with C++ workload.
goto :failed

:failed
echo.
echo Standalone publish failed. Review the error message above.
pause
exit /b 1
