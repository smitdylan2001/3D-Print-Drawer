using System.Collections.Generic;
using UnityEngine;
// --- ADDED: Required namespaces for the new Input System ---
using UnityEngine.InputSystem;
// --- ADDED: Required namespaces for Spatial Anchors and async operations ---
using Meta.XR.MRUtilityKit;
using System.Threading.Tasks;

public class ModelBuilder : MonoBehaviour
{
    [Header("Object References")]
    [SerializeField] private VrStylusHandler MxInkHandler;
    [SerializeField] private Transform ControllerTransform;
    [SerializeField] private GameObject DotPrefab;
    [SerializeField] private MeshFilter TargetMeshFilter;

    [Header("Snapping Settings")]
    [SerializeField] private float snapDistance = 0.05f;
    public bool CanSnap = true;
    [SerializeField] private float axisSnapThreshold = 0.1f;

    // --- ADDED: Input Action fields to be assigned in the Inspector ---
    [Header("Input Actions")]
    [SerializeField] private InputAction placeVertexAction; // For the "front" button
    [SerializeField] private InputAction undoAction; // For the "back" button

    private Mesh generatedMesh;
    private LineRenderer lineRenderer;

    private readonly List<Vector3> meshVertices = new List<Vector3>();
    private readonly List<int> meshTriangles = new List<int>();

    private readonly List<Vector3> currentTrianglePoints = new List<Vector3>();
    private readonly List<GameObject> activeDots = new List<GameObject>();

    private bool isPlacingVertex = false;
    private GameObject previewDot;

    // --- REMOVED: No longer needed with the event-based input system ---
    // private bool HasPressedSecond; 

    private STLExporter exporter;

    // --- ADDED: A reference to the created spatial anchor ---
    private OVRSpatialAnchor _spatialAnchor;

    [ContextMenu("ExportTest")]
    public void Test()
    {
        var printables = GameObject.FindGameObjectsWithTag("Print");
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

    // --- ADDED: OnEnable and OnDisable to handle input action subscriptions ---
    private void OnEnable()
    {
        // Subscribe to the "performed" (press) and "canceled" (release) events
        placeVertexAction.performed += OnPlaceVertexStarted;
        placeVertexAction.canceled += OnPlaceVertexEnded;
        undoAction.performed += OnUndoAction;

        // Enable the actions
        placeVertexAction.Enable();
        undoAction.Enable();
    }

    private void OnDisable()
    {
        // Unsubscribe to prevent memory leaks
        placeVertexAction.performed -= OnPlaceVertexStarted;
        placeVertexAction.canceled -= OnPlaceVertexEnded;
        undoAction.performed -= OnUndoAction;

        // Disable the actions
        placeVertexAction.Disable();
        undoAction.Disable();
    }

    // --- MODIFIED: Update now only handles the visual preview while placing ---
    private void Update()
    {
        // If we are in the middle of placing a vertex, update the preview line/dot
        if (isPlacingVertex)
        {
            UpdatePlacementPreview();
        }
    }

    // --- ADDED: Handler for when the place vertex action starts (button pressed) ---
    private void OnPlaceVertexStarted(InputAction.CallbackContext context)
    {
        isPlacingVertex = true;
        if (DotPrefab != null)
        {
            previewDot = Instantiate(DotPrefab, ControllerTransform.position, Quaternion.identity);
        }
    }

    // --- ADDED: Handler for when the place vertex action ends (button released) ---
    private void OnPlaceVertexEnded(InputAction.CallbackContext context)
    {
        if (isPlacingVertex)
        {
            isPlacingVertex = false;
            PlaceVertex();
        }
    }

    // --- ADDED: Handler for when the undo action is performed ---
    private void OnUndoAction(InputAction.CallbackContext context)
    {
        OnUndoPerformed();
    }

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

    private void PlaceVertex()
    {
        Vector3 positionAfterAxisSnap = GetAxisSnappedPosition(ControllerTransform.position);
        Vector3 finalVertexPosition = positionAfterAxisSnap;

        float closestDistSqr = snapDistance * snapDistance;

        for (int i = 0; i < meshVertices.Count; i++)
        {
            float distSqr = (meshVertices[i] - positionAfterAxisSnap).sqrMagnitude;
            if (distSqr < closestDistSqr)
            {
                closestDistSqr = distSqr;
                finalVertexPosition = meshVertices[i];
            }
        }

        currentTrianglePoints.Add(finalVertexPosition);

        // --- MODIFIED: Check if this is the very first vertex being placed ---
        // If the main mesh has no vertices yet, and we've just added the first
        // point to our current triangle, create the anchor.
        if (meshVertices.Count == 0 && currentTrianglePoints.Count == 1)
        {
            CreateSpatialAnchor(finalVertexPosition);
        }

        if (previewDot != null)
        {
            previewDot.transform.position = finalVertexPosition;
            activeDots.Add(previewDot);
            previewDot = null;
        }

        UpdateCreationState();
        MxInkHandler.TriggerHapticClick();
    }

    // --- ADDED: New method to create and save the spatial anchor ---
    private async void CreateSpatialAnchor(Vector3 position)
    {
        var result = await OVRSpatialAnchor.EraseAnchorsAsync(FindObjectsByType<OVRSpatialAnchor>(FindObjectsSortMode.None), null);

        if (result.Success)
        {
            Debug.Log($"Successfully erased anchors.");
        }
        else
        {
            Debug.LogError($"Failed to erase anchors with result {result.Status}");
        }

        // Prevent creating more than one anchor
        if (_spatialAnchor != null) return;

        // 1. Create a new GameObject to host the anchor component
        var anchorGo = new GameObject("Model_Root_SpatialAnchor");
        anchorGo.transform.position = position;

        // 2. Add the OVRSpatialAnchor component
        _spatialAnchor = anchorGo.AddComponent<OVRSpatialAnchor>();

        // 3. Wait until the component is created and localized
        while (!_spatialAnchor.Created)
        {
            await Task.Yield();
        }
        Debug.Log($"Created anchor {_spatialAnchor.Uuid}");
        TargetMeshFilter.transform.SetParent(_spatialAnchor.transform, worldPositionStays: true);
        return;
        // 4. Asynchronously save the anchor to device storage
        var saveResult = await _spatialAnchor.SaveAnchorAsync();

        if (saveResult)
        {
            Debug.Log($"✅ Successfully created and saved Spatial Anchor with UUID: {_spatialAnchor.Uuid}");
            // Optional: Parent the model to the anchor so their positions are linked
        }
        else
        {
            Debug.LogError("❌ Failed to save the Spatial Anchor.");
        }
    }

    private void OnUndoPerformed()
    {
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
        else if (meshTriangles.Count > 0)
        {
            Debug.Log("Undoing last triangle.");
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

    private void AddTriangleToMesh()
    {
        int baseIndex = meshVertices.Count;
        meshVertices.AddRange(currentTrianglePoints);
        meshTriangles.Add(baseIndex);
        meshTriangles.Add(baseIndex + 1);
        meshTriangles.Add(baseIndex + 2);
        UpdateMesh();
    }

    private void UpdateMesh()
    {
        generatedMesh.Clear();
        generatedMesh.SetVertices(meshVertices);
        generatedMesh.SetTriangles(meshTriangles, 0);

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