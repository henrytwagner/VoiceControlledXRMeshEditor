using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

/// <summary>
/// Blender-style transform info panel
/// Shows position, rotation, and scale of selected object or camera
/// Toggle with N key (like Blender)
/// </summary>
public class TransformPanel : MonoBehaviour
{
    [Header("Panel Settings")]
    public bool showPanel = true;
    public bool showOnStart = true;
    public KeyCode toggleKey = KeyCode.N;
    public PanelPosition position = PanelPosition.Right;
    
    [Header("Display Options")]
    public bool showCamera = true;
    public bool showSelectedObject = true;
    public bool useLocalTransform = false; // If false, uses world transform
    
    [Header("Style")]
    public float panelWidth = 250f;
    public float panelPadding = 10f;
    public int fontSize = 12;
    public Color panelColor = new Color(0.2f, 0.2f, 0.2f, 0.9f);
    public Color headerColor = new Color(0.3f, 0.3f, 0.3f, 0.9f);
    public Color textColor = Color.white;
    public Color labelColor = new Color(0.7f, 0.7f, 0.7f, 1f);
    
    [Header("References")]
    public Camera targetCamera;
    public Transform selectedObject;
    public EditableMesh editableMesh; // Direct reference to mesh being edited
    
    public enum PanelPosition
    {
        Left,
        Right
    }
    
    private GUIStyle panelStyle;
    private GUIStyle headerStyle;
    private GUIStyle labelStyle;
    private GUIStyle valueStyle;
    private bool stylesInitialized = false;
    
    void Start()
    {
        if (targetCamera == null)
            targetCamera = Camera.main;
        
        // Auto-find EditableMesh reference (but don't auto-select it)
        if (editableMesh == null)
            editableMesh = FindObjectOfType<EditableMesh>();
        
        showPanel = showOnStart;
    }
    
    void Update()
    {
        HandleToggle();
        UpdateCameraReference();
    }
    
    void UpdateCameraReference()
    {
        // Prioritize DesktopCamera, then Camera.main
        GameObject desktopCam = GameObject.Find("DesktopCamera");
        if (desktopCam != null)
        {
            Camera cam = desktopCam.GetComponent<Camera>();
            if (cam != null && cam.enabled && cam.gameObject.activeInHierarchy)
            {
                targetCamera = cam;
                return;
            }
        }
        
        if (targetCamera == null || !targetCamera.enabled)
            targetCamera = Camera.main;
    }
    
    void HandleToggle()
    {
        bool togglePressed = false;
        
        #if ENABLE_INPUT_SYSTEM
        Keyboard keyboard = Keyboard.current;
        if (keyboard != null)
        {
            togglePressed = keyboard.nKey.wasPressedThisFrame;
        }
        #else
        togglePressed = Input.GetKeyDown(toggleKey);
        #endif
        
        if (togglePressed)
        {
            showPanel = !showPanel;
        }
    }
    
    void OnGUI()
    {
        if (!showPanel)
            return;
        
        InitializeStyles();
        DrawPanel();
    }
    
    void InitializeStyles()
    {
        if (stylesInitialized)
            return;
        
        panelStyle = new GUIStyle(GUI.skin.box);
        panelStyle.normal.background = MakeTex(2, 2, panelColor);
        panelStyle.padding = new RectOffset(10, 10, 10, 10);
        
        headerStyle = new GUIStyle(GUI.skin.box);
        headerStyle.normal.background = MakeTex(2, 2, headerColor);
        headerStyle.normal.textColor = textColor;
        headerStyle.fontSize = fontSize + 2;
        headerStyle.fontStyle = FontStyle.Bold;
        headerStyle.alignment = TextAnchor.MiddleLeft;
        headerStyle.padding = new RectOffset(10, 10, 5, 5);
        
        labelStyle = new GUIStyle(GUI.skin.label);
        labelStyle.normal.textColor = labelColor;
        labelStyle.fontSize = fontSize;
        labelStyle.alignment = TextAnchor.MiddleLeft;
        
        valueStyle = new GUIStyle(GUI.skin.label);
        valueStyle.normal.textColor = textColor;
        valueStyle.fontSize = fontSize;
        valueStyle.alignment = TextAnchor.MiddleRight;
        valueStyle.fontStyle = FontStyle.Bold;
        
        stylesInitialized = true;
    }
    
