using UnityEngine;
using System.Collections.Generic;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

/// <summary>
/// Runtime editor for EditableMesh - works with any imported mesh.
/// Press Tab to toggle Mesh/Vertex modes.
/// Click and drag vertices to edit in Play mode.
/// 
/// IMPORTANT: This should be on a SEPARATE manager GameObject (like "MeshEditor" or "UIManager"),
/// NOT on the same GameObject as EditableMesh. If it's on the EditableMesh GameObject and that
/// object gets disabled, vertex editing will break.
/// </summary>
public class RuntimeMeshEditor : MonoBehaviour
{
    [Header("References")]
    [Tooltip("LEGACY: Leave empty if using auto-selection. Should be on separate GameObject from EditableMesh!")]
    public EditableMesh targetMesh; // Legacy: set this manually OR use auto-selection
    public Camera editCamera;
    public DesktopCameraController cameraController;
    public ObjectSelector objectSelector;
    
    [Header("Auto-Selection")]
    public bool useSelectedObject = true; // Use ObjectSelector's selected object instead of fixed targetMesh
    
    [Header("Settings")]
    [Tooltip("Auto-detect crosshair mode based on cursor lock state")]
    public bool autoDetectCrosshairMode = true;
    [Tooltip("Manual override: Use crosshair at screen center (only if autoDetect is false)")]
    public bool useCrosshairSelection = true;
    [Tooltip("Selection radius in screen pixels")]
    public float selectRadius = 500f;
    public Color selectedColor = Color.yellow;
    public Color normalColor = Color.white;
    [Tooltip("Disable mouse look in edit mode (keeps WASD movement)")]
    public bool disableMouseLookInEditMode = true;
    public bool showLabels = true;
    
    [Header("Mesh Transformation")]
    [Tooltip("Keys to toggle transformation modes")]
    public KeyCode translateKey = KeyCode.Z;
    public KeyCode rotateKey = KeyCode.X;
    public KeyCode scaleKey = KeyCode.C;
    public Color transformModeColor = Color.green;
    public float rotationSensitivity = 0.5f;
    public float scaleSensitivity = 0.002f;
    
    [Header("Visual Aids")]
    public bool showEdges = true;
    public Color edgeColor = Color.cyan;
    public bool showInGameView = true; // Show edges in Game view, not just Scene view
    
    // State
    private enum TransformMode { Vertices, Translate, Rotate, Scale }
    private TransformMode currentTransformMode = TransformMode.Vertices;
    
    private EditableMesh.DisplayMode lastMode;
    private int selectedVertexIndex = -1;
    private Renderer selectedRenderer;
    private Color originalColor;
    private Plane dragPlane;
    private bool isDraggingTransform = false;
    private Vector3 transformStartPos;
    private Quaternion transformStartRot;
    private Vector3 transformStartScale;
    private Vector2 transformStartMousePos;
    private bool wasInMouseLookMode = false; // Track if we were in mouse look before transform
    
    void Awake()
    {
        // Warn if RuntimeMeshEditor is on the same GameObject as an EditableMesh
        EditableMesh meshOnSameObject = GetComponent<EditableMesh>();
        if (meshOnSameObject != null)
        {
            Debug.LogWarning($"[RuntimeMeshEditor] WARNING: RuntimeMeshEditor is on the same GameObject as EditableMesh ({gameObject.name}). " +
                           "This will cause vertex editing to break if the EditableMesh GameObject is disabled. " +
                           "Move RuntimeMeshEditor to a separate manager GameObject (like 'MeshEditor' or 'UIManager').");
        }
        
        Debug.Log("[RuntimeMeshEditor] Initialized");
    }
    
    void Start()
    {
        Invoke(nameof(InitializeAfterSetup), 0.1f);
    }
    
    void InitializeAfterSetup()
    {
        UpdateEditCamera();
        FindComponents();
        
        if (targetMesh != null)
            lastMode = targetMesh.mode;
    }
    
