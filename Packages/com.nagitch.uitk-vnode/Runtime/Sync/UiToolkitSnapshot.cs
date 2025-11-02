using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

namespace RuntimeUIVDOM.Sync
{
    [RequireComponent(typeof(UIDocument))]
    public class UiToolkitSnapshot : MonoBehaviour
    {
        [SerializeField]
        private UIDocument _document = default!;

        private readonly List<UiNodeSnapshot> _snapshot = new();
        private bool _registered;
        private bool _dirty;

        public IReadOnlyList<UiNodeSnapshot> Nodes => _snapshot;

        public event Action<IReadOnlyList<UiNodeSnapshot>>? SnapshotUpdated;

        private void Awake()
        {
            if (_document == null)
            {
                _document = GetComponent<UIDocument>();
            }
        }

        private void OnEnable()
        {
            RegisterCallbacks();
            MarkDirty();
        }

        private void OnDisable()
        {
            UnregisterCallbacks();
            _snapshot.Clear();
        }

        private void Update()
        {
            if (!_dirty)
            {
                return;
            }

            _dirty = false;
            RefreshSnapshot();
            SnapshotUpdated?.Invoke(_snapshot);
        }

        private void RegisterCallbacks()
        {
            if (_registered)
            {
                return;
            }

            if (_document == null)
            {
                Debug.LogWarning("UiToolkitSnapshot requires a UIDocument reference.", this);
                return;
            }

            var root = _document.rootVisualElement;
            if (root == null)
            {
                return;
            }

            root.RegisterCallback<AttachToPanelEvent>(OnAttachToPanel, TrickleDown.TrickleDown);
            root.RegisterCallback<DetachFromPanelEvent>(OnDetachFromPanel, TrickleDown.TrickleDown);
            root.RegisterCallback<GeometryChangedEvent>(OnGeometryChanged, TrickleDown.TrickleDown);
            _registered = true;
        }

        private void UnregisterCallbacks()
        {
            if (!_registered || _document == null)
            {
                return;
            }

            var root = _document.rootVisualElement;
            if (root == null)
            {
                return;
            }

            root.UnregisterCallback<AttachToPanelEvent>(OnAttachToPanel, TrickleDown.TrickleDown);
            root.UnregisterCallback<DetachFromPanelEvent>(OnDetachFromPanel, TrickleDown.TrickleDown);
            root.UnregisterCallback<GeometryChangedEvent>(OnGeometryChanged, TrickleDown.TrickleDown);
            _registered = false;
        }

        private void OnAttachToPanel(AttachToPanelEvent _)
        {
            MarkDirty();
        }

        private void OnDetachFromPanel(DetachFromPanelEvent _)
        {
            MarkDirty();
        }

        private void OnGeometryChanged(GeometryChangedEvent _)
        {
            MarkDirty();
        }

        private void MarkDirty()
        {
            _dirty = true;
        }

        private void RefreshSnapshot()
        {
            _snapshot.Clear();

            if (_document == null)
            {
                return;
            }

            var root = _document.rootVisualElement;
            if (root == null)
            {
                return;
            }

            CollectRecursive(root);
        }

        private void CollectRecursive(VisualElement element)
        {
            if (!string.IsNullOrEmpty(element.name))
            {
                var parent = element.parent;
                var worldBound = element.worldBound;
                bool visible = element.resolvedStyle.display != DisplayStyle.None &&
                               element.resolvedStyle.visibility == Visibility.Visible;

                var snapshot = new UiNodeSnapshot
                {
                    Id = element.name,
                    X = worldBound.xMin,
                    Y = worldBound.yMin,
                    Width = worldBound.width,
                    Height = worldBound.height,
                    Visible = visible,
                    ParentId = parent != null ? parent.name : string.Empty,
                    OrderInParent = parent != null ? parent.IndexOf(element) : 0
                };

                _snapshot.Add(snapshot);
            }

            foreach (var child in element.Children())
            {
                CollectRecursive(child);
            }
        }
    }
}