    void DrawPanel()
    {
        float xPos = position == PanelPosition.Right ? Screen.width - panelWidth - panelPadding : panelPadding;
        float yPos = panelPadding;
        
        GUILayout.BeginArea(new Rect(xPos, yPos, panelWidth, Screen.height - panelPadding * 2), panelStyle);
        
        // Title
        GUILayout.Box("Transform", headerStyle, GUILayout.Height(30));
        GUILayout.Space(5);
        
        // Show selected object if available, otherwise show camera
        if (showSelectedObject && selectedObject != null)
        {
            // Show only selected object
            DrawTransformSection(selectedObject.name, selectedObject);
            GUILayout.Space(10);
        }
        else if (showCamera && targetCamera != null)
        {
            // Show camera when nothing is selected
            DrawTransformSection("Camera", targetCamera.transform);
            GUILayout.Space(10);
        }
        else
        {
            // Show "nothing selected" message
            GUILayout.Label("Selected Object", headerStyle);
            GUILayout.Space(5);
            
            GUIStyle italicStyle = new GUIStyle(labelStyle);
            italicStyle.fontStyle = FontStyle.Italic;
            italicStyle.alignment = TextAnchor.MiddleCenter;
            
            GUILayout.Label("No object selected", italicStyle);
            GUILayout.Label("Click an object to select", italicStyle);
            GUILayout.Space(10);
        }
        
        // Instructions
        GUILayout.FlexibleSpace();
        GUILayout.BeginHorizontal();
        GUILayout.Label("Press N to toggle", labelStyle);
        GUILayout.EndHorizontal();
        
        GUILayout.EndArea();
    }
    
    void DrawTransformSection(string title, Transform t)
    {
        // Section header
        GUILayout.BeginVertical();
        GUILayout.Label(title, headerStyle);
        GUILayout.Space(5);
        
        // Get transform values
        Vector3 pos = useLocalTransform ? t.localPosition : t.position;
        Vector3 rot = useLocalTransform ? t.localEulerAngles : t.eulerAngles;
        Vector3 scale = t.localScale;
        
        // Position
        DrawVector3Field("Location", pos, "X", "Y", "Z");
        GUILayout.Space(3);
        
        // Rotation
        DrawVector3Field("Rotation", rot, "X", "Y", "Z");
        GUILayout.Space(3);
        
        // Scale
        DrawVector3Field("Scale", scale, "X", "Y", "Z");
        
        GUILayout.EndVertical();
    }
    
    void DrawVector3Field(string label, Vector3 value, string xLabel, string yLabel, string zLabel)
    {
        GUILayout.BeginVertical();
        
        // Label
        GUILayout.Label(label, labelStyle);
        
        // X component
        GUILayout.BeginHorizontal();
        GUILayout.Label($"  {xLabel}:", labelStyle, GUILayout.Width(30));
        GUILayout.FlexibleSpace();
        GUILayout.Label(value.x.ToString("F3"), valueStyle);
        GUILayout.EndHorizontal();
        
        // Y component
        GUILayout.BeginHorizontal();
        GUILayout.Label($"  {yLabel}:", labelStyle, GUILayout.Width(30));
        GUILayout.FlexibleSpace();
        GUILayout.Label(value.y.ToString("F3"), valueStyle);
        GUILayout.EndHorizontal();
        
        // Z component
        GUILayout.BeginHorizontal();
        GUILayout.Label($"  {zLabel}:", labelStyle, GUILayout.Width(30));
        GUILayout.FlexibleSpace();
        GUILayout.Label(value.z.ToString("F3"), valueStyle);
        GUILayout.EndHorizontal();
        
        GUILayout.EndVertical();
    }
    
    Texture2D MakeTex(int width, int height, Color col)
    {
        Color[] pix = new Color[width * height];
        for (int i = 0; i < pix.Length; i++)
            pix[i] = col;
        
        Texture2D result = new Texture2D(width, height);
        result.SetPixels(pix);
        result.Apply();
        return result;
    }
    
    // Public API for external scripts to set selected object
    public void SetSelectedObject(Transform obj)
    {
        selectedObject = obj;
    }
    
    public void ClearSelection()
    {
        selectedObject = null;
    }
}

