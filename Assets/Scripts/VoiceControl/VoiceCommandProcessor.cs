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
    public MeshSpawner meshSpawner; // For creating objects
    public TransformPanel transformPanel; // For UI control
    public OrientationGizmo orientationGizmo; // For gizmo control
    public DesktopCameraController cameraController; // For camera control
    public RuntimeMeshEditor runtimeMeshEditor; // For label control
    
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
        // Auto-find components if not assigned
        if (targetMesh == null)
            targetMesh = FindAnyObjectByType<EditableMesh>();
        
        if (objectSelector == null)
            objectSelector = FindAnyObjectByType<ObjectSelector>();
        
        if (meshSpawner == null)
            meshSpawner = FindAnyObjectByType<MeshSpawner>();
        
        if (transformPanel == null)
            transformPanel = FindAnyObjectByType<TransformPanel>();
        
        if (orientationGizmo == null)
            orientationGizmo = FindAnyObjectByType<OrientationGizmo>();
        
        if (cameraController == null)
            cameraController = FindAnyObjectByType<DesktopCameraController>();
        
        if (runtimeMeshEditor == null)
            runtimeMeshEditor = FindAnyObjectByType<RuntimeMeshEditor>();
        
        Debug.Log($"[VoiceCommandProcessor] Initialized - useSelectedObject: {useSelectedObject}");
        if (objectSelector != null)
            Debug.Log($"[VoiceCommandProcessor] ObjectSelector found, will use selected object");
        else
            Debug.LogWarning("[VoiceCommandProcessor] No ObjectSelector found! Assign it in Inspector or ensure one exists in scene.");
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
            
            // Navigation & Control Commands
            case "spawn_object":
            case "create_object":
                result = SpawnObject(command);
                break;
            case "delete_object":
            case "remove_object":
                result = DeleteObject(command);
                break;
            case "select_object":
                result = SelectObject(command);
                break;
            case "set_mode":
            case "switch_mode":
                result = SetMode(command);
                break;
            case "move_camera":
                result = MoveCamera(command);
                break;
            case "toggle_transform_panel":
                result = ToggleTransformPanel(command);
                break;
            case "toggle_orientation_gizmo":
                result = ToggleOrientationGizmo(command);
                break;
            case "toggle_mouse_look":
                result = ToggleMouseLook(command);
                break;
            case "toggle_labels":
                result = ToggleLabels(command);
                break;
            case "clear_all":
                result = ClearAll();
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
        EditableMesh result = null;
        
        // If object_name is specified, find by name
        if (!string.IsNullOrEmpty(command.object_name))
        {
            EditableMesh[] allMeshes = FindObjectsByType<EditableMesh>(FindObjectsSortMode.None);
            foreach (EditableMesh mesh in allMeshes)
            {
                if (mesh.gameObject.name == command.object_name)
                {
                    result = mesh;
                    Debug.Log($"[VoiceCommand] Targeting object by name: {command.object_name}");
                    break;
                }
            }
            
            if (result == null)
                Debug.LogWarning($"[VoiceCommand] Object '{command.object_name}' not found in scene!");
            
            return result;
        }
        
        // If useSelectedObject is enabled, try to get selected object
        if (useSelectedObject && objectSelector != null)
        {
            Transform selected = objectSelector.GetCurrentSelection();
            if (selected != null)
            {
                EditableMesh mesh = selected.GetComponent<EditableMesh>();
                if (mesh != null)
                {
                    Debug.Log($"[VoiceCommand] Targeting selected object: {mesh.gameObject.name}");
                    return mesh;
                }
                else
                {
                    Debug.LogWarning($"[VoiceCommand] Selected object '{selected.name}' has no EditableMesh component!");
                }
            }
            else
            {
                Debug.LogWarning("[VoiceCommand] No object selected! Select an object first or specify 'object_name' in JSON.");
            }
        }
        
        // Fallback to assigned targetMesh
        if (targetMesh != null)
        {
            Debug.Log($"[VoiceCommand] Using fallback target: {targetMesh.gameObject.name}");
            return targetMesh;
        }
        
        Debug.LogError("[VoiceCommand] No target mesh found! Select an object or assign targetMesh in Inspector.");
        return null;
    }
    
    #endregion
    
    #region Vertex Commands
    
    /// <summary>
    /// Move a single vertex by an offset
    /// JSON: {"command":"move_vertex", "vertex":1, "offset":{"x":0,"y":0.1,"z":-0.07}}
    /// </summary>
    CommandResult MoveVertex(MeshCommand cmd)
    {
        // Ensure mesh is in Edit mode for vertex editing
        if (targetMesh.mode != EditableMesh.DisplayMode.Edit)
        {
            targetMesh.mode = EditableMesh.DisplayMode.Edit;
            targetMesh.ApplyModeActiveStates();
            Debug.Log($"[VoiceCommand] Auto-switched {targetMesh.gameObject.name} to Edit mode");
        }
        
        if (!ValidateVertexIndex(cmd.vertex, out string error))
            return new CommandResult(false, error);
        
        Vector3 currentPos = targetMesh.GetVertex(cmd.vertex);
        Vector3 newPos = currentPos + cmd.offset;
        targetMesh.SetVertex(cmd.vertex, newPos);
        
        Debug.Log($"[VoiceCommand] Vertex {cmd.vertex} moved from {currentPos} to {newPos}");
        
        return new CommandResult(true, $"Moved vertex {cmd.vertex} by {cmd.offset}");
    }
    
    /// <summary>
    /// Move multiple vertices by the same offset
    /// JSON: {"command":"move_vertices", "vertices":[0,1,2], "offset":{"x":0,"y":0.1,"z":0}}
    /// </summary>
    CommandResult MoveVertices(MeshCommand cmd)
    {
        // Ensure mesh is in Edit mode for vertex editing
        if (targetMesh.mode != EditableMesh.DisplayMode.Edit)
        {
            targetMesh.mode = EditableMesh.DisplayMode.Edit;
            targetMesh.ApplyModeActiveStates();
            Debug.Log($"[VoiceCommand] Auto-switched {targetMesh.gameObject.name} to Edit mode");
        }
        
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
        // Ensure mesh is in Edit mode for vertex editing
        if (targetMesh.mode != EditableMesh.DisplayMode.Edit)
        {
            targetMesh.mode = EditableMesh.DisplayMode.Edit;
            targetMesh.ApplyModeActiveStates();
            Debug.Log($"[VoiceCommand] Auto-switched {targetMesh.gameObject.name} to Edit mode");
        }
        
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
        // Ensure mesh is in Edit mode for vertex editing
        if (targetMesh.mode != EditableMesh.DisplayMode.Edit)
        {
            targetMesh.mode = EditableMesh.DisplayMode.Edit;
            targetMesh.ApplyModeActiveStates();
            Debug.Log($"[VoiceCommand] Auto-switched {targetMesh.gameObject.name} to Edit mode");
        }
        
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
    
    #region Query Commands
    
    /// <summary>
    /// Get vertex position command wrapper
    /// JSON: {"command":"get_vertex_position", "vertex":1}
    /// </summary>
    CommandResult GetVertexPosition(MeshCommand cmd)
    {
        if (!ValidateVertexIndex(cmd.vertex, out string error))
            return new CommandResult(false, error);
        
        Vector3 pos = targetMesh.GetVertex(cmd.vertex);
        string message = $"Vertex {cmd.vertex} position: ({pos.x:F3}, {pos.y:F3}, {pos.z:F3})";
        Debug.Log(message);
        return new CommandResult(true, message);
    }
    
    /// <summary>
    /// Get all vertices command wrapper
    /// JSON: {"command":"get_all_vertices"}
    /// </summary>
    CommandResult GetAllVertexPositions()
    {
        Vector3[] vertices = targetMesh.GetVertices();
        string message = $"Mesh has {vertices.Length} vertices:\n";
        for (int i = 0; i < vertices.Length; i++)
        {
            Vector3 v = vertices[i];
            message += $"V{i}: ({v.x:F3}, {v.y:F3}, {v.z:F3})\n";
        }
        Debug.Log(message);
        return new CommandResult(true, message);
    }
    
    /// <summary>
    /// Get mesh transform command wrapper
    /// JSON: {"command":"get_mesh_transform"}
    /// </summary>
    CommandResult GetMeshTransform()
    {
        Vector3 pos = targetMesh.transform.position;
        Vector3 rot = targetMesh.transform.eulerAngles;
        Vector3 scale = targetMesh.transform.localScale;
        
        string message = $"Mesh '{targetMesh.gameObject.name}' transform:\n" +
                        $"Position: ({pos.x:F3}, {pos.y:F3}, {pos.z:F3})\n" +
                        $"Rotation: ({rot.x:F1}, {rot.y:F1}, {rot.z:F1})\n" +
                        $"Scale: ({scale.x:F3}, {scale.y:F3}, {scale.z:F3})";
        Debug.Log(message);
        return new CommandResult(true, message);
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
    
    #region Navigation & Control Commands
    
    /// <summary>
    /// Spawn/Create a new object
    /// JSON: {"command":"spawn_object", "primitive_type":"Cube"}
    /// </summary>
    CommandResult SpawnObject(MeshCommand cmd)
    {
        MeshSpawner spawner = EnsureMeshSpawner();
        if (spawner == null)
            return new CommandResult(false, "No MeshSpawner found in scene");
        
        if (string.IsNullOrEmpty(cmd.primitive_type))
            return new CommandResult(false, "No primitive_type specified");
        
        PrimitiveType primitiveType;
        if (!System.Enum.TryParse(cmd.primitive_type, true, out primitiveType))
            return new CommandResult(false, $"Invalid primitive type '{cmd.primitive_type}'. Use: Cube, Sphere, Cylinder, Capsule, or Plane");
        
        EditableMesh newMesh = spawner.SpawnPrimitive(primitiveType);
        
        if (newMesh != null)
        {
            EnsureObjectSelector()?.SelectTransform(newMesh.transform);
            return new CommandResult(true, $"Created {cmd.primitive_type}: {newMesh.gameObject.name}");
        }
        else
            return new CommandResult(false, $"Failed to create {cmd.primitive_type}");
    }
    
    /// <summary>
    /// Delete an object
    /// JSON: {"command":"delete_object", "object_name":"Cube_1"}
    /// </summary>
    CommandResult DeleteObject(MeshCommand cmd)
    {
        if (string.IsNullOrEmpty(cmd.object_name))
            return new CommandResult(false, "No object_name specified");
        
        EditableMesh[] allMeshes = FindObjectsByType<EditableMesh>(FindObjectsSortMode.None);
        foreach (EditableMesh mesh in allMeshes)
        {
            if (mesh.gameObject.name == cmd.object_name)
            {
                ObjectSelector selector = EnsureObjectSelector();
                if (selector != null && selector.GetCurrentSelection() == mesh.transform)
                    selector.ClearSelection();
                
                string name = mesh.gameObject.name;
                Destroy(mesh.gameObject);
                return new CommandResult(true, $"Deleted {name}");
            }
        }
        
        return new CommandResult(false, $"Object '{cmd.object_name}' not found");
    }
    
    /// <summary>
    /// Select an object by name
    /// JSON: {"command":"select_object", "object_name":"Cube_1"}
    /// </summary>
    CommandResult SelectObject(MeshCommand cmd)
    {
        ObjectSelector selector = EnsureObjectSelector();
        if (selector == null)
            return new CommandResult(false, "No ObjectSelector found in scene");
        
        if (string.IsNullOrEmpty(cmd.object_name))
            return new CommandResult(false, "No object_name specified");
        
        if (selector.SelectByName(cmd.object_name))
            return new CommandResult(true, $"Selected {cmd.object_name}");
        
        return new CommandResult(false, $"Object '{cmd.object_name}' not found");
    }
    
    /// <summary>
    /// Set mode for an object
    /// JSON: {"command":"set_mode", "object_name":"Cube_1", "mode":"Edit"}
    /// OR: {"command":"set_mode", "mode":"Object"} for selected object
    /// </summary>
    CommandResult SetMode(MeshCommand cmd)
    {
        EditableMesh mesh = GetTargetMesh(cmd);
        if (mesh == null)
            return new CommandResult(false, "No target object");
        
        if (string.IsNullOrEmpty(cmd.mode))
            return new CommandResult(false, "No mode specified. Use 'Object' or 'Edit'");
        
        EditableMesh.DisplayMode newMode;
        if (cmd.mode.ToLower() == "object")
            newMode = EditableMesh.DisplayMode.Object;
        else if (cmd.mode.ToLower() == "edit")
            newMode = EditableMesh.DisplayMode.Edit;
        else
            return new CommandResult(false, $"Invalid mode '{cmd.mode}'. Use 'Object' or 'Edit'");
        
        mesh.mode = newMode;
        mesh.ApplyModeActiveStates();
        
        return new CommandResult(true, $"Set {mesh.gameObject.name} to {cmd.mode} mode");
    }
    
    /// <summary>
    /// Move camera to position
    /// JSON: {"command":"move_camera", "position":{"x":0,"y":2,"z":-5}}
    /// </summary>
    CommandResult MoveCamera(MeshCommand cmd)
    {
        if (cameraController == null)
            cameraController = FindAnyObjectByType<DesktopCameraController>();
        if (cameraController == null)
            return new CommandResult(false, "No DesktopCameraController found");
        
        if (cmd.position != Vector3.zero)
        {
            cameraController.transform.position = cmd.position;
            return new CommandResult(true, $"Moved camera to {cmd.position}");
        }
        else if (cmd.offset != Vector3.zero)
        {
            cameraController.transform.position += cmd.offset;
            return new CommandResult(true, $"Offset camera by {cmd.offset}");
        }
        else
        {
            return new CommandResult(false, "Specify 'position' or 'offset' for move_camera command");
        }
    }
    
    /// <summary>
    /// Toggle Transform Panel visibility
    /// JSON: {"command":"toggle_transform_panel"} or {"command":"toggle_transform_panel", "enable":true}
    /// </summary>
    CommandResult ToggleTransformPanel(MeshCommand cmd)
    {
        TransformPanel panel = EnsureTransformPanel();
        if (panel == null)
            return new CommandResult(false, "No TransformPanel found");
        
        bool enable;
        if (TryParseState(cmd.state, out enable))
            panel.showPanel = enable;
        else
            panel.showPanel = !panel.showPanel;
        
        return new CommandResult(true, $"Transform panel: {(panel.showPanel ? "ON" : "OFF")}");
    }
    
    /// <summary>
    /// Toggle Orientation Gizmo visibility
    /// JSON: {"command":"toggle_orientation_gizmo"}
    /// </summary>
    CommandResult ToggleOrientationGizmo(MeshCommand cmd)
    {
        OrientationGizmo gizmo = EnsureOrientationGizmo();
        if (gizmo == null)
            return new CommandResult(false, "No OrientationGizmo found");
        
        bool enable;
        if (TryParseState(cmd.state, out enable))
            gizmo.enabled = enable;
        else
            gizmo.enabled = !gizmo.enabled;
        
        return new CommandResult(true, $"Orientation gizmo: {(gizmo.enabled ? "ON" : "OFF")}");
    }
    
    /// <summary>
    /// Toggle mouse look mode
    /// JSON: {"command":"toggle_mouse_look"}
    /// </summary>
    CommandResult ToggleMouseLook(MeshCommand cmd)
    {
        if (cameraController == null)
            cameraController = FindAnyObjectByType<DesktopCameraController>();
        if (cameraController == null)
            return new CommandResult(false, "No DesktopCameraController found");
        
        bool enable;
        if (TryParseState(cmd.state, out enable))
            cameraController.SetMouseLook(enable);
        else
            cameraController.ToggleMouseLook();
        
        bool currentState = cameraController.IsMouseLookEnabled();
        return new CommandResult(true, $"Mouse look {(currentState ? "ON" : "OFF")}");
    }
    
    /// <summary>
    /// Toggle labels (vertex and object labels)
    /// JSON: {"command":"toggle_labels"} or {"command":"toggle_labels", "enable":true}
    /// </summary>
    CommandResult ToggleLabels(MeshCommand cmd)
    {
        if (runtimeMeshEditor == null)
            runtimeMeshEditor = FindAnyObjectByType<RuntimeMeshEditor>();
        if (runtimeMeshEditor == null)
            return new CommandResult(false, "No RuntimeMeshEditor found");
        
        bool enable;
        if (TryParseState(cmd.state, out enable))
            runtimeMeshEditor.SetLabels(enable);
        else
            runtimeMeshEditor.ToggleLabels();
        
        return new CommandResult(true, $"Labels: {(runtimeMeshEditor.showLabels ? "ON" : "OFF")}");
    }
    
    /// <summary>
    /// Clear all spawned objects
    /// JSON: {"command":"clear_all"}
    /// </summary>
    CommandResult ClearAll()
    {
        MeshSpawner spawner = EnsureMeshSpawner();
        if (spawner == null)
            return new CommandResult(false, "No MeshSpawner found");
        
        spawner.ClearAll();
        EnsureObjectSelector()?.ClearSelection();
        return new CommandResult(true, "Cleared all objects");
    }
    
    #endregion
    
    #region Public API Functions (for direct code usage)
    
    /// <summary>
    /// Get current vertex position (for direct code access, not JSON commands)
    /// </summary>
    public Vector3 GetVertexPositionDirect(int vertexIndex)
    {
        if (!ValidateVertexIndex(vertexIndex, out string error))
        {
            Debug.LogError(error);
            return Vector3.zero;
        }
        
        return targetMesh.GetVertex(vertexIndex);
    }
    
    /// <summary>
    /// Get all vertex positions (for direct code access, not JSON commands)
    /// </summary>
    public Vector3[] GetAllVertexPositionsDirect()
    {
        return targetMesh.GetVertices();
    }
    
    /// <summary>
    /// Get mesh transform info (for direct code access, not JSON commands)
    /// </summary>
    public (Vector3 position, Quaternion rotation, Vector3 scale) GetMeshTransformDirect()
    {
        return (
            targetMesh.transform.position,
            targetMesh.transform.rotation,
            targetMesh.transform.localScale
        );
    }
    
    #endregion

    TransformPanel EnsureTransformPanel()
    {
        if (transformPanel == null)
            transformPanel = FindAnyObjectByType<TransformPanel>();
        if (transformPanel == null)
        {
            GameObject panelGO = new GameObject("TransformPanel_Auto");
            transformPanel = panelGO.AddComponent<TransformPanel>();
        }
        return transformPanel;
    }
    
    OrientationGizmo EnsureOrientationGizmo()
    {
        if (orientationGizmo == null)
            orientationGizmo = FindAnyObjectByType<OrientationGizmo>();
        if (orientationGizmo == null)
        {
            GameObject gizmoGO = new GameObject("OrientationGizmo_Auto");
            orientationGizmo = gizmoGO.AddComponent<OrientationGizmo>();
        }
        return orientationGizmo;
    }
    
    MeshSpawner EnsureMeshSpawner()
    {
        if (meshSpawner == null)
            meshSpawner = FindAnyObjectByType<MeshSpawner>();
        return meshSpawner;
    }
    
    ObjectSelector EnsureObjectSelector()
    {
        if (objectSelector == null)
            objectSelector = FindAnyObjectByType<ObjectSelector>();
        return objectSelector;
    }
    
    bool TryParseState(string stateValue, out bool enable)
    {
        enable = false;
        if (string.IsNullOrEmpty(stateValue))
            return false;
        string value = stateValue.ToLower();
        if (value == "on" || value == "true" || value == "enable")
        {
            enable = true;
            return true;
        }
        if (value == "off" || value == "false" || value == "disable")
        {
            enable = false;
            return true;
        }
        return false;
    }
}

/// <summary>
/// Data structure for mesh commands from JSON
/// </summary>
[System.Serializable]
public class MeshCommand
{
    // Core
    public string command;              // Command type (e.g., "move_vertex")
    public string object_name;          // Optional: Name of object to target (e.g., "Cube_1")
    
    // Vertex operations
    public int vertex;                  // Single vertex index
    public int[] vertices;              // Multiple vertex indices
    
    // Transform values
    public Vector3 offset;              // Offset for movement
    public Vector3 position;            // Absolute position
    public Vector3 rotation;            // Rotation (euler angles)
    public float scale;                 // Uniform scale
    public Vector3 scaleVector;         // Non-uniform scale
    
    // Object creation
    public string primitive_type;       // For spawn_object: "Cube", "Sphere", "Cylinder", "Capsule", "Plane"
    
    // Mode control
    public string mode;                 // For set_mode: "Object" or "Edit"
    
    // UI control
    public bool enable;                 // For toggle commands: true/false
    public string state;                // For toggle commands: "on", "off", "true", "false", "enable", "disable"
}

