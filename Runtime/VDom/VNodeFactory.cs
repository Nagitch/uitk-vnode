using System;
using System.Globalization;
using UnityEngine.UIElements;

namespace RuntimeUIVDOM.VDom
{
    /// <summary>
    /// Creates UI Toolkit elements from VNode descriptors and applies properties to them.
    /// </summary>
    public static class VNodeFactory
    {
        private static readonly char[] ClassSeparators = { ' ' };

        public static VisualElement Create(VNode node)
        {
            if (node == null) throw new ArgumentNullException(nameof(node));

            VisualElement element = (VisualElement)Activator.CreateInstance(GetElementType(node.Type))!;

            element.name = node.Id;
            ApplyProps(node, element);
            return element;
        }

        public static bool IsElementCompatible(VNode node, VisualElement element)
        {
            if (node == null) throw new ArgumentNullException(nameof(node));
            if (element == null) throw new ArgumentNullException(nameof(element));

            var expectedType = GetElementType(node.Type);
            return element.GetType() == expectedType;
        }

        public static void ApplyProps(VNode node, VisualElement element)
        {
            if (node == null) throw new ArgumentNullException(nameof(node));
            if (element == null) throw new ArgumentNullException(nameof(element));

            if (node.Props.TryGetValue("text", out var textValue))
            {
                if (element is TextElement textElement)
                {
                    textElement.text = textValue ?? string.Empty;
                }
            }

            if (node.Props.TryGetValue("tooltip", out var tooltip))
            {
                element.tooltip = tooltip;
            }

            if (node.Props.TryGetValue("class", out var classList))
            {
                ApplyClassList(element, classList);
            }
            else
            {
                element.ClearClassList();
            }

            if (node.Props.TryGetValue("width", out var widthValue) && float.TryParse(widthValue, NumberStyles.Float, CultureInfo.InvariantCulture, out var width))
            {
                element.style.width = width;
            }
            else
            {
                element.style.width = StyleKeyword.Null;
            }

            if (node.Props.TryGetValue("height", out var heightValue) && float.TryParse(heightValue, NumberStyles.Float, CultureInfo.InvariantCulture, out var height))
            {
                element.style.height = height;
            }
            else
            {
                element.style.height = StyleKeyword.Null;
            }

            if (node.Props.TryGetValue("display", out var displayValue))
            {
                element.style.display = string.Equals(displayValue, "none", StringComparison.OrdinalIgnoreCase)
                    ? DisplayStyle.None
                    : DisplayStyle.Flex;
            }
            else
            {
                element.style.display = StyleKeyword.Null;
            }
        }

        private static void ApplyClassList(VisualElement element, string? classList)
        {
            element.ClearClassList();
            if (string.IsNullOrWhiteSpace(classList))
            {
                return;
            }

            var tokens = classList.Split(ClassSeparators, StringSplitOptions.RemoveEmptyEntries);
            foreach (var token in tokens)
            {
                element.AddToClassList(token);
            }
        }

        private static Type GetElementType(string? type)
        {
            return type switch
            {
                "label" => typeof(Label),
                "button" => typeof(Button),
                "image" => typeof(Image),
                "container" => typeof(VisualElement),
                _ => typeof(VisualElement),
            };
        }
    }
}
