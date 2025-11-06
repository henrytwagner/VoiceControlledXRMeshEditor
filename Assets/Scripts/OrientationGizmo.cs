using UnityEngine;

/// <summary>
/// Displays an orientation gizmo showing XYZ axes
/// Similar to Unity's Scene View orientation widget
/// Can be displayed as 3D axes or as a 2D screen overlay
/// </summary>
public class OrientationGizmo : MonoBehaviour
{
    [Header("Gizmo Style")]
    public GizmoMode mode = GizmoMode.ScreenOverlay;
    
    [Header("3D Gizmo Settings (when attached to object)")]
    public float axisLength = 0.5f;
    public float arrowHeadSize = 0.1f;
    public float lineThickness = 2f;
    
    [Header("Screen Overlay Settings")]
    public bool showInGameView = true;
    public ScreenPosition screenPosition = ScreenPosition.BottomRight;
    public float overlaySize = 80f; // Pixels
    public float overlayMargin = 20f; // Pixels from edge
    
    [Header("Colors")]
    public Color xAxisColor = new Color(1f, 0.2f, 0.2f); // Red
    public Color yAxisColor = new Color(0.3f, 1f, 0.3f); // Green
    public Color zAxisColor = new Color(0.2f, 0.5f, 1f); // Blue
    public Color centerColor = Color.white;
    public Color backgroundColor = new Color(0f, 0f, 0f, 0.3f); // Semi-transparent black
    public bool showBackground = true;
    
    [Header("Labels")]
    public bool showLabels = true;
    public Color labelColor = Color.white;
    public int labelFontSize = 14;
    
    [Header("Camera Reference (for overlay mode)")]
    public Camera targetCamera;
    
    [Header("Debug")]
    public bool showDebugInfo = false;
    
    private Material lineMaterial;
    private float cameraCheckTimer = 0f;
    private const float cameraCheckInterval = 0.5f; // Check every 0.5 seconds instead of every frame
    
    public enum GizmoMode
    {
        ThreeDimensional,  // Shows at object's position
        ScreenOverlay      // Shows in corner of screen
    }
    
    public enum ScreenPosition
    {
        TopLeft,
        TopRight,
        BottomLeft,
        BottomRight
    }
    
    void Start()
    {
        CreateLineMaterial();
        UpdateCameraReference(); // Find camera immediately
    }
    
    void Update()
    {
        // Periodically check for camera changes (not every frame for performance)
        cameraCheckTimer += Time.deltaTime;
        if (cameraCheckTimer >= cameraCheckInterval || targetCamera == null)
        {
            cameraCheckTimer = 0f;
            UpdateCameraReference();
        }
    }
    
    void UpdateCameraReference()
    {
        Camera newCamera = null;
        
        // Priority 1: Look for DesktopCamera (for desktop mode)
        GameObject desktopCam = GameObject.Find("DesktopCamera");
        if (desktopCam != null)
        {
            Camera cam = desktopCam.GetComponent<Camera>();
            if (cam != null && cam.enabled && cam.gameObject.activeInHierarchy)
            {
                newCamera = cam;
            }
        }
        
        // Priority 2: Camera.main (if DesktopCamera not found or inactive)
        if (newCamera == null && Camera.main != null && Camera.main.enabled)
        {
            newCamera = Camera.main;
        }
        
        // Priority 3: Any active camera
        if (newCamera == null)
        {
            Camera[] cameras = FindObjectsOfType<Camera>();
            foreach (Camera cam in cameras)
            {
                if (cam.enabled && cam.gameObject.activeInHierarchy)
                {
                    newCamera = cam;
                    break;
                }
            }
        }
        
        // Only update and log if camera actually changed
        if (newCamera != targetCamera && newCamera != null)
        {
            targetCamera = newCamera;
            if (showDebugInfo)
            {
                Debug.Log($"[OrientationGizmo] Now tracking camera: {targetCamera.name}");
            }
        }
    }
    
    void CreateLineMaterial()
    {
        if (lineMaterial == null)
        {
            Shader shader = Shader.Find("Hidden/Internal-Colored");
            lineMaterial = new Material(shader);
            lineMaterial.hideFlags = HideFlags.HideAndDontSave;
            lineMaterial.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
            lineMaterial.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
            lineMaterial.SetInt("_Cull", (int)UnityEngine.Rendering.CullMode.Off);
            lineMaterial.SetInt("_ZWrite", 0);
            lineMaterial.SetInt("_ZTest", (int)UnityEngine.Rendering.CompareFunction.Always);
        }
    }
    
