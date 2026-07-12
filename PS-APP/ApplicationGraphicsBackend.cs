using Hexa.NET.SDL3;

namespace PS.APP;

internal enum ApplicationGraphicsBackend
{
    SdlGpu,
    SdlRenderer
}

internal static class ApplicationGraphicsBackendFactory
{
    internal static unsafe SDLGPUDevice* TryCreateGpuDevice()
    {
        const uint allFormats =
            (uint)(SDLGPUShaderFormat.Spirv | SDLGPUShaderFormat.Dxil | SDLGPUShaderFormat.Metallib);
        const uint dxilOnly = (uint)SDLGPUShaderFormat.Dxil;

        (uint Formats, bool Debug, string? Driver)[] attempts =
        [
            (allFormats, false, null),
            (allFormats, false, "direct3d12"),
            (allFormats, false, "vulkan"),
            (dxilOnly, false, "direct3d12"),
            (dxilOnly, false, "vulkan"),
            (allFormats, true, null)
        ];

        foreach (var (formats, debug, driver) in attempts)
        {
            SDLGPUDevice* device = driver == null
                ? SDL.CreateGPUDevice(formats, debug, (byte*)null)
                : SDL.CreateGPUDevice(formats, debug, driver);

            if (device != null)
                return device;
        }

        return null;
    }

    internal static unsafe SDLRenderer* CreateSdlRenderer(SDLWindow* window)
    {
        var driverPreference = OperatingSystem.IsWindows()
            ? "direct3d11,direct3d,opengl,software"
            : OperatingSystem.IsMacOS()
                ? "metal,opengl,software"
                : "opengl,vulkan,software";

        SDLRenderer* renderer = SDL.CreateRenderer(window, driverPreference);
        if (renderer != null)
            return renderer;

        renderer = SDL.CreateRenderer(window, (byte*)null);
        if (renderer != null)
            return renderer;

        throw new InvalidOperationException($"SDL_CreateRenderer(): {SDL.GetErrorS()}");
    }
}
