# Scripts Organization

This folder contains all C# scripts for the voice-controlled mesh editor.

## Folder Structure

### 📁 **MeshEditing/**
Core mesh editing and manipulation scripts.

- **EditableMesh.cs** - Generic editable mesh system (works with any OBJ/FBX/primitive)
- **EditableCube.cs** - Legacy procedural cube system
- **RuntimeMeshEditor.cs** - Runtime editing controller (Z/X/C transforms + vertex editing)
- **RuntimeVertexEditor.cs** - Legacy vertex editor for EditableCube

### 📁 **UI/**
User interface and interaction scripts.

- **TopMenuBar.cs** - Top menu for adding objects (Add Object, Clear All)
- **TransformPanel.cs** - Blender-style transform info panel (N key)
- **OrientationGizmo.cs** - XYZ axis widget in bottom-right corner
- **ObjectSelector.cs** - Screen-space object selection system
- **MeshSpawner.cs** - Creates primitive meshes at runtime

### 📁 **Camera/**
Camera controls and XR/Desktop switching.

- **DesktopCameraController.cs** - FPS camera controls (WASD, mouse look, crosshair)
- **XRAutoSetup.cs** - Auto-detects VR/Desktop and configures scene
- **AdaptiveCanvas.cs** - Adapts UI Canvas for VR/Desktop rendering

### 📁 **VoiceControl/**
Voice command system for AI/LLM integration.

- **VoiceCommandProcessor.cs** - Executes JSON commands (19+ commands)
- **VoiceCommandTester.cs** - Testing tool (press T to execute JSON)
- **vlm.py.txt** - Python example for Vision-Language Model integration
- **SendToVLM.cs.txt** - C# example for VLM screenshot capture

### 📁 **Editor/**
Unity Editor-only scripts (Scene view tools).

- **EditableMeshEditor.cs** - Scene view handles for EditableMesh
- **EditableCubeEditor.cs** - Scene view handles for EditableCube

### 📁 **Legacy/**
Old/unused scripts kept for reference.

- **UniversalInput.cs** - Old cross-platform input wrapper
- **UniversalCubeInteractor.cs** - Old VR interaction example
- **IInteractable.cs** - Old interaction interface
- **FIXES_APPLIED.md** - Historical bug fix documentation
- **XR_Desktop_Setup_Guide.md** - Old setup guide

---

## Key Dependencies

### MeshEditing depends on:
- UI (ObjectSelector for selection awareness)
- Camera (DesktopCameraController for camera reference)

### UI depends on:
- MeshEditing (EditableMesh components)
- Camera (for screen-space calculations)

### VoiceControl depends on:
- **Everything** (controls entire app)

### Circular Dependencies:
- RuntimeMeshEditor ↔ ObjectSelector (selection integration)
- VoiceCommandProcessor → All systems

---

## Adding New Scripts

### Where to put new files:

- **Mesh operations** → `MeshEditing/`
- **UI elements** → `UI/`
- **Camera/input** → `Camera/`
- **Voice/AI** → `VoiceControl/`
- **Editor tools** → `Editor/`
- **Obsolete** → `Legacy/`

### Naming Conventions:
- PascalCase for classes and files
- One class per file
- File name matches class name
- Use descriptive names (e.g., `RuntimeMeshEditor` not `RME`)

---

## Script Count
- **MeshEditing**: 4 scripts
- **UI**: 5 scripts
- **Camera**: 3 scripts
- **VoiceControl**: 4 scripts
- **Editor**: 2 scripts
- **Legacy**: 5 scripts

**Total Active**: 18 scripts  
**Total Legacy**: 5 scripts

---

## Migration Notes

If you encounter missing script references after reorganization:
1. Unity should auto-update references via .meta files
2. If broken, manually reassign in Inspector
3. Check Console for "Missing script" warnings
4. Delete and re-add component if needed

The .meta files track GUIDs, so Unity should handle the move automatically.

