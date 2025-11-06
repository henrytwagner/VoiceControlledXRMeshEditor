using UnityEngine;
using System;
using System.Collections.Generic;

/// <summary>
/// Processes voice commands (via AI-generated JSON) to edit meshes.
/// This is the bridge between voice/AI input and mesh manipulation.
/// </summary>
public class VoiceCommandProcessor : MonoBehaviour
{
    [Header("References")]
    public EditableMesh targetMesh; // Legacy: specific mesh
    public ObjectSelector objectSelector; // For getting currently selected object
    
    [Header("Settings")]
    public bool logCommands = true;
    [Tooltip("Use currently selected object if no object_name specified in command")]
    public bool useSelectedObject = true;
    
    // Command result feedback
    public struct CommandResult
    {
        public bool success;
        public string message;
        
        public CommandResult(bool success, string message)
        {
            this.success = success;
            this.message = message;
        }
    }
    
    void Start()
    {
        if (targetMesh == null)
            targetMesh = FindAnyObjectByType<EditableMesh>();
        
        if (objectSelector == null)
            objectSelector = FindAnyObjectByType<ObjectSelector>();
    }
    
    #region JSON Command Processing
    
    /// <summary>
    /// Main entry point for processing JSON commands from AI/VLM
    /// </summary>
    public CommandResult ProcessCommand(string jsonCommand)
    {
        if (string.IsNullOrEmpty(jsonCommand))
            return new CommandResult(false, "Empty command");
        
        try
        {
            // Parse JSON into command object
            MeshCommand command = JsonUtility.FromJson<MeshCommand>(jsonCommand);
            
            if (logCommands)
                Debug.Log($"[VoiceCommand] Processing: {command.command}");
            
            return ExecuteCommand(command);
        }
        catch (Exception e)
        {
            return new CommandResult(false, $"JSON parse error: {e.Message}");
        }
    }
    
    /// <summary>
    /// Execute a parsed command
    /// </summary>
    CommandResult ExecuteCommand(MeshCommand command)
    {
        // Get the target mesh for this command
        EditableMesh mesh = GetTargetMesh(command);
        
        if (mesh == null)
            return new CommandResult(false, command.object_name != null ? 
                $"Object '{command.object_name}' not found" : 
                "No target mesh assigned or selected");
        
        // Temporarily set targetMesh for legacy command methods
        EditableMesh previousTarget = targetMesh;
        targetMesh = mesh;
        
        CommandResult result;
        switch (command.command.ToLower())
        {
            case "move_vertex":
                result = MoveVertex(command);
                break;
            case "move_vertices":
                result = MoveVertices(command);
                break;
            case "set_vertex":
                result = SetVertex(command);
                break;
            case "translate_mesh":
                result = TranslateMesh(command);
                break;
            case "rotate_mesh":
                result = RotateMesh(command);
                break;
            case "scale_mesh":
                result = ScaleMesh(command);
                break;
            case "reset_vertex":
                result = ResetVertex(command);
                break;
            case "rebuild_mesh":
                result = RebuildMesh(command);
                break;
            case "get_vertex_position":
                result = GetVertexPosition(command);
                break;
            case "get_all_vertices":
                result = GetAllVertexPositions();
                break;
            case "get_mesh_transform":
                result = GetMeshTransform();
                break;
            case "list_objects":
                result = ListObjects();
                break;
            default:
                result = new CommandResult(false, $"Unknown command: {command.command}");
                break;
        }
        
        // Restore previous target
        targetMesh = previousTarget;
        return result;
    }
    
    /// <summary>
    /// Get the target mesh based on command object_name or current selection
    /// </summary>
    EditableMesh GetTargetMesh(MeshCommand command)
    {
        // If object_name is specified, find by name
        if (!string.IsNullOrEmpty(command.object_name))
        {
            EditableMesh[] allMeshes = FindObjectsByType<EditableMesh>(FindObjectsSortMode.None);
            foreach (EditableMesh mesh in allMeshes)
            {
                if (mesh.gameObject.name == command.object_name)
                    return mesh;
            }
            return null; // Object not found
        }
        
        // If useSelectedObject is enabled, try to get selected object
        if (useSelectedObject && objectSelector != null)
        {
            Transform selected = objectSelector.GetCurrentSelection();
            if (selected != null)
            {
                EditableMesh mesh = selected.GetComponent<EditableMesh>();
                if (mesh != null)
                    return mesh;
            }
        }
        
        // Fallback to assigned targetMesh
        return targetMesh;
    }
    