    void UpdateEditCamera()
    {
        Camera activeCamera = null;
        
        // Prioritize DesktopCamera
        GameObject desktopCameraGO = GameObject.Find("DesktopCamera");
        if (desktopCameraGO != null && desktopCameraGO.activeInHierarchy)
        {
            Camera cam = desktopCameraGO.GetComponent<Camera>();
            if (cam != null && cam.enabled)
                activeCamera = cam;
        }
        
        // Fallback to any active camera
        if (activeCamera == null)
        {
            Camera[] cameras = FindObjectsByType<Camera>(FindObjectsSortMode.None);
            foreach (Camera cam in cameras)
            {
                if (cam.enabled && cam.gameObject.activeInHierarchy)
                {
                    activeCamera = cam;
                    break;
                }
            }
        }
        
        editCamera = activeCamera != null ? activeCamera : Camera.main;
        Debug.Log($"[RuntimeMeshEditor] Using camera: {(editCamera != null ? editCamera.name : "NULL")}");
    }
    
    void FindComponents()
    {
        if (targetMesh == null && !useSelectedObject)
            targetMesh = FindAnyObjectByType<EditableMesh>();
        
        if (cameraController == null)
            cameraController = FindFirstObjectByType<DesktopCameraController>(FindObjectsInactive.Include);
        
        if (objectSelector == null)
            objectSelector = FindAnyObjectByType<ObjectSelector>();
    }
    
    void UpdateTargetMesh()
    {
        if (!useSelectedObject)
            return;
        
        // Find ObjectSelector if we don't have it
        if (objectSelector == null)
        {
            objectSelector = FindAnyObjectByType<ObjectSelector>();
            if (objectSelector == null)
            {
                Debug.LogWarning("[RuntimeMeshEditor] No ObjectSelector found! Vertex editing requires ObjectSelector.");
                return;
            }
        }
        
        // Use ObjectSelector's selected object if available
        Transform selected = objectSelector.GetCurrentSelection();
        if (selected != null)
        {
            EditableMesh mesh = selected.GetComponent<EditableMesh>();
            if (mesh != null && mesh != targetMesh)
            {
                // Switched to a different mesh
                Deselect(); // Clear previous vertex selection
                targetMesh = mesh;
                lastMode = mesh.mode;
                Debug.Log($"[RuntimeMeshEditor] Now editing: {mesh.gameObject.name}");
            }
        }
        else if (targetMesh == null)
        {
            // No selection and no target mesh - try to find any active EditableMesh
            EditableMesh anyMesh = FindAnyObjectByType<EditableMesh>();
            if (anyMesh != null)
            {
                targetMesh = anyMesh;
                lastMode = anyMesh.mode;
                Debug.Log($"[RuntimeMeshEditor] Auto-selected first mesh: {anyMesh.gameObject.name}");
            }
        }
    }
    
    void Update()
    {
        // Always update camera and components in case they change
        if (editCamera == null || !editCamera.gameObject.activeInHierarchy || !editCamera.enabled)
        {
            UpdateEditCamera();
        }
        
        // Find components if missing (they might be on disabled objects)
        if (objectSelector == null || cameraController == null)
        {
            FindComponents();
        }
        
        // Update target mesh based on selection
        UpdateTargetMesh();
        
        // Exit early if no target mesh
        if (targetMesh == null)
        {
            // Try one more time to find a mesh if useSelectedObject is false
            if (!useSelectedObject)
            {
                targetMesh = FindAnyObjectByType<EditableMesh>();
            }
            if (targetMesh == null)
                return;
        }
        
        // Update camera if needed (double-check after finding mesh)
        if (editCamera == null || !editCamera.enabled)
            UpdateEditCamera();
        
        // Check mode changes
        if (targetMesh.mode != lastMode)
        {
            OnModeChanged(targetMesh.mode);
            lastMode = targetMesh.mode;
        }
        
        // Handle transform mode toggle (available in both Object and Edit modes)
        HandleTransformModeToggle();
        
        // Handle different transform modes
        switch (currentTransformMode)
        {
            case TransformMode.Translate:
                HandleMeshTranslation();
                break;
            case TransformMode.Rotate:
                HandleMeshRotation();
                break;
            case TransformMode.Scale:
                HandleMeshScale();
                break;
            case TransformMode.Vertices:
            default:
                // Vertex editing only available in Edit mode
                if (targetMesh.mode == EditableMesh.DisplayMode.Edit)
                {
                    HandleVertexSelection();
                    HandleVertexDrag();
                }
                break;
        }
    }
    
