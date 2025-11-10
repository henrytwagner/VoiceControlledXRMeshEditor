using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.XR;
using XRInputDevice = UnityEngine.XR.InputDevice;
using XRCommonUsages = UnityEngine.XR.CommonUsages;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

/// <summary>
/// Simple VR menu that appears in front of the user when the X button is pressed.
/// The menu contains sample buttons for future expansion.
/// </summary>
public class VRMenuController : MonoBehaviour
{
    [Header("Menu Settings")]
    [Tooltip("Distance in meters to spawn the menu in front of the user")]
    public float menuDistance = 1.25f;
    [Tooltip("Vertical offset (in meters) for the menu relative to the camera")]
    public float verticalOffset = -0.1f;
    [Tooltip("Scale applied to the world space canvas")]
    public float menuScale = 0.0025f;
    [Tooltip("Initial visibility of the menu when the application starts")]
    public bool startVisible = false;
    [Tooltip("Keyboard key used to toggle the menu while testing in the editor")]
    public KeyCode desktopToggleKey = KeyCode.F;
    [Tooltip("Log button state information each frame (for debugging controller input)")]
    public bool logButtonStates = false;
    [Tooltip("Optional: assign an existing world-space menu root. If null, one will be created.")]
    public GameObject menuRoot;

    private Canvas menuCanvas;
    private bool menuVisible;
    private bool lastButtonState = false;

    private Transform menuPanel;
    private GameObject loadPanel;
    private GameObject saveAsPanel;
    private InputField saveAsInputField;

    private MeshSpawner meshSpawner;
    private TransformPersistenceManager persistenceManager;

    // Auto-create a controller instance after the scene loads
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void AutoCreate()
    {
        UnityEngine.Object existing = UnityEngine.Object.FindAnyObjectByType<VRMenuController>();
        if (existing == null)
        {
            GameObject go = new GameObject("VRMenuController");
            go.AddComponent<VRMenuController>();
            UnityEngine.Object.DontDestroyOnLoad(go);
            Debug.Log("[VRMenu] Auto-created VRMenuController");
        }
    }

    void Awake()
    {
        menuVisible = startVisible;

        if (menuRoot == null)
        {
            menuRoot = CreateMenu();
        }

        if (menuRoot != null)
        {
            menuCanvas = menuRoot.GetComponent<Canvas>();
            menuRoot.SetActive(menuVisible);
            if (menuVisible)
                PositionMenu();
        }
    }

    void Update()
    {
        bool isXR = XRSettings.isDeviceActive;

        bool buttonPressed = GetXButtonPressed(isXR);
        if (buttonPressed && !lastButtonState)
        {
            ToggleMenu();
        }
        lastButtonState = buttonPressed;

        if (menuVisible && menuRoot != null)
        {
            PositionMenu();
        }
    }

