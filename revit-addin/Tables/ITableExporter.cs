using Autodesk.Revit.DB;

namespace BimDown.RevitAddin.Tables;

public interface ITableExporter
{
    string TableName { get; }
    IReadOnlyList<string> Columns { get; }
    List<Dictionary<string, string?>> Export(Document doc);
}
