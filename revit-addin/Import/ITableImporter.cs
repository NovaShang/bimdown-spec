using Autodesk.Revit.DB;

namespace BimDown.RevitAddin.Import;

interface ITableImporter
{
    string TableName { get; }
    int Order { get; }
    ImportResult Import(Document doc, List<Dictionary<string, string?>> csvRows);
}

record ImportResult(int Created, int Updated, int Deleted, List<string> Errors);
