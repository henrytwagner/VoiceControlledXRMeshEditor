using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

/// <summary>
/// Simple runtime vertex editor for EditableCube.
/// Press V to enable edit mode, then click near vertices to select and drag them.
/// Press V again or Escape to exit edit mode.
/// </summary>
public class RuntimeVertexEditor : MonoBehaviour
{
    [Header("References")]
    public EditableCube targetCube;
    public Camera editCamera;
    public DesktopCameraController cameraController; // Will auto-find if not assigned
    
    [Header("Settings")]
    [Tooltip("Selection radius in screen pixels (500 = reasonable default)")]
    public float selectRadius = 500f; // Selection radius in pixels
    public Color selectedColor = Color.yellow;
    public Color normalColor = Color.white;
    public float dragSensitivity = 0.01f;
    public bool disableCameraInVertexMode = true;
    
    [Header("UI")]
    public bool showInstructions = true;
    
    // State
    private EditableCube.DisplayMode lastMode;
    private int selectedCornerIndex = -1;
    private Renderer selectedRenderer;
    private Color originalColor;
    private Vector3 lastMousePos;
    private Plane dragPlane;
    
    void Awake()
    {
        Debug.Log("[RuntimeVertexEditor] ========== AWAKE CALLED ==========");
        Debug.Log($"[RuntimeVertexEditor] GameObject: {gameObject.name}, Active: {gameObject.activeInHierarchy}");
    }
    
    void Start()
    {
        // Delay camera detection to let XRAutoSetup run first
        Invoke(nameof(InitializeAfterSetup), 0.1f);
    }
    
    void InitializeAfterSetup()
    {
        UpdateEditCamera();
    }
    
    void UpdateEditCamera()
    {
        // Find the currently rendering camera (after XRAutoSetup has done its thing)
        Camera activeCamera = null;
        
        // First, try to find DesktopCamera if it's enabled
        GameObject desktopCam = GameObject.Find("DesktopCamera");
        if (desktopCam != null && desktopCam.activeInHierarchy)
        {
            Camera cam = desktopCam.GetComponent<Camera>();
            if (cam != null && cam.enabled)
            {
                activeCamera = cam;
                Debug.Log($"[RuntimeVertexEditor] Found active DesktopCamera");
            }
        }
        
        // If no desktop camera, find any active camera
        if (activeCamera == null)
        {
            Camera[] cameras = FindObjectsOfType<Camera>();
            foreach (Camera cam in cameras)
            {
                if (cam.enabled && cam.gameObject.activeInHierarchy)
                {
                    activeCamera = cam;
                    Debug.Log($"[RuntimeVertexEditor] Using first active camera: {cam.name}");
                    break;
                }
            }
        }
        
        // Final fallback
        if (activeCamera != null)
        {
            editCamera = activeCamera;
            Debug.Log($"[RuntimeVertexEditor] Edit camera set to: {editCamera.name}");
        }
        else
        {
            editCamera = Camera.main;
            Debug.Log("[RuntimeVertexEditor] Fallback to Camera.main: " + (editCamera != null ? editCamera.name : "NULL"));
        }
            
        if (targetCube == null)
        {
            targetCube = FindObjectOfType<EditableCube>();
            Debug.Log("[RuntimeVertexEditor] Auto-found EditableCube: " + (targetCube != null ? targetCube.name : "NULL"));
        }
        
        // Debug: Print all corner positions and screen positions
        if (targetCube != null && editCamera != null)
        {
            Vector3[] corners = targetCube.GetCorners();
            Transform cubeTransform = targetCube.transform;
            Debug.Log($"[RuntimeVertexEditor] Corner positions (local and screen):");
            for (int i = 0; i < corners.Length; i++)
            {
                Vector3 worldPos = cubeTransform.TransformPoint(corners[i]);
                Vector3 screenPos = editCamera.WorldToScreenPoint(worldPos);
                Debug.Log($"  Corner {i}: local={corners[i]}, world={worldPos}, screen=({screenPos.x:F0},{screenPos.y:F0},z={screenPos.z:F1})");
            }
        }
        
        if (cameraController == null)
        {
            // Try to find it - also check disabled objects
            cameraController = FindObjectOfType<DesktopCameraController>(true); // Include inactive
            
            if (cameraController == null)
            {
                // Also try finding by name
                GameObject desktopCameraObj = GameObject.Find("DesktopCamera");
                if (desktopCameraObj != null)
                {
                    cameraController = desktopCameraObj.GetComponent<DesktopCameraController>();
                }
            }
            
            if (cameraController != null)
            {
                Debug.Log($"[RuntimeVertexEditor] Auto-found DesktopCameraController on: {cameraController.gameObject.name}, enabled: {cameraController.enabled}");
            }
            else
            {
                Debug.LogWarning("[RuntimeVertexEditor] No DesktopCameraController found! Camera won't be disabled in edit mode.");
                Debug.LogWarning("[RuntimeVertexEditor] To fix: Make sure DesktopCamera GameObject has DesktopCameraController component.");
            }
        }
        
        if (targetCube == null)
        {
            Debug.LogError("[RuntimeVertexEditor] No EditableCube assigned or found! Assign it in Inspector.");
        }
        
        Debug.Log("[RuntimeVertexEditor] RuntimeVertexEditor initialized. Press Tab to toggle Mesh/Vertex modes.");
        
        // Initialize last mode
        lastMode = targetCube.mode;
    }
    
