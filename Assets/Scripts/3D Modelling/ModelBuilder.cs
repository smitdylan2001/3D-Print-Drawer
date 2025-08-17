using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

public class ModelBuilder : MonoBehaviour
{
    [Header("XR Input")]
    [SerializeField]
    private InputActionReference m_CreateAction; // Reference to the Trigger press action

    [Header("Object References")]
    [SerializeField]
    private Transform m_ControllerTransform; // The transform of the controller to get the position from

    [SerializeField]
    private GameObject m_DotPrefab; // A small sphere to visualize placed points

    [SerializeField]
    private MeshFilter m_TargetMeshFilter; // The MeshFilter that will hold our final mesh

    // Private variables to manage the mesh creation state
    private Mesh m_GeneratedMesh;
    private LineRenderer m_LineRenderer;

    // Lists to store the data for the final mesh
    private readonly List<Vector3> m_AllVertices = new List<Vector3>();
    private readonly List<int> m_AllTriangles = new List<int>();

    // Temporary lists for building one triangle at a time
    private readonly List<Vector3> m_CurrentTriangleVertices = new List<Vector3>();
    private readonly List<GameObject> m_ActiveDots = new List<GameObject>();

    private void Awake()
    {
        // Get the LineRenderer component
        m_LineRenderer = GetComponent<LineRenderer>();
        // Configure the LineRenderer
        m_LineRenderer.positionCount = 0;
        m_LineRenderer.startWidth = 0.005f;
        m_LineRenderer.endWidth = 0.005f;
        m_LineRenderer.useWorldSpace = true;

        // Ensure the target MeshFilter is assigned
        if (m_TargetMeshFilter == null)
        {
            Debug.LogError("Target Mesh Filter is not assigned!");
            enabled = false;
            return;
        }

        // Create a new mesh and assign it to the filter
        m_GeneratedMesh = new Mesh();
        m_GeneratedMesh.name = "GeneratedCustomMesh";
        m_TargetMeshFilter.mesh = m_GeneratedMesh;
    }

    private void OnEnable()
    {
        // Subscribe to the 'performed' event of the input action
        m_CreateAction.action.performed += OnCreatePerformed;
    }

    private void OnDisable()
    {
        // Unsubscribe to prevent memory leaks
        m_CreateAction.action.performed -= OnCreatePerformed;
    }

    /// <summary>
    /// This method is called every time the trigger button is fully pressed.
    /// </summary>
    private void OnCreatePerformed(InputAction.CallbackContext context)
    {
        // Get the controller's current position
        Vector3 newPoint = m_ControllerTransform.position;

        // Instantiate a visual dot at the new point
        if (m_DotPrefab != null)
        {
            GameObject dot = Instantiate(m_DotPrefab, newPoint, Quaternion.identity);
            m_ActiveDots.Add(dot);
        }

        // Add the new point to our temporary list for the current triangle
        m_CurrentTriangleVertices.Add(newPoint);

        // Update the state based on how many points we have for the current triangle
        UpdateCreationState();
    }

    /// <summary>
    /// A state machine to handle the creation process step-by-step.
    /// </summary>
    private void UpdateCreationState()
    {   
        switch (m_CurrentTriangleVertices.Count)
        {
            case 1:
                // First point placed. Do nothing else.
                Debug.Log("First point placed. Awaiting second point.");
                // Ensure line renderer is cleared from previous triangle
                m_LineRenderer.positionCount = 0;
                break;

            case 2:
                // Second point placed. Draw a line between the two points.
                Debug.Log("Second point placed. Drawing line. Awaiting third point.");
                m_LineRenderer.positionCount = 2;
                m_LineRenderer.SetPositions(m_CurrentTriangleVertices.ToArray());
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
        int baseIndex = m_AllVertices.Count;

        // Add the three new vertices to our master list
        m_AllVertices.AddRange(m_CurrentTriangleVertices);

        // Define the new triangle using the indices of the vertices we just added.
        // The order (winding order) determines which face of the triangle is visible.
        // This order (0, 1, 2 relative to the new set) makes it front-facing.
        m_AllTriangles.Add(baseIndex);
        m_AllTriangles.Add(baseIndex + 1);
        m_AllTriangles.Add(baseIndex + 2);

        // --- Update the actual Mesh object ---
        m_GeneratedMesh.Clear(); // Clear old data

        m_GeneratedMesh.vertices = m_AllVertices.ToArray();
        m_GeneratedMesh.triangles = m_AllTriangles.ToArray();

        // Recalculate normals for correct lighting and bounds for culling
        m_GeneratedMesh.RecalculateNormals();
        m_GeneratedMesh.RecalculateBounds();
    }

    /// <summary>
    /// Cleans up the temporary dots and lines to prepare for the next triangle.
    /// </summary>
    private void ResetForNextTriangle()
    {
        // Clear the list of vertices for the current triangle
        m_CurrentTriangleVertices.Clear();

        // Destroy the temporary dot GameObjects
        foreach (GameObject dot in m_ActiveDots)
        {
            Destroy(dot);
        }
        m_ActiveDots.Clear();

        // Hide the line renderer
        m_LineRenderer.positionCount = 0;
    }
}