using Hexa.NET.ImGui;
using Hexa.NET.ImGui.Backends.SDL3;
using Hexa.NET.SDL3;
using PS.APP.Dialogs;
using PS.APP.Localization;
using PS.APP.Settings;
using PS.APP.Resources;
using PS.APP.Windows;
using System.Numerics;
using ImSDLEvent = Hexa.NET.ImGui.Backends.SDL3.SDLEvent;
using ImSDLWindow = Hexa.NET.ImGui.Backends.SDL3.SDLWindow;
using SDLWindow = Hexa.NET.SDL3.SDLWindow;
using SDLEvent = Hexa.NET.SDL3.SDLEvent;
using SDLGPUDevice = Hexa.NET.SDL3.SDLGPUDevice;
using ImSDLGPUDevice = Hexa.NET.ImGui.Backends.SDL3.SDLGPUDevice;
using SDLGPUCommandBuffer = Hexa.NET.SDL3.SDLGPUCommandBuffer;
using ImSDLGPUCommandBuffer = Hexa.NET.ImGui.Backends.SDL3.SDLGPUCommandBuffer;
using SDLGPURenderPass = Hexa.NET.SDL3.SDLGPURenderPass;
using ImSDLGPURenderPass = Hexa.NET.ImGui.Backends.SDL3.SDLGPURenderPass;

namespace PS.APP;

public class Application
{
#pragma warning disable CS8618
    public static Application Instance { get; private set; } = null!;
#pragma warning restore CS8618

    private bool _shouldClose;
    private unsafe SDLGPUDevice* _gpuDevice;
    private ImageManager? _images;
    private Form? _mainForm;
    private WindowRuntime? _mainRuntime;
    private WindowRuntime? _currentRuntime;
    private readonly List<WindowRuntime> _windows = [];
    private readonly Queue<(Form Form, AppWindowOptions Options)> _pendingWindows = [];
    private readonly Queue<PendingSaveFileDialogRequest> _pendingSaveDialogs = [];
    private float _mainScale = 1f;

    public Application(ILocalizer? localizer = null, SettingsManager? settings = null)
    {
        Localizer = localizer;
        Settings = settings;
        Instance = this;
    }

    public ILocalizer? Localizer { get; }

    public SettingsManager? Settings { get; }

    public bool CanOpenSettings => Settings != null;

    public Form? MainForm => _mainForm;

    public Form? ActiveForm => _mainRuntime?.ActiveForm;

    public AppWindow? CurrentWindow => _currentRuntime?.AppWindow;

    public IReadOnlyList<AppWindow> Windows => _windows.Select(w => w.AppWindow).ToList();

    public ImageManager Images => _images ?? throw new InvalidOperationException("Application is not running.");

    public unsafe bool ShowOpenFileDialog(
        IReadOnlyList<FileDialogFilter> filters,
        Action<FileDialogResult> onComplete,
        string? defaultLocation = null,
        bool allowMultiple = false)
    {
        var parent = GetDialogParentWindow();
        if (parent == null)
            throw new InvalidOperationException("There is no application running.");

        return NativeFileDialog.ShowOpenFile(parent, filters, onComplete, defaultLocation, allowMultiple);
    }

    public bool ShowSaveFileDialog(
        IReadOnlyList<FileDialogFilter> filters,
        Action<FileDialogResult> onComplete,
        string? defaultLocation = null)
    {
        if (_mainRuntime == null)
            throw new InvalidOperationException("There is no application running.");

        if (NativeFileDialog.IsDialogOpen || _pendingSaveDialogs.Count > 0)
            return false;

        _pendingSaveDialogs.Enqueue(new PendingSaveFileDialogRequest(
            _currentRuntime ?? _mainRuntime,
            filters.ToArray(),
            onComplete,
            defaultLocation));
        return true;
    }

    public Vector4 ClearColor { get; set; } = new(0.45f, 0.55f, 0.60f, 1.00f);

    public static void Run(Form form, ILocalizer? localizer = null, SettingsManager? settings = null) =>
        new Application(localizer, settings).Execute(form);