    void Update()
    {
        if (targetCube == null)
            return;
        
        // Update which camera we're using (in case it changed)
        if (editCamera == null || !editCamera.enabled)
        {
            UpdateEditCamera();
        }
        
        // Check if mode changed and sync camera controls
        if (targetCube.mode != lastMode)
        {
            OnModeChanged(targetCube.mode);
            lastMode = targetCube.mode;
        }
        
        // Only allow editing in Vertices mode
        if (targetCube.mode != EditableCube.DisplayMode.Vertices)
            return;
        
        HandleVertexSelection();
        HandleVertexDrag();
    }
    
    void OnModeChanged(EditableCube.DisplayMode newMode)
    {
        bool isVertexMode = (newMode == EditableCube.DisplayMode.Vertices);
        
        Debug.Log($"[RuntimeVertexEditor] Mode changed to: {newMode}");
        Debug.Log($"[RuntimeVertexEditor] cameraController = {(cameraController != null ? cameraController.gameObject.name : "NULL")}");
        Debug.Log($"[RuntimeVertexEditor] disableCameraInVertexMode = {disableCameraInVertexMode}");
        
        if (disableCameraInVertexMode && cameraController != null)
        {
            cameraController.enabled = !isVertexMode;
            Debug.Log($"[RuntimeVertexEditor] Set cameraController.enabled = {cameraController.enabled}");
        }
        else if (cameraController == null)
        {
            Debug.LogWarning("[RuntimeVertexEditor] cameraController is NULL - cannot disable camera!");
        }
        
        if (isVertexMode)
        {
            // Unlock cursor for vertex editing
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
            Debug.Log("[RuntimeVertexEditor] Unlocked cursor for vertex editing");
        }
        else
        {
            // Deselect when leaving vertex mode
            Deselect();
        }
    }
    