    #endregion
    
    #region Vertex Commands
    
    /// <summary>
    /// Move a single vertex by an offset
    /// JSON: {"command":"move_vertex", "vertex":1, "offset":{"x":0,"y":0.1,"z":-0.07}}
    /// </summary>
    CommandResult MoveVertex(MeshCommand cmd)
    {
        if (!ValidateVertexIndex(cmd.vertex, out string error))
            return new CommandResult(false, error);
        
        Vector3 currentPos = targetMesh.GetVertex(cmd.vertex);
        Vector3 newPos = currentPos + cmd.offset;
        targetMesh.SetVertex(cmd.vertex, newPos);
        
        return new CommandResult(true, $"Moved vertex {cmd.vertex} by {cmd.offset}");
    }
    
    /// <summary>
    /// Move multiple vertices by the same offset
    /// JSON: {"command":"move_vertices", "vertices":[0,1,2], "offset":{"x":0,"y":0.1,"z":0}}
    /// </summary>
    CommandResult MoveVertices(MeshCommand cmd)
    {
        if (cmd.vertices == null || cmd.vertices.Length == 0)
            return new CommandResult(false, "No vertices specified");
        
        int movedCount = 0;
        foreach (int vertexIndex in cmd.vertices)
        {
            if (!ValidateVertexIndex(vertexIndex, out string error))
            {
                Debug.LogWarning($"Skipping vertex {vertexIndex}: {error}");
                continue;
            }
            
            Vector3 currentPos = targetMesh.GetVertex(vertexIndex);
            Vector3 newPos = currentPos + cmd.offset;
            targetMesh.SetVertex(vertexIndex, newPos);
            movedCount++;
        }
        
        return new CommandResult(true, $"Moved {movedCount} vertices by {cmd.offset}");
    }
    
    /// <summary>
    /// Set a vertex to an absolute position
    /// JSON: {"command":"set_vertex", "vertex":1, "position":{"x":0.5,"y":0.5,"z":0.5}}
    /// </summary>
    CommandResult SetVertex(MeshCommand cmd)
    {
        if (!ValidateVertexIndex(cmd.vertex, out string error))
            return new CommandResult(false, error);
        
        targetMesh.SetVertex(cmd.vertex, cmd.position);
        
        return new CommandResult(true, $"Set vertex {cmd.vertex} to {cmd.position}");
    }
    
    /// <summary>
    /// Reset a vertex to its original position from source mesh
    /// JSON: {"command":"reset_vertex", "vertex":1}
    /// </summary>
    CommandResult ResetVertex(MeshCommand cmd)
    {
        if (!ValidateVertexIndex(cmd.vertex, out string error))
            return new CommandResult(false, error);
        
        // Get original position from source mesh
        if (targetMesh.sourceMesh == null)
            return new CommandResult(false, "No source mesh to reset from");
        
        Vector3[] sourceVerts = targetMesh.sourceMesh.vertices;
        if (cmd.vertex >= sourceVerts.Length)
            return new CommandResult(false, "Vertex index out of range in source mesh");
        
        targetMesh.SetVertex(cmd.vertex, sourceVerts[cmd.vertex]);
        
        return new CommandResult(true, $"Reset vertex {cmd.vertex} to original position");
    }
    
    #endregion
    
    #region Mesh Transform Commands
    
    /// <summary>
    /// Translate the entire mesh
    /// JSON: {"command":"translate_mesh", "offset":{"x":0.5,"y":0,"z":0}}
    /// </summary>
    CommandResult TranslateMesh(MeshCommand cmd)
    {
        targetMesh.transform.position += cmd.offset;
        return new CommandResult(true, $"Translated mesh by {cmd.offset}");
    }
    
    /// <summary>
    /// Rotate the mesh
    /// JSON: {"command":"rotate_mesh", "rotation":{"x":0,"y":90,"z":0}}
    /// </summary>
    CommandResult RotateMesh(MeshCommand cmd)
    {
        targetMesh.transform.Rotate(cmd.rotation.x, cmd.rotation.y, cmd.rotation.z, Space.World);
        return new CommandResult(true, $"Rotated mesh by {cmd.rotation}");
    }
    
