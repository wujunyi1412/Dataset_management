using System.Runtime.InteropServices;
using DatasetManager.Core.Services;

namespace DatasetManager.Native;

public sealed class NativeImageService : IImageMetadataReader
{
    public string GetNativeVersion()
    {
        try
        {
            var pointer = NativeMethods.dm_get_version();
            return Marshal.PtrToStringUTF8(pointer) ?? "未知版本";
        }
        catch (DllNotFoundException)
        {
            return "OpenCV 组件尚未构建";
        }
        catch (BadImageFormatException)
        {
            return "OpenCV 组件位数不匹配";
        }
    }

    public bool TryGetImageInfo(string path, out ImageInfo info)
    {
        info = default;
        try
        {
            return NativeMethods.dm_get_image_info(path, out info) == 0;
        }
        catch (DllNotFoundException)
        {
            return false;
        }
    }

    public bool TryRead(string path, out ImageMetadata metadata)
    {
        metadata = default;
        if (!TryGetImageInfo(path, out var info)) return false;
        metadata = new ImageMetadata(info.Width, info.Height, info.BitDepth, info.Channels);
        return true;
    }

    private static class NativeMethods
    {
        private const string LibraryName = "DatasetManager.OpenCvNative.dll";

        [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl)]
        internal static extern IntPtr dm_get_version();

        [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Unicode)]
        internal static extern int dm_get_image_info(string path, out ImageInfo info);
    }
}

[StructLayout(LayoutKind.Sequential)]
public struct ImageInfo
{
    public int Width;
    public int Height;
    public int BitDepth;
    public int Channels;
}
