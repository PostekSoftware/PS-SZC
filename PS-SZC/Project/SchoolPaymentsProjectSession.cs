using Microsoft.EntityFrameworkCore;
using PS.APP.Dialogs;
using PS.APP.Localization;
using PS.APP.Projects;
using PS_SZC.Data;

namespace PS_SZC.Project;

internal sealed class SchoolPaymentsProjectSession : IDisposable
{
    private static readonly string DefaultProjectFileName = $"School Payments{ProjectManager.FileExtension}";

    private static readonly FileDialogFilter[] ProjectFilters =
    [
        new("PS-SZC Project", "psszc"),
        new("All files", "*")
    ];

    private ProjectFile? _project;
    private SchoolPaymentsContext? _context;

    public ProjectFile? Project => _project;

    public SchoolPaymentsContext? Context => _context;

    public bool IsOpen => _project != null && _context != null;

    public string? ProjectPath => _project?.FilePath;

    public bool CanUseFileDialogs => !NativeFileDialog.IsDialogOpen;

    public void Dispose() => CloseProject();

    public void PromptCreateNewProject(Action<string> setStatus)
    {
        if (!CanUseFileDialogs)
        {
            setStatus(LocalizedString.FromId("Project.DialogBusy"));
            return;
        }

        if (!TryShowSaveDialog(DefaultProjectFileName, result =>
            {
                if (result.Kind == FileDialogResultKind.Cancelled)
                    return;

                if (result.Kind == FileDialogResultKind.Error)
                {
                    setStatus(LocalizedString.FromId("Project.DialogError", () => result.ErrorMessage ?? string.Empty));
                    return;
                }

                var filePath = EnsureProjectExtension(result.Path!);
                var projectName = Path.GetFileNameWithoutExtension(filePath);
                CreateNewProject(projectName, filePath, setStatus);
            }))
        {
            setStatus(LocalizedString.FromId("Project.DialogBusy"));
        }
    }

    public void PromptOpenProject(Action<string> setStatus)
    {
        if (!CanUseFileDialogs)
        {
            setStatus(LocalizedString.FromId("Project.DialogBusy"));
            return;
        }

        if (!TryShowOpenDialog(null, result =>
            {
                if (result.Kind == FileDialogResultKind.Cancelled)
                    return;

                if (result.Kind == FileDialogResultKind.Error)
                {
                    setStatus(LocalizedString.FromId("Project.DialogError", () => result.ErrorMessage ?? string.Empty));
                    return;
                }

                OpenProject(EnsureProjectExtension(result.Path!), setStatus);
            }))
        {
            setStatus(LocalizedString.FromId("Project.DialogBusy"));
        }
    }

    public void PromptSaveProjectAs(Action<string> setStatus)
    {
        if (_project == null)
            return;

        if (!CanUseFileDialogs)
        {
            setStatus(LocalizedString.FromId("Project.DialogBusy"));
            return;
        }

        var defaultFileName = string.IsNullOrWhiteSpace(_project.FilePath)
            ? $"{SanitizeFileName(_project.Name)}{ProjectManager.FileExtension}"
            : Path.GetFileName(_project.FilePath);

        var defaultLocation = string.IsNullOrWhiteSpace(_project.FilePath)
            ? null
            : Path.GetDirectoryName(_project.FilePath);

        if (!TryShowSaveDialog(defaultFileName, result =>
            {
                if (result.Kind == FileDialogResultKind.Cancelled)
                    return;

                if (result.Kind == FileDialogResultKind.Error)
                {
                    setStatus(LocalizedString.FromId("Project.DialogError", () => result.ErrorMessage ?? string.Empty));
                    return;
                }

                SaveProjectAs(EnsureProjectExtension(result.Path!), setStatus);
            }, defaultLocation))
        {
            setStatus(LocalizedString.FromId("Project.DialogBusy"));
        }
    }

    public void CreateNewProject(string name, string? filePath, Action<string> setStatus)
    {
        try
        {
            CloseProject();

            _project = ProjectManager.Create(name);
            EnsureDatabase();

            if (!string.IsNullOrWhiteSpace(filePath))
                SaveProjectAs(filePath, setStatus);
            else
                setStatus(LocalizedString.FromId("Project.Created", () => name));
        }
        catch (Exception ex)
        {
            setStatus(LocalizedString.FromId("Project.Error", () => ex.Message));
        }
    }

    public void OpenProject(string filePath, Action<string> setStatus)
    {
        try
        {
            CloseProject();

            _project = ProjectManager.Open(filePath);
            EnsureDatabase();
            setStatus(LocalizedString.FromId("Project.Opened", () => Path.GetFileName(filePath)));
        }
        catch (Exception ex)
        {
            setStatus(LocalizedString.FromId("Project.Error", () => ex.Message));
        }
    }

    public void SaveProject(Action<string> setStatus)
    {
        if (_project == null)
            return;

        try
        {
            _context?.SaveChanges();

            if (string.IsNullOrWhiteSpace(_project.FilePath))
            {
                PromptSaveProjectAs(setStatus);
                return;
            }

            ExecuteWithClosedContextConnection(() => _project.Save());
            setStatus(LocalizedString.FromId("Project.Saved", () => _project.FilePath!));
        }
        catch (Exception ex)
        {
            setStatus(LocalizedString.FromId("Project.Error", () => ex.Message));
        }
    }

