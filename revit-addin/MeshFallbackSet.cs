using Autodesk.Revit.DB;

namespace BimDown.RevitAddin;

/// <summary>
/// Collects elements that cannot be precisely represented by their table's parametric model.
/// These elements will get a GLB mesh export and mesh_file field set.
/// </summary>
public class MeshFallbackSet
{
    readonly Dictionary<ElementId, string> _elements = new();

    public void Add(ElementId id, string reason) => _elements.TryAdd(id, reason);
    public bool Contains(ElementId id) => _elements.ContainsKey(id);
    public IReadOnlyDictionary<ElementId, string> Elements => _elements;
    public int Count => _elements.Count;
}
