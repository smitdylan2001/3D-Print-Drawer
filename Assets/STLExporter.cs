using UnityEngine;
using Parabox.Stl;
using System.Collections;

public class STLExporter : MonoBehaviour
{
    public void ExportMeshToSTL(MeshFilter mesh)
    {
        var filename = Time.time.ToString("F2") + "_exported_mesh";
        // Convert Unity mesh to STL format
        string stlString = Exporter.WriteString(mesh.sharedMesh);

        // For Quest, save to persistent data path
        string filePath = System.IO.Path.Combine(Application.persistentDataPath, filename + ".stl");

        try 
        {
            System.IO.File.WriteAllText(filePath, stlString);
            Debug.Log($"STL exported successfully to: {filePath}");
        }
        catch (System.Exception e)
        {
            Debug.LogError($"Failed to export STL: {e.Message}");
        }
    }

    // Example: Export a GameObject's mesh
    public void ExportGameObjectMesh(GameObject obj)
    {
        MeshFilter meshFilter = obj.GetComponent<MeshFilter>();
        if (meshFilter != null && meshFilter.mesh != null)
        {
            ExportMeshToSTL(meshFilter);
        }
    }

    // Coroutine version for better performance
    public IEnumerator ExportSTLCoroutine(Mesh mesh, string filename)
    {
        yield return null; // Wait a frame

        string stlData = Exporter.WriteString(mesh);
        string filePath = Application.persistentDataPath + "/" + filename + ".stl";

        System.IO.File.WriteAllText(filePath, stlData);
        Debug.Log($"STL exported to: {filePath}");
    }
}