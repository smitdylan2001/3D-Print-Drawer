using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

public class ModelBuilder : MonoBehaviour
{
    [SerializeField] private VrStylusHandler MxInkHandler;

    [SerializeField]
    private Transform ControllerTransform; // The transform of the controller to get the position from

    [SerializeField]
    private GameObject DotPrefab; // A small sphere to visualize placed points

    [SerializeField]
    private MeshFilter TargetMeshFilter; // The MeshFilter that will hold our final mesh

    // Private variables to manage the mesh creation state
    private Mesh generatedMesh;
    private LineRenderer lineRenderer;

    // Lists to store the data for the final mesh
    private readonly List<Vector3> allVertices = new List<Vector3>();
    private readonly List<int> allTriangles = new List<int>();

    // Temporary lists for building one triangle at a time
    private readonly List<Vector3> currentTriangleVertices = new List<Vector3>();
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
    }

    private void Update()
    {
        OVRPlugin.GetActionStateBoolean("front", out bool stylus_front_button);
        OVRPlugin.GetActionStateBoolean("back", out bool stylus_back_button);

        if(stylus_front_button && !HasPressedMain)
        {
            OnCreatePerformed();
            HasPressedMain = true;
            Debug.Log("Action");
        }
        else
        {
            if(stylus_front_button) Debug.Log("Supressing action");
            HasPressedMain = false;
        }
        if (stylus_back_button && !HasPressedSecond)
        {
            OnUndoPerformed();
            HasPressedSecond = true;
        }
        else
        {
            HasPressedSecond = false;
        }
    }

    /// <summary>
    /// This method is called every time the trigger button is fully pressed.
    /// </summary>
    private void OnCreatePerformed()
    {
        // Get the controller's current position
        Vector3 newPoint = ControllerTransform.position;

        // Instantiate a visual dot at the new point
        if (DotPrefab != null)
        {
            GameObject dot = Instantiate(DotPrefab, newPoint, Quaternion.identity);
            activeDots.Add(dot);
        }

        // Add the new point to our temporary list for the current triangle
        currentTriangleVertices.Add(newPoint);

        // Update the state based on how many points we have for the current triangle
        UpdateCreationState();

        MxInkHandler.TriggerHapticClick();
    }

    // NEW METHOD: Handles the Undo action
    /// <summary>
    /// This method is called when the Undo action is performed.
    /// It either removes the last placed point or the last completed triangle.
    /// </summary>
    private void OnUndoPerformed()
    {
        // Case 1: We are in the middle of creating a triangle. Undo the last point.
        if (currentTriangleVertices.Count > 0)
        {
            // Remove the last vertex added to the temporary list.
            currentTriangleVertices.RemoveAt(currentTriangleVertices.Count - 1);

            // Remove and destroy the corresponding visual dot.
            if (activeDots.Count > 0)
            {
                GameObject dotToUndo = activeDots[activeDots.Count - 1];
                activeDots.RemoveAt(activeDots.Count - 1);
                Destroy(dotToUndo);
            }

            // Update the visual state (e.g., the line renderer) to reflect the removal.
            UpdateCreationState();
            Debug.Log("Last point undone.");
        }
        // Case 2: We are not creating a triangle. Undo the last completed triangle from the mesh.
        else if (allVertices.Count > 0)
        {
            Debug.Log("Undoing last triangle.");
            // Remove the last 3 vertices that formed the last triangle.
            allVertices.RemoveRange(allVertices.Count - 3, 3);

            // Remove the last 3 triangle indices.
            allTriangles.RemoveRange(allTriangles.Count - 3, 3);

            // Update the mesh with the new (smaller) data lists.
            UpdateMesh();
        }
        else
        {
            Debug.Log("Nothing to undo.");
        }
    }

    /// <summary>
    /// A state machine to handle the creation process step-by-step.
    /// </summary>
    private void UpdateCreationState()
    {
        switch (currentTriangleVertices.Count)
        {
            // NEW CASE: Handles state after undoing the first point of a triangle.
            case 0:
                // Ensure the line renderer is cleared.
                lineRenderer.positionCount = 0;
                break;

            case 1:
                // First point placed.
                Debug.Log("First point placed. Awaiting second point.");
                // Ensure line renderer is cleared (important for when we undo from 2 points to 1).
                lineRenderer.positionCount = 0;
                break;

            case 2:
                // Second point placed. Draw a line between the two points.
                Debug.Log("Second point placed. Drawing line. Awaiting third point.");
                lineRenderer.positionCount = 2;
                lineRenderer.SetPositions(currentTriangleVertices.ToArray());
                break;

            case 3:
                // Third point placed. Create the triangle mesh.
                Debug.Log("Third point placed. Creating triangle and resetting.");
                AddTriangleToMesh();
                ResetForNextTriangle();
                break;
        }
    }

    /// <summary>
    /// Adds the three vertices from m_CurrentTriangleVertices to the main mesh data.
    /// </summary>
    private void AddTriangleToMesh()
    {
        // The starting index for the new vertices will be the current total count
        int baseIndex = allVertices.Count;

        // Add the three new vertices to our master list
        allVertices.AddRange(currentTriangleVertices);

        // Define the new triangle using the indices of the vertices we just added.
        allTriangles.Add(baseIndex);
        allTriangles.Add(baseIndex + 1);
        allTriangles.Add(baseIndex + 2);

        // Update the actual Mesh object
        UpdateMesh(); // MODIFIED: Call to the new helper method
    }

    // NEW HELPER METHOD: Refactored from AddTriangleToMesh
    /// <summary>
    /// Applies the current vertex and triangle lists to the generatedMesh object.
    /// </summary>
    private void UpdateMesh()
    {
        generatedMesh.Clear(); // Clear old data

        generatedMesh.vertices = allVertices.ToArray();
        generatedMesh.triangles = allTriangles.ToArray();

        // Recalculate normals for correct lighting and bounds for culling
        generatedMesh.RecalculateNormals();
        generatedMesh.RecalculateBounds();
    }

    /// <summary>
    /// Cleans up the temporary dots and lines to prepare for the next triangle.
    /// </summary>
    private void ResetForNextTriangle()
    {
        // Clear the list of vertices for the current triangle
        currentTriangleVertices.Clear();

        // Destroy the temporary dot GameObjects
        foreach (GameObject dot in activeDots)
        {
            Destroy(dot);
        }
        activeDots.Clear();

        // Hide the line renderer
        lineRenderer.positionCount = 0;
    }
}