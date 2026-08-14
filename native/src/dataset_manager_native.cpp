#include "dataset_manager_native.h"

#include <fstream>
#include <iterator>
#include <vector>
#include <opencv2/core.hpp>
#include <opencv2/imgcodecs.hpp>

namespace
{
    cv::Mat read_image(const wchar_t* path)
    {
        std::ifstream stream(path, std::ios::binary);
        if (!stream) return {};
        std::vector<unsigned char> bytes(
            (std::istreambuf_iterator<char>(stream)),
            std::istreambuf_iterator<char>());
        return bytes.empty() ? cv::Mat{} : cv::imdecode(bytes, cv::IMREAD_UNCHANGED);
    }
}

const char* dm_get_version() noexcept
{
    return "DatasetManager Native 0.1 / OpenCV " CV_VERSION;
}

int dm_get_image_info(const wchar_t* path, DmImageInfo* result) noexcept
{
    if (path == nullptr || result == nullptr) return 1;
    try
    {
        const auto image = read_image(path);
        if (image.empty()) return 2;
        result->width = image.cols;
        result->height = image.rows;
        result->bit_depth = static_cast<int>(image.elemSize1() * 8);
        result->channels = image.channels();
        return 0;
    }
    catch (...)
    {
        return 3;
    }
}