    GameObject CreateMenu()
    {
        // Root canvas
        GameObject root = new GameObject("VRMenuCanvas");
        Canvas canvas = root.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.WorldSpace;
        canvas.worldCamera = GetActiveCamera();
        canvas.sortingOrder = 20;

        CanvasScaler scaler = root.AddComponent<CanvasScaler>();
        scaler.dynamicPixelsPerUnit = 10f;
        root.AddComponent<GraphicRaycaster>();

        RectTransform canvasRect = canvas.GetComponent<RectTransform>();
        canvasRect.sizeDelta = new Vector2(400f, 300f);
        root.transform.localScale = Vector3.one * menuScale;

        // Panel
        GameObject panel = new GameObject("Panel");
        panel.transform.SetParent(root.transform, false);
        RectTransform panelRect = panel.AddComponent<RectTransform>();
        panelRect.anchorMin = Vector2.zero;
        panelRect.anchorMax = Vector2.one;
        panelRect.offsetMin = Vector2.zero;
        panelRect.offsetMax = Vector2.zero;
        menuPanel = panel.transform;

        Image panelImage = panel.AddComponent<Image>();
        panelImage.color = new Color(0.15f, 0.15f, 0.2f, 0.85f);

        VerticalLayoutGroup layout = panel.AddComponent<VerticalLayoutGroup>();
        layout.padding = new RectOffset(25, 25, 25, 25);
        layout.spacing = 20f;
        layout.childAlignment = TextAnchor.MiddleCenter;

        ContentSizeFitter fitter = panel.AddComponent<ContentSizeFitter>();
        fitter.horizontalFit = ContentSizeFitter.FitMode.PreferredSize;
        fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        // Sample header text
        CreateLabel(panel.transform, "VR Scene Tools", 30);

        // Spawn primitives
        CreateButton(panel.transform, "Add Cube", () => SpawnPrimitive(PrimitiveType.Cube));
        CreateButton(panel.transform, "Add Sphere", () => SpawnPrimitive(PrimitiveType.Sphere));
        CreateButton(panel.transform, "Add Cylinder", () => SpawnPrimitive(PrimitiveType.Cylinder));
        CreateButton(panel.transform, "Add Capsule", () => SpawnPrimitive(PrimitiveType.Capsule));
        CreateButton(panel.transform, "Add Plane", () => SpawnPrimitive(PrimitiveType.Plane));

        CreateDivider(panel.transform);

        // Scene management
        CreateButton(panel.transform, "Clear All", ClearAllObjects);
        CreateButton(panel.transform, "Save Scene", SaveScene);
        CreateButton(panel.transform, "Save Scene As...", SaveSceneAs);
        CreateButton(panel.transform, "Load Saved Scene", ShowLoadSceneDialogVR);

        CreateDivider(panel.transform);

        // Close menu
        CreateButton(panel.transform, "Close Menu", ToggleMenu);

        return root;
    }

    void CreateLabel(Transform parent, string text, int fontSize)
    {
        GameObject labelGO = new GameObject("Label");
        labelGO.transform.SetParent(parent, false);
        Text label = labelGO.AddComponent<Text>();
        label.text = text;
        label.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        label.fontSize = fontSize;
        label.alignment = TextAnchor.MiddleCenter;
        label.color = Color.white;

        RectTransform rect = label.GetComponent<RectTransform>();
        rect.sizeDelta = new Vector2(320f, 60f);
    }

    void CreateButton(Transform parent, string text, UnityEngine.Events.UnityAction callback)
    {
        GameObject buttonGO = new GameObject(text.Replace(" ", "") + "Button");
        buttonGO.transform.SetParent(parent, false);

        Image img = buttonGO.AddComponent<Image>();
        img.color = new Color(0.25f, 0.4f, 0.8f, 1f);

        Button button = buttonGO.AddComponent<Button>();
        button.onClick.AddListener(callback);

        RectTransform rect = buttonGO.GetComponent<RectTransform>();
        rect.sizeDelta = new Vector2(320f, 70f);

        GameObject textGO = new GameObject("Text");
        textGO.transform.SetParent(buttonGO.transform, false);
        Text buttonText = textGO.AddComponent<Text>();
        buttonText.text = text;
        buttonText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        buttonText.fontSize = 24;
        buttonText.alignment = TextAnchor.MiddleCenter;
        buttonText.color = Color.white;

        RectTransform textRect = buttonText.GetComponent<RectTransform>();
        textRect.anchorMin = Vector2.zero;
        textRect.anchorMax = Vector2.one;
        textRect.offsetMin = Vector2.zero;
        textRect.offsetMax = Vector2.zero;
    }

    void CreateDivider(Transform parent)
    {
        GameObject divider = new GameObject("Divider");
        divider.transform.SetParent(parent, false);
        Image image = divider.AddComponent<Image>();
        image.color = new Color(1f, 1f, 1f, 0.2f);

        RectTransform rect = image.rectTransform;
        rect.sizeDelta = new Vector2(320f, 2f);

        LayoutElement layoutElement = divider.AddComponent<LayoutElement>();
        layoutElement.preferredHeight = 2f;
        layoutElement.minHeight = 2f;
    }

