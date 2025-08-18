using System.Collections.Generic;
using UnityEngine;

public class ModelBuilder : MonoBehaviour
{
    [SerializeField] private VrStylusHandler MxInkHandler;
    [SerializeField] private Transform ControllerTransform;
    [SerializeField] private GameObject DotPrefab;
    [SerializeField] private MeshFilter TargetMeshFilter;
    [SerializeField] private float snapDistance = 0.05f;

    public bool CanSnap = true;
    [SerializeField] private float axisSnapThreshold = 0.1f;

    private Mesh generatedMesh;
    private LineRenderer lineRenderer;

    // --- MODIFIED: Renamed for clarity. These lists hold the final mesh data.
    private readonly List<Vector3> meshVertices = new List<Vector3>();
    private readonly List<int> meshTriangles = new List<int>();

    // --- MODIFIED: This list now only stores the Vector3 positions for the triangle currently being built.
    private readonly List<Vector3> currentTrianglePoints = new List<Vector3>();
    private readonly List<GameObject> activeDots = new List<GameObject>();

    private bool isPlacingVertex = false;
    private GameObject previewDot;

    private bool HasPressedSecond;
    private STLExporter exporter;

    [ContextMenu("ExportTest")]
    public void Test()
    {
        // This is your custom export logic, you can keep it or remove it.
        var printables = GameObject.FindGameObjectsWithTag("Print");
        //GetComponent<STLScaler>().ScaleSTLMesh(TargetMeshFilter);
        if (printables.Length > 0)
        {
            exporter = GetComponent<STLExporter>();

            exporter.ExportMultiModels(printables);
            return;
        }
    }

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

