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
    
    [Header("Cursor")]
    [Tooltip("Lock cursor on start")]
    public bool lockCursorOnStart = true;
    
    [Tooltip("Key to toggle cursor lock")]
    public KeyCode toggleCursorKey = KeyCode.Escape;
    
    // Private state
    private Vector3 currentVelocity;
    private float pitch = 0f;
    private bool cursorLocked;
    
    void Start()
    {
        if (lockCursorOnStart)
        {
            LockCursor(true);
        }
    }
    
    void Update()
    {
        HandleCursorToggle();
        
        if (cursorLocked)
        {
            HandleLook();
        }
        
        HandleMovement();
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
    
    void HandleCursorToggle()
    {
        bool togglePressed = false;
        
        #if ENABLE_INPUT_SYSTEM
        var keyboard = Keyboard.current;
        if (keyboard != null)
        {
            // Check the specific key based on toggleCursorKey
            if (toggleCursorKey == KeyCode.Escape)
                togglePressed = keyboard.escapeKey.wasPressedThisFrame;
            else if (toggleCursorKey == KeyCode.Tab)
                togglePressed = keyboard.tabKey.wasPressedThisFrame;
            else if (toggleCursorKey == KeyCode.BackQuote)
                togglePressed = keyboard.backquoteKey.wasPressedThisFrame;
            // Add more key mappings as needed
        }
        #else
        togglePressed = Input.GetKeyDown(toggleCursorKey);
        #endif
        
        if (togglePressed)
        {
            LockCursor(!cursorLocked);
        }
    }
    
    void LockCursor(bool lockState)
    {
        cursorLocked = lockState;
        
        if (lockState)
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
    
    public bool IsCursorLocked()
    {
        return cursorLocked;
    }
}