    public unsafe void Execute(Form form)
    {
        if (_mainForm != null)
            throw new InvalidOperationException("There already is an application running.");

        _mainForm = form;
        Settings?.Load();

        if (!SDL.Init((uint)(SDLInitFlags.Video | SDLInitFlags.Gamepad)))
        {
            Console.WriteLine($"Error: SDL_Init(): {SDL.GetErrorS()}");
            return;
        }

        _mainScale = SDL.GetDisplayContentScale(SDL.GetPrimaryDisplay());

        _gpuDevice = SDL.CreateGPUDevice(
            (uint)(SDLGPUShaderFormat.Spirv | SDLGPUShaderFormat.Dxil | SDLGPUShaderFormat.Metallib),
            true, (byte*)null);
        if (_gpuDevice == null)
        {
            Console.WriteLine($"Error: SDL_CreateGPUDevice(): {SDL.GetErrorS()}");
            SDL.Quit();
            return;
        }

        _images = new ImageManager(_gpuDevice);

        var mainRuntime = CreateWindowRuntime(form, isMain: true, AppWindowOptions.Default);
        _mainRuntime = mainRuntime;
        _windows.Add(mainRuntime);

        SDL.ShowWindow(mainRuntime.SdlWindow);

        while (!_shouldClose)
        {
            _images.BeginFrame();

            ProcessPendingWindows();

            SDLEvent e;
            while (SDL.PollEvent(&e))
            {
                if (TryHandleApplicationEvent(&e))
                    continue;

                var runtime = FindRuntimeForEvent(&e);
                if (runtime == null)
                    continue;

                ActivateRuntime(runtime);
                ImGuiImplSDL3.ProcessEvent((ImSDLEvent*)&e);
            }

            NativeFileDialog.ProcessMainThreadQueue();
            ClosePendingWindows();

            if (_windows.All(IsMinimized))
            {
                SDL.Delay(10);
                continue;
            }

            foreach (var runtime in _windows.ToArray())
            {
                if (runtime.ShouldClose)
                    continue;

                if (IsMinimized(runtime))
                    continue;

                RenderWindow(runtime);
            }

            ProcessPendingSaveDialogs();

            if (_mainRuntime != null)
                ActivateRuntime(_mainRuntime);
        }

        SDL.WaitForGPUIdle(_gpuDevice);
        if (Settings?.AutoSaveOnExit == true)
            Settings.Save();

        foreach (var runtime in _windows.Where(w => !w.IsMain).ToArray())
            DestroyWindow(runtime);

        if (_mainRuntime != null)
            DestroyWindow(_mainRuntime);

        FontFactory.Dispose();
        _images.Dispose();

        SDL.DestroyGPUDevice(_gpuDevice);
        SDL.Quit();

        _mainForm = null;
        _mainRuntime = null;
        _currentRuntime = null;
        _windows.Clear();
        _images = null;
    }

    public void ShowWindow(Form form, AppWindowOptions? options = null)
    {
        if (_mainRuntime == null)
            throw new InvalidOperationException("There is no application running.");

        _pendingWindows.Enqueue((form, options ?? AppWindowOptions.Default));
    }

    private unsafe void ProcessPendingWindows()
    {
        while (_pendingWindows.Count > 0)
        {
            var (form, options) = _pendingWindows.Dequeue();
            var runtime = CreateWindowRuntime(form, isMain: false, options);
            _windows.Add(runtime);
            SDL.ShowWindow(runtime.SdlWindow);
        }
    }

    public void CloseWindow(AppWindow window)
    {
        if (window.IsMain)
            RequestExit();
        else
            window.Close();
    }

    public void OpenSettings()
    {
        if (Settings == null)
            throw new InvalidOperationException("No SettingsManager was provided to the application.");

        if (_mainRuntime == null)
            throw new InvalidOperationException("There is no application running.");

        var settingsForm = new SettingsForm(Settings)
        {
            Size = _mainRuntime.ActiveForm?.Size ?? _mainForm!.Size,
            Window = _mainRuntime.AppWindow
        };
        _mainRuntime.ActiveForm = settingsForm;
    }

    public void ReturnToMainForm()
    {
        if (_mainRuntime == null || _mainForm == null)
            throw new InvalidOperationException("There is no application running.");

        _mainRuntime.ActiveForm = _mainForm;
        SetWindowTitle(_mainRuntime, _mainForm.Title);
    }

    public void RequestExit()
    {
        if (_mainForm == null)
            throw new InvalidOperationException("There is no application running.");

        if (!_mainForm.TryRequestClose())
            return;

        Exit();
    }

    public void Exit()
    {
        if (_mainForm == null)
            throw new InvalidOperationException("There is no application running.");

        _shouldClose = true;
    }

    public unsafe void SetSize(Vector2 size)
    {
        if (_mainRuntime == null)
            throw new InvalidOperationException("There is no application running.");

        _mainForm!.Size = size;
        SDL.SetWindowSize(_mainRuntime.SdlWindow, (int)size.X, (int)size.Y);
    }

