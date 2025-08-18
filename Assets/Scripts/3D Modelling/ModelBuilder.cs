using System.Collections.Generic;
using UnityEngine;

public class ModelBuilder : MonoBehaviour
{
    [SerializeField] private VrStylusHandler MxInkHandler;
    [SerializeField] private Transform ControllerTransform;
    [SerializeField] private GameObject DotPrefab;
    [SerializeField] private MeshFilter TargetMeshFilter;
    [SerializeField] private float snapDistance = 0.05f;

    private Mesh generatedMesh;
    private LineRenderer lineRenderer;

    private readonly List<Vector3> allVertices = new List<Vector3>();
    private readonly List<int> allTriangles = new List<int>();

    private readonly List<Vector3> currentTriangleVertices = new List<Vector3>();
    private readonly List<int> currentTriangleIndices = new List<int>();
    private readonly List<GameObject> activeDots = new List<GameObject>();

    // MODIFIED: Replaced HasPressedMain with a stateful boolean for press-and-hold logic.
    private bool isPlacingVertex = false;
    // NEW: A reference to the temporary dot used for visual feedback while placing.
    private GameObject previewDot;

    private bool HasPressedSecond;
    private STLExporter exporter;

    private void Awake()
    {
        exporter = GetComponent<STLExporter>();
        lineRenderer = GetComponent<LineRenderer>();
        lineRenderer.positionCount = 0;
        lineRenderer.startWidth = 0.005f;
        lineRenderer.endWidth = 0.005f;
        lineRenderer.useWorldSpace = true;

        if (TargetMeshFilter == null)
        {
            Debug.LogError("Target Mesh Filter is not assigned!");
            enabled = false;
            return;
        }

        generatedMesh = new Mesh();
        generatedMesh.name = "GeneratedCustomMesh";
        TargetMeshFilter.mesh = generatedMesh;
        generatedMesh.MarkDynamic();
    }

