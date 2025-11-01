# UITK VNode

Runtime UI Virtual DOM for Unity (Design Draft)

> **Status:** Draft / 0.1.0  
> **Target:** Unity 2022+ / UI Toolkit (runtime)  
> **Scope:** Design specification for a system that separates logical UI (Virtual DOM) from rendering layers such as VFX Graph, shaders, or uGUI.

This document defines the design of a runtime Virtual DOM system for Unity that uses **UI Toolkit as a layout and event layer**, while delegating **visual rendering** to separate systems (VFX Graph, Shader Graph, etc.).  
The goal is to make the logical UI structure reusable, data-driven, and easily synchronized with visual effects.

---

## 1. Concept

1. **Maintain a Virtual DOM in C#**
   - Represent the actual UI structure (buttons, labels, containers, etc.) as a C# object tree (`VNode`).
   - Allow runtime addition, removal, and reordering of elements.
   - Treat this tree as the *source of truth*.

2. **Use UI Toolkit only as the rendering engine**
   - A Reconciler applies VNode contents to the UI Toolkit `VisualElement` tree.
   - No direct modifications to UI Toolkit; logical state lives in the VNode.

3. **Rendering systems observe the result, not the logic**
   - Collect the computed layout (x, y, width, height) and dataset values.
   - Pack the data into GPU-friendly arrays (`GraphicsBuffer` / `ComputeBuffer`) and pass them to VFX Graph or shaders.
   - The GPU never references the VNode directly.

4. **Provide a dataset mechanism (HTML-like `data-*`)**
   - Each VNode can hold arbitrary metadata for logic or visual effects.
   - Enables effects such as “highlight elements where `state=running`”.

---

## 2. Use Cases

- UIs that dynamically change during gameplay (shop lists, scoreboards, lobbies, player lists…)
- Background VFX reacts dynamically to UI layout or visibility
- Designers work in UI Toolkit/USS for structure, while technical artists control appearance with C# + VFX Graph

---

## 3. Package Layout (Proposed)

```text
RuntimeUIVDOM/
  Runtime/
    VDom/
      VNode.cs
      VNodeQuery.cs
      VNodeFactory.cs
      UiReconciler.cs
    Sync/
      UiToolkitSnapshot.cs
      UiToVfxBridge.cs
    Util/
      IdUtility.cs
  Editor/
    (optional) VNode inspector / debug viewer
  README.md
  package.json
```

---

## 4. VNode Specification (Logical UI Representation)

```csharp
public class VNode
{
    public string Id;   // Stable identifier
    public string Type; // "container", "label", "button", "image", etc.
    public Dictionary<string, string> Props = new();
    public Dictionary<string, string> Dataset = new();  // data-* equivalent
    public List<VNode> Children = new();
}
```

### 4.1 Id
- Must be unique and stable (e.g., “lobby/player-list”)
- Used by the Reconciler to diff and reuse nodes
- Duplicate IDs under the same parent are invalid

### 4.2 Type
- Logical type name for element creation
- Translated to a concrete `VisualElement` via `VNodeFactory.Create()`

### 4.3 Props
- Presentation or layout intent (e.g., `text`, `class`, `width`)
- Applied to the UI Toolkit element by the Reconciler

### 4.4 Dataset
- Metadata for logic or rendering systems
- Example: `state=running`, `role=enemy`, `index=3`
- Stored as string pairs for flexibility; encoded later for GPU use

---

## 5. Reconciler (VNode → UI Toolkit)

### 5.1 Purpose
- Apply the Virtual DOM to the actual UI Toolkit tree.
- Reuse existing elements where possible.
- Remove obsolete ones.
- Maintain correct child order.

### 5.2 Simplified Algorithm

1. Build a dictionary of existing children (keyed by Id)
2. Iterate over children in VNode:
   - Update existing VisualElements if found.
   - Create and insert new ones if missing.
   - Recurse for subtrees.
   - Remove handled elements from the dictionary.
3. Remove remaining elements not found in VNode.

### 5.3 Creation Rules
- Implemented via `VNodeFactory.Create(VNode node)`
- Supports extension with custom VisualElements

---

## 6. QuerySelector-like API

VNode can be queried in a CSS-like fashion for convenience.

### Supported selectors (initial)

- `#id`
- `.class` (search in Props["class"])
- `type`
- `[key=value]` (search in Props or Dataset)

```csharp
var node = rootVNode.QuerySelector("#play-button");
var labels = rootVNode.QuerySelectorAll("label");
var running = rootVNode.QuerySelectorAll("[state=running]");
```

---

## 7. Capturing Runtime UI State (Snapshot)

Since UI Toolkit layout is resolved after updates,  
we track changes through events to build a runtime snapshot.

### Relevant Events
- `AttachToPanelEvent` — when added to panel
- `DetachFromPanelEvent` — when removed from panel
- `GeometryChangedEvent` — when position or size changes

### Snapshot Struct
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

This snapshot represents *the actual rendered state* and is separate from VNode.

---

## 8. C# → VFX / GPU Bridge

### 8.1 Principle
- The GPU/VFX should not access the VNode directly.
- C# converts runtime UI state into GPU-readable data.

### 8.2 GPU Data Struct
```csharp
struct UIElementData
{
    float4 rect;     // x, y, w, h
    uint id;         // hash of VNode.Id
    uint dataset0;   // mapped value (e.g., state)
    uint dataset1;   // another dataset value
};
```

- Sent as `GraphicsBuffer` / `ComputeBuffer`
- Bound to VFX via `vfx.SetGraphicsBuffer("UIElements", buffer);`

### 8.3 Dataset Mapping
- Only map dataset keys relevant to VFX (e.g., “state”, “role”)
- Encode string values to integers before upload
- Example: `state=running` → `dataset0 = 2`

---

## 9. Data Flow Overview

```text
[1] Game Logic
     ↓ (generate or update VNode)
[2] Virtual DOM (VNode)
     ↓ (Reconciler)
[3] UI Toolkit Visual Tree
     ↓ (event observation)
[4] Snapshot (UiNodeSnapshot List)
     ↓ (C# encoding)
[5] GPU / VFX Buffer
     ↓
[6] Dynamic visual effects driven by UI
```

The direction is always one-way → predictable and easy to maintain.

---

## 10. Future Extensions

1. **Delta updates** — Only re-upload changed nodes to GPU
2. **Typed props/datasets** — Optional `VNodeValue` struct for type safety
3. **Editor integration** — Inspector viewer for VNode trees
4. **Multi-panel support** — Centralized VDOM manager per `UIDocument`

---

## 11. Limitations & Notes

- UI Toolkit may fire multiple `GeometryChangedEvent`s; consider dirty flags.
- Predefine a reasonable GPU buffer size (256, 512 elements).
- Changing IDs at runtime causes unnecessary rebuilds.
- Always encode dataset strings before sending to GPU.

---

## 12. Summary

- **VNode (Virtual DOM)** is the logical truth.
- **Reconciler** applies it to UI Toolkit efficiently.
- **Snapshots** observe final layout and visibility.
- **C# bridge** encodes this data for VFX Graph or shaders.
- **VFX remains decoupled** — it only reads data, never the VNode.

This architecture ensures:
- predictable, one-directional data flow
- clean separation of logic and visuals
- easy extension to other render systems or remote UIs
