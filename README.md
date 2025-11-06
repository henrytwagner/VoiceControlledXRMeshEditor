# 598 VR Project - Voice-Controlled 3D Mesh Editor

A Unity VR/Desktop project featuring voice-controlled mesh editing with AI/LLM integration support.

## Features

### **🎤 Voice Command System**
- **JSON-based API**: Complete mesh editing via voice/AI commands
- **Multi-object support**: Create, edit, and manage multiple meshes
- **Natural language**: Convert speech to mesh operations via LLM
- **Full app control**: Navigation, UI, camera, and editing via voice

### **🎮 Interactive Editing**
- **Runtime mesh editing**: Edit any imported mesh (OBJ, FBX) or primitives
- **Dual control modes**: 
  - **Object mode**: Transform entire mesh (Z=Translate, X=Rotate, C=Scale)
  - **Edit mode**: Move individual vertices
- **FPS-style controls**: Crosshair mode (Alt) or mouse cursor mode
- **Visual feedback**: Vertex labels, wireframe, origin indicators

### **🖥️ Universal XR/Desktop Support**
- **Auto-detection**: Seamlessly switches between VR and desktop
- **Desktop controls**: WASD movement, Alt for mouse look/crosshair
- **VR ready**: Full XR Interaction Toolkit integration

### **🎨 Professional UI**
- **Blender-style interface**: Transform panel (N key), orientation gizmo
- **Top menu bar**: Add objects (Cube, Sphere, Cylinder, Capsule, Plane)
- **Selection system**: Click or crosshair-based object/vertex selection

## Unity Version

- Unity 6.2 (6000.2.10f1)
- Universal Render Pipeline (URP)

## Getting Started

### Clone the Repository

```bash
git clone <your-repo-url>
cd 598_v1
```

### Open in Unity

1. Open Unity Hub
2. Click "Add" → "Add project from disk"
3. Navigate to the cloned folder
4. Click "Open"

### Desktop Mode (No VR Headset)

1. Press Play
2. Click "Add Object" menu → Select a primitive (Cube, Sphere, etc.)
3. Click object to select it
4. Press **Tab** to enter Edit mode
5. See detailed controls below

### VR Mode (With Headset)

1. Connect VR headset
2. Press Play
3. Use VR controllers as normal
4. Automatically detects and switches to VR mode

## Project Structure

```
Assets/
├── Materials/              # Materials and shaders
│   ├── Axis.shader         # X/Z axis lines
│   └── Grid.shader         # World grid
├── Meshes/                 # Imported mesh files
│   ├── Cube.obj           # Sample mesh (Read/Write enabled)
│   └── Cone.obj           # Sample mesh (Read/Write enabled)
├── Scripts/               # C# scripts
│   ├── EditableMesh.cs             # Generic editable mesh system
│   ├── RuntimeMeshEditor.cs        # Runtime editing (Z/X/C transforms + vertices)
│   ├── VoiceCommandProcessor.cs    # JSON command execution
│   ├── VoiceCommandTester.cs       # Testing tool (T key)
│   ├── ObjectSelector.cs           # Object selection system
│   ├── MeshSpawner.cs             # Create primitives at runtime
│   ├── TopMenuBar.cs              # UI menu for adding objects
│   ├── TransformPanel.cs          # Blender-style transform info (N key)
│   ├── OrientationGizmo.cs        # XYZ axis gizmo (bottom-right)
│   ├── DesktopCameraController.cs # FPS camera + crosshair
│   ├── XRAutoSetup.cs             # Auto XR/Desktop detection
│   └── Editor/                     # Editor-only scripts
│       └── EditableMeshEditor.cs  # Scene view handles
├── Scenes/                # Unity scenes
└── VRTemplateAssets/      # XR Interaction Toolkit assets
```

## Key Scripts

### EditableMesh
Generic system for making any mesh editable at runtime:
- Works with OBJ, FBX, or Unity primitives
- Extracts unique vertices for clean editing
- Dual display modes: Object (render mesh) / Edit (show vertices)
- Auto-generates MeshColliders for selection

### RuntimeMeshEditor
Master controller for all editing operations:
- Handles vertex selection and dragging (mouse or crosshair)
- Transform modes: Z=Translate, X=Rotate, C=Scale
- Wireframe rendering in Edit mode
- Auto-switches based on selected object

### VoiceCommandProcessor
Executes JSON commands from AI/voice input:
- 19+ commands for editing and navigation
- Multi-object support via `object_name` field
- Auto-discovers UI components
- Returns success/failure feedback
- See [VOICE_COMMAND_API.md](VOICE_COMMAND_API.md)