    void OnGUI()
    {
        if (mode != GizmoMode.ScreenOverlay || targetCamera == null || !showInGameView)
        {
            if (showDebugInfo)
            {
                GUI.Box(new Rect(10, 10, 200, 40), "Gizmo not drawing:\n" + 
                    $"Mode: {mode}\nCamera: {(targetCamera != null ? "OK" : "NULL")}", GUI.skin.box);
            }
            return;
        }
        
        // Draw the gizmo using GUI
        DrawScreenOverlayGUI();
        
        // Debug info
        if (showDebugInfo)
        {
            GUIStyle debugStyle = new GUIStyle(GUI.skin.box);
            debugStyle.normal.textColor = Color.yellow;
            debugStyle.fontSize = 12;
            string debugText = $"Camera: {(targetCamera != null ? targetCamera.name : "NULL")}\n" +
                             $"Rotation: {(targetCamera != null ? targetCamera.transform.rotation.eulerAngles.ToString("F1") : "N/A")}\n" +
                             $"Gizmo Drawing: YES";
            GUI.Box(new Rect(10, Screen.height - 100, 250, 80), debugText, debugStyle);
            
            // Draw a test box where gizmo should be
            Vector2 gizmoPos = GetOverlayCenter();
            GUI.Box(new Rect(gizmoPos.x - 50, gizmoPos.y - 50, 100, 100), "GIZMO HERE", debugStyle);
        }
    }
    
    void OnDrawGizmos()
    {
        if (mode == GizmoMode.ThreeDimensional)
        {
            Draw3DGizmo(transform.position, transform.rotation);
        }
    }
    
    /// <summary>
    /// Draw 3D gizmo at object's position (for Gizmos API)
    /// </summary>
    void Draw3DGizmo(Vector3 position, Quaternion rotation)
    {
        // X Axis (Red)
        DrawAxisWithArrow(position, rotation * Vector3.right, xAxisColor, "X");
        
        // Y Axis (Green)
        DrawAxisWithArrow(position, rotation * Vector3.up, yAxisColor, "Y");
        
        // Z Axis (Blue)
        DrawAxisWithArrow(position, rotation * Vector3.forward, zAxisColor, "Z");
        
        // Center sphere
        Gizmos.color = centerColor;
        Gizmos.DrawSphere(position, axisLength * 0.05f);
    }
    
    void DrawAxisWithArrow(Vector3 origin, Vector3 direction, Color color, string label)
    {
        Vector3 end = origin + direction * axisLength;
        
        // Main line
        Gizmos.color = color;
        Gizmos.DrawLine(origin, end);
        
        // Arrow head
        Vector3 right = Vector3.Cross(direction, Vector3.up).normalized;
        if (right == Vector3.zero)
            right = Vector3.Cross(direction, Vector3.right).normalized;
        
        Vector3 up = Vector3.Cross(right, direction).normalized;
        
        float headLength = arrowHeadSize;
        float headWidth = arrowHeadSize * 0.5f;
        
        Vector3 arrowBase = end - direction * headLength;
        Vector3 arrowTip = end;
        
        Gizmos.DrawLine(arrowTip, arrowBase + right * headWidth);
        Gizmos.DrawLine(arrowTip, arrowBase - right * headWidth);
        Gizmos.DrawLine(arrowTip, arrowBase + up * headWidth);
        Gizmos.DrawLine(arrowTip, arrowBase - up * headWidth);
        
        // Label (in Scene view)
        #if UNITY_EDITOR
        if (showLabels)
        {
            GUIStyle style = new GUIStyle();
            style.normal.textColor = color;
            style.fontSize = labelFontSize;
            style.fontStyle = FontStyle.Bold;
            UnityEditor.Handles.Label(end + direction * 0.1f, label, style);
        }
        #endif
    }
    
    /// <summary>
    /// Draw screen overlay gizmo using GUI (called from OnGUI)
    /// </summary>
    void DrawScreenOverlayGUI()
    {
        Vector2 center = GetOverlayCenter();
        float size = overlaySize * 0.4f;
        
        Quaternion camRot = targetCamera.transform.rotation;
        
        // Simple GUI texture-based drawing (more reliable than GL)
        DrawSimpleGizmo(center, camRot, size);
        
        // Draw labels
        if (showLabels)
            DrawScreenOverlayLabels();
    }
    
