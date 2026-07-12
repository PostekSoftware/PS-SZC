using System.Collections.Concurrent;
using System.Runtime.InteropServices;
using System.Text;
using Hexa.NET.SDL3;
using SDLWindow = Hexa.NET.SDL3.SDLWindow;

namespace PS.APP.Dialogs;

public enum FileDialogResultKind
{
    Success,
    Cancelled,
    Error
}

public readonly struct FileDialogResult
{
    public FileDialogResult(FileDialogResultKind kind, string? path = null, string? errorMessage = null)
    {
        Kind = kind;
        Path = path;
        ErrorMessage = errorMessage;
    }

    public FileDialogResultKind Kind { get; }

    public string? Path { get; }

    public string? ErrorMessage { get; }
}

public readonly struct FileDialogFilter
{
    public FileDialogFilter(string name, string pattern)
    {
        Name = name;
        Pattern = pattern;
    }

    public string Name { get; }

    public string Pattern { get; }
}

public static unsafe class NativeFileDialog
{
    private static readonly ConcurrentQueue<Action> MainThreadQueue = new();
    private static readonly SDLDialogFileCallback DialogCallback = OnDialogCompleted;

    public static bool IsDialogOpen { get; private set; }

    public static void ProcessMainThreadQueue()
    {
        while (MainThreadQueue.TryDequeue(out var action))
            action();
    }

    public static bool ShowOpenFile(
        SDLWindow* window,
        IReadOnlyList<FileDialogFilter> filters,
        Action<FileDialogResult> onComplete,
        string? defaultLocation = null,
        bool allowMultiple = false)
    {
        ArgumentNullException.ThrowIfNull(onComplete);

        if (window == null)
        {
            onComplete(new FileDialogResult(FileDialogResultKind.Error, errorMessage: "Application window is not available."));
            return false;
        }

        if (IsDialogOpen)
            return false;

        var request = new DialogRequest(onComplete);
        if (!request.TryConfigureFilters(filters))
        {
            request.Complete(new FileDialogResult(FileDialogResultKind.Error, errorMessage: "Invalid file dialog filters."));
            request.Dispose();
            return false;
        }

        IsDialogOpen = true;
        SDL.ShowOpenFileDialog(
            DialogCallback,
            request.UserData,
            window,
            request.FiltersPtr,
            request.FilterCount,
            defaultLocation,
            allowMultiple);
        return true;
    }

    public static bool ShowSaveFile(
        SDLWindow* window,
        IReadOnlyList<FileDialogFilter> filters,
        Action<FileDialogResult> onComplete,
        string? defaultLocation = null)
    {
        ArgumentNullException.ThrowIfNull(onComplete);

        if (window == null)
        {
            onComplete(new FileDialogResult(FileDialogResultKind.Error, errorMessage: "Application window is not available."));
            return false;
        }

        if (IsDialogOpen)
            return false;

        var request = new DialogRequest(onComplete);
        if (!request.TryConfigureFilters(filters))
        {
            request.Complete(new FileDialogResult(FileDialogResultKind.Error, errorMessage: "Invalid file dialog filters."));
            request.Dispose();
            return false;
        }

        IsDialogOpen = true;
        SDL.ShowSaveFileDialog(
            DialogCallback,
            request.UserData,
            window,
            request.FiltersPtr,
            request.FilterCount,
            defaultLocation);
        return true;
    }

    private static void OnDialogCompleted(void* userdata, byte** filelist, int filter)
    {
        var request = DialogRequest.FromUserData(userdata);
        var result = ParseResult(filelist);

        MainThreadQueue.Enqueue(() =>
        {
            request.Complete(result);
            request.Dispose();
            IsDialogOpen = false;
        });

        _ = filter;
    }

    private static unsafe FileDialogResult ParseResult(byte** filelist)
    {
        if (filelist == null)
            return new FileDialogResult(FileDialogResultKind.Error, errorMessage: SDL.GetErrorS());

        if (*filelist == null)
            return new FileDialogResult(FileDialogResultKind.Cancelled);

        var path = ReadUtf8String(*filelist);
        return string.IsNullOrWhiteSpace(path)
            ? new FileDialogResult(FileDialogResultKind.Cancelled)
            : new FileDialogResult(FileDialogResultKind.Success, path);
    }

    private static unsafe string ReadUtf8String(byte* value)
    {
        if (value == null)
            return string.Empty;

        var length = 0;
        while (value[length] != 0)
            length++;

        return Encoding.UTF8.GetString(value, length);
    }

    private sealed unsafe class DialogRequest : IDisposable
    {
        private readonly Action<FileDialogResult> _onComplete;
        private readonly GCHandle _selfHandle;
        private readonly List<GCHandle> _stringHandles = [];
        private GCHandle _filtersHandle;
        private SDLDialogFileFilter[]? _filters;
        private bool _disposed;

        public DialogRequest(Action<FileDialogResult> onComplete)
        {
            _onComplete = onComplete;
            _selfHandle = GCHandle.Alloc(this);
            UserData = (void*)GCHandle.ToIntPtr(_selfHandle);
        }

        public void* UserData { get; }

        public SDLDialogFileFilter* FiltersPtr => (SDLDialogFileFilter*)_filtersHandle.AddrOfPinnedObject();

        public int FilterCount => _filters?.Length ?? 0;

        public static DialogRequest FromUserData(void* userdata) =>
            (DialogRequest)GCHandle.FromIntPtr((IntPtr)userdata).Target!;

        public bool TryConfigureFilters(IReadOnlyList<FileDialogFilter> filters)
        {
            if (filters.Count == 0)
                return false;

            _filters = new SDLDialogFileFilter[filters.Count];
            for (var i = 0; i < filters.Count; i++)
                _filters[i] = CreateFilter(filters[i].Name, filters[i].Pattern);

            _filtersHandle = GCHandle.Alloc(_filters, GCHandleType.Pinned);
            return true;
        }

        public void Complete(FileDialogResult result) => _onComplete(result);

        public void Dispose()
        {
            if (_disposed)
                return;

            _disposed = true;

            if (_filtersHandle.IsAllocated)
                _filtersHandle.Free();

            foreach (var handle in _stringHandles)
            {
                if (handle.IsAllocated)
                    handle.Free();
            }

            _stringHandles.Clear();

            if (_selfHandle.IsAllocated)
                _selfHandle.Free();
        }

        private SDLDialogFileFilter CreateFilter(string name, string pattern)
        {
            var nameBytes = Encoding.UTF8.GetBytes(name + '\0');
            var patternBytes = Encoding.UTF8.GetBytes(pattern + '\0');

            var nameHandle = GCHandle.Alloc(nameBytes, GCHandleType.Pinned);
            var patternHandle = GCHandle.Alloc(patternBytes, GCHandleType.Pinned);
            _stringHandles.Add(nameHandle);
            _stringHandles.Add(patternHandle);

            unsafe
            {
                return new SDLDialogFileFilter(
                    (byte*)nameHandle.AddrOfPinnedObject(),
                    (byte*)patternHandle.AddrOfPinnedObject());
            }
        }
    }
}
