# AGENT.md — Runtime UI Virtual DOM for Unity

You are an assistant (agent) that will implement a Unity runtime package based on the specification below.  
This package creates a Virtual DOM (VNode) in C#, reconciles it into Unity UI Toolkit (runtime), observes the runtime layout, and pushes UI data to a VFX / shader layer.

Your tasks are **implementation-oriented**. Do NOT change the architecture unless explicitly requested.  
Focus on clean, small, composable C# scripts.

---

## 1. Project Goal

Implement a Unity **runtime** package that:

1. Represents logical UI as a **Virtual DOM (VNode)** in C#.
2. Applies (reconciles) this VNode tree to **UI Toolkit** runtime `VisualElement` tree.
3. Observes UI Toolkit’s actual rendered layout (rect, visibility, order).
4. Packs that observed data into a **GPU-friendly buffer** (e.g. `GraphicsBuffer`) so **VFX Graph / shaders** can react to UI changes.
5. Keeps **logic (VNode)** and **visuals (VFX / shaders)** **decoupled**.

The agent must keep the pipeline **one-way**:

`VNode → UIToolkit → Snapshot → GPU (VFX)`

---

## 2. Architecture (must keep)

### 2.1 VNode (Virtual DOM model)

- File: `Runtime/VDom/VNode.cs`
- Class:

```csharp
public class VNode
{
    public string Id;
    public string Type;
    public Dictionary<string, string> Props = new();
    public Dictionary<string, string> Dataset = new(); // like data-* in HTML
    public List<VNode> Children = new();
}
```

**Rules:**
- `Id` must be stable and unique among siblings.
- `Type` determines which `VisualElement` to create.
- `Props` are for look & layout (text, class, width, height, display).
- `Dataset` is for logic / VFX use. Keep as string-to-string. Do NOT try to make it strongly typed here.

### 2.2 VNode Factory

- File: `Runtime/VDom/VNodeFactory.cs`
- Responsibility: map `VNode.Type` → actual `VisualElement`.
- Must support at least: `container`, `label`, `button`, `image`.
- Must apply props right after creation.

```csharp
public static class VNodeFactory
{
    public static VisualElement Create(VNode node) { ... }
    public static void ApplyProps(VNode node, VisualElement ve) { ... }
}
```

Props to support first:
- `text`
- `class` (space separated → AddToClassList)
- `width`, `height` (float)
- `display` (“none” → `DisplayStyle.None`)
- `tooltip`

Dataset is **not** automatically applied to VE. It is for syncing to VFX later.

### 2.3 Reconciler

- File: `Runtime/VDom/UiReconciler.cs`
- Responsibility: apply VNode tree to an existing `VisualElement` root.
- Algorithm:
  1. Build a map of current children of the parent VE by name (VE.name).
  2. For each child in VNode.Children:
     - If VE with same name exists → update props, ensure correct order.
     - Else → create VE from VNode and insert at correct index.
     - Recurse.
  3. Delete VEs that were not present in the VNode.
- VE.name **must** be set to VNode.Id.

This reconciler is called by the app/game code whenever the logical UI changes.

### 2.4 QuerySelector-like helper

- File: `Runtime/VDom/VNodeQuery.cs`
- Add extension methods:

```csharp
public static VNode? QuerySelector(this VNode root, string selector)
public static List<VNode> QuerySelectorAll(this VNode root, string selector)
```

- Support these selectors:
  - `#id`
  - `.class` (from Props["class"])
  - `type`
  - `[key=value]` (search Props and Dataset)

This is to make runtime modifications easy in game code.

### 2.5 UI Snapshot (observing UITK)

- File: `Runtime/Sync/UiToolkitSnapshot.cs`
- Component/utility that:
  - Hooks `AttachToPanelEvent`, `DetachFromPanelEvent`, `GeometryChangedEvent` on the UITK root (`UIDocument.rootVisualElement`)
  - Maintains a `List<UiNodeSnapshot>` with the **current** rendered UI state

```csharp
public struct UiNodeSnapshot
{
    public string Id;
    public float X;
    public float Y;
    public float Width;
    public float Height;
    public bool Visible;
    public string ParentId;
    public int OrderInParent;
}
```

- **Important**: Some elements will have `worldBound` = 0,0,0,0 at attach-time. Update on GeometryChanged.