        if (stylus_front_button)
        {
            if (!isPlacingVertex)
            {
                isPlacingVertex = true;
                if (DotPrefab != null)
                {
                    previewDot = Instantiate(DotPrefab, ControllerTransform.position, Quaternion.identity);
                }
            }

            UpdatePlacementPreview();
        }
        else
        {
            if (isPlacingVertex)
            {
                isPlacingVertex = false;
                PlaceVertex();
            }
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

    // --- NO CHANGES IN THIS METHOD ---
    private Vector3 GetAxisSnappedPosition(Vector3 currentPos)
    {
        if (!CanSnap || currentTrianglePoints.Count == 0)
        {
            return currentPos;
        }

        Vector3 previousVertex = currentTrianglePoints[currentTrianglePoints.Count - 1];
        Vector3 delta = currentPos - previousVertex;

        if (SnapGridRotate.Instance != null)
        {
            Transform grid = SnapGridRotate.Instance.transform;
            Vector3 projectionX = Vector3.Project(delta, grid.right);
            Vector3 projectionY = Vector3.Project(delta, grid.up);
            Vector3 projectionZ = Vector3.Project(delta, grid.forward);
            Vector3 snappedDelta = Vector3.zero;

            if (projectionX.magnitude > axisSnapThreshold) snappedDelta += projectionX;
            if (projectionY.magnitude > axisSnapThreshold) snappedDelta += projectionY;
            if (projectionZ.magnitude > axisSnapThreshold) snappedDelta += projectionZ;

            return previousVertex + snappedDelta;
        }
        else
        {
            Vector3 snappedPos = currentPos;
            if (Mathf.Abs(delta.x) < axisSnapThreshold) snappedPos.x = previousVertex.x;
            if (Mathf.Abs(delta.y) < axisSnapThreshold) snappedPos.y = previousVertex.y;
            if (Mathf.Abs(delta.z) < axisSnapThreshold) snappedPos.z = previousVertex.z;
            return snappedPos;
        }
    }

    private void UpdatePlacementPreview()
    {
        Vector3 controllerPos = ControllerTransform.position;
        Vector3 previewPos = GetAxisSnappedPosition(controllerPos);

        if (previewDot != null)
        {
            previewDot.transform.position = previewPos;
        }

        switch (currentTrianglePoints.Count)
        {
            case 1:
                lineRenderer.positionCount = 2;
                lineRenderer.SetPosition(0, currentTrianglePoints[0]);
                lineRenderer.SetPosition(1, previewPos);
                lineRenderer.loop = false;
                break;
            case 2:
                lineRenderer.positionCount = 3;
                lineRenderer.SetPosition(0, currentTrianglePoints[0]);
                lineRenderer.SetPosition(1, currentTrianglePoints[1]);
                lineRenderer.SetPosition(2, previewPos);
                lineRenderer.loop = true;
                break;
        }
    }

    /// <summary>
    /// Places a vertex point. Snapping aligns the position, but the vertex data itself will be new.
    /// </summary>
    private void PlaceVertex()
    {
        Vector3 positionAfterAxisSnap = GetAxisSnappedPosition(ControllerTransform.position);
        Vector3 finalVertexPosition = positionAfterAxisSnap;

        // --- MODIFIED: Vertex Snapping Logic ---
        // This still snaps the *position*, but we no longer care about reusing an index.
        float closestDistSqr = snapDistance * snapDistance;
        // --- NOTE: Now searching meshVertices instead of allVertices
        for (int i = 0; i < meshVertices.Count; i++)
        {
            float distSqr = (meshVertices[i] - positionAfterAxisSnap).sqrMagnitude;
            if (distSqr < closestDistSqr)
            {
                closestDistSqr = distSqr;
                finalVertexPosition = meshVertices[i]; // Snap to the exact position of the existing vertex
            }
        }

        // Add the final position to the list for the current triangle.
        currentTrianglePoints.Add(finalVertexPosition);

        if (previewDot != null)
        {
            previewDot.transform.position = finalVertexPosition;
            activeDots.Add(previewDot);
            previewDot = null;
        }

        UpdateCreationState();
        MxInkHandler.TriggerHapticClick();
    }

    // --- MODIFIED: Simplified and more robust Undo logic ---
    private void OnUndoPerformed()
    {
        // If we are in the middle of creating a triangle, undo the last point.
        if (currentTrianglePoints.Count > 0)
        {
            currentTrianglePoints.RemoveAt(currentTrianglePoints.Count - 1);

            if (activeDots.Count > 0)
            {
                GameObject dotToUndo = activeDots[activeDots.Count - 1];
                activeDots.RemoveAt(activeDots.Count - 1);
                Destroy(dotToUndo);
            }
            UpdateCreationState();
            Debug.Log("Last point undone.");
        }
        // Otherwise, if there are triangles in the mesh, undo the last one.
        else if (meshTriangles.Count > 0)
        {
            Debug.Log("Undoing last triangle.");
            // Remove the last 3 vertex positions and 3 triangle indices.
            meshVertices.RemoveRange(meshVertices.Count - 3, 3);
            meshTriangles.RemoveRange(meshTriangles.Count - 3, 3);
            UpdateMesh();
        }
        else
        {
            Debug.Log("Nothing to undo.");
        }
    }

    private void UpdateCreationState()
    {
        lineRenderer.loop = false;
        switch (currentTrianglePoints.Count)
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
                lineRenderer.SetPositions(currentTrianglePoints.ToArray());
                break;
            case 3:
                Debug.Log("Third point placed. Creating triangle and resetting.");
                AddTriangleToMesh();
                ResetForNextTriangle();
                break;
        }
    }

    /// <summary>
    /// Adds the three collected points to the mesh as a new, independent triangle.
    /// </summary>
    private void AddTriangleToMesh()
    {
        // --- CORE LOGIC CHANGE ---
        // 1. Get the index where the new vertices will start.
        int baseIndex = meshVertices.Count;

        // 2. Add the three new vertex positions. They are now part of the mesh data.
        meshVertices.AddRange(currentTrianglePoints);

        // 3. Add the indices for the new triangle, pointing to the vertices we just added.
        meshTriangles.Add(baseIndex);
        meshTriangles.Add(baseIndex + 1);
        meshTriangles.Add(baseIndex + 2);

        UpdateMesh();
    }

    /// <summary>
    /// Clears and applies all vertex and triangle data to the actual mesh component.
    /// </summary>
    private void UpdateMesh()
    {
        generatedMesh.Clear();
        generatedMesh.SetVertices(meshVertices);
        generatedMesh.SetTriangles(meshTriangles, 0);

        // --- IMPORTANT: This must be called to calculate lighting data.
        generatedMesh.RecalculateNormals();
        generatedMesh.RecalculateBounds();
    }

    private void ResetForNextTriangle()
    {
        currentTrianglePoints.Clear();
        foreach (GameObject dot in activeDots)
        {
            Destroy(dot);
        }
        activeDots.Clear();
        lineRenderer.positionCount = 0;
    }
}