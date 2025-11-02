using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine.UIElements;

namespace RuntimeUIVDOM.VDom
{
    /// <summary>
    /// Applies a VNode tree to an existing UI Toolkit visual tree.
    /// </summary>
    public class UiReconciler
    {
        public void Apply(VNode rootNode, VisualElement rootElement)
        {
            if (rootNode == null) throw new ArgumentNullException(nameof(rootNode));
            if (rootElement == null) throw new ArgumentNullException(nameof(rootElement));

            if (!string.IsNullOrEmpty(rootNode.Id))
            {
                rootElement.name = rootNode.Id;
            }

            VNodeFactory.ApplyProps(rootNode, rootElement);
            ReconcileChildren(rootNode, rootElement);
        }

        private void ReconcileChildren(VNode parentNode, VisualElement parentElement)
        {
            var existingChildren = new Dictionary<string, VisualElement>(StringComparer.Ordinal);
            var orderedChildren = parentElement.Children().ToList();

            foreach (var child in orderedChildren)
            {
                if (!string.IsNullOrEmpty(child.name) && !existingChildren.ContainsKey(child.name))
                {
                    existingChildren.Add(child.name, child);
                }
            }

            var handledIds = new HashSet<string>(StringComparer.Ordinal);

            for (int i = 0; i < parentNode.Children.Count; i++)
            {
                var childNode = parentNode.Children[i];
                if (string.IsNullOrEmpty(childNode.Id))
                {
                    throw new InvalidOperationException("VNode.Id must not be null or empty.");
                }

                if (!existingChildren.TryGetValue(childNode.Id, out var childElement))
                {
                    childElement = VNodeFactory.Create(childNode);
                    parentElement.Insert(i, childElement);
                }
                else
                {
                    EnsureCorrectParent(parentElement, childElement, i);
                }

                childElement.name = childNode.Id;
                handledIds.Add(childNode.Id);
                VNodeFactory.ApplyProps(childNode, childElement);
                ReconcileChildren(childNode, childElement);
            }

            foreach (var child in orderedChildren)
            {
                if (string.IsNullOrEmpty(child.name) || !handledIds.Contains(child.name))
                {
                    child.RemoveFromHierarchy();
                }
            }
        }

        private static void EnsureCorrectParent(VisualElement parentElement, VisualElement childElement, int targetIndex)
        {
            if (childElement.parent != parentElement)
            {
                childElement.RemoveFromHierarchy();
                parentElement.Insert(targetIndex, childElement);
                return;
            }

            int currentIndex = parentElement.IndexOf(childElement);
            if (currentIndex != targetIndex)
            {
                parentElement.RemoveAt(currentIndex);
                parentElement.Insert(targetIndex, childElement);
            }
        }
    }
}