    void HandleVertexSelection()
    {
        if (editCamera == null)
        {
            Debug.LogError("[RuntimeVertexEditor] Edit camera is null! Assign Camera.main or a camera reference.");
            return;
        }
        
        bool clickPressed = false;
        
        #if ENABLE_INPUT_SYSTEM
        clickPressed = Mouse.current?.leftButton.wasPressedThisFrame ?? false;
        #else
        clickPressed = Input.GetMouseButtonDown(0);
        #endif
        
        if (!clickPressed)
            return;
        
        // Find closest vertex to mouse click
        Ray ray = GetMouseRay();
        Vector3[] corners = targetCube.GetCorners();
        Transform cubeTransform = targetCube.transform;
        
        float pixelRadius = selectRadius; // Use directly as pixels
        float closestDist = float.MaxValue; // Start with max value
        Debug.Log($"[RuntimeVertexEditor] Using selection radius: {pixelRadius} pixels");
        int closestIndex = -1;
        
        for (int i = 0; i < 8; i++)
        {
            Vector3 worldPos = cubeTransform.TransformPoint(corners[i]);
            Vector3 screenPos = editCamera.WorldToScreenPoint(worldPos);
            
            #if ENABLE_INPUT_SYSTEM
            Vector2 mousePos = Mouse.current?.position.ReadValue() ?? Vector2.zero;
            #else
            Vector2 mousePos = Input.mousePosition;
            #endif
            
            // Skip vertices behind camera
            if (screenPos.z < 0)
            {
                if (i == 0) Debug.Log($"[RuntimeVertexEditor] Vertex {i} is behind camera, skipping");
                continue;
            }
            
            float dist = Vector2.Distance(new Vector2(screenPos.x, screenPos.y), mousePos);
            
            // Debug
            if (i == 0) Debug.Log($"[RuntimeVertexEditor] Click at ({mousePos.x:F0},{mousePos.y:F0}), vertex 0 at ({screenPos.x:F0},{screenPos.y:F0}), distance: {dist:F0}, threshold: {pixelRadius:F0}");
            
            if (dist < closestDist && dist < pixelRadius)
            {
                closestDist = dist;
                closestIndex = i;
            }
        }
        
        if (closestIndex >= 0)
        {
            Debug.Log($"[RuntimeVertexEditor] Found vertex {closestIndex} at distance {closestDist}");
            SelectVertex(closestIndex);
        }
        else
        {
            Debug.Log("[RuntimeVertexEditor] No vertex close enough to click");
            Deselect();
        }
    }
    
    void HandleVertexDrag()
    {
        if (selectedCornerIndex < 0 || editCamera == null)
            return;
        
        bool isDragging = false;
        
        #if ENABLE_INPUT_SYSTEM
        isDragging = Mouse.current?.leftButton.isPressed ?? false;
        #else
        isDragging = Input.GetMouseButton(0);
        #endif
        
        if (!isDragging)
        {
            Debug.Log($"[RuntimeVertexEditor] Released vertex {selectedCornerIndex}");
            Deselect();
            return;
        }
        
        // Project mouse ray onto drag plane
        Ray ray = GetMouseRay();
        float distance;
        
        if (dragPlane.Raycast(ray, out distance))
        {
            Vector3 worldPoint = ray.GetPoint(distance);
            Vector3 localPoint = targetCube.transform.InverseTransformPoint(worldPoint);
            
            Debug.Log($"[RuntimeVertexEditor] Dragging vertex {selectedCornerIndex} to local: {localPoint}");
            
            targetCube.SetCorner(selectedCornerIndex, localPoint);
        }
        else
        {
            Debug.LogWarning("[RuntimeVertexEditor] Drag plane raycast failed!");
        }
    }
    
    void SelectVertex(int index)
    {
        Deselect(); // Clear previous selection
        
        selectedCornerIndex = index;
        
        // Highlight the sphere
        Transform vertsRoot = targetCube.transform.Find("_CubeVertices");
        if (vertsRoot != null)
        {
            Transform sphere = vertsRoot.Find($"corner_{index}");
            if (sphere != null)
            {
                selectedRenderer = sphere.GetComponent<Renderer>();
                if (selectedRenderer != null)
                {
                    originalColor = selectedRenderer.material.color;
                    selectedRenderer.material.color = selectedColor;
                }
            }
        }
        
        // Setup drag plane perpendicular to camera view
        Vector3[] corners = targetCube.GetCorners();
        Vector3 worldPos = targetCube.transform.TransformPoint(corners[index]);
        Vector3 cameraForward = editCamera.transform.forward;
        dragPlane = new Plane(cameraForward, worldPos);
        
        Debug.Log($"[RuntimeVertexEditor] Selected corner {index} at world position {worldPos}");
        Debug.Log($"[RuntimeVertexEditor] Drag plane: normal={cameraForward}, point={worldPos}");
    }
    
    void Deselect()
    {
        if (selectedRenderer != null)
        {
            selectedRenderer.material.color = originalColor;
            selectedRenderer = null;
        }
        selectedCornerIndex = -1;
    }
    
    Ray GetMouseRay()
    {
        if (editCamera == null)
            return new Ray(Vector3.zero, Vector3.forward);
        
        #if ENABLE_INPUT_SYSTEM
        Vector2 mousePos = Mouse.current?.position.ReadValue() ?? Vector2.zero;
        #else
        Vector2 mousePos = Input.mousePosition;
        #endif
        
        return editCamera.ScreenPointToRay(mousePos);
    }
    
