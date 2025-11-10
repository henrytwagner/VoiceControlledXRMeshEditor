using UnityEngine;
using System.Collections.Generic;
using UnityEngine.XR;
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
    public KeyCode rotateKey = KeyCode.R;
    public KeyCode scaleKey = KeyCode.C;
    public Color transformModeColor = Color.green;
    public float rotationSensitivity = 0.5f;
    public float scaleSensitivity = 0.002f;
    
    [Header("Visual Aids")]
    public bool showEdges = true;
    public Color edgeColor = Color.cyan;
    public float edgeLineWidth = 0.002f;
    public bool showInGameView = true; // Show edges in Game view, not just Scene view
    public bool showObjectLabels = true; // Show object name labels
    public Color objectLabelColor = Color.white;
    [Tooltip("Key to toggle labels on/off")]
    public KeyCode toggleLabelsKey = KeyCode.L;
    
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
    
    // 3D wireframe rendering
    private GameObject wireframeRoot;
    private List<LineRenderer> edgeLineRenderers = new List<LineRenderer>();
    private Material edgeMaterial;
    
    // 3D vertex labels
    private Dictionary<int, GameObject> vertexLabelObjects = new Dictionary<int, GameObject>();
    
    // 3D object labels
    private Dictionary<EditableMesh, GameObject> objectLabelObjects = new Dictionary<EditableMesh, GameObject>();
    
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
                isDraggingTransform = false; // Cancel any ongoing transform drag
                currentTransformMode = TransformMode.Vertices; // Reset to default mode
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
        
        // Update wireframe and labels based on XR mode
        bool isXR = XRSettings.isDeviceActive;
        if (targetMesh.mode == EditableMesh.DisplayMode.Edit)
        {
            if (isXR)
            {
                // XR mode: Use 3D LineRenderers and TextMesh
                UpdateWireframe3D();
                UpdateVertexLabels3D();
            }
            else
            {
                // Desktop mode: Use GL rendering (handled in OnRenderObject) and OnGUI labels
                CleanupWireframe();
                CleanupVertexLabels();
            }
        }
        else
        {
            CleanupWireframe();
            CleanupVertexLabels();
        }
        
        // Update object labels (works in both modes)
        if (showObjectLabels)
        {
            UpdateObjectLabels(isXR);
        }
        else
        {
            CleanupObjectLabels();
        }
        
        // Update billboard rotation for all labels every frame in XR mode
        if (isXR && (showLabels || showObjectLabels))
        {
            UpdateAllLabelBillboards();
        }
        
        // Handle transform mode toggle (available in both Object and Edit modes)
        HandleTransformModeToggle();
        
        // Handle label toggle
        HandleLabelToggle();
        
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
        }
        else
        {
            // Exiting Edit mode - clear vertex selection but keep M/R/S modes available
            Deselect();
            isDraggingTransform = false;
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
        
        ApplyTransformModeChange(newMode);
    }

    void ApplyTransformModeChange(TransformMode newMode)
    {
        if (newMode == currentTransformMode)
            return;

        currentTransformMode = newMode;
        Deselect(); // Clear vertex selection when switching modes
        isDraggingTransform = false;
    }

    public void SetVertexMode()
    {
        ApplyTransformModeChange(TransformMode.Vertices);
    }

    public void ToggleTranslateMode()
    {
        TransformMode newMode = (currentTransformMode == TransformMode.Translate) ? TransformMode.Vertices : TransformMode.Translate;
        ApplyTransformModeChange(newMode);
    }

    public void ToggleRotateMode()
    {
        TransformMode newMode = (currentTransformMode == TransformMode.Rotate) ? TransformMode.Vertices : TransformMode.Rotate;
        ApplyTransformModeChange(newMode);
    }

    public void ToggleScaleMode()
    {
        TransformMode newMode = (currentTransformMode == TransformMode.Scale) ? TransformMode.Vertices : TransformMode.Scale;
        ApplyTransformModeChange(newMode);
    }
    
    void HandleLabelToggle()
    {
        bool labelKeyPressed = false;
        
        #if ENABLE_INPUT_SYSTEM
        Keyboard keyboard = Keyboard.current;
        if (keyboard != null)
        {
            labelKeyPressed = keyboard.lKey.wasPressedThisFrame;
        }
        #else
        labelKeyPressed = Input.GetKeyDown(toggleLabelsKey);
        #endif
        
        if (labelKeyPressed)
        {
            ToggleLabels();
        }
    }
    
    public void ToggleLabels()
    {
        showLabels = !showLabels;
        showObjectLabels = !showObjectLabels;
        if (!showLabels)
            CleanupVertexLabels();
        if (!showObjectLabels)
            CleanupObjectLabels();
        Debug.Log($"[RuntimeMeshEditor] Labels: {(showLabels ? "ON" : "OFF")}");
    }
    
    public void SetLabels(bool enabled)
    {
        showLabels = enabled;
        showObjectLabels = enabled;
        if (!showLabels)
            CleanupVertexLabels();
        if (!showObjectLabels)
            CleanupObjectLabels();
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
        if (editCamera == null)
            return;
        
        bool inEditMode = targetMesh != null && (targetMesh.mode == EditableMesh.DisplayMode.Edit);
        
        // In desktop mode, draw vertex labels using OnGUI (screen space) - only in edit mode
        // In XR mode, labels are 3D TextMesh (handled by UpdateVertexLabels3D)
        bool isXR = XRSettings.isDeviceActive;
        if (!isXR && showLabels && inEditMode && targetMesh != null)
        {
            DrawVertexLabelsGUI();
        }
        
        // In desktop mode, draw object labels using OnGUI - always show if enabled
        if (!isXR && showObjectLabels)
        {
            DrawObjectLabelsGUI();
        }
        
        // Draw UI instructions - only in edit mode
        if (inEditMode && targetMesh != null)
        {
            DrawUIInstructions();
        }
    }
    
    void DrawVertexLabelsGUI()
    {
        // Draw vertex labels in screen space (desktop mode only)
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
    }
    
    void DrawObjectLabelsGUI()
    {
        // Draw object name labels in screen space (desktop mode only)
        EditableMesh[] allMeshes = FindObjectsByType<EditableMesh>(FindObjectsSortMode.None);
        Transform selectedTransform = objectSelector != null ? objectSelector.GetCurrentSelection() : null;
        
        GUIStyle labelStyle = new GUIStyle(GUI.skin.label);
        labelStyle.fontSize = 14;
        labelStyle.fontStyle = FontStyle.Bold;
        labelStyle.alignment = TextAnchor.MiddleCenter;
        
        foreach (EditableMesh mesh in allMeshes)
        {
            if (mesh == null || !mesh.gameObject.activeInHierarchy)
                continue;
            
            // Skip labels while the object is in Edit mode
            if (mesh.mode == EditableMesh.DisplayMode.Edit)
                continue;
            
            bool isSelected = selectedTransform != null &&
                (selectedTransform == mesh.transform || selectedTransform.IsChildOf(mesh.transform));
            Color labelColor = isSelected ? mesh.originColorSelected : objectLabelColor;
            labelStyle.normal.textColor = labelColor;
            
            // Position label at object's origin
            Vector3 worldPos = mesh.transform.position;
            Vector3 screenPos = editCamera.WorldToScreenPoint(worldPos);
            
            if (screenPos.z > 0)
            {
                Vector2 guiPos = new Vector2(screenPos.x, Screen.height - screenPos.y);
                string objectName = mesh.gameObject.name;
                
                // Calculate label size based on text
                Vector2 labelSize = labelStyle.CalcSize(new GUIContent(objectName));
                Rect labelRect = new Rect(guiPos.x - labelSize.x / 2, guiPos.y - labelSize.y / 2, labelSize.x, labelSize.y);
                
                GUI.Label(labelRect, objectName, labelStyle);
            }
        }
    }
    
    void DrawUIInstructions()
    {
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
        
        // Offset UI to avoid menu bar (menuHeight = 30, add 10px spacing = 40)
        float uiOffsetY = 40f;
        
        GUI.Box(new Rect(10, uiOffsetY, 250, 30), $"Mode: {modeText}", modeStyle);
        
        // Show current control mode
        bool isUsingCrosshair = autoDetectCrosshairMode ? 
            (Cursor.lockState == CursorLockMode.Locked) : 
            useCrosshairSelection;
        string selectionMode = isUsingCrosshair ? "Crosshair" : "Mouse Cursor";
        string instructions = $"Control: {selectionMode} (Alt to toggle)\nZ=Translate | X=Rotate | C=Scale\nWASD=Move | Q/E=Up/Down | Tab=Exit";
        GUI.Box(new Rect(10, uiOffsetY + 40, 350, 80), instructions, boxStyle);
        
        if (currentTransformMode == TransformMode.Vertices && selectedVertexIndex >= 0)
        {
            GUI.Box(new Rect(10, uiOffsetY + 130, 150, 30), $"Selected: V{selectedVertexIndex}", boxStyle);
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
            GUI.Box(new Rect(10, uiOffsetY + 130, 200, 30), dragText, boxStyle);
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
        // Only use GL rendering in desktop mode (not XR)
        if (XRSettings.isDeviceActive)
            return;
            
        if (!showEdges || !showInGameView || targetMesh == null)
            return;
        
        bool inEditMode = (targetMesh.mode == EditableMesh.DisplayMode.Edit);
        if (!inEditMode || !Application.isPlaying)
            return;
        
        // Use GL to draw lines in Game view (desktop mode only)
        // Original working code - no camera parameter needed
        DrawMeshEdgesGL();
    }
    
    void DrawMeshEdgesGL()
    {
        // For Game view (GL rendering) - desktop mode only
        // Original working code restored
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
    
    void UpdateWireframe3D()
    {
        if (!showEdges || !showInGameView || targetMesh == null)
        {
            CleanupWireframe();
            return;
        }
        
        var edges = GetMeshEdges();
        if (edges == null)
        {
            CleanupWireframe();
            return;
        }
        
        Transform meshTransform = targetMesh.transform;
        MeshFilter mf = meshTransform.GetComponentInChildren<MeshFilter>();
        if (mf == null || mf.sharedMesh == null)
        {
            CleanupWireframe();
            return;
        }
        
        Vector3[] meshVerts = mf.sharedMesh.vertices;
        
        // Create wireframe root if needed
        if (wireframeRoot == null)
        {
            wireframeRoot = new GameObject("_WireframeEdges");
            wireframeRoot.transform.SetParent(meshTransform, false);
        }
        
        // Create edge material if needed
        if (edgeMaterial == null)
        {
            edgeMaterial = new Material(Shader.Find("Universal Render Pipeline/Unlit"));
            edgeMaterial.color = edgeColor;
            edgeMaterial.hideFlags = HideFlags.HideAndDontSave;
        }
        edgeMaterial.color = edgeColor;
        
        // Ensure we have enough LineRenderers
        int edgeCount = edges.Count;
        while (edgeLineRenderers.Count < edgeCount)
        {
            GameObject lineObj = new GameObject($"Edge_{edgeLineRenderers.Count}");
            lineObj.transform.SetParent(wireframeRoot.transform, false);
            LineRenderer lr = lineObj.AddComponent<LineRenderer>();
            lr.material = edgeMaterial;
            lr.startWidth = edgeLineWidth;
            lr.endWidth = edgeLineWidth;
            lr.useWorldSpace = true;
            lr.positionCount = 2;
            lr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            lr.receiveShadows = false;
            edgeLineRenderers.Add(lr);
        }
        
        // Remove excess LineRenderers
        while (edgeLineRenderers.Count > edgeCount)
        {
            LineRenderer lr = edgeLineRenderers[edgeLineRenderers.Count - 1];
            edgeLineRenderers.RemoveAt(edgeLineRenderers.Count - 1);
            if (lr != null)
            {
                if (Application.isPlaying)
                    Destroy(lr.gameObject);
                else
                    DestroyImmediate(lr.gameObject);
            }
        }
        
        // Update line positions
        int lineIndex = 0;
        foreach (var edge in edges)
        {
            if (lineIndex >= edgeLineRenderers.Count)
                break;
                
            LineRenderer lr = edgeLineRenderers[lineIndex];
            if (lr != null)
            {
                Vector3 start = meshTransform.TransformPoint(meshVerts[edge.Item1]);
                Vector3 end = meshTransform.TransformPoint(meshVerts[edge.Item2]);
                lr.SetPosition(0, start);
                lr.SetPosition(1, end);
                lr.enabled = true;
            }
            lineIndex++;
        }
        
        // Disable unused lines
        for (int i = lineIndex; i < edgeLineRenderers.Count; i++)
        {
            if (edgeLineRenderers[i] != null)
                edgeLineRenderers[i].enabled = false;
        }
    }
    
    void UpdateVertexLabels3D()
    {
        if (!showLabels || targetMesh == null)
        {
            CleanupVertexLabels();
            return;
        }
        
        Vector3[] vertices = targetMesh.GetVertices();
        Transform meshTransform = targetMesh.transform;
        
        // Create/update label objects
        for (int i = 0; i < vertices.Length; i++)
        {
            if (!vertexLabelObjects.ContainsKey(i))
            {
                // Create new label
                GameObject labelObj = new GameObject($"VertexLabel_{i}");
                labelObj.transform.SetParent(meshTransform, false);
                
                // Add TextMesh component
                TextMesh textMesh = labelObj.AddComponent<TextMesh>();
                textMesh.text = $"V{i}";
                textMesh.fontSize = 20;
                textMesh.characterSize = 0.1f;
                textMesh.anchor = TextAnchor.MiddleCenter;
                textMesh.alignment = TextAlignment.Center;
                textMesh.color = (i == selectedVertexIndex) ? selectedColor : normalColor;
                
                vertexLabelObjects[i] = labelObj;
            }
            
            // Update label position and color
            GameObject existingLabelObj = vertexLabelObjects[i];
            if (existingLabelObj != null)
            {
                existingLabelObj.transform.position = meshTransform.TransformPoint(vertices[i]);
                
                // Make label face camera (billboard effect)
                UpdateLabelBillboard(existingLabelObj.transform);
                
                TextMesh textMesh = existingLabelObj.GetComponent<TextMesh>();
                if (textMesh != null)
                {
                    textMesh.color = (i == selectedVertexIndex) ? selectedColor : normalColor;
                }
            }
        }
        
        // Remove labels for vertices that no longer exist
        List<int> toRemove = new List<int>();
        foreach (var kvp in vertexLabelObjects)
        {
            if (kvp.Key >= vertices.Length)
                toRemove.Add(kvp.Key);
        }
        foreach (int key in toRemove)
        {
            GameObject obj = vertexLabelObjects[key];
            if (obj != null)
            {
                if (Application.isPlaying)
                    Destroy(obj);
                else
                    DestroyImmediate(obj);
            }
            vertexLabelObjects.Remove(key);
        }
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
        if (edgeMaterial != null)
            DestroyImmediate(edgeMaterial);
        CleanupWireframe();
        CleanupVertexLabels();
        CleanupObjectLabels();
    }
    
    void CleanupWireframe()
    {
        if (wireframeRoot != null)
        {
            if (Application.isPlaying)
                Destroy(wireframeRoot);
            else
                DestroyImmediate(wireframeRoot);
            wireframeRoot = null;
        }
        edgeLineRenderers.Clear();
    }
    
    void CleanupVertexLabels()
    {
        foreach (var labelObj in vertexLabelObjects.Values)
        {
            if (labelObj != null)
            {
                if (Application.isPlaying)
                    Destroy(labelObj);
                else
                    DestroyImmediate(labelObj);
            }
        }
        vertexLabelObjects.Clear();
    }
    
    void CleanupObjectLabels()
    {
        foreach (var labelObj in objectLabelObjects.Values)
        {
            if (labelObj != null)
            {
                if (Application.isPlaying)
                    Destroy(labelObj);
                else
                    DestroyImmediate(labelObj);
            }
        }
        objectLabelObjects.Clear();
    }

    void RemoveObjectLabel(EditableMesh mesh)
    {
        if (mesh == null)
            return;

        if (objectLabelObjects.TryGetValue(mesh, out GameObject labelObj))
        {
            if (labelObj != null)
            {
                if (Application.isPlaying)
                    Destroy(labelObj);
                else
                    DestroyImmediate(labelObj);
            }
            objectLabelObjects.Remove(mesh);
        }
    }

    void UpdateLabelBillboard(Transform labelTransform)
    {
        // Make label always face the camera (billboard effect)
        Camera cam = GetActiveCamera();
        
        if (cam != null && labelTransform != null)
        {
            // Make label face camera
            Vector3 directionToCamera = cam.transform.position - labelTransform.position;
            if (directionToCamera != Vector3.zero)
            {
                labelTransform.rotation = Quaternion.LookRotation(-directionToCamera);
            }
        }
    }
    
    void UpdateAllLabelBillboards()
    {
        // Update billboard rotation for all vertex and object labels
        Camera cam = GetActiveCamera();
        if (cam == null)
            return;
        
        // Update vertex labels
        foreach (var labelObj in vertexLabelObjects.Values)
        {
            if (labelObj != null)
            {
                Vector3 directionToCamera = cam.transform.position - labelObj.transform.position;
                if (directionToCamera != Vector3.zero)
                {
                    labelObj.transform.rotation = Quaternion.LookRotation(-directionToCamera);
                }
            }
        }
        
        // Update object labels
        foreach (var labelObj in objectLabelObjects.Values)
        {
            if (labelObj != null)
            {
                Vector3 directionToCamera = cam.transform.position - labelObj.transform.position;
                if (directionToCamera != Vector3.zero)
                {
                    labelObj.transform.rotation = Quaternion.LookRotation(-directionToCamera);
                }
            }
        }
    }
    
    Camera GetActiveCamera()
    {
        bool isXR = XRSettings.isDeviceActive;
        
        if (isXR)
        {
            // In XR, use the main camera or find the active XR camera
            Camera cam = Camera.main;
            if (cam == null)
            {
                Camera[] cameras = FindObjectsByType<Camera>(FindObjectsSortMode.None);
                foreach (Camera c in cameras)
                {
                    if (c.enabled && c.gameObject.activeInHierarchy)
                    {
                        cam = c;
                        break;
                    }
                }
            }
            return cam;
        }
        else
        {
            // Desktop mode: use editCamera
            if (editCamera != null)
                return editCamera;
            return Camera.main;
        }
    }
    
    void UpdateObjectLabels(bool isXR)
    {
        if (!showObjectLabels)
        {
            CleanupObjectLabels();
            return;
        }
        
        EditableMesh[] allMeshes = FindObjectsByType<EditableMesh>(FindObjectsSortMode.None);
        HashSet<EditableMesh> currentMeshes = new HashSet<EditableMesh>(allMeshes);
        Transform selectedTransform = objectSelector != null ? objectSelector.GetCurrentSelection() : null;
        
        if (isXR)
        {
            // XR mode: Use 3D TextMesh labels
            foreach (EditableMesh mesh in allMeshes)
            {
                if (mesh == null || !mesh.gameObject.activeInHierarchy)
                    continue;
                
                bool isInEditMode = mesh.mode == EditableMesh.DisplayMode.Edit;
                if (isInEditMode)
                {
                    RemoveObjectLabel(mesh);
                    continue;
                }
                
                bool isSelected = selectedTransform != null &&
                    (selectedTransform == mesh.transform || selectedTransform.IsChildOf(mesh.transform));
                Color labelColor = isSelected ? mesh.originColorSelected : objectLabelColor;
                
                if (!objectLabelObjects.ContainsKey(mesh))
                {
                    // Create new label
                    GameObject labelObj = new GameObject($"ObjectLabel_{mesh.gameObject.name}");
                    labelObj.transform.SetParent(mesh.transform, false);
                    
                    // Add TextMesh component
                    TextMesh textMesh = labelObj.AddComponent<TextMesh>();
                    textMesh.text = mesh.gameObject.name;
                    textMesh.fontSize = 30;
                    textMesh.characterSize = 0.15f;
                    textMesh.anchor = TextAnchor.MiddleCenter;
                    textMesh.alignment = TextAlignment.Center;
                    textMesh.color = labelColor;
                    
                    // Position above object (at origin + offset upward)
                    labelObj.transform.localPosition = Vector3.up * 0.5f;
                    
                    objectLabelObjects[mesh] = labelObj;
                }
                else
                {
                    // Update existing label
                    GameObject labelObj = objectLabelObjects[mesh];
                    if (labelObj != null)
                    {
                        TextMesh textMesh = labelObj.GetComponent<TextMesh>();
                        if (textMesh != null)
                        {
                            textMesh.text = mesh.gameObject.name;
                            textMesh.color = labelColor;
                        }
                        // Update position
                        labelObj.transform.localPosition = Vector3.up * 0.5f;
                        
                        // Make label face camera (billboard effect)
                        UpdateLabelBillboard(labelObj.transform);
                    }
                }
            }
        }
        else
        {
            // Desktop mode: Labels are drawn in OnGUI, just clean up 3D labels
            CleanupObjectLabels();
        }
        
        // Remove labels for objects that no longer exist
        List<EditableMesh> toRemove = new List<EditableMesh>();
        foreach (var kvp in objectLabelObjects)
        {
            if (!currentMeshes.Contains(kvp.Key) || kvp.Key == null)
                toRemove.Add(kvp.Key);
        }
        foreach (EditableMesh mesh in toRemove)
        {
            RemoveObjectLabel(mesh);
        }
    }
}