    public void SaveProjectAs(string filePath, Action<string> setStatus)
    {
        if (_project == null)
            return;

        try
        {
            _context?.SaveChanges();
            var normalizedPath = EnsureProjectExtension(filePath);
            ExecuteWithClosedContextConnection(() => _project.SaveAs(normalizedPath));
            setStatus(LocalizedString.FromId("Project.Saved", () => normalizedPath));
        }
        catch (Exception ex)
        {
            setStatus(LocalizedString.FromId("Project.Error", () => ex.Message));
        }
    }

    public bool HasUnsavedChanges => _project?.IsDirty ?? false;

    public void SaveProjectForExit(Action onSaved, Action<string> setStatus)
    {
        if (_project == null)
        {
            onSaved();
            return;
        }

        try
        {
            _context?.SaveChanges();

            if (string.IsNullOrWhiteSpace(_project.FilePath))
            {
                if (!CanUseFileDialogs)
                {
                    setStatus(LocalizedString.FromId("Project.DialogBusy"));
                    return;
                }

                var defaultFileName = $"{SanitizeFileName(_project.Name)}{ProjectManager.FileExtension}";
                if (!TryShowSaveDialog(defaultFileName, result =>
                    {
                        if (result.Kind == FileDialogResultKind.Cancelled)
                            return;

                        if (result.Kind == FileDialogResultKind.Error)
                        {
                            setStatus(LocalizedString.FromId("Project.DialogError", () => result.ErrorMessage ?? string.Empty));
                            return;
                        }

                        SaveProjectAs(EnsureProjectExtension(result.Path!), setStatus);
                        if (!HasUnsavedChanges)
                            onSaved();
                    }))
                {
                    setStatus(LocalizedString.FromId("Project.DialogBusy"));
                }

                return;
            }

            ExecuteWithClosedContextConnection(() => _project.Save());
            setStatus(LocalizedString.FromId("Project.Saved", () => _project.FilePath!));
            onSaved();
        }
        catch (Exception ex)
        {
            setStatus(LocalizedString.FromId("Project.Error", () => ex.Message));
        }
    }

    private void ExecuteWithClosedContextConnection(Action action)
    {
        _context?.SaveChanges();
        _context?.Dispose();
        _context = null;

        try
        {
            action();
        }
        finally
        {
            EnsureDatabase();
        }
    }

    public void CloseProject(Action<string>? setStatus = null)
    {
        _context?.Dispose();
        _context = null;
        _project?.Dispose();
        _project = null;
        setStatus?.Invoke(LocalizedString.FromId("Status.ProjectClosed"));
    }

    private void EnsureDatabase()
    {
        if (_project == null)
            return;

        _context?.Dispose();

        if (!_project.HasCustomDatabase(SchoolPaymentsContext.DatabaseName))
        {
            _context = _project.CreateEfDatabase<SchoolPaymentsContext>(
                SchoolPaymentsContext.DatabaseName,
                options => new SchoolPaymentsContext(options));
            return;
        }

        _context = _project.OpenEfContext<SchoolPaymentsContext>(
            SchoolPaymentsContext.DatabaseName,
            options => new SchoolPaymentsContext(options));
        _context.Database.EnsureCreated();
    }

    public void SaveChanges()
    {
        if (_project == null || _context == null)
            return;

        _project.SaveEfChanges(_context, SchoolPaymentsContext.DatabaseName);
    }

    public Family LoadFamilyDetails(int familyId)
    {
        if (_context == null)
            throw new InvalidOperationException("Project is not open.");

        return _context.Families
            .Include(x => x.Parents)
            .Include(x => x.Children)
            .Include(x => x.Prices)
            .Include(x => x.Discounts)
            .Include(x => x.Transfers)
            .First(x => x.Id == familyId);
    }

    private static bool TryShowOpenDialog(string? defaultLocation, Action<FileDialogResult> onComplete) =>
        PS.APP.Application.Instance.ShowOpenFileDialog(ProjectFilters, onComplete, defaultLocation);

    private static bool TryShowSaveDialog(
        string defaultFileName,
        Action<FileDialogResult> onComplete,
        string? defaultLocation = null)
    {
        var defaultPath = string.IsNullOrWhiteSpace(defaultLocation)
            ? defaultFileName
            : Path.Combine(defaultLocation, defaultFileName);

        return PS.APP.Application.Instance.ShowSaveFileDialog(ProjectFilters, onComplete, defaultPath);
    }

    private static string EnsureProjectExtension(string filePath) =>
        Path.GetExtension(filePath).Equals(ProjectManager.FileExtension, StringComparison.OrdinalIgnoreCase)
            ? filePath
            : filePath + ProjectManager.FileExtension;

    private static string SanitizeFileName(string name)
    {
        var invalid = Path.GetInvalidFileNameChars();
        var sanitized = new string(name.Select(ch => invalid.Contains(ch) ? '_' : ch).ToArray()).Trim();
        return string.IsNullOrWhiteSpace(sanitized) ? "project" : sanitized;
    }
}
