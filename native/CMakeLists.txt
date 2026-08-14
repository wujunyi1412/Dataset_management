cmake_minimum_required(VERSION 3.20)

# 也允许 native 目录作为独立 CMake 工程使用。
if(CMAKE_SOURCE_DIR STREQUAL CMAKE_CURRENT_SOURCE_DIR)
    project(DatasetManagerNative VERSION 0.1.0 LANGUAGES CXX)
endif()

set(CMAKE_CXX_STANDARD 17)
set(CMAKE_CXX_STANDARD_REQUIRED ON)

set(OPENCV_INSTALL_DIR "D:/opencv-4.5.3/opencv-4.5.3/build/install" CACHE PATH "OpenCV install directory")

# 当前 OpenCV 是裁剪过的 Release 安装，OpenCVModules.cmake 中仍引用了未安装的
# dnn 等模块，直接 find_package(OpenCV) 会失败。只查找本项目实际使用的三个库。
find_library(OPENCV_CORE_LIBRARY NAMES opencv_core453
    PATHS "${OPENCV_INSTALL_DIR}/lib" NO_DEFAULT_PATH REQUIRED)
find_library(OPENCV_IMGCODECS_LIBRARY NAMES opencv_imgcodecs453
    PATHS "${OPENCV_INSTALL_DIR}/lib" NO_DEFAULT_PATH REQUIRED)
find_library(OPENCV_IMGPROC_LIBRARY NAMES opencv_imgproc453
    PATHS "${OPENCV_INSTALL_DIR}/lib" NO_DEFAULT_PATH REQUIRED)

add_library(DatasetManager.OpenCvNative SHARED
    src/dataset_manager_native.cpp
    include/dataset_manager_native.h)

target_compile_definitions(DatasetManager.OpenCvNative PRIVATE DATASET_MANAGER_NATIVE_EXPORTS)
target_include_directories(DatasetManager.OpenCvNative
    PUBLIC include
    PRIVATE "${OPENCV_INSTALL_DIR}/include")
target_link_libraries(DatasetManager.OpenCvNative PRIVATE
    "${OPENCV_CORE_LIBRARY}"
    "${OPENCV_IMGCODECS_LIBRARY}"
    "${OPENCV_IMGPROC_LIBRARY}")

if(MSVC)
    target_compile_options(DatasetManager.OpenCvNative PRIVATE /W4 /utf-8)
endif()

# 根工程会指定统一输出目录；独立构建 native 时仍复制到 WPF 默认输出目录。
if(DEFINED DATASET_MANAGER_OUTPUT_DIR)
    set(WPF_NATIVE_DIR "${DATASET_MANAGER_OUTPUT_DIR}")
else()
    set(WPF_NATIVE_DIR "${CMAKE_CURRENT_SOURCE_DIR}/../src/DatasetManager.App/bin/$<CONFIG>/net8.0-windows")
endif()
add_custom_command(TARGET DatasetManager.OpenCvNative POST_BUILD
    COMMAND ${CMAKE_COMMAND} -E make_directory "${WPF_NATIVE_DIR}"
    COMMAND ${CMAKE_COMMAND} -E copy_if_different "$<TARGET_FILE:DatasetManager.OpenCvNative>" "${WPF_NATIVE_DIR}/"
    COMMAND ${CMAKE_COMMAND} -E copy_if_different
        "${OPENCV_INSTALL_DIR}/bin/opencv_core453.dll"
        "${OPENCV_INSTALL_DIR}/bin/opencv_imgcodecs453.dll"
        "${OPENCV_INSTALL_DIR}/bin/opencv_imgproc453.dll"
        "${WPF_NATIVE_DIR}/")
