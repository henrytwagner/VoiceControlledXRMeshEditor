using UnityEngine;
using UnityEngine.XR;

/// <summary>
/// Helper component to expose runtime editing actions to UI buttons.
/// Attach this to a manager object and wire up the public methods from the inspector.
/// </summary>
public class RuntimeUIButtonBinder : MonoBehaviour
{
    [Header("References")]
    public MeshSpawner meshSpawner;
    public ObjectSelector objectSelector;
    public RuntimeMeshEditor meshEditor;
    public GameObject contentRootMenu;
    
    [Header("Controller Toggle")]
    public bool enableControllerMenuToggle = false;
    public XRNode controllerNode = XRNode.LeftHand;
    public bool usePrimaryButton = true;
    public bool useSecondaryButton = false;

    private bool lastPrimaryPressed = false;
    private bool lastSecondaryPressed = false;

    void Reset()
    {
        AutoAssign();
    }

    void OnValidate()
    {
        if (!Application.isPlaying)
        {
            AutoAssign();
        }
    }

    void AutoAssign()
    {
        if (meshSpawner == null)
            meshSpawner = UnityCompatibility.FindAnyObject<MeshSpawner>();
        if (objectSelector == null)
            objectSelector = UnityCompatibility.FindAnyObject<ObjectSelector>();
        if (meshEditor == null)
            meshEditor = UnityCompatibility.FindAnyObject<RuntimeMeshEditor>();
    }

    void Update()
    {
        if (!enableControllerMenuToggle || contentRootMenu == null)
            return;

        InputDevice device = InputDevices.GetDeviceAtXRNode(controllerNode);
        if (!device.isValid)
            return;

        if (usePrimaryButton)
        {
            bool primaryPressed = false;
            if (device.TryGetFeatureValue(CommonUsages.primaryButton, out primaryPressed))
            {
                if (primaryPressed && !lastPrimaryPressed)
                {
                    ToggleContentMenu();
                }
                lastPrimaryPressed = primaryPressed;
            }
        }

        if (useSecondaryButton)
        {
            bool secondaryPressed = false;
            if (device.TryGetFeatureValue(CommonUsages.secondaryButton, out secondaryPressed))
            {
                if (secondaryPressed && !lastSecondaryPressed)
                {
                    ToggleContentMenu();
                }
                lastSecondaryPressed = secondaryPressed;
            }
        }
    }

    EditableMesh GetSelectedEditableMesh()
    {
        if (objectSelector == null)
            return null;

        Transform selected = objectSelector.GetCurrentSelection();
        if (selected == null)
            return null;

        return selected.GetComponent<EditableMesh>();
    }

    public void SpawnCube()
    {
        meshSpawner?.SpawnPrimitive(PrimitiveType.Cube);
    }

    public void ClearAllObjects()
    {
        meshSpawner?.ClearAll();
    }

    public void ToggleSelectedEditMode()
    {
        EditableMesh mesh = GetSelectedEditableMesh();
        if (mesh != null)
        {
            mesh.ToggleMode();
        }
    }

    public void ToggleTranslateMode()
    {
        meshEditor?.ToggleTranslateMode();
    }

    public void ToggleRotateMode()
    {
        meshEditor?.ToggleRotateMode();
    }

    public void ToggleScaleMode()
    {
        meshEditor?.ToggleScaleMode();
    }

    public void ReturnToVertexMode()
    {
        meshEditor?.SetVertexMode();
    }

    public void ToggleContentMenu()
    {
        if (contentRootMenu != null)
        {
            contentRootMenu.SetActive(!contentRootMenu.activeSelf);
        }
    }

    public void CloseContentMenu()
    {
        if (contentRootMenu != null)
        {
            contentRootMenu.SetActive(false);
        }
    }
}

