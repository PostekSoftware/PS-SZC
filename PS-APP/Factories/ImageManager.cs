using Hexa.NET.ImGui;
using Hexa.NET.SDL3;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using System.Numerics;
using SDLGPUDevice = Hexa.NET.SDL3.SDLGPUDevice;

namespace PS.APP;

public sealed class ImageResource : IDisposable
{
    internal ImageResource(Image<Rgba32> image, nint textureId, int width, int height)
    {
        Image = image;
        TextureId = textureId;
        Width = width;
        Height = height;
    }

    public Image<Rgba32> Image { get; }

    public nint TextureId { get; }

    public int Width { get; }

    public int Height { get; }

    public Vector2 Size => new(Width, Height);

    public void Draw(Vector2? size = null)
    {
        var drawSize = size ?? Size;
        unsafe
        {
            ImGui.Image(new ImTextureRef(null, (ImTextureID)TextureId), drawSize);
        }
    }

    public void Dispose() => Application.Instance?.Images.Unload(this);
}

public sealed unsafe class ImageManager
{
    private readonly SDLGPUDevice* _gpuDevice;
    private readonly Dictionary<Image<Rgba32>, ImageResource> _resources = [];
    private readonly Dictionary<nint, Image<Rgba32>> _textureLookup = [];
    private readonly HashSet<nint> _usedThisFrame = [];

    internal ImageManager(SDLGPUDevice* gpuDevice) => _gpuDevice = gpuDevice;

    public ImageResource LoadFromFile(string path)
    {
        var image = Image.Load<Rgba32>(path);
        return Load(image);
    }

    public ImageResource Load(Image<Rgba32> image)
    {
        if (_resources.TryGetValue(image, out var existing))
        {
            _usedThisFrame.Add(existing.TextureId);
            return existing;
        }

        var textureId = CreateGpuTexture(image);
        var resource = new ImageResource(image, textureId, image.Width, image.Height);
        _resources[image] = resource;
        _textureLookup[textureId] = image;
        _usedThisFrame.Add(textureId);
        return resource;
    }

    public void Unload(ImageResource resource)
    {
        if (!_resources.Remove(resource.Image, out _))
            return;

        _textureLookup.Remove(resource.TextureId);
        _usedThisFrame.Remove(resource.TextureId);
        ReleaseTexture(resource.TextureId);
        resource.Image.Dispose();
    }

    internal void BeginFrame() => _usedThisFrame.Clear();

    internal void Dispose()
    {
        foreach (var resource in _resources.Values.ToArray())
            Unload(resource);
    }

    private nint CreateGpuTexture(Image<Rgba32> image)
    {
        SDLGPUTexture* gpuTexture = SDL.CreateGPUTexture(_gpuDevice, new SDLGPUTextureCreateInfo
        {
            Width = (uint)image.Width,
            Height = (uint)image.Height,
            Format = SDLGPUTextureFormat.R8G8B8A8Unorm,
            Type = SDLGPUTextureType.Texturetype2D,
            LayerCountOrDepth = 1,
            NumLevels = 1,
            SampleCount = SDLGPUSampleCount.Samplecount1,
            Usage = (uint)SDLGPUTextureUsageFlags.Sampler
        });

        UploadTexture(gpuTexture, image);
        return (nint)gpuTexture;
    }

    private unsafe void UploadTexture(SDLGPUTexture* gpuTexture, Image<Rgba32> image)
    {
        int size = image.Width * image.Height * 4;

        SDLGPUTransferBuffer* transferBuffer = SDL.CreateGPUTransferBuffer(_gpuDevice, new SDLGPUTransferBufferCreateInfo
        {
            Size = (uint)size,
            Usage = SDLGPUTransferBufferUsage.Upload
        });

        void* texturePtr = SDL.MapGPUTransferBuffer(_gpuDevice, transferBuffer, true);

        image.ProcessPixelRows(accessor =>
        {
            for (int y = 0; y < accessor.Height; y++)
            {
                var dest = new Span<Rgba32>((void*)((nuint)texturePtr + (nuint)(y * image.Width * 4)), image.Width);
                accessor.GetRowSpan(y).CopyTo(dest);
            }
        });

        SDL.UnmapGPUTransferBuffer(_gpuDevice, transferBuffer);

        var transferInfo = new SDLGPUTextureTransferInfo
        {
            Offset = 0,
            TransferBuffer = transferBuffer
        };

        var textureRegion = new SDLGPUTextureRegion
        {
            Texture = gpuTexture,
            X = 0,
            Y = 0,
            W = (uint)image.Width,
            H = (uint)image.Height,
            D = 1
        };

        SDLGPUCommandBuffer* cmd = SDL.AcquireGPUCommandBuffer(_gpuDevice);
        SDLGPUCopyPass* copyPass = SDL.BeginGPUCopyPass(cmd);
        SDL.UploadToGPUTexture(copyPass, transferInfo, textureRegion, false);
        SDL.EndGPUCopyPass(copyPass);
        SDL.SubmitGPUCommandBuffer(cmd);

        SDL.ReleaseGPUTransferBuffer(_gpuDevice, transferBuffer);
    }

    private unsafe void ReleaseTexture(nint textureId)
    {
        if (textureId == 0)
            return;

        SDL.ReleaseGPUTexture(_gpuDevice, (SDLGPUTexture*)textureId);
    }
}
