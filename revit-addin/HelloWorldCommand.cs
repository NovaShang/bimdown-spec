using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;

namespace BimDown.RevitAddin;

[Transaction(TransactionMode.Manual)]
public class HelloWorldCommand : IExternalCommand
{
    public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
    {
        TaskDialog.Show("BimDown", "Hello, World!");
        return Result.Succeeded;
    }
}