### 2.6 C# → VFX Bridge

- File: `Runtime/Sync/UiToVfxBridge.cs`
- MonoBehaviour that:
  - Gets latest `List<UiNodeSnapshot>` from the snapshot component
  - Packs them into a GPU struct array
  - Uploads to `GraphicsBuffer` / `ComputeBuffer`
  - Sets on a `VisualEffect` as:
    - `UIElements` (buffer)
    - `UIElementCount` (int)

GPU-side struct (C# side):

```csharp
struct UIElementData
{
    public Vector4 rect;
    public uint id;
    public uint dataset0;
    public uint dataset1;
}
```

**Note:** dataset0/dataset1 must be encoded from the **VNode.Dataset** (string → int) by a small mapper. Keep the mapper simple and deterministic.

### 2.7 Mapper (dataset → int)

- File: `Runtime/Sync/DatasetEncoder.cs` (or similar)
- Purpose: convert VNode.Dataset known keys to numeric values.
- Example:
  - key: `state`
    - `ready` → 1
    - `running` → 2
    - `disabled` → 3
    - default → 0

This allows VFX Graph to filter/highlight UI by logical state.

---

## 3. Coding Guidelines

1. **Do not** reference Editor-only APIs in Runtime.
2. **Do not** mix VNode model and UnityEngine.Object types.
3. Prefer **small, independent** C# files.
4. All public runtime types must be in a namespace, e.g. `RuntimeUIVDOM` or `RuntimeUIVDOM.VDom`.
5. Avoid allocations in Update when possible — preallocate buffers.
6. Keep the data flow **one-way**.
7. Do not make VFX depend on UnityEngine.UIElements.
8. Keep method names descriptive; this package is intended to be read by other devs.

---

## 4. Implementation Order (for the agent)

1. **Models**
   - `VNode.cs`
   - `UiNodeSnapshot.cs`
2. **Reconciliation**
   - `VNodeFactory.cs`
   - `UiReconciler.cs`
3. **Query**
   - `VNodeQuery.cs`
4. **Snapshot / UITK observer**
   - `UiToolkitSnapshot.cs` (MonoBehaviour)
5. **Bridge to VFX**
   - `UiToVfxBridge.cs`
   - `DatasetEncoder.cs`
6. **Demo component**
   - `UiRuntimeController.cs` that creates a sample VNode tree and applies it

Each step must compile independently.

---

## 5. Sample Usage (target)

```csharp
public class DemoUI : MonoBehaviour
{
    public UIDocument uiDocument;
    public UiToVfxBridge vfxBridge;

    private VNode _root;
    private UiReconciler _reconciler = new UiReconciler();

    void Start()
    {
        // 1. build virtual UI
        _root = new VNode {
            Id = "root",
            Type = "container",
            Children = new List<VNode> {
                new VNode {
                    Id = "title",
                    Type = "label",
                    Props = { ["text"] = "Hello VDOM" , ["class"] = "title" },
                    Dataset = { ["state"] = "running" }
                },
                new VNode {
                    Id = "play-button",
                    Type = "button",
                    Props = { ["text"] = "Play" },
                    Dataset = { ["state"] = "ready", ["role"] = "primary" }
                }
            }
        };

        // 2. reconcile once
        _reconciler.Apply(_root, uiDocument.rootVisualElement);

        // 3. snapshot component (on same GameObject) will observe layout
        // 4. vfxBridge will upload every frame
    }
}
```

---

## 6. Non-goals (for now)

- No editor-time uxml/uss generation
- No hot-reload from external JSON (may be added later)
- No full CSS selector engine (only minimal selectors)
- No two-way binding from UITK back into VNode (currently one-way only)

---

## 7. Output / Deliverables

- C# runtime scripts as described above
- Namespaces applied
- A minimal scene-level demo script that shows:
  - VNode is created
  - UITK is updated
  - Snapshot is generated
  - VFX buffer is updated (can be mocked if VFX is not present)

If a part cannot be implemented (e.g., VFX absent), stub it with a clear TODO.

---

## 8. Notes for the Agent

- Treat the provided README (JP + EN) as **authoritative**.
- If a conflict occurs between README and AGENT, prefer AGENT.
- Keep all public APIs small and predictable.
