using NUnit.Framework.Internal;
using Parabox.Stl;
using System.Collections;
using System.Linq;
using UnityEngine;

public class STLExporter : MonoBehaviour
{
    public GameObject[] exports;

    [ContextMenu("Test")]
    public void TestExport()
    {
        ExportMultiModels(exports);
    }


    public void ExportMeshToSTL(MeshFilter mesh)
    {
        ExportMeshToSTL(mesh.sharedMesh);
    }

    public void ExportMeshToSTL(Mesh mesh)
    {
        var filename = Time.time.ToString("F2") + "_exported_mesh";

        // For Quest, save to persistent data path
        string filePath = System.IO.Path.Combine(Application.persistentDataPath, filename + ".stl");
        // Convert Unity mesh to STL format
        string stlString = Exporter.WriteString(mesh);

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

    public void ExportMultiModels(GameObject[] models)
    {
        var filename = Time.time.ToString("F2") + "_exported_mesh";

        var meshes = new UnityEngine.Mesh[models.Length];
        for (int i = 0; i < models.Length; i++)
        {
            if(models[i].GetComponent<MeshFilter>()) meshes[i] = models[i].GetComponent<MeshFilter>().sharedMesh;
        }
        // Convert Unity mesh to STL format
        string filePath = System.IO.Path.Combine(Application.persistentDataPath, filename + ".stl");
        Exporter.Export(filePath, models, FileType.Binary);

        Debug.Log(filePath );
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