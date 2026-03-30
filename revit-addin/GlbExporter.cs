using Autodesk.Revit.DB;
using SharpGLTF.Geometry;
using SharpGLTF.Geometry.VertexTypes;
using SharpGLTF.Materials;
using SharpGLTF.Scenes;

namespace BimDown.RevitAddin;

using VERTEX = VertexPosition;

static class GlbExporter
{
    /// <summary>
    /// Exports an element's geometry to a GLB file.
    /// Returns the relative path (e.g., "mesh/mesh-1.glb") or null if no geometry found.
    /// </summary>
    internal static string? ExportElement(Element element, string outputDir, string shortId)
    {
        var triangles = ExtractTriangles(element);
        if (triangles.Count == 0) return null;

        var material = new MaterialBuilder("default")
            .WithDoubleSide(true)
            .WithMetallicRoughnessShader()
            .WithBaseColor(new System.Numerics.Vector4(0.7f, 0.7f, 0.7f, 1f));

        var mesh = new MeshBuilder<VERTEX>("mesh");
        var primitive = mesh.UsePrimitive(material);

        foreach (var (a, b, c) in triangles)
        {
            primitive.AddTriangle(
                new VERTEX(a),
                new VERTEX(b),
                new VERTEX(c));
        }

        var scene = new SceneBuilder();
        scene.AddRigidMesh(mesh, System.Numerics.Matrix4x4.Identity);
        var model = scene.ToGltf2();

        var meshDir = Path.Combine(outputDir, "mesh");
        Directory.CreateDirectory(meshDir);
        var fileName = $"{shortId}.glb";
        var fullPath = Path.Combine(meshDir, fileName);
        model.SaveGLB(fullPath);

        return $"mesh/{fileName}";
    }

    static List<(System.Numerics.Vector3 A, System.Numerics.Vector3 B, System.Numerics.Vector3 C)>
        ExtractTriangles(Element element)
    {
        var result = new List<(System.Numerics.Vector3, System.Numerics.Vector3, System.Numerics.Vector3)>();

        var options = new Options
        {
            DetailLevel = ViewDetailLevel.Fine,
            ComputeReferences = false
        };

        var geomElement = element.get_Geometry(options);
        if (geomElement is null) return result;

        CollectTriangles(geomElement, result);
        return result;
    }

    static void CollectTriangles(GeometryElement geomElement,
        List<(System.Numerics.Vector3, System.Numerics.Vector3, System.Numerics.Vector3)> result)
    {
        foreach (var geomObj in geomElement)
        {
            switch (geomObj)
            {
                case Solid solid when solid.Faces.Size > 0:
                    foreach (Face face in solid.Faces)
                    {
                        var mesh = face.Triangulate();
                        if (mesh is null) continue;
                        for (var i = 0; i < mesh.NumTriangles; i++)
                        {
                            var tri = mesh.get_Triangle(i);
                            result.Add((
                                ToVec3(tri.get_Vertex(0)),
                                ToVec3(tri.get_Vertex(1)),
                                ToVec3(tri.get_Vertex(2))));
                        }
                    }
                    break;
                case GeometryInstance instance:
                    CollectTriangles(instance.GetInstanceGeometry(), result);
                    break;
            }
        }
    }

    static System.Numerics.Vector3 ToVec3(XYZ pt)
    {
        // Convert from Revit feet to meters
        var m = UnitConverter.Length(1.0); // feet-to-meters factor
        return new System.Numerics.Vector3(
            (float)(pt.X * m),
            (float)(pt.Z * m),  // Revit Z → GLB Y (up)
            (float)(-pt.Y * m)); // Revit Y → GLB -Z (forward)
    }
}