    void OnModeChanged(EditableMesh.DisplayMode newMode)
    {
        bool isEditMode = (newMode == EditableMesh.DisplayMode.Edit);
        
        if (isEditMode)
        {
            // Don't touch cursor state - let DesktopCameraController handle it
            // User can toggle mouse look with Alt to use crosshair for vertex selection
            Debug.Log("[RuntimeMeshEditor] Edit mode: Vertex editing enabled, Z/X/C for transform");
        }
        else
        {
            // Exiting Edit mode - clear vertex selection but keep M/R/S modes available
            Deselect();
            isDraggingTransform = false;
            Debug.Log("[RuntimeMeshEditor] Object mode: Z/X/C available for whole-mesh transform");
        }
    }
    
    void HandleTransformModeToggle()
    {
        bool translatePressed = false;
        bool rotatePressed = false;
        bool scalePressed = false;
        bool escPressed = false;
        
        #if ENABLE_INPUT_SYSTEM
        Keyboard keyboard = Keyboard.current;
        if (keyboard != null)
        {
            translatePressed = keyboard.zKey.wasPressedThisFrame;
            rotatePressed = keyboard.xKey.wasPressedThisFrame;
            scalePressed = keyboard.cKey.wasPressedThisFrame;
            escPressed = keyboard.escapeKey.wasPressedThisFrame;
        }
        #else
        translatePressed = Input.GetKeyDown(translateKey);
        rotatePressed = Input.GetKeyDown(rotateKey);
        scalePressed = Input.GetKeyDown(scaleKey);
        escPressed = Input.GetKeyDown(KeyCode.Escape);
        #endif
        
        TransformMode newMode = currentTransformMode;
        
        if (translatePressed)
        {
            newMode = (currentTransformMode == TransformMode.Translate) ? TransformMode.Vertices : TransformMode.Translate;
        }
        else if (rotatePressed)
        {
            newMode = (currentTransformMode == TransformMode.Rotate) ? TransformMode.Vertices : TransformMode.Rotate;
        }
        else if (scalePressed)
        {
            newMode = (currentTransformMode == TransformMode.Scale) ? TransformMode.Vertices : TransformMode.Scale;
        }
        else if (escPressed && currentTransformMode != TransformMode.Vertices)
        {
            newMode = TransformMode.Vertices;
        }
        
        if (newMode != currentTransformMode)
        {
            currentTransformMode = newMode;
            Deselect(); // Clear vertex selection when switching modes
            isDraggingTransform = false;
            
            string modeName = currentTransformMode switch
            {
                TransformMode.Translate => "TRANSLATE",
                TransformMode.Rotate => "ROTATE",
                TransformMode.Scale => "SCALE",
                _ => "VERTICES"
            };
            Debug.Log($"[RuntimeMeshEditor] Switched to {modeName} mode");
        }
    }
    