    internal void SetCurrentWindow(WindowRuntime runtime) => _currentRuntime = runtime;

    internal unsafe void SetWindowTitle(WindowRuntime runtime, string title) =>
        SDL.SetWindowTitle(runtime.SdlWindow, title);

    internal unsafe void SetWindowTitle(string title)
    {
        if (_mainRuntime != null)
            SetWindowTitle(_mainRuntime, title);
    }

    private unsafe WindowRuntime CreateWindowRuntime(Form form, bool isMain, AppWindowOptions options)
    {
        form.ShowMenuBar = isMain || options.ShowMenuBar;

        var size = options.Size ?? form.Size;
        form.Size = size;

        var window = CreateSdlWindow(
            form,
            size,
            options.Position,
            options.Resizable,
            centered: isMain && options.Position == null);

        var scale = SDL.GetDisplayContentScale(SDL.GetDisplayForWindow(window));
        var context = ImGui.CreateContext();
        ImGui.SetCurrentContext(context);
        ConfigureImGuiIo(ImGui.GetIO(), scale);

        FontFactory.Initialize(context, ImGui.GetIO(), scale);

        ImGuiImplSDL3.SetCurrentContext(context);
        ImGuiImplSDL3.InitForSDLGPU((ImSDLWindow*)window);

        ImGuiImplSDLGPU3InitInfo initInfo = new()
        {
            Device = (ImSDLGPUDevice*)_gpuDevice,
            ColorTargetFormat = (int)SDL.GetGPUSwapchainTextureFormat(_gpuDevice, window),
            MSAASamples = (int)SDLGPUSampleCount.Samplecount1
        };
        ImGuiImplSDL3.SDLGPU3Init(&initInfo);

        var runtime = new WindowRuntime
        {
            SdlWindow = window,
            ImGuiContext = context,
            Form = form,
            Scale = scale,
            IsMain = isMain,
            ActiveForm = form,
            AppWindow = null!
        };

        runtime.AppWindow = new AppWindow(runtime);
        form.Window = runtime.AppWindow;

        return runtime;
    }

    private unsafe SDLWindow* CreateSdlWindow(
        Form form,
        Vector2 size,
        Vector2? position = null,
        bool resizable = true,
        bool centered = false)
    {
        var windowFlags = SDLWindowFlags.Hidden | SDLWindowFlags.HighPixelDensity;
        if (resizable)
            windowFlags |= SDLWindowFlags.Resizable;

        SDLWindow* window = SDL.CreateWindow(form.Title, (int)size.X, (int)size.Y, (uint)windowFlags);
        if (window == null)
            throw new InvalidOperationException($"SDL_CreateWindow(): {SDL.GetErrorS()}");

        if (position is { } explicitPosition)
            SDL.SetWindowPosition(window, (int)explicitPosition.X, (int)explicitPosition.Y);
        else if (centered)
            SDL.SetWindowPosition(window, (int)SDL.SDL_WINDOWPOS_CENTERED_MASK, (int)SDL.SDL_WINDOWPOS_CENTERED_MASK);

        if (!SDL.ClaimWindowForGPUDevice(_gpuDevice, window))
        {
            SDL.DestroyWindow(window);
            throw new InvalidOperationException($"SDL_ClaimWindowForGPUDevice(): {SDL.GetErrorS()}");
        }

        ConfigureSwapchain(_gpuDevice, window);
        return window;
    }

    private static unsafe void ConfigureImGuiIo(ImGuiIOPtr io, float scale)
    {
        io.IniFilename = null;
        io.ConfigFlags |= ImGuiConfigFlags.NavEnableKeyboard | ImGuiConfigFlags.NavEnableGamepad;
        io.ConfigWindowsMoveFromTitleBarOnly = true;
        io.ConfigDpiScaleFonts = true;

        ImGui.StyleColorsLight();
        var style = ImGui.GetStyle();
        style.ScaleAllSizes(scale);
        style.FontScaleDpi = scale;
    }

