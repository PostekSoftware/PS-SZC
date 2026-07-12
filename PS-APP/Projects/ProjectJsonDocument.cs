using System.Text.Json;
using System.Text.Json.Nodes;

namespace PS.APP.Projects;

public sealed class ProjectJsonDocument
{
    internal ProjectJsonDocument(ProjectFile project, ProjectDocumentEntry entry, JsonNode root)
    {
        Project = project;
        Entry = entry;
        Root = root;
    }

    public string Id => Entry.Id;

    public string Name => Entry.Name;

    public JsonNode Root { get; set; }

    internal ProjectDocumentEntry Entry { get; set; }

    internal ProjectFile Project { get; }

    public void Save()
    {
        Project.SaveJsonDocument(this);
    }
}
