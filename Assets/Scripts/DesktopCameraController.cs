using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

/// <summary>
/// First-person camera controller for desktop mode.
/// Provides WASD movement, mouse look, and typical FPS-style controls.
/// Automatically disabled when in XR mode.
/// </summary>
public class DesktopCameraController : MonoBehaviour
{
    [Header("Movement")]
    [Tooltip("Base movement speed in meters per second")]
    public float moveSpeed = 5f;
    
    [Tooltip("Speed multiplier when holding sprint key")]
    public float sprintMultiplier = 2f;
    
    [Tooltip("Smoothing time for movement acceleration")]
    public float smoothTime = 0.1f;
    
    [Header("Look")]
    [Tooltip("Mouse sensitivity multiplier")]
    public float mouseSensitivity = 2f;
    
    [Tooltip("Maximum vertical look angle in degrees")]
    public float maxLookAngle = 85f;
    
    [Tooltip("Invert Y-axis look")]
    public bool invertY = false;
    
    [Header("Vertical Movement")]
    [Tooltip("Allow vertical movement with Q/E keys")]
    public bool allowVerticalMovement = true;
    
    [Header("Mouse Look")]
    [Tooltip("Enable mouse look on start")]
    public bool mouseLookOnStart = false;
    
    [Tooltip("Key to toggle mouse look on/off")]
    public KeyCode toggleMouseLookKey = KeyCode.LeftAlt;
    
    [Header("Crosshair")]
    [Tooltip("Show crosshair when mouse look is active")]
    public bool showCrosshair = true;
    public Color crosshairColor = new Color(1f, 1f, 1f, 0.8f);
    public float crosshairSize = 10f;
    public float crosshairThickness = 2f;
    
    // Private state
    private Vector3 currentVelocity;
    private float pitch = 0f;
    private bool mouseLookEnabled;
    
    void Start()
    {
        mouseLookEnabled = mouseLookOnStart;
        UpdateCursorState();
    }
    
    void Update()
    {
        HandleMouseLookToggle();
        
        // Only handle mouse look if enabled AND cursor is actually locked
        // This prevents mouse look from interfering with UI interactions or object transformations
        if (mouseLookEnabled && Cursor.lockState == CursorLockMode.Locked)
        {
            HandleLook();
        }
        
        HandleMovement();
    }
    
    void HandleMouseLookToggle()
    {
        bool togglePressed = false;
        
        #if ENABLE_INPUT_SYSTEM
        Keyboard keyboard = Keyboard.current;
        if (keyboard != null)
        {
            togglePressed = keyboard.leftAltKey.wasPressedThisFrame || keyboard.rightAltKey.wasPressedThisFrame;
        }
        #else
        togglePressed = Input.GetKeyDown(toggleMouseLookKey);
        #endif
        
        if (togglePressed)
        {
            mouseLookEnabled = !mouseLookEnabled;
            UpdateCursorState();
        }
    }
    
    void UpdateCursorState()
    {
        if (mouseLookEnabled)
        {
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
        }
        else
        {
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
        }
    }
    
    void HandleMovement()
    {
        Vector3 moveInput = Vector3.zero;
        bool sprint = false;
        
        #if ENABLE_INPUT_SYSTEM
        var keyboard = Keyboard.current;
        if (keyboard != null)
        {
            // Horizontal movement
            if (keyboard.wKey.isPressed) moveInput.z += 1;
            if (keyboard.sKey.isPressed) moveInput.z -= 1;
            if (keyboard.aKey.isPressed) moveInput.x -= 1;
            if (keyboard.dKey.isPressed) moveInput.x += 1;
            
            // Vertical movement
            if (allowVerticalMovement)
            {
                if (keyboard.qKey.isPressed) moveInput.y -= 1;
                if (keyboard.eKey.isPressed) moveInput.y += 1;
            }
            
            // Sprint
            sprint = keyboard.shiftKey.isPressed;
        }
        #else
        // Legacy Input System
        moveInput.x = Input.GetAxis("Horizontal");
        moveInput.z = Input.GetAxis("Vertical");
        
        if (allowVerticalMovement)
        {
            if (Input.GetKey(KeyCode.Q)) moveInput.y -= 1;
            if (Input.GetKey(KeyCode.E)) moveInput.y += 1;
        }
        
        sprint = Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift);
        #endif
        
