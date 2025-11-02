using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;
using RuntimeUIVDOM.VDom;
using RuntimeUIVDOM.Sync;

namespace RuntimeUIVDOM.Demo
{
    /// <summary>
    /// Minimal demo component that builds a VNode tree, applies it to UI Toolkit, and bridges to VFX.
    /// </summary>
    [DisallowMultipleComponent]
    public class UiRuntimeController : MonoBehaviour
    {
        [SerializeField]
        private UIDocument _document = default!;

        [SerializeField]
        private UiToolkitSnapshot _snapshot = default!;

        [SerializeField]
        private UiToVfxBridge _bridge = default!;

        private readonly UiReconciler _reconciler = new();
        private VNode? _root;

        private void Awake()
        {
            if (_document == null)
            {
                _document = GetComponent<UIDocument>();
            }

            if (_snapshot == null)
            {
                _snapshot = GetComponent<UiToolkitSnapshot>();
            }

            if (_bridge == null)
            {
                _bridge = GetComponent<UiToVfxBridge>();
            }
        }

        private void Start()
        {
            _root = BuildDemoTree();
            ApplyVNode();
        }

        public void Refresh()
        {
            ApplyVNode();
        }

        private void ApplyVNode()
        {
            if (_root == null || _document == null)
            {
                return;
            }

            var rootElement = _document.rootVisualElement;
            if (rootElement == null)
            {
                return;
            }

            _reconciler.Apply(_root, rootElement);
            _bridge?.SetRootVNode(_root);
        }

        private static VNode BuildDemoTree()
        {
            return new VNode
            {
                Id = "root",
                Type = "container",
                Props =
                {
                    ["class"] = "root-container"
                },
                Children = new List<VNode>
                {
                    new VNode
                    {
                        Id = "title",
                        Type = "label",
                        Props =
                        {
                            ["text"] = "Hello VDOM",
                            ["class"] = "title"
                        },
                        Dataset =
                        {
                            ["state"] = "running"
                        }
                    },
                    new VNode
                    {
                        Id = "play-button",
                        Type = "button",
                        Props =
                        {
                            ["text"] = "Play",
                            ["tooltip"] = "Start the experience",
                            ["class"] = "primary"
                        },
                        Dataset =
                        {
                            ["state"] = "ready",
                            ["role"] = "primary"
                        }
                    }
                }
            };
        }
    }
}