    private unsafe bool TryHandleApplicationEvent(SDLEvent* e)
    {
        var type = (SDLEventType)e->Type;
        var runtime = FindRuntimeForEvent(e);

        switch (type)
        {
            case SDLEventType.Quit:
                RequestExit();
                return true;

            case SDLEventType.WindowCloseRequested when runtime != null:
                if (runtime.IsMain)
                    RequestExit();
                else
                    runtime.RequestClose();
                return true;

            case SDLEventType.WindowShown when runtime != null:
                runtime.PendingLoad = true;
                return true;

            case SDLEventType.WindowResized when runtime != null:
            {
                int w = 0, h = 0;
                SDL.GetWindowSize(runtime.SdlWindow, ref w, ref h);
                var activeForm = GetActiveForm(runtime);
                activeForm.Size = new Vector2(w, h);
                activeForm.OnResized();
                return true;
            }

            default:
                return false;
        }
    }

    private unsafe void RenderWindow(WindowRuntime runtime)
    {
        ActivateRuntime(runtime);

        ImGuiImplSDL3.SDLGPU3NewFrame();
        ImGuiImplSDL3.NewFrame();
        ImGui.NewFrame();

        if (runtime.PendingLoad)
        {
            GetActiveForm(runtime).OnLoad();
            runtime.PendingLoad = false;
        }

        GetActiveForm(runtime).Update(runtime);

        ImGui.Render();
        ImDrawData* drawData = ImGui.GetDrawData();
        bool isDrawMinimized = drawData->DisplaySize.X <= 0 || drawData->DisplaySize.Y <= 0;

        SDLGPUCommandBuffer* commandBuffer = SDL.AcquireGPUCommandBuffer(_gpuDevice);
        SDLGPUTexture* swapTexture;
        if (!SDL.WaitAndAcquireGPUSwapchainTexture(commandBuffer, runtime.SdlWindow, &swapTexture, null, null))
        {
            SDL.CancelGPUCommandBuffer(commandBuffer);
            return;
        }

        if (swapTexture != null && !isDrawMinimized)
        {
            ImGuiImplSDL3.SDLGPU3PrepareDrawData(drawData, (ImSDLGPUCommandBuffer*)commandBuffer);

            SDLGPUColorTargetInfo targetInfo = new()
            {
                Texture = swapTexture,
                ClearColor = new SDLFColor
                {
                    R = ClearColor.X,
                    G = ClearColor.Y,
                    B = ClearColor.Z,
                    A = ClearColor.W
                },
                LoadOp = SDLGPULoadOp.Clear,
                StoreOp = SDLGPUStoreOp.Store,
                MipLevel = 0,
                LayerOrDepthPlane = 0,
                Cycle = 0
            };

            SDLGPURenderPass* renderPass = SDL.BeginGPURenderPass(commandBuffer, &targetInfo, 1, null);
            ImGuiImplSDL3.SDLGPU3RenderDrawData(drawData, (ImSDLGPUCommandBuffer*)commandBuffer, (ImSDLGPURenderPass*)renderPass, null);
            SDL.EndGPURenderPass(renderPass);
        }

        SDL.SubmitGPUCommandBuffer(commandBuffer);
    }

    private unsafe void ActivateRuntime(WindowRuntime runtime)
    {
        _currentRuntime = runtime;
        ImGui.SetCurrentContext(runtime.ImGuiContext);
        ImGuiImplSDL3.SetCurrentContext(runtime.ImGuiContext);
    }

    private unsafe WindowRuntime? FindRuntimeForEvent(SDLEvent* e)
    {
        if (TryGetEventWindowId(e, out var windowId) && windowId != 0)
            return _windows.FirstOrDefault(w => w.WindowId == windowId);

        SDLWindow* mouseFocus = SDL.GetMouseFocus();
        if (mouseFocus != null)
        {
            var mouseWindowId = SDL.GetWindowID(mouseFocus);
            return _windows.FirstOrDefault(w => w.WindowId == mouseWindowId);
        }

        SDLWindow* keyboardFocus = SDL.GetKeyboardFocus();
        if (keyboardFocus != null)
        {
            var keyboardWindowId = SDL.GetWindowID(keyboardFocus);
            return _windows.FirstOrDefault(w => w.WindowId == keyboardWindowId);
        }

        return _mainRuntime;
    }

    private static unsafe bool TryGetEventWindowId(SDLEvent* e, out uint windowId)
    {
        switch ((SDLEventType)e->Type)
        {
            case SDLEventType.MouseMotion:
                windowId = e->Motion.WindowID;
                return true;
            case SDLEventType.MouseButtonDown:
            case SDLEventType.MouseButtonUp:
                windowId = e->Button.WindowID;
                return true;
            case SDLEventType.MouseWheel:
                windowId = e->Wheel.WindowID;
                return true;
            case SDLEventType.KeyDown:
            case SDLEventType.KeyUp:
                windowId = e->Key.WindowID;
                return true;
            case SDLEventType.TextInput:
                windowId = e->Text.WindowID;
                return true;
            default:
                if (IsWindowEvent((SDLEventType)e->Type))
                {
                    windowId = e->Window.WindowID;
                    return true;
                }

                windowId = 0;
                return false;
        }
    }

