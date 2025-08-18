using UnityEngine;

/// <summary>
/// Creates a singleton object that provides a custom rotation reference for snapping.
/// It draws a visual gizmo in the Scene view and can also create a visible representation at runtime.
/// </summary>
public class SnapGridRotate : MonoBehaviour
{
    // A "singleton" instance, allowing any script to access this object easily.
    public static SnapGridRotate Instance { get; private set; }

    [Header("Axis Settings")]
    [SerializeField]
    [Tooltip("The length of the axis lines for both the editor gizmo and runtime visual.")]
    private float axisLength = 0.5f;

    [Header("Runtime Visuals")]
    [SerializeField]
    [Tooltip("If checked, the axes will be visible as colored lines during gameplay.")]
    private bool showAtRuntime = true;

    [SerializeField]
    [Tooltip("The thickness of the axis lines drawn at runtime.")]
    private float runtimeLineThickness = 0.005f;

    private void Awake()
    {
        // --- Singleton Pattern ---
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
        }
        else
        {
            Instance = this;
        }
    }

    private void Start()
    {
        // Only create the runtime visuals if the option is enabled and the game is actually playing.
        if (showAtRuntime && Application.isPlaying)
        {
            CreateAxisLine(Vector3.right, Color.red, "Runtime_X_Axis");
            CreateAxisLine(Vector3.up, Color.green, "Runtime_Y_Axis");
            CreateAxisLine(Vector3.forward, Color.blue, "Runtime_Z_Axis");
        }
    }

    /// <summary>
    /// Creates and configures a child GameObject with a LineRenderer to draw an axis.
    /// </summary>
    private void CreateAxisLine(Vector3 direction, Color color, string name)
    {
        // Create a new child GameObject for the line.
        GameObject lineObject = new GameObject(name);
        lineObject.transform.SetParent(this.transform);

        // Reset its local position and rotation.
        lineObject.transform.localPosition = Vector3.zero;
        lineObject.transform.localRotation = Quaternion.identity;

        // Add and configure the LineRenderer component.
        LineRenderer lineRenderer = lineObject.AddComponent<LineRenderer>();

        // Use local space so the line rotates with this parent object.
        lineRenderer.useWorldSpace = false;

        // Define the line's start and end points.
        lineRenderer.positionCount = 2;
        lineRenderer.SetPosition(0, Vector3.zero); // Start at the object's origin
        lineRenderer.SetPosition(1, direction * axisLength); // End along the specified direction

        // Set the thickness.
        lineRenderer.startWidth = runtimeLineThickness;
        lineRenderer.endWidth = runtimeLineThickness;

        // Set the color using a simple, unlit material.
        lineRenderer.material = new Material(Shader.Find("Unlit/Color"));
        lineRenderer.material.color = color;
        lineRenderer.startColor = color;
        lineRenderer.endColor = color;
    }

#if UNITY_EDITOR
    // This editor-only function draws the gizmos in the Scene view for easy editing.
    private void OnDrawGizmosSelected()
    {
        Vector3 position = transform.position;

        // Draw Custom X-Axis (Red)
        Gizmos.color = Color.red;
        Gizmos.DrawRay(position, transform.right * axisLength);

        // Draw Custom Y-Axis (Green)
        Gizmos.color = Color.green;
        Gizmos.DrawRay(position, transform.up * axisLength);

        // Draw Custom Z-Axis (Blue)
        Gizmos.color = Color.blue;
        Gizmos.DrawRay(position, transform.forward * axisLength);
    }
#endif
}