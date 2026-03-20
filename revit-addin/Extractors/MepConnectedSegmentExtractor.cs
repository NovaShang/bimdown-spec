using Autodesk.Revit.DB;

namespace BimDown.RevitAddin.Extractors;

public class MepConnectedSegmentExtractor : IFieldExtractor
{
    public IReadOnlyList<string> FieldNames { get; } = ["start_node_id", "end_node_id"];

    public Dictionary<string, string?> Extract(Element element)
    {
        var fields = new Dictionary<string, string?>();

        var connectorSet = GetConnectors(element);
        if (connectorSet is null) return fields;

        Connector? startConn = null;
        Connector? endConn = null;
        var fallbacks = new List<Connector>();

        foreach (Connector conn in connectorSet)
        {
            if (conn.ConnectorType != ConnectorType.End) continue;

            if (conn.Direction == FlowDirectionType.In)
                startConn ??= conn;
            else if (conn.Direction == FlowDirectionType.Out)
                endConn ??= conn;
            else
                fallbacks.Add(conn);
        }

        // Fill missing from fallbacks
        foreach (var conn in fallbacks)
        {
            if (startConn is null)
                startConn = conn;
            else if (endConn is null)
                endConn = conn;
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
