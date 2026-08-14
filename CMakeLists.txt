cmake_minimum_required(VERSION 3.20)
project(DatasetManager VERSION 0.1.0 LANGUAGES CXX)

find_program(DOTNET_EXECUTABLE dotnet REQUIRED)

# 所有可运行文件集中到仓库根目录的 bin/<配置>。
set(DATASET_MANAGER_OUTPUT_DIR
    "${CMAKE_CURRENT_SOURCE_DIR}/bin/$<CONFIG>")

add_subdirectory(native)

set(WPF_PROJECT
    "${CMAKE_CURRENT_SOURCE_DIR}/src/DatasetManager.App/DatasetManager.App.csproj")

# WPF/C# 仍使用官方 .NET SDK 编译，CMake 作为整个仓库的统一构建入口。
add_custom_target(DatasetManager.Wpf ALL
    COMMAND "${DOTNET_EXECUTABLE}" build "${WPF_PROJECT}"
        --configuration "$<CONFIG>"
        --output "${DATASET_MANAGER_OUTPUT_DIR}"
    WORKING_DIRECTORY "${CMAKE_CURRENT_SOURCE_DIR}"
    COMMENT "Building DatasetManager WPF application"
    VERBATIM)

# 先构建并复制原生 DLL，再构建 WPF。dotnet 增量构建不会清理这些原生文件。
add_dependencies(DatasetManager.Wpf DatasetManager.OpenCvNative)
