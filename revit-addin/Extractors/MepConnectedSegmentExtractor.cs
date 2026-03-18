using Autodesk.Revit.DB;
using Autodesk.Revit.DB.Mechanical;
using Autodesk.Revit.DB.Plumbing;
using Autodesk.Revit.DB.Electrical;

namespace BimDown.RevitAddin.Extractors;

public class MepConnectedSegmentExtractor : IFieldExtractor
{
    public IReadOnlyList<string> FieldNames { get; } = ["start_node_id", "end_node_id"];

    public Dictionary<string, string?> Extract(Element element)
    {
        var fields = new Dictionary<string, string?>();

        var connectorSet = GetConnectors(element);
        if (connectorSet is null) return fields;

        // MEP curves typically have two main connectors (start/end)
        Connector? startConn = null;
        Connector? endConn = null;

        foreach (Connector conn in connectorSet)
        {
            if (conn.ConnectorType != ConnectorType.End) continue;

            if (startConn is null)
                startConn = conn;
            else
            {
                endConn = conn;
                break;
            }
        }

        if (startConn is not null)
            fields["start_node_id"] = GetConnectedElementId(startConn);
        if (endConn is not null)
            fields["end_node_id"] = GetConnectedElementId(endConn);

        return fields;
    }

    static ConnectorSet? GetConnectors(Element element)
    {
        if (element is MEPCurve mepCurve)
            return mepCurve.ConnectorManager?.Connectors;
        if (element is FamilyInstance fi)
            return fi.MEPModel?.ConnectorManager?.Connectors;
        return null;
    }

    static string? GetConnectedElementId(Connector connector)
    {
        foreach (Connector other in connector.AllRefs)
        {
            if (other.Owner.Id != connector.Owner.Id)
                return other.Owner.UniqueId;
        }
        return null;
    }
}