    /// <summary>
    /// Simple gizmo drawing using GUI (no GL, more reliable)
    /// </summary>
    void DrawSimpleGizmo(Vector2 center, Quaternion camRot, float size)
    {
        // Draw background box
        if (showBackground)
        {
            float padding = 15f;
            float boxSize = (size + padding) * 2f;
            Rect bgRect = new Rect(center.x - boxSize/2, center.y - boxSize/2, boxSize, boxSize);
            
            Texture2D bgTex = Texture2D.whiteTexture;
            GUI.color = backgroundColor;
            GUI.DrawTexture(bgRect, bgTex);
            GUI.color = Color.white;
        }
        
        // Get camera transform for view projection
        Transform camTransform = targetCamera.transform;
        
        // Project WORLD axes into camera view space
        // This shows how the world X, Y, Z axes appear from the camera's perspective
        
        // Draw X axis (Red) - World right direction
        DrawGizmoAxis(center, Vector3.right, camTransform, size, xAxisColor, "X");
        
        // Draw Y axis (Green) - World up direction
        DrawGizmoAxis(center, Vector3.up, camTransform, size, yAxisColor, "Y");
        
        // Draw Z axis (Blue) - World forward direction
        DrawGizmoAxis(center, Vector3.forward, camTransform, size, zAxisColor, "Z");
        
        // Draw center dot
        Texture2D dot = Texture2D.whiteTexture;
        GUI.color = centerColor;
        GUI.DrawTexture(new Rect(center.x - 3, center.y - 3, 6, 6), dot);
        GUI.color = Color.white;
    }
    
    void DrawGizmoAxis(Vector2 center, Vector3 worldDir, Transform camTransform, float length, Color color, string label)
    {
        // Transform world direction to camera's local space
        // This tells us how the world axis appears from the camera's perspective
        Vector3 viewDir = camTransform.InverseTransformDirection(worldDir);
        
        // Project to screen: X stays X, Y stays Y, ignore Z (depth)
        // Note: viewDir.y is flipped because screen Y goes down but world Y goes up
        float x = viewDir.x;
        float y = -viewDir.y; // Flip Y for screen coordinates
        
        // Create 2D screen direction
        Vector2 screenDir = new Vector2(x, y);
        
        // Only draw if not too small (axis pointing mostly toward/away from camera)
        if (screenDir.magnitude > 0.01f)
        {
            screenDir.Normalize();
            Vector2 end = center + screenDir * length;
            
            // Draw line using GUI texture
            DrawLine(center, end, color, 3f);
            
            // Draw arrowhead
            Vector2 perpendicular = new Vector2(-screenDir.y, screenDir.x);
            float arrowSize = 10f;
            Vector2 arrowBase = end - screenDir * arrowSize;
            
            DrawLine(end, arrowBase + perpendicular * arrowSize * 0.4f, color, 3f);
            DrawLine(end, arrowBase - perpendicular * arrowSize * 0.4f, color, 3f);
        }
    }
    
    void DrawLine(Vector2 start, Vector2 end, Color color, float width)
    {
        Vector2 diff = end - start;
        float angle = Mathf.Atan2(diff.y, diff.x) * Mathf.Rad2Deg;
        float length = diff.magnitude;
        
        Texture2D tex = Texture2D.whiteTexture;
        GUI.color = color;
        
        Matrix4x4 matrix = GUI.matrix;
        GUIUtility.RotateAroundPivot(angle, start);
        GUI.DrawTexture(new Rect(start.x, start.y - width / 2, length, width), tex);
        GUI.matrix = matrix;
        GUI.color = Color.white;
    }
    
    /// <summary>
    /// Draw screen overlay gizmo using GL (deprecated - keeping for reference)
    /// </summary>
    void DrawScreenOverlayGizmo()
    {
        if (targetCamera == null)
            return;
        
        // Calculate overlay position
        Vector2 center = GetOverlayCenter();
        float size = overlaySize * 0.4f;
        
        // Get camera rotation to determine axis directions in screen space
        Quaternion camRot = targetCamera.transform.rotation;
        
        lineMaterial.SetPass(0);
        
        GL.PushMatrix();
        GL.LoadPixelMatrix();
        GL.Begin(GL.LINES);
        
        // Draw axes from center
        DrawScreenAxis(center, camRot * Vector3.right, size, xAxisColor);   // X (Red)
        DrawScreenAxis(center, camRot * Vector3.up, size, yAxisColor);      // Y (Green)
        DrawScreenAxis(center, camRot * Vector3.forward, size, zAxisColor); // Z (Blue)
        
        GL.End();
        
        // Draw center circle
        GL.Begin(GL.LINES);
        DrawScreenCircle(center, 5f, centerColor);
        GL.End();
        
        GL.PopMatrix();
    }
    
