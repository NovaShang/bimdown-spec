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

        // Collect all end connectors with their connected element
        var connected = new List<(Connector Conn, string? ConnectedId)>();
        foreach (Connector conn in connectorSet)
        {
            if (conn.ConnectorType != ConnectorType.End) continue;
            connected.Add((conn, GetConnectedElementId(conn)));
        }

        // If no End connectors found, try all physical connectors
        if (connected.Count == 0)
        {
            foreach (Connector conn in connectorSet)
            {
                if (conn.ConnectorType is ConnectorType.End or ConnectorType.Curve or ConnectorType.Physical)
                    connected.Add((conn, GetConnectedElementId(conn)));
            }
        }

        if (connected.Count == 0) return fields;

        // Assign start/end by flow direction, then by order
        Connector? startConn = null;
        Connector? endConn = null;
        string? startId = null;
        string? endId = null;

        foreach (var (conn, id) in connected)
        {
            if (conn.Direction == FlowDirectionType.In && startConn is null)
            {
                startConn = conn;
                startId = id;
            }
            else if (conn.Direction == FlowDirectionType.Out && endConn is null)
            {
                endConn = conn;
                endId = id;
            }
        }

        // Fill missing from remaining connectors
        foreach (var (conn, id) in connected)
        {
            if (conn == startConn || conn == endConn) continue;
            if (startConn is null) { startConn = conn; startId = id; }
            else if (endConn is null) { endConn = conn; endId = id; }
        }

        if (startId is not null) fields["start_node_id"] = startId;
        if (endId is not null) fields["end_node_id"] = endId;

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
        try
        {
            var refs = connector.AllRefs;
            if (refs is null) return null;

            foreach (Connector other in refs)
            {
                if (other.Owner.Id == connector.Owner.Id) continue;
                // Skip logical connectors (system-level, not physical)
                if (other.ConnectorType == ConnectorType.Logical) continue;
                return other.Owner.UniqueId;
            }
        }
        catch
        {
            // AllRefs can throw if connector is not connected
        }
        return null;
    }
}
