using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

/// <summary>
/// Test interface for voice commands - demonstrates how to use VoiceCommandProcessor
/// Attach this to a GameObject to test voice commands via Inspector or keyboard
/// </summary>
public class VoiceCommandTester : MonoBehaviour
{
    [Header("References")]
    public VoiceCommandProcessor commandProcessor;
    
    [Header("Test Commands")]
    [TextArea(3, 10)]
    public string testJsonCommand = @"{""command"":""move_vertex"", ""vertex"":1, ""offset"":{""x"":0,""y"":0,""z"":-0.07}}";
    
    [Header("Quick Tests")]
    public KeyCode testKey = KeyCode.T;
    
    void Start()
    {
        if (commandProcessor == null)
            commandProcessor = FindAnyObjectByType<VoiceCommandProcessor>();
    }
    
    void Update()
    {
        bool keyPressed = false;
        
        #if ENABLE_INPUT_SYSTEM
        Keyboard keyboard = Keyboard.current;
        if (keyboard != null)
        {
            keyPressed = keyboard.tKey.wasPressedThisFrame;
        }
        #else
        keyPressed = Input.GetKeyDown(testKey);
        #endif
        
        if (keyPressed)
        {
            ExecuteTestCommand();
        }
    }
    
    /// <summary>
    /// Execute the JSON command in the inspector field
    /// </summary>
    public void ExecuteTestCommand()
    {
        if (commandProcessor == null)
        {
            Debug.LogError("No VoiceCommandProcessor assigned!");
            return;
        }
        
        var result = commandProcessor.ProcessCommand(testJsonCommand);
        
        if (result.success)
            Debug.Log($"✓ Command succeeded: {result.message}");
        else
            Debug.LogError($"✗ Command failed: {result.message}");
    }
    
    #region Example Voice Commands (from Inspector buttons)
    
    [ContextMenu("Example: Move V1 Back 7cm")]
    public void Example_MoveV1Back7cm()
    {
        string json = @"{
            ""command"":""move_vertex"",
            ""vertex"":1,
            ""offset"":{""x"":0, ""y"":0, ""z"":-0.07}
        }";
        
        Debug.Log("Executing: 'Move vertex 1 back 7 centimeters'");
        var result = commandProcessor.ProcessCommand(json);
        Debug.Log(result.success ? $"✓ {result.message}" : $"✗ {result.message}");
    }
    
    [ContextMenu("Example: Move V0 Up 5cm")]
    public void Example_MoveV0Up5cm()
    {
        string json = @"{
            ""command"":""move_vertex"",
            ""vertex"":0,
            ""offset"":{""x"":0, ""y"":0.05, ""z"":0}
        }";
        
        Debug.Log("Executing: 'Move vertex 0 up 5 centimeters'");
        var result = commandProcessor.ProcessCommand(json);
        Debug.Log(result.success ? $"✓ {result.message}" : $"✗ {result.message}");
    }
    
    [ContextMenu("Example: Move vertices 0,1,2 right 3cm")]
    public void Example_MoveMultipleVertices()
    {
        string json = @"{
            ""command"":""move_vertices"",
            ""vertices"":[0,1,2],
            ""offset"":{""x"":0.03, ""y"":0, ""z"":0}
        }";
        
        Debug.Log("Executing: 'Move vertices 0, 1, 2 right 3 centimeters'");
        var result = commandProcessor.ProcessCommand(json);
        Debug.Log(result.success ? $"✓ {result.message}" : $"✗ {result.message}");
    }
    
    [ContextMenu("Example: Set V1 to position (0.5, 0.5, 0.5)")]
    public void Example_SetVertexPosition()
    {
        string json = @"{
            ""command"":""set_vertex"",
            ""vertex"":1,
            ""position"":{""x"":0.5, ""y"":0.5, ""z"":0.5}
        }";
        
        Debug.Log("Executing: 'Set vertex 1 to position 0.5, 0.5, 0.5'");
        var result = commandProcessor.ProcessCommand(json);
        Debug.Log(result.success ? $"✓ {result.message}" : $"✗ {result.message}");
    }
    
    [ContextMenu("Example: Translate mesh forward 1 meter")]
    public void Example_TranslateMesh()
    {
        string json = @"{
            ""command"":""translate_mesh"",
            ""offset"":{""x"":0, ""y"":0, ""z"":1}
        }";
        
        Debug.Log("Executing: 'Move mesh forward 1 meter'");
        var result = commandProcessor.ProcessCommand(json);
        Debug.Log(result.success ? $"✓ {result.message}" : $"✗ {result.message}");
    }
    
    [ContextMenu("Example: Rotate mesh 45 degrees on Y axis")]
    public void Example_RotateMesh()
    {
        string json = @"{
            ""command"":""rotate_mesh"",
            ""rotation"":{""x"":0, ""y"":45, ""z"":0}
        }";
        
        Debug.Log("Executing: 'Rotate mesh 45 degrees'");
        var result = commandProcessor.ProcessCommand(json);
        Debug.Log(result.success ? $"✓ {result.message}" : $"✗ {result.message}");
    }
    
    [ContextMenu("Example: Scale mesh to 1.5x")]
    public void Example_ScaleMesh()
    {
        string json = @"{
            ""command"":""scale_mesh"",
            ""scale"":1.5
        }";
        
        Debug.Log("Executing: 'Scale mesh to 1.5 times'");
        var result = commandProcessor.ProcessCommand(json);
        Debug.Log(result.success ? $"✓ {result.message}" : $"✗ {result.message}");
    }
    
    [ContextMenu("Example: Reset vertex 1")]
    public void Example_ResetVertex()
    {
        string json = @"{
            ""command"":""reset_vertex"",
            ""vertex"":1
        }";
        
        Debug.Log("Executing: 'Reset vertex 1'");
        var result = commandProcessor.ProcessCommand(json);
        Debug.Log(result.success ? $"✓ {result.message}" : $"✗ {result.message}");
    }
    
    [ContextMenu("Example: Rebuild entire mesh")]
    public void Example_RebuildMesh()
    {
        string json = @"{
            ""command"":""rebuild_mesh""
        }";
        
        Debug.Log("Executing: 'Rebuild mesh from source'");
        var result = commandProcessor.ProcessCommand(json);
        Debug.Log(result.success ? $"✓ {result.message}" : $"✗ {result.message}");
    }
    
    #endregion
}