    /// <summary>
    /// Scale the mesh (uniform or per-axis)
    /// JSON: {"command":"scale_mesh", "scale":1.5}  OR  {"command":"scale_mesh", "scaleVector":{"x":1,"y":2,"z":1}}
    /// </summary>
    CommandResult ScaleMesh(MeshCommand cmd)
    {
        if (cmd.scale > 0)
        {
            // Uniform scale
            targetMesh.transform.localScale = Vector3.one * cmd.scale;
            return new CommandResult(true, $"Scaled mesh uniformly to {cmd.scale}");
        }
        else if (cmd.scaleVector != Vector3.zero)
        {
            // Per-axis scale
            targetMesh.transform.localScale = cmd.scaleVector;
            return new CommandResult(true, $"Scaled mesh to {cmd.scaleVector}");
        }
        
        return new CommandResult(false, "No valid scale value provided");
    }
    
    #endregion
    
    #region Utility Commands
    
    /// <summary>
    /// Rebuild mesh from source
    /// JSON: {"command":"rebuild_mesh"}
    /// </summary>
    CommandResult RebuildMesh(MeshCommand cmd)
    {
        targetMesh.RebuildFromSource();
        return new CommandResult(true, "Rebuilt mesh from source");
    }
    
    #endregion
    
    #region Helper Functions
    
    bool ValidateVertexIndex(int index, out string error)
    {
        if (index < 0)
        {
            error = "Vertex index cannot be negative";
            return false;
        }
        
        int vertexCount = targetMesh.GetVertexCount();
        if (index >= vertexCount)
        {
            error = $"Vertex index {index} out of range (mesh has {vertexCount} vertices)";
            return false;
        }
        
        error = null;
        return true;
    }
    
    /// <summary>
    /// Convert centimeters to Unity units (meters)
    /// </summary>
    public static float CmToUnits(float cm)
    {
        return cm * 0.01f;
    }
    
    /// <summary>
    /// Convert Unity units (meters) to centimeters
    /// </summary>
    public static float UnitsToCm(float units)
    {
        return units * 100f;
    }
    
    #endregion
    
    #region Public API Functions (for testing/debugging)
    
    /// <summary>
    /// Get current vertex position (useful for AI context)
    /// </summary>
    public Vector3 GetVertexPosition(int vertexIndex)
    {
        if (!ValidateVertexIndex(vertexIndex, out string error))
        {
            Debug.LogError(error);
            return Vector3.zero;
        }
        
        return targetMesh.GetVertex(vertexIndex);
    }
    
    /// <summary>
    /// Get all vertex positions (useful for AI context)
    /// </summary>
    public Vector3[] GetAllVertexPositions()
    {
        return targetMesh.GetVertices();
    }
    
    /// <summary>
    /// Get mesh transform info (useful for AI context)
    /// </summary>
    public (Vector3 position, Quaternion rotation, Vector3 scale) GetMeshTransform()
    {
        return (
            targetMesh.transform.position,
            targetMesh.transform.rotation,
            targetMesh.transform.localScale
        );
    }
    
    /// <summary>
    /// List all editable objects in the scene
    /// JSON: {"command":"list_objects"}
    /// </summary>
    CommandResult ListObjects()
    {
        EditableMesh[] allMeshes = FindObjectsByType<EditableMesh>(FindObjectsSortMode.None);
        
        if (allMeshes.Length == 0)
            return new CommandResult(true, "No objects in scene");
        
        string objectList = "Objects in scene:\n";
        for (int i = 0; i < allMeshes.Length; i++)
        {
            EditableMesh mesh = allMeshes[i];
            int vertexCount = mesh.GetVertexCount();
            Vector3 pos = mesh.transform.position;
            objectList += $"- {mesh.gameObject.name}: {vertexCount} vertices, position ({pos.x:F2}, {pos.y:F2}, {pos.z:F2})\n";
        }
        
        Debug.Log(objectList);
        return new CommandResult(true, objectList);
    }
    
    #endregion
}

/// <summary>
/// Data structure for mesh commands from JSON
/// </summary>
[System.Serializable]
public class MeshCommand
{
    public string command;              // Command type (e.g., "move_vertex")
    public string object_name;          // Optional: Name of object to target (e.g., "Cube_1")
    public int vertex;                  // Single vertex index
    public int[] vertices;              // Multiple vertex indices
    public Vector3 offset;              // Offset for movement
    public Vector3 position;            // Absolute position
    public Vector3 rotation;            // Rotation (euler angles)
    public float scale;                 // Uniform scale
    public Vector3 scaleVector;         // Non-uniform scale
}