    private static bool IsWindowEvent(SDLEventType type) =>
        type is SDLEventType.WindowShown
            or SDLEventType.WindowHidden
            or SDLEventType.WindowExposed
            or SDLEventType.WindowMoved
            or SDLEventType.WindowResized
            or SDLEventType.WindowPixelSizeChanged
            or SDLEventType.WindowMinimized
            or SDLEventType.WindowMaximized
            or SDLEventType.WindowRestored
            or SDLEventType.WindowMouseEnter
            or SDLEventType.WindowMouseLeave
            or SDLEventType.WindowFocusGained
            or SDLEventType.WindowFocusLost
            or SDLEventType.WindowCloseRequested
            or SDLEventType.WindowDisplayScaleChanged
            or SDLEventType.WindowDisplayChanged;

    private static Form GetActiveForm(WindowRuntime runtime) =>
        runtime.IsMain ? runtime.ActiveForm! : runtime.Form;

    private unsafe SDLWindow* GetDialogParentWindow()
    {
        var runtime = _currentRuntime ?? _mainRuntime;
        return runtime == null ? null : runtime.SdlWindow;
    }

    private unsafe void ProcessPendingSaveDialogs()
    {
        if (_pendingSaveDialogs.Count == 0 || NativeFileDialog.IsDialogOpen)
            return;

        var pending = _pendingSaveDialogs.Dequeue();
        var parent = pending.ParentRuntime.SdlWindow;
        if (!NativeFileDialog.ShowSaveFile(parent, pending.Filters, pending.OnComplete, pending.DefaultLocation))
        {
            pending.OnComplete(new FileDialogResult(
                FileDialogResultKind.Error,
                errorMessage: NativeFileDialog.IsDialogOpen
                    ? "Another file dialog is already open."
                    : "Could not open save dialog."));
        }
    }

    private sealed class PendingSaveFileDialogRequest(
        WindowRuntime parentRuntime,
        FileDialogFilter[] filters,
        Action<FileDialogResult> onComplete,
        string? defaultLocation)
    {
        public WindowRuntime ParentRuntime { get; } = parentRuntime;

        public FileDialogFilter[] Filters { get; } = filters;

        public Action<FileDialogResult> OnComplete { get; } = onComplete;

        public string? DefaultLocation { get; } = defaultLocation;
    }

    private void ClosePendingWindows()
    {
        foreach (var runtime in _windows.Where(w => !w.IsMain && w.ShouldClose).ToArray())
            DestroyWindow(runtime);
    }

    private unsafe void DestroyWindow(WindowRuntime runtime)
    {
        var activeForm = GetActiveForm(runtime);
        activeForm.OnClosed();

        ActivateRuntime(runtime);

        ImGuiImplSDL3.Shutdown();
        ImGuiImplSDL3.SDLGPU3Shutdown();
        FontFactory.ReleaseContext(runtime.ImGuiContext);
        ImGui.DestroyContext(runtime.ImGuiContext);

        SDL.ReleaseWindowFromGPUDevice(_gpuDevice, runtime.SdlWindow);
        SDL.DestroyWindow(runtime.SdlWindow);

        runtime.Form.Window = null;
        _windows.Remove(runtime);

        if (runtime.IsMain)
            _mainRuntime = null;
    }

    private static unsafe bool IsMinimized(WindowRuntime runtime) =>
        (SDL.GetWindowFlags(runtime.SdlWindow) & (ulong)SDLWindowFlags.Minimized) != 0;

    private static unsafe void ConfigureSwapchain(SDLGPUDevice* gpuDevice, SDLWindow* window)
    {
        const SDLGPUSwapchainComposition composition = SDLGPUSwapchainComposition.Sdr;
        var presentMode = SDL.WindowSupportsGPUPresentMode(gpuDevice, window, SDLGPUPresentMode.Mailbox)
            ? SDLGPUPresentMode.Mailbox
            : SDLGPUPresentMode.Vsync;

        if (!SDL.SetGPUSwapchainParameters(gpuDevice, window, composition, presentMode))
            SDL.SetGPUSwapchainParameters(gpuDevice, window, composition, SDLGPUPresentMode.Vsync);
    }
}