    void ToggleMenu()
    {
        menuVisible = !menuVisible;
        Debug.Log($"[VRMenu] Menu {(menuVisible ? "shown" : "hidden")}");
        if (menuRoot != null)
        {
            menuRoot.SetActive(menuVisible);
            if (menuVisible)
                PositionMenu();
            else
                HideOverlayPanels();
        }
    }

    void HideOverlayPanels()
    {
        if (loadPanel != null)
            loadPanel.SetActive(false);
        if (saveAsPanel != null)
            saveAsPanel.SetActive(false);
    }

    void PositionMenu()
    {
        Camera cam = GetActiveCamera();
        if (cam == null || menuRoot == null)
            return;

        Vector3 forward = cam.transform.forward;
        forward.y = 0f;
        if (forward.sqrMagnitude < 0.001f)
            forward = cam.transform.forward;
        forward.Normalize();

        Vector3 menuPosition = cam.transform.position + forward * menuDistance;
        menuPosition.y += verticalOffset;

        menuRoot.transform.position = menuPosition;
        menuRoot.transform.rotation = Quaternion.LookRotation(forward, Vector3.up);

        if (menuCanvas != null)
            menuCanvas.worldCamera = cam;
    }

    bool GetXButtonPressed(bool isXR)
    {
        bool pressed = false;

        if (isXR)
        {
            XRInputDevice leftHand = InputDevices.GetDeviceAtXRNode(XRNode.LeftHand);
            if (leftHand.isValid)
            {
                bool primaryPressed = false;
                bool secondaryPressed = false;
                bool menuPressed = false;
                leftHand.TryGetFeatureValue(XRCommonUsages.primaryButton, out primaryPressed);
                leftHand.TryGetFeatureValue(XRCommonUsages.secondaryButton, out secondaryPressed);
                leftHand.TryGetFeatureValue(XRCommonUsages.menuButton, out menuPressed);

                if (primaryPressed || secondaryPressed || menuPressed)
                    pressed = true;

                if (logButtonStates)
                {
                    Debug.Log($"[VRMenu] Left controller primary: {primaryPressed}, secondary: {secondaryPressed}, menu: {menuPressed}");
                }
            }
            else if (logButtonStates)
            {
                Debug.Log("[VRMenu] Left controller not valid yet");
            }

            XRInputDevice rightHand = InputDevices.GetDeviceAtXRNode(XRNode.RightHand);
            if (!pressed && rightHand.isValid)
            {
                bool rightPrimary = false;
                bool rightSecondary = false;
                bool rightMenu = false;
                rightHand.TryGetFeatureValue(XRCommonUsages.primaryButton, out rightPrimary);
                rightHand.TryGetFeatureValue(XRCommonUsages.secondaryButton, out rightSecondary);
                rightHand.TryGetFeatureValue(XRCommonUsages.menuButton, out rightMenu);
                if (rightPrimary || rightSecondary || rightMenu)
                    pressed = true;

                if (logButtonStates)
                {
                    Debug.Log($"[VRMenu] Right controller primary: {rightPrimary}, secondary: {rightSecondary}, menu: {rightMenu}");
                }
            }
            else if (!pressed && logButtonStates)
            {
                Debug.Log("[VRMenu] Right controller not valid yet");
            }
        }

        // Keyboard fallback for testing (works even without XR)
        #if ENABLE_INPUT_SYSTEM
        if (Keyboard.current != null)
        {
            Key? keyEnum = ConvertKeyCode(desktopToggleKey);
            if (keyEnum.HasValue && Keyboard.current[keyEnum.Value].wasPressedThisFrame)
                pressed = true;
        }
        #else
        if (Input.GetKeyDown(desktopToggleKey))
            pressed = true;
        #endif

        return pressed;
    }

