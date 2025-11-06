using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

/// <summary>
/// Top menu bar UI for creating and managing objects
/// Similar to Blender's top menu
/// </summary>
public class TopMenuBar : MonoBehaviour
{
    [Header("References")]
    public MeshSpawner meshSpawner;
    
    [Header("Menu Style")]
    public float menuHeight = 30f;
    public float menuWidth = 220f; // Compact width for left corner
    public Color menuBarColor = new Color(0.25f, 0.25f, 0.25f, 0.95f);
    public Color buttonColor = new Color(0.3f, 0.3f, 0.3f, 1f);
    public Color buttonHoverColor = new Color(0.4f, 0.4f, 0.4f, 1f);
    public int fontSize = 13;
    
    [Header("Settings")]
    public bool showMenu = true;
    public KeyCode toggleMenuKey = KeyCode.F1;
    
    private bool showAddDropdown = false;
    private Rect addButtonRect;
    
    private GUIStyle menuBarStyle;
    private GUIStyle buttonStyle;
    private GUIStyle dropdownStyle;
    private bool stylesInitialized = false;
    
    void Start()
    {
        if (meshSpawner == null)
            meshSpawner = FindAnyObjectByType<MeshSpawner>();
    }
    
    void Update()
    {
        HandleToggle();
    }
    
    void HandleToggle()
    {
        bool togglePressed = false;
        
        #if ENABLE_INPUT_SYSTEM
        Keyboard keyboard = Keyboard.current;
        if (keyboard != null)
        {
            togglePressed = keyboard.f1Key.wasPressedThisFrame;
        }
        #else
        togglePressed = Input.GetKeyDown(toggleMenuKey);
        #endif
        
        if (togglePressed)
        {
            showMenu = !showMenu;
        }
    }
    
    void OnGUI()
    {
        if (!showMenu)
            return;
        
        InitializeStyles();
        DrawMenuBar();
    }
    
    void InitializeStyles()
    {
        if (stylesInitialized)
            return;
        
        menuBarStyle = new GUIStyle(GUI.skin.box);
        menuBarStyle.normal.background = MakeTex(2, 2, menuBarColor);
        menuBarStyle.padding = new RectOffset(5, 5, 5, 5);
        
        buttonStyle = new GUIStyle(GUI.skin.button);
        buttonStyle.normal.background = MakeTex(2, 2, buttonColor);
        buttonStyle.hover.background = MakeTex(2, 2, buttonHoverColor);
        buttonStyle.normal.textColor = Color.white;
        buttonStyle.fontSize = fontSize;
        buttonStyle.fontStyle = FontStyle.Bold;
        buttonStyle.alignment = TextAnchor.MiddleCenter;
        buttonStyle.padding = new RectOffset(10, 10, 5, 5);
        
        dropdownStyle = new GUIStyle(GUI.skin.box);
        dropdownStyle.normal.background = MakeTex(2, 2, new Color(0.2f, 0.2f, 0.2f, 0.95f));
        dropdownStyle.normal.textColor = Color.white;
        dropdownStyle.fontSize = fontSize;
        dropdownStyle.alignment = TextAnchor.MiddleLeft;
        dropdownStyle.padding = new RectOffset(10, 10, 5, 5);
        
        stylesInitialized = true;
    }
    
    void DrawMenuBar()
    {
        // Draw menu bar background - only in left corner
        GUI.Box(new Rect(0, 0, menuWidth, menuHeight), "", menuBarStyle);
        
        float xPos = 10f;
        float buttonWidth = 100f;
        float buttonHeight = menuHeight - 6f;
        float yPos = 3f;
        
        // Add Object button
        addButtonRect = new Rect(xPos, yPos, buttonWidth, buttonHeight);
        if (GUI.Button(addButtonRect, "Add Object ▼", buttonStyle))
        {
            showAddDropdown = !showAddDropdown;
        }
        
        xPos += buttonWidth + 5f;
        
        // Clear All button
        if (GUI.Button(new Rect(xPos, yPos, buttonWidth, buttonHeight), "Clear All", buttonStyle))
        {
            if (meshSpawner != null)
                meshSpawner.ClearAll();
            showAddDropdown = false;
        }
        
        // Draw dropdown if open
        if (showAddDropdown)
        {
            DrawAddDropdown();
        }
        
        // Close dropdown if clicking elsewhere
        if (Event.current.type == EventType.MouseDown && !addButtonRect.Contains(Event.current.mousePosition))
        {
            if (showAddDropdown)
            {
                Rect dropdownRect = new Rect(addButtonRect.x, addButtonRect.y + addButtonRect.height, 150f, 200f);
                if (!dropdownRect.Contains(Event.current.mousePosition))
                {
                    showAddDropdown = false;
                }
            }
        }
    }
    
    void DrawAddDropdown()
    {
        float dropdownWidth = 150f;
        float itemHeight = 25f;
        float dropdownHeight = itemHeight * 5 + 10f; // 5 primitives
        
        Rect dropdownRect = new Rect(addButtonRect.x, addButtonRect.y + addButtonRect.height, 
                                      dropdownWidth, dropdownHeight);
        
        GUI.Box(dropdownRect, "", dropdownStyle);
        
        float yPos = dropdownRect.y + 5f;
        
        // Primitive buttons
        PrimitiveType[] primitives = new PrimitiveType[]
        {
            PrimitiveType.Cube,
            PrimitiveType.Sphere,
            PrimitiveType.Cylinder,
            PrimitiveType.Capsule,
            PrimitiveType.Plane
        };
        
        foreach (PrimitiveType primitive in primitives)
        {
            Rect itemRect = new Rect(dropdownRect.x + 5f, yPos, dropdownWidth - 10f, itemHeight);
            
            if (GUI.Button(itemRect, primitive.ToString(), buttonStyle))
            {
                SpawnPrimitive(primitive);
                showAddDropdown = false;
            }
            
            yPos += itemHeight;
        }
    }
    
    void SpawnPrimitive(PrimitiveType primitiveType)
    {
        if (meshSpawner == null)
        {
            Debug.LogError("[TopMenuBar] No MeshSpawner found!");
            return;
        }
        
        EditableMesh newMesh = meshSpawner.SpawnPrimitive(primitiveType);
        
        if (newMesh != null)
        {
            Debug.Log($"[TopMenuBar] Created {primitiveType}");
            
            // Select the new object
            ObjectSelector selector = FindAnyObjectByType<ObjectSelector>();
            if (selector != null)
            {
                selector.AddSelectableObject(newMesh.gameObject.name);
            }
        }
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
}

