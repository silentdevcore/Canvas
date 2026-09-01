using System.Runtime.InteropServices;

namespace PXA.Importer.Color;

/// <summary>
/// Uses LittleCMS for real ICC transforms. The adapter is optional and fail-closed: a
/// deployment without the native library never falls back to an approximate profile
/// interpretation while claiming ICC conversion.
/// </summary>
internal static class PdfIccColorTransform
{
    private const uint IntentRelativeColorimetric = 1;
    private const uint FloatFlag = 1u << 22;
    private const uint PtGray = 3;
    private const uint PtRgb = 4;
    private const uint PtCmyk = 6;
    private static string? _loadError;
    private static readonly Lazy<Api?> Native = new(Load, LazyThreadSafetyMode.ExecutionAndPublication);

    internal static bool IsAvailable => Native.Value is not null;
    internal static string? LoadError
    {
        get
        {
            _ = Native.Value;
            return _loadError;
        }
    }

    internal static bool IsValidProfile(byte[] profileBytes)
    {
        var api = Native.Value;
        if (api is null || profileBytes.Length < 128)
        {
            return false;
        }

        var handle = GCHandle.Alloc(profileBytes, GCHandleType.Pinned);
        try
        {
            var profile = api.OpenProfile(handle.AddrOfPinnedObject(), checked((uint)profileBytes.Length));
            if (profile == IntPtr.Zero)
            {
                return false;
            }

            api.CloseProfile(profile);
            return true;
        }
        finally
        {
            handle.Free();
        }
    }

    internal static double[]? ToSrgb(
        byte[] profileBytes,
        int componentCount,
        IReadOnlyList<double> components)
    {
        var api = Native.Value;
        if (api is null || profileBytes.Length < 128 || componentCount is not (1 or 3 or 4) ||
            components.Count < componentCount)
        {
            return null;
        }

        var input = new double[componentCount];
        for (var index = 0; index < componentCount; index++)
        {
            var value = Math.Clamp(components[index], 0d, 1d);
            input[index] = componentCount == 4 ? value * 100d : value;
        }

        var handle = GCHandle.Alloc(profileBytes, GCHandleType.Pinned);
        IntPtr inputProfile = IntPtr.Zero;
        IntPtr outputProfile = IntPtr.Zero;
        IntPtr transform = IntPtr.Zero;
        try
        {
            inputProfile = api.OpenProfile(handle.AddrOfPinnedObject(), checked((uint)profileBytes.Length));
            outputProfile = api.CreateSrgbProfile();
            if (inputProfile == IntPtr.Zero || outputProfile == IntPtr.Zero)
            {
                return null;
            }

            transform = api.CreateTransform(
                inputProfile,
                PixelFormat(componentCount),
                outputProfile,
                PixelFormat(3),
                IntentRelativeColorimetric,
                0);
            if (transform == IntPtr.Zero)
            {
                return null;
            }

            var output = new double[3];
            api.DoTransform(transform, input, output, 1);
            return output.All(double.IsFinite)
                ? output.Select(static value => Math.Clamp(value, 0d, 1d)).ToArray()
                : null;
        }
        finally
        {
            if (transform != IntPtr.Zero)
            {
                api.DeleteTransform(transform);
            }

            if (outputProfile != IntPtr.Zero)
            {
                api.CloseProfile(outputProfile);
            }

            if (inputProfile != IntPtr.Zero)
            {
                api.CloseProfile(inputProfile);
            }

            handle.Free();
        }
    }

    private static uint PixelFormat(int components)
    {
        var colorSpace = components switch
        {
            1 => PtGray,
            3 => PtRgb,
            4 => PtCmyk,
            _ => 0u
        };
        return FloatFlag | colorSpace << 16 | checked((uint)components) << 3;
    }

    private static Api? Load()
    {
        string[] candidates = OperatingSystem.IsWindows()
            ? ["lcms2.dll"]
            : OperatingSystem.IsMacOS()
                ? [
                    "liblcms2.2.dylib",
                    "liblcms2.dylib",
                    "/opt/homebrew/lib/liblcms2.2.dylib",
                    "/usr/local/lib/liblcms2.2.dylib",
                    "lcms2"
                ]
                : ["liblcms2.so.2", "liblcms2.so", "lcms2"];
        foreach (var candidate in candidates)
        {
            if (!NativeLibrary.TryLoad(candidate, out var library))
            {
                continue;
            }

            try
            {
                var api = new Api(
                    library,
                    Get<OpenProfileDelegate>(library, "cmsOpenProfileFromMem"),
                    Get<CreateSrgbProfileDelegate>(library, "cmsCreate_sRGBProfile"),
                    Get<CreateTransformDelegate>(library, "cmsCreateTransform"),
                    Get<DoTransformDelegate>(library, "cmsDoTransform"),
                    Get<DeleteTransformDelegate>(library, "cmsDeleteTransform"),
                    Get<CloseProfileDelegate>(library, "cmsCloseProfile"));
                _loadError = null;
                return api;
            }
            catch (Exception exception) when (exception is EntryPointNotFoundException or MarshalDirectiveException)
            {
                _loadError = $"{candidate}: {exception.GetType().Name}: {exception.Message}";
                NativeLibrary.Free(library);
            }
        }

        return null;
    }

    private static T Get<T>(IntPtr library, string name) where T : Delegate =>
        Marshal.GetDelegateForFunctionPointer<T>(NativeLibrary.GetExport(library, name));

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    private delegate IntPtr OpenProfileDelegate(IntPtr bytes, uint length);

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    private delegate IntPtr CreateSrgbProfileDelegate();

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    private delegate IntPtr CreateTransformDelegate(
        IntPtr inputProfile,
        uint inputFormat,
        IntPtr outputProfile,
        uint outputFormat,
        uint intent,
        uint flags);

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    private delegate void DoTransformDelegate(IntPtr transform, double[] input, double[] output, uint size);

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    private delegate void DeleteTransformDelegate(IntPtr transform);

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    private delegate int CloseProfileDelegate(IntPtr profile);

    private sealed record Api(
        IntPtr Library,
        OpenProfileDelegate OpenProfile,
        CreateSrgbProfileDelegate CreateSrgbProfile,
        CreateTransformDelegate CreateTransform,
        DoTransformDelegate DoTransform,
        DeleteTransformDelegate DeleteTransform,
        CloseProfileDelegate CloseProfile);
}