    Camera GetActiveCamera()
    {
        Camera cam = Camera.main;
        if (cam != null)
            return cam;

        Camera[] cameras = FindObjectsByType<Camera>(FindObjectsSortMode.None);
        foreach (Camera c in cameras)
        {
            if (c.enabled && c.gameObject.activeInHierarchy)
                return c;
        }

        return null;
    }

#if ENABLE_INPUT_SYSTEM
    Key? ConvertKeyCode(KeyCode keyCode)
    {
        switch (keyCode)
        {
            case KeyCode.X: return Key.X;
            case KeyCode.M: return Key.M;
            case KeyCode.Z: return Key.Z;
            case KeyCode.R: return Key.R;
            case KeyCode.T: return Key.T;
            case KeyCode.Alpha1: return Key.Digit1;
            case KeyCode.Alpha2: return Key.Digit2;
            case KeyCode.Alpha3: return Key.Digit3;
            case KeyCode.Space: return Key.Space;
            case KeyCode.F: return Key.F;
            default:
                return null;
        }
    }
#endif

    void SpawnPrimitive(PrimitiveType primitive)
    {
        EnsureManagers();
        if (meshSpawner == null)
        {
            Debug.LogWarning("[VRMenu] MeshSpawner not found in scene.");
            return;
        }
        meshSpawner.SpawnPrimitive(primitive);
    }

    void ClearAllObjects()
    {
        EnsureManagers();
        if (meshSpawner == null)
        {
            Debug.LogWarning("[VRMenu] MeshSpawner not found in scene.");
            return;
        }
        meshSpawner.ClearAll();
    }

    void SaveScene()
    {
        EnsureManagers();
        if (persistenceManager == null)
        {
            Debug.LogWarning("[VRMenu] TransformPersistenceManager not found in scene.");
            return;
        }

        if (!string.IsNullOrEmpty(persistenceManager.CurrentSceneName))
        {
            persistenceManager.SaveTransforms();
            Debug.Log($"[VRMenu] Scene '{persistenceManager.CurrentSceneName}' saved.");
            if (loadPanel != null && loadPanel.activeSelf)
                PopulateLoadPanel();
        }
        else
        {
            SaveSceneAs();
        }
    }

    void SaveSceneAs()
    {
        EnsureManagers();
        if (persistenceManager == null)
        {
            Debug.LogWarning("[VRMenu] TransformPersistenceManager not found in scene.");
            return;
        }

        if (saveAsPanel == null)
            CreateSaveAsPanel();

        if (loadPanel != null)
            loadPanel.SetActive(false);

        string defaultName = !string.IsNullOrEmpty(persistenceManager.CurrentSceneName)
            ? persistenceManager.CurrentSceneName
            : DateTime.Now.ToString("yyyy-MM-dd_HH-mm-ss");

        if (saveAsInputField != null)
            saveAsInputField.text = defaultName;

        saveAsPanel.SetActive(true);
    }

    void OnConfirmSaveAs()
    {
        EnsureManagers();
        if (persistenceManager == null)
        {
            Debug.LogWarning("[VRMenu] TransformPersistenceManager not found in scene.");
            return;
        }

        string name = saveAsInputField != null ? saveAsInputField.text : string.Empty;
        if (string.IsNullOrWhiteSpace(name))
            name = DateTime.Now.ToString("yyyy-MM-dd_HH-mm-ss");

        persistenceManager.SaveTransforms(name);
        Debug.Log($"[VRMenu] Scene saved as '{name}'");
        if (saveAsPanel != null)
            saveAsPanel.SetActive(false);

        if (loadPanel != null && loadPanel.activeSelf)
            PopulateLoadPanel();
    }

