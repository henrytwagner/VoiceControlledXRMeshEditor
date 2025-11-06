using UnityEngine;

/// <summary>
/// Interface for objects that can be interacted with in both VR and desktop modes.
/// Implement this interface on GameObjects that should respond to universal interactions.
/// </summary>
public interface IInteractable
{
    /// <summary>
    /// Called when the object is interacted with (VR trigger or mouse click)
    /// </summary>
    void OnInteract();
    
    /// <summary>
    /// Optional: Called when pointer/ray enters the object
    /// </summary>
    void OnHoverEnter();
    
    /// <summary>
    /// Optional: Called when pointer/ray exits the object
    /// </summary>
    void OnHoverExit();
}

/// <summary>
/// Example implementation of IInteractable
/// </summary>
public class ExampleInteractable : MonoBehaviour, IInteractable
{
    [Header("Interaction Settings")]
    public Color normalColor = Color.white;
    public Color hoverColor = Color.yellow;
    public Color clickColor = Color.green;
    
    private Renderer objectRenderer;
    private MaterialPropertyBlock propBlock;
    
    void Start()
    {
        objectRenderer = GetComponent<Renderer>();
        propBlock = new MaterialPropertyBlock();
    }
    
    public void OnInteract()
    {
        Debug.Log($"[{gameObject.name}] Interacted!");
        
        // Flash the object
        if (objectRenderer != null)
        {
            objectRenderer.GetPropertyBlock(propBlock);
            propBlock.SetColor("_Color", clickColor);
            objectRenderer.SetPropertyBlock(propBlock);
        }
        
        // Add your interaction logic here
        // Examples:
        // - Toggle active state
        // - Play animation
        // - Spawn object
        // - Open UI
    }
    
    public void OnHoverEnter()
    {
        Debug.Log($"[{gameObject.name}] Hover enter");
        
        if (objectRenderer != null)
        {
            objectRenderer.GetPropertyBlock(propBlock);
            propBlock.SetColor("_Color", hoverColor);
            objectRenderer.SetPropertyBlock(propBlock);
        }
    }
    
    public void OnHoverExit()
    {
        Debug.Log($"[{gameObject.name}] Hover exit");
        
        if (objectRenderer != null)
        {
            objectRenderer.GetPropertyBlock(propBlock);
            propBlock.SetColor("_Color", normalColor);
            objectRenderer.SetPropertyBlock(propBlock);
        }
    }
}

