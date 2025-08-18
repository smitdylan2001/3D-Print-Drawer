using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Text;
using Parabox.Stl;
using UnityEngine;

public class STLScaler : MonoBehaviour
{
    /// <summary>
    /// Scales the mesh of a given MeshFilter from its center point.
    /// </summary>
    /// <param name="meshFilter">The MeshFilter component containing the mesh to scale.</param>
    public void ScaleSTLMesh(MeshFilter meshFilter)
    {
        // --- Configuration ---
        // The percentage to scale the model by. 
        // e.g., 0.5f means a 0.5% increase in size.
        float scalePercent = 0.5f;

        // Calculate the final scale factor. 
        // For a 0.5% scale, the factor will be 1.005.
        float scaleFactor = 1f + (scalePercent / 100f);

        // --- Scaling Logic ---
        // Get the shared mesh from the filter to modify it.
        Mesh mesh = meshFilter.sharedMesh;
        if (mesh == null)
        {
            Debug.LogError("MeshFilter does not have a mesh to scale.", this);
            return;
        }

        Vector3[] vertices = mesh.vertices;

        // Find the center of the mesh's bounding box. This is our pivot point.
        Vector3 center = mesh.bounds.center;

        // Iterate through each vertex in the mesh
        for (int i = 0; i < vertices.Length; i++)
        {
            // The core logic to scale from the center:
            // 1. (vertices[i] - center): Translate the vertex so the mesh's center is at the world origin (0,0,0).
            // 2. * scaleFactor: Apply the scaling to the translated vertex.
            // 3. + center: Translate the scaled vertex back to its original position relative to the center.
            vertices[i] = (vertices[i] - center) * scaleFactor + center;
        }

        // Apply the newly scaled vertices back to the mesh
        mesh.vertices = vertices;

        // Recalculate the mesh's bounding box and normals to ensure lighting and culling work correctly.
        mesh.RecalculateBounds();
        mesh.RecalculateNormals();
    }
}