    void HandleMeshTranslation()
    {
        if (editCamera == null)
            return;
        
        bool clickPressed = false;
        bool clickHeld = false;
        bool clickReleased = false;
        
        #if ENABLE_INPUT_SYSTEM
        Mouse mouse = Mouse.current;
        if (mouse != null)
        {
            clickPressed = mouse.leftButton.wasPressedThisFrame;
            clickHeld = mouse.leftButton.isPressed;
            clickReleased = mouse.leftButton.wasReleasedThisFrame;
        }
        #else
        clickPressed = Input.GetMouseButtonDown(0);
        clickHeld = Input.GetMouseButton(0);
        clickReleased = Input.GetMouseButtonUp(0);
        #endif
        
        if (clickPressed && !isDraggingTransform)
        {
            isDraggingTransform = true;
            transformStartPos = targetMesh.transform.position;
            
            // Unlock cursor for mouse transformation
            wasInMouseLookMode = (Cursor.lockState == CursorLockMode.Locked);
            if (wasInMouseLookMode)
            {
                Cursor.lockState = CursorLockMode.None;
                Cursor.visible = true;
            }
            
            #if ENABLE_INPUT_SYSTEM
            Vector2 mousePos = Mouse.current?.position.ReadValue() ?? Vector2.zero;
            #else
            Vector2 mousePos = Input.mousePosition;
            #endif
            transformStartMousePos = mousePos;
        }
        
        if (clickReleased && isDraggingTransform)
        {
            isDraggingTransform = false;
            
            // Re-lock cursor if we were in mouse look mode
            if (wasInMouseLookMode)
            {
                Cursor.lockState = CursorLockMode.Locked;
                Cursor.visible = false;
            }
        }
        
        if (isDraggingTransform && clickHeld)
        {
            #if ENABLE_INPUT_SYSTEM
            Vector2 currentMousePos = Mouse.current?.position.ReadValue() ?? Vector2.zero;
            #else
            Vector2 currentMousePos = Input.mousePosition;
            #endif
            
            Vector2 mouseDelta = currentMousePos - transformStartMousePos;
            
            Vector3 right = editCamera.transform.right;
            Vector3 up = editCamera.transform.up;
            
            float distance = Vector3.Distance(editCamera.transform.position, targetMesh.transform.position);
            float sensitivity = distance * 0.001f;
            
            Vector3 worldDelta = (right * mouseDelta.x + up * mouseDelta.y) * sensitivity;
            targetMesh.transform.position = transformStartPos + worldDelta;
        }
    }
    
    void HandleMeshRotation()
    {
        if (editCamera == null)
            return;
        
        bool clickPressed = false;
        bool clickHeld = false;
        bool clickReleased = false;
        
        #if ENABLE_INPUT_SYSTEM
        Mouse mouse = Mouse.current;
        if (mouse != null)
        {
            clickPressed = mouse.leftButton.wasPressedThisFrame;
            clickHeld = mouse.leftButton.isPressed;
            clickReleased = mouse.leftButton.wasReleasedThisFrame;
        }
        #else
        clickPressed = Input.GetMouseButtonDown(0);
        clickHeld = Input.GetMouseButton(0);
        clickReleased = Input.GetMouseButtonUp(0);
        #endif
        
        if (clickPressed && !isDraggingTransform)
        {
            isDraggingTransform = true;
            transformStartRot = targetMesh.transform.rotation;
            
            // Unlock cursor for mouse transformation
            wasInMouseLookMode = (Cursor.lockState == CursorLockMode.Locked);
            if (wasInMouseLookMode)
            {
                Cursor.lockState = CursorLockMode.None;
                Cursor.visible = true;
            }
            
            #if ENABLE_INPUT_SYSTEM
            Vector2 mousePos = Mouse.current?.position.ReadValue() ?? Vector2.zero;
            #else
            Vector2 mousePos = Input.mousePosition;
            #endif
            transformStartMousePos = mousePos;
        }
        
        if (clickReleased && isDraggingTransform)
        {
            isDraggingTransform = false;
            
            // Re-lock cursor if we were in mouse look mode
            if (wasInMouseLookMode)
            {
                Cursor.lockState = CursorLockMode.Locked;
                Cursor.visible = false;
            }
        }
        
        if (isDraggingTransform && clickHeld)
        {
            #if ENABLE_INPUT_SYSTEM
            Vector2 currentMousePos = Mouse.current?.position.ReadValue() ?? Vector2.zero;
            #else
            Vector2 currentMousePos = Input.mousePosition;
            #endif
            
            Vector2 mouseDelta = currentMousePos - transformStartMousePos;
            
            float yawDelta = mouseDelta.x * rotationSensitivity;
            float pitchDelta = -mouseDelta.y * rotationSensitivity;
            
            Quaternion yawRotation = Quaternion.AngleAxis(yawDelta, Vector3.up);
            Quaternion pitchRotation = Quaternion.AngleAxis(pitchDelta, editCamera.transform.right);
            
            targetMesh.transform.rotation = yawRotation * pitchRotation * transformStartRot;
        }
    }
    