    private void Update()
    {
        OVRPlugin.GetActionStateBoolean("front", out bool stylus_front_button);
        OVRPlugin.GetActionStateBoolean("back", out bool stylus_back_button);

        // --- MODIFIED: Handle Press, Hold, and Release for Vertex Placement ---
        if (stylus_front_button)
        {
            if (!isPlacingVertex) // --- Button was JUST PRESSED ---
            {
                isPlacingVertex = true;
                // Create a preview dot that will follow the controller
                if (DotPrefab != null)
                {
                    previewDot = Instantiate(DotPrefab, ControllerTransform.position, Quaternion.identity);
                }
            }

            // --- Button is BEING HELD ---
            // Update the visuals (dot and line) every frame
            UpdatePlacementPreview();
        }
        else // Button is not being pressed
        {
            if (isPlacingVertex) // --- Button was JUST RELEASED ---
            {
                isPlacingVertex = false;
                // Finalize the vertex position and place it
                PlaceVertex();
            }
        }
        // --- End of Modification ---

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
    /// NEW: This method is called every frame while the placement button is held down.
    /// It updates the position of the preview dot and the line renderer to give live feedback.
    /// </summary>
    private void UpdatePlacementPreview()
    {
        Vector3 controllerPos = ControllerTransform.position;

        if (previewDot != null)
        {
            previewDot.transform.position = controllerPos;
        }

        // Update the Line Renderer to show the potential new line or triangle
        switch (currentTriangleVertices.Count)
        {
            case 1: // If one point is placed, draw a line from it to the controller
                lineRenderer.positionCount = 2;
                lineRenderer.SetPosition(0, currentTriangleVertices[0]);
                lineRenderer.SetPosition(1, controllerPos);
                lineRenderer.loop = false;
                break;
            case 2: // If two points are placed, draw a full triangle preview
                lineRenderer.positionCount = 3;
                lineRenderer.SetPosition(0, currentTriangleVertices[0]);
                lineRenderer.SetPosition(1, currentTriangleVertices[1]);
                lineRenderer.SetPosition(2, controllerPos);
                lineRenderer.loop = true; // Close the shape to form a triangle
                break;
        }
    }

    /// <summary>
    /// MODIFIED: Renamed from OnCreatePerformed. This is now called on button *release* to finalize the vertex placement.
    /// </summary>
    private void PlaceVertex()
    {
        // The vertex position is the final position of our preview dot/controller.
        Vector3 controllerPos = ControllerTransform.position;
        Vector3 vertexToAdd = controllerPos;
        int vertexIndex = -1;

        // --- Vertex Snapping Logic (unchanged) ---
        float closestDistSqr = snapDistance * snapDistance;
        for (int i = 0; i < allVertices.Count; i++)
        {
            float distSqr = (allVertices[i] - controllerPos).sqrMagnitude;
            if (distSqr < closestDistSqr)
            {
                closestDistSqr = distSqr;
                vertexIndex = i;
            }
        }

        if (vertexIndex != -1)
        {
            vertexToAdd = allVertices[vertexIndex];
            Debug.Log($"Snapped to existing vertex at index {vertexIndex}");
        }
        else
        {
            allVertices.Add(vertexToAdd);
            vertexIndex = allVertices.Count - 1;
            Debug.Log($"Created new vertex at index {vertexIndex}");
        }

        currentTriangleVertices.Add(vertexToAdd);
        currentTriangleIndices.Add(vertexIndex);

        // --- MODIFIED: Finalize the preview dot instead of creating a new one ---
        if (previewDot != null)
        {
            // Move the dot to its final snapped position
            previewDot.transform.position = vertexToAdd;
            // Add it to the list of permanent dots
            activeDots.Add(previewDot);
            // Clear the preview reference so we can create a new one next time
            previewDot = null;
        }

        UpdateCreationState();
        MxInkHandler.TriggerHapticClick();
    }

    // --- NO CHANGES NEEDED BELOW THIS LINE ---

    private void OnUndoPerformed()
    {
        exporter.ExportMeshToSTL(TargetMeshFilter);
        return;

        if (currentTriangleVertices.Count > 0)
        {
            int lastIndex = currentTriangleIndices[currentTriangleIndices.Count - 1];
            bool isIndexUsedElsewhere = allTriangles.Contains(lastIndex);

            if (!isIndexUsedElsewhere && lastIndex == allVertices.Count - 1)
            {
                allVertices.RemoveAt(lastIndex);
                Debug.Log($"Removed newly created vertex at index {lastIndex}.");
            }

            currentTriangleVertices.RemoveAt(currentTriangleVertices.Count - 1);
            currentTriangleIndices.RemoveAt(currentTriangleIndices.Count - 1);

            if (activeDots.Count > 0)
            {
                GameObject dotToUndo = activeDots[activeDots.Count - 1];
                activeDots.RemoveAt(activeDots.Count - 1);
                Destroy(dotToUndo);
            }

            UpdateCreationState();
            Debug.Log("Last point undone.");
        }
        else if (allTriangles.Count > 0)
        {
            Debug.Log("Undoing last triangle.");
            allTriangles.RemoveRange(allTriangles.Count - 3, 3);
            UpdateMesh();
        }
        else
        {
            Debug.Log("Nothing to undo.");
        }
    }

    private void UpdateCreationState()
    {
        lineRenderer.loop = false; // Ensure loop is off for static lines
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

    private void AddTriangleToMesh()
    {
        allTriangles.AddRange(currentTriangleIndices);
        UpdateMesh();
    }

    private void UpdateMesh()
    {
        generatedMesh.Clear();
        generatedMesh.SetVertices(allVertices);
        generatedMesh.SetTriangles(allTriangles, 0);
        generatedMesh.RecalculateNormals();
        generatedMesh.RecalculateBounds();
    }

    private void ResetForNextTriangle()
    {
        currentTriangleVertices.Clear();
        currentTriangleIndices.Clear();
        foreach (GameObject dot in activeDots)
        {
            Destroy(dot);
        }
        activeDots.Clear();
        lineRenderer.positionCount = 0;
    }
}