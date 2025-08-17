using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Text;
using Parabox.Stl;
using UnityEngine;
public class STLScaler : MonoBehaviour
{
    public void ScaleSTLMesh(MeshFilter meshfilter)
    {
        float scalePercent = 1; // Default scale percent (1% = 1.01)
        float scaleFactor = 1f + (scalePercent / 100f); // 1% = 1.01

        Vector3[] vertices = meshfilter.sharedMesh.vertices;

        // Scale all vertices
        for (int i = 0; i < vertices.Length; i++)
        {
            vertices[i] *= scaleFactor;
        }

        // Apply scaled vertices back to mesh
        meshfilter.sharedMesh.vertices = vertices;
        meshfilter.sharedMesh.RecalculateBounds();
    }
}