    void HandleMeshScale()
    {
        if (editCamera == null)
            return;
        
        bool clickPressed = false;
        bool clickHeld = false;
        bool clickReleased = false;
        
        #if ENABLE_INPUT_SYSTEM
        Mouse mouse = Mouse.current;
        if (mouse != null)
        {
            clickPressed = mouse.leftButton.wasPressedThisFrame;
            clickHeld = mouse.leftButton.isPressed;
            clickReleased = mouse.leftButton.wasReleasedThisFrame;
        }
        #else
        clickPressed = Input.GetMouseButtonDown(0);
        clickHeld = Input.GetMouseButton(0);
        clickReleased = Input.GetMouseButtonUp(0);
        #endif
        
        if (clickPressed && !isDraggingTransform)
        {
            isDraggingTransform = true;
            transformStartScale = targetMesh.transform.localScale;
            
            // Unlock cursor for mouse transformation
            wasInMouseLookMode = (Cursor.lockState == CursorLockMode.Locked);
            if (wasInMouseLookMode)
            {
                Cursor.lockState = CursorLockMode.None;
                Cursor.visible = true;
            }
            
            #if ENABLE_INPUT_SYSTEM
            Vector2 mousePos = Mouse.current?.position.ReadValue() ?? Vector2.zero;
            #else
            Vector2 mousePos = Input.mousePosition;
            #endif
            transformStartMousePos = mousePos;
        }
        
        if (clickReleased && isDraggingTransform)
        {
            isDraggingTransform = false;
            
            // Re-lock cursor if we were in mouse look mode
            if (wasInMouseLookMode)
            {
                Cursor.lockState = CursorLockMode.Locked;
                Cursor.visible = false;
            }
        }
        
        if (isDraggingTransform && clickHeld)
        {
            #if ENABLE_INPUT_SYSTEM
            Vector2 currentMousePos = Mouse.current?.position.ReadValue() ?? Vector2.zero;
            #else
            Vector2 currentMousePos = Input.mousePosition;
            #endif
            
            Vector2 mouseDelta = currentMousePos - transformStartMousePos;
            
            float scaleDelta = mouseDelta.y * scaleSensitivity;
            float scaleMultiplier = 1.0f + scaleDelta;
            scaleMultiplier = Mathf.Max(scaleMultiplier, 0.01f);
            
            Vector3 newScale = transformStartScale * scaleMultiplier;
            targetMesh.transform.localScale = newScale;
        }
    }
    
    void HandleVertexSelection()
    {
        if (editCamera == null)
            return;
        
        bool clickPressed = false;
        
        #if ENABLE_INPUT_SYSTEM
        clickPressed = Mouse.current?.leftButton.wasPressedThisFrame ?? false;
        #else
        clickPressed = Input.GetMouseButtonDown(0);
        #endif
        
        if (!clickPressed)
            return;
        
        // Auto-detect mode based on cursor state, or use manual setting
        bool useCrosshair = autoDetectCrosshairMode ? 
            (Cursor.lockState == CursorLockMode.Locked) : 
            useCrosshairSelection;
        
        if (useCrosshair)
        {
            HandleRaycastSelection();
        }
        else
        {
            HandleMouseSelection();
        }
    }
    
