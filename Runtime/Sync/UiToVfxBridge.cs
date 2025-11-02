using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using UnityEngine;
using RuntimeUIVDOM.VDom;

namespace RuntimeUIVDOM.Sync
{
    /// <summary>
    /// Converts UI Toolkit snapshot data into a GPU buffer for shaders or VFX Graph.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(UiToolkitSnapshot))]
    public class UiToVfxBridge : MonoBehaviour
    {
        [SerializeField]
        private UiToolkitSnapshot _snapshot = default!;

        [SerializeField]
        private int _maxElements = 512;

        [SerializeField]
        private string _shaderProperty = "_RuntimeUiElements";

        [SerializeField]
        private string[] _trackedDatasetKeys = { "state", "role" };

        private GraphicsBuffer? _buffer;
        private PackedUiNode[] _uploadBuffer = Array.Empty<PackedUiNode>();
        private readonly List<UiNodeSnapshot> _latestSnapshot = new();
        private readonly Dictionary<string, VNode> _nodeLookup = new(StringComparer.Ordinal);
        private int _lastUploadCount;

        public GraphicsBuffer? Buffer => _buffer;
        public int LastUploadCount => _lastUploadCount;

        private void Awake()
        {
            if (_snapshot == null)
            {
                _snapshot = GetComponent<UiToolkitSnapshot>();
            }
        }

        private void OnEnable()
        {
            if (_snapshot != null)
            {
                _snapshot.SnapshotUpdated += OnSnapshotUpdated;
            }
            EnsureBuffer();
        }

        private void OnDisable()
        {
            if (_snapshot != null)
            {
                _snapshot.SnapshotUpdated -= OnSnapshotUpdated;
            }

            ReleaseBuffer();
        }

        public void SetRootVNode(VNode? root)
        {
            _nodeLookup.Clear();
            if (root != null)
            {
                IndexNode(root);
            }

            if (_latestSnapshot.Count > 0)
            {
                UploadSnapshot(_latestSnapshot);
            }
        }

        private void OnSnapshotUpdated(IReadOnlyList<UiNodeSnapshot> nodes)
        {
            CacheSnapshot(nodes);
            UploadSnapshot(_latestSnapshot);
        }

        private void CacheSnapshot(IReadOnlyList<UiNodeSnapshot> nodes)
        {
            _latestSnapshot.Clear();
            for (int i = 0; i < nodes.Count; i++)
            {
                _latestSnapshot.Add(nodes[i]);
            }
        }

        private void UploadSnapshot(IReadOnlyList<UiNodeSnapshot> nodes)
        {
            if (nodes.Count == 0)
            {
                _lastUploadCount = 0;
                return;
            }

            EnsureBuffer();
            if (_buffer == null)
            {
                return;
            }

            EnsureUploadBuffer();

            int count = Mathf.Min(nodes.Count, _maxElements);
            if (nodes.Count > _maxElements)
            {
                Debug.LogWarning($"UiToVfxBridge is truncating snapshot size {nodes.Count} to buffer capacity {_maxElements}.", this);
            }
            for (int i = 0; i < count; i++)
            {
                var node = nodes[i];
                var packed = new PackedUiNode
                {
                    Rect = new Vector4(node.X, node.Y, node.Width, node.Height),
                    Id = (uint)ComputeHash(node.Id),
                    ParentId = string.IsNullOrEmpty(node.ParentId) ? 0u : (uint)ComputeHash(node.ParentId),
                    Visible = node.Visible ? 1f : 0f,
                    OrderInParent = node.OrderInParent,
                };

                if (_trackedDatasetKeys.Length > 0)
                {
                    packed.Dataset0 = EncodeDataset(node.Id, _trackedDatasetKeys[0]);
                }

                if (_trackedDatasetKeys.Length > 1)
                {
                    packed.Dataset1 = EncodeDataset(node.Id, _trackedDatasetKeys[1]);
                }

                _uploadBuffer[i] = packed;
            }

            _buffer.SetData(_uploadBuffer, 0, 0, count);
            _lastUploadCount = count;

            if (!string.IsNullOrEmpty(_shaderProperty))
            {
                Shader.SetGlobalBuffer(_shaderProperty, _buffer);
            }
        }

        private void EnsureBuffer()
        {
            if (_buffer != null && _buffer.count >= _maxElements)
            {
                return;
            }

            ReleaseBuffer();
            _maxElements = Mathf.Max(1, _maxElements);
            int stride = Marshal.SizeOf<PackedUiNode>();
            _buffer = new GraphicsBuffer(GraphicsBuffer.Target.Structured, _maxElements, stride);
        }

        private void ReleaseBuffer()
        {
            _buffer?.Dispose();
            _buffer = null;
            _lastUploadCount = 0;
        }

        private void EnsureUploadBuffer()
        {
            if (_uploadBuffer.Length < _maxElements)
            {
                Array.Resize(ref _uploadBuffer, _maxElements);
            }
        }

        private void IndexNode(VNode node)
        {
            if (!string.IsNullOrEmpty(node.Id))
            {
                _nodeLookup[node.Id] = node;
            }

            foreach (var child in node.Children)
            {
                IndexNode(child);
            }
        }

        private uint EncodeDataset(string nodeId, string key)
        {
            if (string.IsNullOrEmpty(key))
            {
                return 0;
            }

            if (!_nodeLookup.TryGetValue(nodeId, out var vnode))
            {
                return 0;
            }

            if (!vnode.Dataset.TryGetValue(key, out var value))
            {
                return 0;
            }

            return (uint)DatasetEncoder.Encode(key, value);
        }

        private static int ComputeHash(string value)
        {
            unchecked
            {
                const int offsetBasis = (int)2166136261;
                const int prime = 16777619;
                int hash = offsetBasis;
                for (int i = 0; i < value.Length; i++)
                {
                    hash ^= value[i];
                    hash *= prime;
                }

                return hash;
            }
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct PackedUiNode
        {
            public Vector4 Rect;
            public uint Id;
            public uint ParentId;
            public float Visible;
            public float OrderInParent;
            public uint Dataset0;
            public uint Dataset1;
            private Vector2 _padding;
        }
    }
}
