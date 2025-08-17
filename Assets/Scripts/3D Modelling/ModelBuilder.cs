using System.Collections.Generic;
using UnityEngine;

// Removed unnecessary using statements for clarity
// using NUnit.Framework.Internal.Filters;
// using System.Threading.Tasks;
// using UnityEngine.InputSystem;

public class ModelBuilder : MonoBehaviour
{
    [SerializeField] private VrStylusHandler MxInkHandler;

    [SerializeField]
    private Transform ControllerTransform; // The transform of the controller to get the position from

    [SerializeField]
    private GameObject DotPrefab; // A small sphere to visualize placed points

    [SerializeField]
    private MeshFilter TargetMeshFilter; // The MeshFilter that will hold our final mesh

    // NEW: A public variable to control how close you need to be to an existing vertex to snap to it.
    // You can adjust this value in the Unity Inspector.
    [SerializeField]
    private float snapDistance = 0.05f;

    // Private variables to manage the mesh creation state
    private Mesh generatedMesh;
    private LineRenderer lineRenderer;

    // Lists to store the data for the final mesh
    private readonly List<Vector3> allVertices = new List<Vector3>();
    private readonly List<int> allTriangles = new List<int>();

    // Temporary lists for building one triangle at a time
    private readonly List<Vector3> currentTriangleVertices = new List<Vector3>();
    // NEW: A temporary list to store the INDICES of the vertices for the current triangle.
    // This is crucial for reusing existing vertices.
    private readonly List<int> currentTriangleIndices = new List<int>();
    private readonly List<GameObject> activeDots = new List<GameObject>();

    private bool HasPressedMain, HasPressedSecond;

    private void Awake()
    {
        // Get the LineRenderer component
        lineRenderer = GetComponent<LineRenderer>();
        // Configure the LineRenderer
        lineRenderer.positionCount = 0;
        lineRenderer.startWidth = 0.005f;
        lineRenderer.endWidth = 0.005f;
        lineRenderer.useWorldSpace = true;

        // Ensure the target MeshFilter is assigned
        if (TargetMeshFilter == null)
        {
            Debug.LogError("Target Mesh Filter is not assigned!");
            enabled = false;
            return;
        }

        // Create a new mesh and assign it to the filter
        generatedMesh = new Mesh();
        generatedMesh.name = "GeneratedCustomMesh";
        TargetMeshFilter.mesh = generatedMesh;
        generatedMesh.MarkDynamic();
    }

    private void Update()
    {
        // Your input handling logic remains the same
        OVRPlugin.GetActionStateBoolean("front", out bool stylus_front_button);
        OVRPlugin.GetActionStateBoolean("back", out bool stylus_back_button);

        if (stylus_front_button)
        {
            if (!HasPressedMain) OnCreatePerformed();
            HasPressedMain = true;
        }
        else
        {
            HasPressedMain = false;
        }

        if (stylus_back_button)
        {
            if (!HasPressedSecond) OnUndoPerformed();
            HasPressedSecond = true;
        }
        else
        {
            HasPressedSecond = false;
        }
    }

    /// <summary>
    /// MODIFIED: This method now checks for nearby vertices before creating a new one.
    /// </summary>
    private void OnCreatePerformed()
    {
        Vector3 controllerPos = ControllerTransform.position;
        Vector3 vertexToAdd = controllerPos;
        int vertexIndex = -1;

        // --- Vertex Snapping Logic ---
        // Find the index of the closest existing vertex, if it's within snapDistance.
        float closestDistSqr = snapDistance * snapDistance; // Use squared distance for performance
        for (int i = 0; i < allVertices.Count; i++)
        {
            float distSqr = (allVertices[i] - controllerPos).sqrMagnitude;
            if (distSqr < closestDistSqr)
            {
                closestDistSqr = distSqr;
                vertexIndex = i;
            }
        }

        // --- Determine Which Vertex to Use ---
        if (vertexIndex != -1)
        {
            // A close vertex was found! Snap to it instead of creating a new one.
            vertexToAdd = allVertices[vertexIndex];
            Debug.Log($"Snapped to existing vertex at index {vertexIndex}");
        }
        else
        {
            // No vertex is close enough, so create a new one.
            allVertices.Add(vertexToAdd);
            vertexIndex = allVertices.Count - 1; // Its index is the last one in the list.
            Debug.Log($"Created new vertex at index {vertexIndex}");
        }

        // Add the chosen vertex position to the temporary list for drawing lines.
        currentTriangleVertices.Add(vertexToAdd);
        // Add its index to the list for building the mesh triangle.
        currentTriangleIndices.Add(vertexIndex);

        // Instantiate a visual dot at the final vertex position (either new or snapped).
        if (DotPrefab != null)
        {
            GameObject dot = Instantiate(DotPrefab, vertexToAdd, Quaternion.identity);
            activeDots.Add(dot);
        }

        // Update the state based on how many points we have for the current triangle.
        UpdateCreationState();
        MxInkHandler.TriggerHapticClick();
    }