    void HandleRaycastSelection()
    {
        // Use screen center (crosshair position) as "mouse" position
        Vector2 crosshairPos = new Vector2(Screen.width / 2f, Screen.height / 2f);
        
        Vector3[] vertices = targetMesh.GetVertices();
        Transform meshTransform = targetMesh.transform;
        
        float closestDist = float.MaxValue;
        int closestIndex = -1;
        
        for (int i = 0; i < vertices.Length; i++)
        {
            Vector3 worldPos = meshTransform.TransformPoint(vertices[i]);
            Vector3 screenPos = editCamera.WorldToScreenPoint(worldPos);
            
            if (screenPos.z < 0) continue; // Behind camera
            
            float dist = Vector2.Distance(new Vector2(screenPos.x, screenPos.y), crosshairPos);
            
            if (dist < selectRadius && dist < closestDist)
            {
                closestDist = dist;
                closestIndex = i;
            }
        }
        
        if (closestIndex >= 0)
        {
            SelectVertex(closestIndex);
        }
        else
        {
            Deselect();
        }
    }
    
    void HandleMouseSelection()
    {
        // Original screen-space selection
        Vector3[] vertices = targetMesh.GetVertices();
        Transform meshTransform = targetMesh.transform;
        
        float closestDist = float.MaxValue;
        int closestIndex = -1;
        
        #if ENABLE_INPUT_SYSTEM
        Vector2 mousePos = Mouse.current?.position.ReadValue() ?? Vector2.zero;
        #else
        Vector2 mousePos = Input.mousePosition;
        #endif
        
        for (int i = 0; i < vertices.Length; i++)
        {
            Vector3 worldPos = meshTransform.TransformPoint(vertices[i]);
            Vector3 screenPos = editCamera.WorldToScreenPoint(worldPos);
            
            if (screenPos.z < 0) continue; // Behind camera
            
            float dist = Vector2.Distance(new Vector2(screenPos.x, screenPos.y), mousePos);
            
            if (dist < selectRadius && dist < closestDist)
            {
                closestDist = dist;
                closestIndex = i;
            }
        }
        
        if (closestIndex >= 0)
        {
            SelectVertex(closestIndex);
        }
        else
        {
            Deselect();
        }
    }
    
    void HandleVertexDrag()
    {
        if (selectedVertexIndex < 0 || editCamera == null)
            return;
        
        bool isDragging = false;
        
        #if ENABLE_INPUT_SYSTEM
        isDragging = Mouse.current?.leftButton.isPressed ?? false;
        #else
        isDragging = Input.GetMouseButton(0);
        #endif
        
        if (!isDragging)
        {
            Deselect();
            return;
        }
        
        Ray ray = GetMouseRay();
        float distance;
        
        if (dragPlane.Raycast(ray, out distance))
        {
            Vector3 worldPoint = ray.GetPoint(distance);
            Vector3 localPoint = targetMesh.transform.InverseTransformPoint(worldPoint);
            targetMesh.SetVertex(selectedVertexIndex, localPoint);
        }
    }
    
