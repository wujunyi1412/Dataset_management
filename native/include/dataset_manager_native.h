#pragma once

#ifdef _WIN32
#  ifdef DATASET_MANAGER_NATIVE_EXPORTS
#    define DM_API __declspec(dllexport)
#  else
#    define DM_API __declspec(dllimport)
#  endif
#else
#  define DM_API
#endif

struct DmImageInfo
{
    int width;
    int height;
    int bit_depth;
    int channels;
};

extern "C"
{
    DM_API const char* dm_get_version() noexcept;
    DM_API int dm_get_image_info(const wchar_t* path, DmImageInfo* result) noexcept;
}