        // Normalize to prevent faster diagonal movement
        if (moveInput.sqrMagnitude > 1f)
        {
            moveInput.Normalize();
        }
        
        // Calculate speed with sprint multiplier
        float currentSpeed = moveSpeed * (sprint ? sprintMultiplier : 1f);
        
        // Transform movement to world space
        Vector3 targetMove = transform.TransformDirection(moveInput);
        
        // Apply movement with smoothing
        transform.position += targetMove * (currentSpeed * Time.deltaTime);
    }
    
    void HandleLook()
    {
        Vector2 mouseDelta = Vector2.zero;
        
        #if ENABLE_INPUT_SYSTEM
        var mouse = Mouse.current;
        if (mouse != null)
        {
            mouseDelta = mouse.delta.ReadValue();
        }
        #else
        mouseDelta = new Vector2(Input.GetAxis("Mouse X"), Input.GetAxis("Mouse Y"));
        #endif
        
        // Apply sensitivity
        Vector2 lookInput = mouseDelta * mouseSensitivity;
        
        // Horizontal rotation (yaw) - rotate around world Y axis
        transform.Rotate(Vector3.up * lookInput.x, Space.World);
        
        // Vertical rotation (pitch) - rotate around local X axis
        float pitchDelta = invertY ? lookInput.y : -lookInput.y;
        pitch += pitchDelta;
        pitch = Mathf.Clamp(pitch, -maxLookAngle, maxLookAngle);
        
        // Apply pitch rotation
        Vector3 currentRotation = transform.localEulerAngles;
        currentRotation.x = pitch;
        transform.localEulerAngles = currentRotation;
    }
    
    
    // Public methods for external control
    public void SetPosition(Vector3 position)
    {
        transform.position = position;
    }
    
    public void SetRotation(float yaw, float pitch)
    {
        this.pitch = Mathf.Clamp(pitch, -maxLookAngle, maxLookAngle);
        Vector3 euler = transform.eulerAngles;
        euler.y = yaw;
        euler.x = this.pitch;
        transform.eulerAngles = euler;
    }
    
    public bool IsMouseLookEnabled()
    {
        return mouseLookEnabled;
    }

    public void ToggleMouseLook()
    {
        mouseLookEnabled = !mouseLookEnabled;
        UpdateCursorState();
    }

    public void SetMouseLook(bool enabled)
    {
        if (mouseLookEnabled != enabled)
        {
            mouseLookEnabled = enabled;
            UpdateCursorState();
        }
    }

    void OnGUI()
    {
        if (mouseLookEnabled && showCrosshair)
        {
            DrawCrosshair();
        }
        else if (!mouseLookEnabled)
        {
            GUIStyle style = new GUIStyle(GUI.skin.box);
            style.normal.textColor = Color.cyan;
            style.fontSize = 16;
            style.fontStyle = FontStyle.Bold;
            style.alignment = TextAnchor.MiddleCenter;
            
            string message = "Mouse Look OFF - Press Left Alt to toggle\nWASD still works";
            Vector2 size = new Vector2(400, 60);
            Vector2 pos = new Vector2((Screen.width - size.x) / 2, 20);
            
            GUI.Box(new Rect(pos.x, pos.y, size.x, size.y), message, style);
        }
    }
    
    void DrawCrosshair()
    {
        float centerX = Screen.width / 2f;
        float centerY = Screen.height / 2f;
        
        // Create texture for crosshair if needed
        Texture2D lineTexture = new Texture2D(1, 1);
        lineTexture.SetPixel(0, 0, crosshairColor);
        lineTexture.Apply();
        
        // Draw horizontal line
        GUI.DrawTexture(
            new Rect(centerX - crosshairSize, centerY - crosshairThickness / 2, crosshairSize * 2, crosshairThickness),
            lineTexture
        );
        
        // Draw vertical line
        GUI.DrawTexture(
            new Rect(centerX - crosshairThickness / 2, centerY - crosshairSize, crosshairThickness, crosshairSize * 2),
            lineTexture
        );
        
        // Draw center dot
        GUI.DrawTexture(
            new Rect(centerX - 1, centerY - 1, 2, 2),
            lineTexture
        );
    }
}