### VoiceCommandTester
Testing harness for voice commands:
- **Temporary tool** for development
- Enter JSON in Inspector, press T to execute
- Will be replaced by LLM integration
- See [VOICE_INTEGRATION_GUIDE.md](VOICE_INTEGRATION_GUIDE.md)

### ObjectSelector
Screen-space object selection:
- Click objects to select (mouse or crosshair)
- Highlights selected object in TransformPanel
- Prevents deselection when in Edit mode
- Used by voice commands for targeting

### MeshSpawner
Creates editable primitives at runtime:
- Spawns Cube, Sphere, Cylinder, Capsule, Plane
- Auto-generates readable mesh copies
- Tracks all spawned objects
- Spawns at world origin (0, 0, 0)

### XRAutoSetup
Automatic VR/Desktop mode switching:
- Detects XR device on startup
- Enables appropriate camera and controls
- No manual scene configuration needed

## Controls

### Desktop Mode

#### **Movement:**
- **W/A/S/D** - Forward/Left/Back/Right
- **Q/E** - Down/Up (vertical movement)
- **Shift** - Sprint (2x speed)

#### **Mouse Look:**
- **Alt** - Toggle mouse look (crosshair mode)
  - **ON**: Cursor locked, crosshair appears, free look
  - **OFF**: Cursor free, click to interact

#### **Object Interaction:**
- **Click** - Select object (with mouse or crosshair)
- **Tab** - Toggle Object/Edit mode for selected object
- **Z** - Translate mode (click-drag to move object)
- **X** - Rotate mode (click-drag to rotate object)
- **C** - Scale mode (click-drag to scale object)

#### **Vertex Editing** (in Edit mode):
- **Click** - Select vertex (with mouse or crosshair)
- **Click & Drag** - Move selected vertex
- **Vertex labels** - V0, V1, V2... show in Game view

#### **UI Controls:**
- **N** - Toggle Transform Panel (Blender-style info)
- **F1** - Toggle Top Menu Bar
- **Add Object menu** - Create primitives
- **Clear All** - Remove all objects

### VR Mode
- **Trigger** - Interact/Select
- **Thumbstick** - Move
- **Grip** - Grab
- **Menu** - Settings

### Voice Commands
- **T** - Execute test command (VoiceCommandTester)
- See [VOICE_INTEGRATION_GUIDE.md](VOICE_INTEGRATION_GUIDE.md) for LLM integration

---

## Voice Command System

### Quick Start (Testing)

1. **Find VoiceCommandTester** in Hierarchy
2. **Select it** and view Inspector
3. **Edit the JSON** in `Test Json Command` field
4. **Press Play**, then **press T** to execute

### Example Commands to Try:

**Create objects:**
```json
{"command":"spawn_object", "primitive_type":"Sphere"}
```

**List scene:**
```json
{"command":"list_objects"}
```

**Edit vertex:**
```json
{"command":"move_vertex", "vertex":0, "offset":{"x":0,"y":0.1,"z":0}}
```

**Transform object:**
```json
{"command":"scale_mesh", "scale":1.5}
```

**Control UI:**
```json
{"command":"toggle_transform_panel"}
```

### Documentation Files

- **[VOICE_COMMAND_API.md](VOICE_COMMAND_API.md)** - Complete JSON command reference (19+ commands)
- **[VOICE_INTEGRATION_GUIDE.md](VOICE_INTEGRATION_GUIDE.md)** - LLM integration guide with code examples

### How It Works

```
User Speech → LLM (GPT-4/Claude) → JSON Command → VoiceCommandProcessor → Unity Scene Updates
```

**Current State**: VoiceCommandTester (manual testing)  
**Future State**: Your LLM integration replaces the tester

The `VoiceCommandProcessor` is production-ready and supports:
- ✅ Mesh editing (vertex movement, transforms)
- ✅ Object management (create, delete, select)
- ✅ UI control (panels, gizmos, camera)
- ✅ Multi-object scenes
- ✅ Query/info commands

See the integration guide for OpenAI and Claude API examples.

---

## Troubleshooting

### Cube faces rendering wrong
- Set material **Render Face** (or **Cull**) to **Back**
- Normals point outward after recent fixes

### Grid/Axes overlaying objects
- Set Grid material Render Queue to 2450-2451
- Ensure objects are opaque or use depth-writing shaders

### Vertex editing not working
- Ensure EditableCube is in **Vertices mode**
- RuntimeVertexEditor must be in the scene
- Check Console for debug messages

### No XR/Desktop mode detection
- Check XRAutoSetup component is in scene
- Assign DesktopCamera and XR Rig references

## Development Notes

- Uses `[ExecuteAlways]` for editor-time cube updates
- Corners are serialized for persistence
- Input works with both legacy and new Input System
- VR-ready with stereo rendering support in shaders

## License

Educational project for course 598.

## Contributors

- Henry Wagner