    void OnGUI()
    {
        if (targetCube == null || editCamera == null)
            return;
        
        bool inVertexMode = (targetCube.mode == EditableCube.DisplayMode.Vertices);
        
        // Show instructions
        if (showInstructions)
        {
            GUIStyle boxStyle = new GUIStyle(GUI.skin.box);
            boxStyle.normal.textColor = Color.white;
            boxStyle.fontSize = 16;
            boxStyle.alignment = TextAnchor.UpperLeft;
            
            string instructions = inVertexMode ? 
                "Vertex Mode: ON\nClick near a vertex to select\nDrag to move\nPress Tab to exit" :
                "Press Tab to enable vertex editing";
            
            GUI.Box(new Rect(10, 10, 300, inVertexMode ? 100 : 50), instructions, boxStyle);
            
            if (selectedCornerIndex >= 0)
            {
                GUI.Box(new Rect(10, inVertexMode ? 120 : 70, 200, 30), $"Selected: Corner {selectedCornerIndex}", boxStyle);
            }
        }
        
        // Draw vertex labels in vertex mode
        if (inVertexMode)
        {
            Vector3[] corners = targetCube.GetCorners();
            Transform cubeTransform = targetCube.transform;
            
            GUIStyle labelStyle = new GUIStyle(GUI.skin.label);
            labelStyle.fontSize = 14;
            labelStyle.fontStyle = FontStyle.Bold;
            labelStyle.alignment = TextAnchor.MiddleCenter;
            
            GUIStyle shadowStyle = new GUIStyle(labelStyle);
            shadowStyle.normal.textColor = Color.black;
            
            for (int i = 0; i < 8; i++)
            {
                Vector3 worldPos = cubeTransform.TransformPoint(corners[i]);
                Vector3 screenPos = editCamera.WorldToScreenPoint(worldPos);
                
                // Only draw if in front of camera
                if (screenPos.z > 0)
                {
                    // Convert to GUI coordinates (flip Y)
                    Vector2 guiPos = new Vector2(screenPos.x, Screen.height - screenPos.y);
                    
                    // Offset for centering
                    Rect labelRect = new Rect(guiPos.x - 20, guiPos.y - 25, 40, 20);
                    Rect shadowRect = new Rect(guiPos.x - 19, guiPos.y - 24, 40, 20);
                    
                    // Choose color based on selection
                    if (i == selectedCornerIndex)
                    {
                        labelStyle.normal.textColor = selectedColor;
                    }
                    else
                    {
                        labelStyle.normal.textColor = normalColor;
                    }
                    
                    // Draw circle background
                    Color circleColor = (i == selectedCornerIndex) ? selectedColor : Color.white;
                    circleColor.a = 0.5f;
                    DrawCircle(guiPos, 15f, circleColor);
                    
                    // Draw shadow for readability
                    GUI.Label(shadowRect, $"V{i}", shadowStyle);
                    // Draw label
                    GUI.Label(labelRect, $"V{i}", labelStyle);
                }
            }
        }
    }
    
    void DrawCircle(Vector2 center, float radius, Color color)
    {
        int segments = 20;
        Vector2 prevPoint = center + new Vector2(radius, 0);
        
        for (int i = 1; i <= segments; i++)
        {
            float angle = (i / (float)segments) * Mathf.PI * 2f;
            Vector2 newPoint = center + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * radius;
            
            // Draw line segment (approximation)
            DrawLine(prevPoint, newPoint, color, 2f);
            prevPoint = newPoint;
        }
    }
    
    void DrawLine(Vector2 start, Vector2 end, Color color, float width)
    {
        // Simple line drawing using GUI.DrawTexture
        Vector2 diff = end - start;
        float length = diff.magnitude;
        float angle = Mathf.Atan2(diff.y, diff.x) * Mathf.Rad2Deg;
        
        GUIUtility.RotateAroundPivot(angle, start);
        
        Color oldColor = GUI.color;
        GUI.color = color;
        GUI.DrawTexture(new Rect(start.x, start.y - width/2, length, width), Texture2D.whiteTexture);
        GUI.color = oldColor;
        
        GUIUtility.RotateAroundPivot(-angle, start);
    }
}