    void ShowLoadSceneDialogVR()
    {
        EnsureManagers();
        if (persistenceManager == null)
        {
            Debug.LogWarning("[VRMenu] TransformPersistenceManager not found in scene.");
            return;
        }

        if (loadPanel == null)
            CreateLoadPanel();

        bool newState = !loadPanel.activeSelf;
        loadPanel.SetActive(newState);
        if (newState)
        {
            if (saveAsPanel != null)
                saveAsPanel.SetActive(false);
            PopulateLoadPanel();
        }
    }

    void LoadSceneFromName(string sceneName)
    {
        EnsureManagers();
        if (persistenceManager == null)
        {
            Debug.LogWarning("[VRMenu] TransformPersistenceManager not found in scene.");
            return;
        }
        if (meshSpawner != null)
            meshSpawner.ClearAll();

        persistenceManager.LoadTransformsFromFile(sceneName);
        Debug.Log($"[VRMenu] Loaded scene '{sceneName}'");
        HideOverlayPanels();
    }

    void EnsureManagers()
    {
        if (meshSpawner == null)
            meshSpawner = FindAnyObjectByType<MeshSpawner>();
        if (persistenceManager == null)
            persistenceManager = FindAnyObjectByType<TransformPersistenceManager>();
    }

    void CreateSaveAsPanel()
    {
        saveAsPanel = CreateOverlayPanel("SaveAsPanel", new Vector2(360f, 220f), new Vector2(0f, -10f));
        VerticalLayoutGroup layout = saveAsPanel.AddComponent<VerticalLayoutGroup>();
        layout.padding = new RectOffset(15, 15, 15, 15);
        layout.spacing = 10f;
        layout.childAlignment = TextAnchor.UpperCenter;

        CreateOverlayLabel(saveAsPanel.transform, "Save Scene As...", 24, TextAnchor.MiddleCenter);
        saveAsInputField = CreateInputField(saveAsPanel.transform, "Scene name");

        CreateOverlayButton(saveAsPanel.transform, "Save", OnConfirmSaveAs);
        CreateOverlayButton(saveAsPanel.transform, "Cancel", () => saveAsPanel.SetActive(false));

        saveAsPanel.SetActive(false);
    }

    void CreateLoadPanel()
    {
        loadPanel = CreateOverlayPanel("LoadPanel", new Vector2(360f, 300f), new Vector2(0f, -40f));
        VerticalLayoutGroup layout = loadPanel.AddComponent<VerticalLayoutGroup>();
        layout.padding = new RectOffset(15, 15, 15, 15);
        layout.spacing = 10f;
        layout.childAlignment = TextAnchor.UpperCenter;

        loadPanel.SetActive(false);
    }

    void PopulateLoadPanel()
    {
        if (loadPanel == null || persistenceManager == null)
            return;

        List<string> sceneNames = persistenceManager.GetSavedSceneNames();

        for (int i = loadPanel.transform.childCount - 1; i >= 0; i--)
        {
            Destroy(loadPanel.transform.GetChild(i).gameObject);
        }

        CreateOverlayLabel(loadPanel.transform, "Load Saved Scene", 24, TextAnchor.MiddleCenter);

        if (sceneNames.Count == 0)
        {
            CreateOverlayLabel(loadPanel.transform, "No saved scenes found.", 20, TextAnchor.MiddleCenter);
        }
        else
        {
            foreach (string sceneName in sceneNames)
            {
                CreateOverlayButton(loadPanel.transform, sceneName, () => LoadSceneFromName(sceneName));
            }
        }

        CreateOverlayButton(loadPanel.transform, "Close", () => loadPanel.SetActive(false));
    }

    GameObject CreateOverlayPanel(string name, Vector2 size, Vector2 anchoredPosition)
    {
        GameObject panel = new GameObject(name);
        panel.transform.SetParent(menuPanel, false);

        RectTransform rect = panel.AddComponent<RectTransform>();
        rect.sizeDelta = size;
        rect.anchorMin = new Vector2(0.5f, 0.5f);
        rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = anchoredPosition;

        LayoutElement layoutElement = panel.AddComponent<LayoutElement>();
        layoutElement.ignoreLayout = true;

        Image bg = panel.AddComponent<Image>();
        bg.color = new Color(0.12f, 0.12f, 0.12f, 0.95f);

        return panel;
    }