    void SelectVertex(int index)
    {
        Deselect();
        selectedVertexIndex = index;
        
        // Highlight sphere
        Transform vertsRoot = targetMesh.transform.Find("_MeshVertices");
        if (vertsRoot != null)
        {
            Transform sphere = vertsRoot.Find($"vertex_{index}");
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
        
        // Setup drag plane
        Vector3[] vertices = targetMesh.GetVertices();
        Vector3 worldPos = targetMesh.transform.TransformPoint(vertices[index]);
        dragPlane = new Plane(editCamera.transform.forward, worldPos);
        
        Debug.Log($"[RuntimeMeshEditor] Selected vertex {index}");
    }
    
    void Deselect()
    {
        if (selectedRenderer != null)
        {
            selectedRenderer.material.color = originalColor;
            selectedRenderer = null;
        }
        selectedVertexIndex = -1;
    }
    
    Ray GetMouseRay()
    {
        if (editCamera == null)
            return new Ray(Vector3.zero, Vector3.forward);
        
        Vector2 screenPos;
        
        // Auto-detect mode based on cursor state, or use manual setting
        bool useCrosshair = autoDetectCrosshairMode ? 
            (Cursor.lockState == CursorLockMode.Locked) : 
            useCrosshairSelection;
        
        if (useCrosshair)
        {
            // Use crosshair at screen center
            screenPos = new Vector2(Screen.width / 2f, Screen.height / 2f);
        }
        else
        {
            // Use mouse cursor position
            #if ENABLE_INPUT_SYSTEM
            screenPos = Mouse.current?.position.ReadValue() ?? Vector2.zero;
            #else
            screenPos = Input.mousePosition;
            #endif
        }
        
        return editCamera.ScreenPointToRay(screenPos);
    }
    
    void OnGUI()
    {
        if (targetMesh == null || editCamera == null || !showLabels)
            return;
        
        bool inEditMode = (targetMesh.mode == EditableMesh.DisplayMode.Edit);
        
        if (!inEditMode)
            return;
        
        // Draw vertex labels
        Vector3[] vertices = targetMesh.GetVertices();
        Transform meshTransform = targetMesh.transform;
        
        GUIStyle labelStyle = new GUIStyle(GUI.skin.label);
        labelStyle.fontSize = 12;
        labelStyle.fontStyle = FontStyle.Bold;
        labelStyle.alignment = TextAnchor.MiddleCenter;
        
        for (int i = 0; i < vertices.Length; i++)
        {
            Vector3 worldPos = meshTransform.TransformPoint(vertices[i]);
            Vector3 screenPos = editCamera.WorldToScreenPoint(worldPos);
            
            if (screenPos.z > 0)
            {
                Vector2 guiPos = new Vector2(screenPos.x, Screen.height - screenPos.y);
                Rect labelRect = new Rect(guiPos.x - 15, guiPos.y - 20, 30, 20);
                
                labelStyle.normal.textColor = (i == selectedVertexIndex) ? selectedColor : normalColor;
                GUI.Label(labelRect, $"V{i}", labelStyle);
            }
        }
        
        // Instructions
        GUIStyle boxStyle = new GUIStyle(GUI.skin.box);
        boxStyle.normal.textColor = Color.white;
        boxStyle.fontSize = 14;
        
        string modeText = currentTransformMode switch
        {
            TransformMode.Translate => "TRANSLATE",
            TransformMode.Rotate => "ROTATE",
            TransformMode.Scale => "SCALE",
            _ => "VERTICES"
        };
        Color modeColor = (currentTransformMode != TransformMode.Vertices) ? transformModeColor : Color.white;
        
        GUIStyle modeStyle = new GUIStyle(GUI.skin.box);
        modeStyle.normal.textColor = modeColor;
        modeStyle.fontSize = 16;
        modeStyle.fontStyle = FontStyle.Bold;
        modeStyle.alignment = TextAnchor.MiddleCenter;
        
        GUI.Box(new Rect(10, 10, 250, 30), $"Mode: {modeText}", modeStyle);
        
        // Show current control mode
        bool isUsingCrosshair = autoDetectCrosshairMode ? 
            (Cursor.lockState == CursorLockMode.Locked) : 
            useCrosshairSelection;
        string selectionMode = isUsingCrosshair ? "Crosshair" : "Mouse Cursor";
        string instructions = $"Control: {selectionMode} (Alt to toggle)\nZ=Translate | X=Rotate | C=Scale\nWASD=Move | Q/E=Up/Down | Tab=Exit";
        GUI.Box(new Rect(10, 50, 350, 80), instructions, boxStyle);
        
        if (currentTransformMode == TransformMode.Vertices && selectedVertexIndex >= 0)
        {
            GUI.Box(new Rect(10, 120, 150, 30), $"Selected: V{selectedVertexIndex}", boxStyle);
        }
        
        if (currentTransformMode != TransformMode.Vertices && isDraggingTransform)
        {
            string dragText = currentTransformMode switch
            {
                TransformMode.Translate => "Translating...",
                TransformMode.Rotate => "Rotating...",
                TransformMode.Scale => "Scaling...",
                _ => ""
            };
            GUI.Box(new Rect(10, 120, 200, 30), dragText, boxStyle);
        }
    }
    
    void OnDrawGizmos()
    {
        if (!showEdges || targetMesh == null || showInGameView)
            return;
        
        bool inEditMode = (targetMesh.mode == EditableMesh.DisplayMode.Edit);
        if (!inEditMode)
            return;
        
        DrawMeshEdges();
    }
    
    void OnRenderObject()
    {
        if (!showEdges || !showInGameView || targetMesh == null)
            return;
        
        bool inEditMode = (targetMesh.mode == EditableMesh.DisplayMode.Edit);
        if (!inEditMode || !Application.isPlaying)
            return;
        
        // Use GL to draw lines in Game view
        DrawMeshEdgesGL();
    }
    
    void DrawMeshEdges()
    {
        // For Scene view (Gizmos)
        var edges = GetMeshEdges();
        if (edges == null)
            return;
        
        Transform meshTransform = targetMesh.transform;
        MeshFilter mf = meshTransform.GetComponentInChildren<MeshFilter>();
        if (mf == null || mf.sharedMesh == null)
            return;
        
        Vector3[] meshVerts = mf.sharedMesh.vertices;
        
        Gizmos.color = edgeColor;
        foreach (var edge in edges)
        {
            Vector3 start = meshTransform.TransformPoint(meshVerts[edge.Item1]);
            Vector3 end = meshTransform.TransformPoint(meshVerts[edge.Item2]);
            Gizmos.DrawLine(start, end);
        }
    }
    
    void DrawMeshEdgesGL()
    {
        // For Game view (GL rendering)
        var edges = GetMeshEdges();
        if (edges == null)
            return;
        
        Transform meshTransform = targetMesh.transform;
        MeshFilter mf = meshTransform.GetComponentInChildren<MeshFilter>();
        if (mf == null || mf.sharedMesh == null)
            return;
        
        Vector3[] meshVerts = mf.sharedMesh.vertices;
        
        // Create material if needed
        if (!lineMaterial)
        {
            Shader shader = Shader.Find("Hidden/Internal-Colored");
            lineMaterial = new Material(shader);
            lineMaterial.hideFlags = HideFlags.HideAndDontSave;
            lineMaterial.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
            lineMaterial.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
            lineMaterial.SetInt("_Cull", (int)UnityEngine.Rendering.CullMode.Off);
            lineMaterial.SetInt("_ZWrite", 0);
        }
        
        lineMaterial.SetPass(0);
        
        GL.PushMatrix();
        GL.MultMatrix(meshTransform.localToWorldMatrix);
        GL.Begin(GL.LINES);
        GL.Color(edgeColor);
        
        foreach (var edge in edges)
        {
            GL.Vertex(meshVerts[edge.Item1]);
            GL.Vertex(meshVerts[edge.Item2]);
        }
        
        GL.End();
        GL.PopMatrix();
    }
    
    HashSet<(int, int)> GetMeshEdges()
    {
        Transform meshTransform = targetMesh.transform;
        MeshFilter mf = meshTransform.GetComponentInChildren<MeshFilter>();
        
        if (mf == null || mf.sharedMesh == null)
            return null;
        
        Mesh mesh = mf.sharedMesh;
        int[] triangles = mesh.triangles;
        HashSet<(int, int)> edges = new HashSet<(int, int)>();
        
        // Extract ALL edges from ALL triangles
        for (int i = 0; i < triangles.Length; i += 3)
        {
            int v0 = triangles[i];
            int v1 = triangles[i + 1];
            int v2 = triangles[i + 2];
            
            // Add all 3 edges of this triangle
            AddEdge(edges, v0, v1);
            AddEdge(edges, v1, v2);
            AddEdge(edges, v2, v0);
        }
        
        return edges;
    }
    
    void AddEdge(HashSet<(int, int)> edges, int v0, int v1)
    {
        // Store in sorted order to avoid duplicates
        if (v0 > v1)
        {
            int temp = v0;
            v0 = v1;
            v1 = temp;
        }
        edges.Add((v0, v1));
    }
    
    private Material lineMaterial;
    
    void OnDestroy()
    {
        if (lineMaterial != null)
            DestroyImmediate(lineMaterial);
    }
}


