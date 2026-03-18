using Autodesk.Revit.DB;

namespace BimDown.RevitAddin.Import;

record DiffResult(
    List<(Dictionary<string, string?> Row, Element Element)> ToUpdate,
    List<Dictionary<string, string?>> ToCreate,
    List<Element> ToDelete);