    void CreateOverlayLabel(Transform parent, string text, int fontSize, TextAnchor alignment)
    {
        GameObject labelGO = new GameObject("OverlayLabel");
        labelGO.transform.SetParent(parent, false);
        Text label = labelGO.AddComponent<Text>();
        label.text = text;
        label.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        label.fontSize = fontSize;
        label.alignment = alignment;
        label.color = Color.white;

        RectTransform rect = label.GetComponent<RectTransform>();
        rect.sizeDelta = new Vector2(320f, 40f);
    }

    InputField CreateInputField(Transform parent, string placeholderText)
    {
        GameObject inputGO = new GameObject("InputField");
        inputGO.transform.SetParent(parent, false);

        Image bg = inputGO.AddComponent<Image>();
        bg.color = new Color(0.2f, 0.2f, 0.2f, 1f);

        InputField input = inputGO.AddComponent<InputField>();
        RectTransform rect = inputGO.GetComponent<RectTransform>();
        rect.sizeDelta = new Vector2(280f, 50f);

        GameObject placeholderGO = new GameObject("Placeholder");
        placeholderGO.transform.SetParent(inputGO.transform, false);
        Text placeholder = placeholderGO.AddComponent<Text>();
        placeholder.text = placeholderText;
        placeholder.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        placeholder.fontSize = 20;
        placeholder.color = new Color(1f, 1f, 1f, 0.4f);
        placeholder.alignment = TextAnchor.MiddleLeft;

        GameObject textGO = new GameObject("Text");
        textGO.transform.SetParent(inputGO.transform, false);
        Text text = textGO.AddComponent<Text>();
        text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        text.fontSize = 20;
        text.color = Color.white;
        text.alignment = TextAnchor.MiddleLeft;

        RectTransform placeholderRect = placeholder.GetComponent<RectTransform>();
        placeholderRect.anchorMin = Vector2.zero;
        placeholderRect.anchorMax = Vector2.one;
        placeholderRect.offsetMin = new Vector2(10f, 0f);
        placeholderRect.offsetMax = new Vector2(-10f, 0f);

        RectTransform textRect = text.GetComponent<RectTransform>();
        textRect.anchorMin = Vector2.zero;
        textRect.anchorMax = Vector2.one;
        textRect.offsetMin = new Vector2(10f, 0f);
        textRect.offsetMax = new Vector2(-10f, 0f);

        input.placeholder = placeholder;
        input.textComponent = text;

        return input;
    }

    void CreateOverlayButton(Transform parent, string text, UnityEngine.Events.UnityAction callback)
    {
        GameObject buttonGO = new GameObject(text.Replace(" ", "") + "OverlayButton");
        buttonGO.transform.SetParent(parent, false);

        Image img = buttonGO.AddComponent<Image>();
        img.color = new Color(0.25f, 0.4f, 0.8f, 1f);

        Button button = buttonGO.AddComponent<Button>();
        button.onClick.AddListener(callback);

        RectTransform rect = buttonGO.GetComponent<RectTransform>();
        rect.sizeDelta = new Vector2(300f, 55f);

        GameObject textGO = new GameObject("Text");
        textGO.transform.SetParent(buttonGO.transform, false);
        Text buttonText = textGO.AddComponent<Text>();
        buttonText.text = text;
        buttonText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        buttonText.fontSize = 22;
        buttonText.alignment = TextAnchor.MiddleCenter;
        buttonText.color = Color.white;

        RectTransform textRect = buttonText.GetComponent<RectTransform>();
        textRect.anchorMin = Vector2.zero;
        textRect.anchorMax = Vector2.one;
        textRect.offsetMin = Vector2.zero;
        textRect.offsetMax = Vector2.zero;
    }
}