    void DrawScreenAxis(Vector2 center, Vector3 worldDirection, float length, Color color)
    {
        // Project world direction to screen space
        Vector3 camForward = targetCamera.transform.forward;
        Vector3 camRight = targetCamera.transform.right;
        Vector3 camUp = targetCamera.transform.up;
        
        float x = Vector3.Dot(worldDirection, camRight);
        float y = Vector3.Dot(worldDirection, camUp);
        
        Vector2 screenDir = new Vector2(x, y).normalized;
        Vector2 end = center + screenDir * length;
        
        GL.Color(color);
        GL.Vertex3(center.x, center.y, 0);
        GL.Vertex3(end.x, end.y, 0);
        
        // Draw arrow head
        Vector2 perpendicular = new Vector2(-screenDir.y, screenDir.x);
        float arrowSize = 8f;
        Vector2 arrowBase = end - screenDir * arrowSize;
        
        GL.Vertex3(end.x, end.y, 0);
        GL.Vertex3(arrowBase.x + perpendicular.x * arrowSize * 0.5f, arrowBase.y + perpendicular.y * arrowSize * 0.5f, 0);
        
        GL.Vertex3(end.x, end.y, 0);
        GL.Vertex3(arrowBase.x - perpendicular.x * arrowSize * 0.5f, arrowBase.y - perpendicular.y * arrowSize * 0.5f, 0);
    }
    
    void DrawScreenCircle(Vector2 center, float radius, Color color)
    {
        GL.Color(color);
        int segments = 16;
        for (int i = 0; i < segments; i++)
        {
            float angle1 = (i / (float)segments) * Mathf.PI * 2f;
            float angle2 = ((i + 1) / (float)segments) * Mathf.PI * 2f;
            
            Vector2 p1 = center + new Vector2(Mathf.Cos(angle1), Mathf.Sin(angle1)) * radius;
            Vector2 p2 = center + new Vector2(Mathf.Cos(angle2), Mathf.Sin(angle2)) * radius;
            
            GL.Vertex3(p1.x, p1.y, 0);
            GL.Vertex3(p2.x, p2.y, 0);
        }
    }
    
    void DrawScreenOverlayLabels()
    {
        Vector2 center = GetOverlayCenter();
        float size = overlaySize * 0.4f;
        
        Transform camTransform = targetCamera.transform;
        
        GUIStyle style = new GUIStyle(GUI.skin.label);
        style.fontSize = labelFontSize;
        style.fontStyle = FontStyle.Bold;
        style.alignment = TextAnchor.MiddleCenter;
        
        // Draw labels at end of each axis (using world directions)
        DrawScreenAxisLabel(center, Vector3.right, camTransform, size, "X", xAxisColor, style);
        DrawScreenAxisLabel(center, Vector3.up, camTransform, size, "Y", yAxisColor, style);
        DrawScreenAxisLabel(center, Vector3.forward, camTransform, size, "Z", zAxisColor, style);
    }
    
    void DrawScreenAxisLabel(Vector2 center, Vector3 worldDir, Transform camTransform, float length, string label, Color color, GUIStyle style)
    {
        // Use same projection as axis drawing
        Vector3 viewDir = camTransform.InverseTransformDirection(worldDir);
        
        float x = viewDir.x;
        float y = -viewDir.y; // Flip Y for screen coordinates
        
        Vector2 screenDir = new Vector2(x, y);
        
        // Only draw label if axis is visible
        if (screenDir.magnitude > 0.01f)
        {
            screenDir.Normalize();
            Vector2 labelPos = center + screenDir * (length + 15f);
            
            style.normal.textColor = color;
            Rect labelRect = new Rect(labelPos.x - 15, labelPos.y - 10, 30, 20);
            GUI.Label(labelRect, label, style);
        }
    }
    
    Vector2 GetOverlayCenter()
    {
        float x = 0, y = 0;
        
        switch (screenPosition)
        {
            case ScreenPosition.TopLeft:
                x = overlayMargin + overlaySize / 2;
                y = Screen.height - (overlayMargin + overlaySize / 2);
                break;
            case ScreenPosition.TopRight:
                x = Screen.width - (overlayMargin + overlaySize / 2);
                y = Screen.height - (overlayMargin + overlaySize / 2);
                break;
            case ScreenPosition.BottomLeft:
                x = overlayMargin + overlaySize / 2;
                y = overlayMargin + overlaySize / 2;
                break;
            case ScreenPosition.BottomRight:
                x = Screen.width - (overlayMargin + overlaySize / 2);
                y = overlayMargin + overlaySize / 2;
                break;
        }
        
        return new Vector2(x, y);
    }
    
    void OnDestroy()
    {
        if (lineMaterial != null)
            DestroyImmediate(lineMaterial);
    }
}