    /// <summary>
    /// MODIFIED: The undo logic is now safer and correctly handles shared vertices.
    /// </summary>
    private void OnUndoPerformed()
    {
        // Case 1: Undo the last point while creating a triangle.
        if (currentTriangleVertices.Count > 0)
        {
            int lastIndex = currentTriangleIndices[currentTriangleIndices.Count - 1];

            // Check if the vertex we're about to remove was a new one and isn't used by any other completed triangle.
            bool isIndexUsedElsewhere = allTriangles.Contains(lastIndex);

            // We can only safely remove a vertex if it was the last one added AND it's not part of another triangle.
            if (!isIndexUsedElsewhere && lastIndex == allVertices.Count - 1)
            {
                allVertices.RemoveAt(lastIndex);
                Debug.Log($"Removed newly created vertex at index {lastIndex}.");
            }

            // Remove the point from the temporary lists.
            currentTriangleVertices.RemoveAt(currentTriangleVertices.Count - 1);
            currentTriangleIndices.RemoveAt(currentTriangleIndices.Count - 1);

            // Remove and destroy the corresponding visual dot.
            if (activeDots.Count > 0)
            {
                GameObject dotToUndo = activeDots[activeDots.Count - 1];
                activeDots.RemoveAt(activeDots.Count - 1);
                Destroy(dotToUndo);
            }

            UpdateCreationState();
            Debug.Log("Last point undone.");
        }
        // Case 2: Undo the last completed triangle.
        else if (allTriangles.Count > 0)
        {
            Debug.Log("Undoing last triangle.");

            // IMPORTANT: We can NOT safely remove vertices, because other triangles might still use them.
            // We only remove the last 3 triangle indices, which effectively removes the last triangle.
            allTriangles.RemoveRange(allTriangles.Count - 3, 3);

            UpdateMesh();
        }
        else
        {
            Debug.Log("Nothing to undo.");
        }
    }

    /// <summary>
    /// A state machine to handle the creation process step-by-step. No changes needed here.
    /// </summary>
    private void UpdateCreationState()
    {
        switch (currentTriangleVertices.Count)
        {
            case 0:
                lineRenderer.positionCount = 0;
                break;
            case 1:
                Debug.Log("First point placed. Awaiting second point.");
                lineRenderer.positionCount = 0;
                break;
            case 2:
                Debug.Log("Second point placed. Drawing line. Awaiting third point.");
                lineRenderer.positionCount = 2;
                lineRenderer.SetPositions(currentTriangleVertices.ToArray());
                break;
            case 3:
                Debug.Log("Third point placed. Creating triangle and resetting.");
                AddTriangleToMesh();
                ResetForNextTriangle();
                break;
        }
    }

    /// <summary>
    /// MODIFIED: Adds the triangle to the mesh using the stored indices.
    /// </summary>
    private void AddTriangleToMesh()
    {
        // The vertices are already in the `allVertices` list.
        // We just add the three indices from our temporary list to define the new triangle.
        allTriangles.AddRange(currentTriangleIndices);
        UpdateMesh();
    }

    /// <summary>
    /// Applies the current vertex and triangle lists to the generatedMesh object. No changes needed here.
    /// </summary>
    private void UpdateMesh()
    {
        generatedMesh.Clear();
        generatedMesh.SetVertices(allVertices);
        generatedMesh.SetTriangles(allTriangles, 0); // Use SetTriangles instead of SetIndices
        generatedMesh.RecalculateNormals();
        generatedMesh.RecalculateBounds();
    }

    /// <summary>
    /// MODIFIED: Cleans up temporary lists to prepare for the next triangle.
    /// </summary>
    private void ResetForNextTriangle()
    {
        currentTriangleVertices.Clear();
        // NEW: Also clear the temporary index list.
        currentTriangleIndices.Clear();

        foreach (GameObject dot in activeDots)
        {
            Destroy(dot);
        }
        activeDots.Clear();

        lineRenderer.positionCount = 0;
    }
}