using System;
using System.Collections.Generic;
using System.Linq;

namespace RuntimeUIVDOM.VDom
{
    public static class VNodeQuery
    {
        public static VNode? QuerySelector(this VNode root, string selector)
        {
            if (root == null) throw new ArgumentNullException(nameof(root));
            if (string.IsNullOrWhiteSpace(selector)) throw new ArgumentException("Selector cannot be empty", nameof(selector));

            var criteria = SelectorCriteria.Parse(selector);
            return QueryFirst(root, criteria);
        }

        public static List<VNode> QuerySelectorAll(this VNode root, string selector)
        {
            if (root == null) throw new ArgumentNullException(nameof(root));
            if (string.IsNullOrWhiteSpace(selector)) throw new ArgumentException("Selector cannot be empty", nameof(selector));

            var criteria = SelectorCriteria.Parse(selector);
            var results = new List<VNode>();
            QueryAll(root, criteria, results);
            return results;
        }

        private static VNode? QueryFirst(VNode node, SelectorCriteria criteria)
        {
            if (criteria.Matches(node))
            {
                return node;
            }

            foreach (var child in node.Children)
            {
                var match = QueryFirst(child, criteria);
                if (match != null)
                {
                    return match;
                }
            }

            return null;
        }

        private static void QueryAll(VNode node, SelectorCriteria criteria, List<VNode> results)
        {
            if (criteria.Matches(node))
            {
                results.Add(node);
            }

            foreach (var child in node.Children)
            {
                QueryAll(child, criteria, results);
            }
        }

        private readonly struct SelectorCriteria
        {
            private readonly string? _id;
            private readonly string? _type;
            private readonly HashSet<string>? _classes;
            private readonly Dictionary<string, string>? _attributes;

            private SelectorCriteria(string? id, string? type, HashSet<string>? classes, Dictionary<string, string>? attributes)
            {
                _id = id;
                _type = type;
                _classes = classes;
                _attributes = attributes;
            }

            public bool Matches(VNode node)
            {
                if (_id != null && !string.Equals(node.Id, _id, StringComparison.Ordinal))
                {
                    return false;
                }

                if (_type != null && !string.Equals(node.Type, _type, StringComparison.OrdinalIgnoreCase))
                {
                    return false;
                }

                if (_classes != null)
                {
                    if (!node.Props.TryGetValue("class", out var classValue))
                    {
                        return false;
                    }

                    var tokens = classValue.Split(' ', StringSplitOptions.RemoveEmptyEntries);
                    var tokenSet = new HashSet<string>(tokens, StringComparer.Ordinal);
                    if (!_classes.All(tokenSet.Contains))
                    {
                        return false;
                    }
                }

                if (_attributes != null)
                {
                    foreach (var pair in _attributes)
                    {
                        if (!MatchesAttribute(node, pair.Key, pair.Value))
                        {
                            return false;
                        }
                    }
                }

                return true;
            }

            public static SelectorCriteria Parse(string selector)
            {
                string? id = null;
                string? type = null;
                HashSet<string>? classes = null;
                Dictionary<string, string>? attributes = null;

                int index = 0;
                while (index < selector.Length)
                {
                    char c = selector[index];
                    switch (c)
                    {
                        case '#':
                            index++;
                            id = ReadIdentifier(selector, ref index);
                            break;
                        case '.':
                            index++;
                            classes ??= new HashSet<string>(StringComparer.Ordinal);
                            classes.Add(ReadIdentifier(selector, ref index));
                            break;
                        case '[':
                            index++;
                            var key = ReadUntil(selector, ref index, '=');
                            index++; // skip '='
                            var value = ReadUntil(selector, ref index, ']');
                            index++; // skip ']'
                            attributes ??= new Dictionary<string, string>(StringComparer.Ordinal);
                            attributes[key] = value;
                            break;
                        default:
                            type = ReadType(selector, ref index);
                            break;
                    }
                }

                return new SelectorCriteria(id, type, classes, attributes);
            }

            private static string ReadIdentifier(string selector, ref int index)
            {
                int start = index;
                while (index < selector.Length)
                {
                    char c = selector[index];
                    if (c == '#' || c == '.' || c == '[' || c == ']')
                    {
                        break;
                    }
                    index++;
                }

                return selector[start..index];
            }

            private static string ReadUntil(string selector, ref int index, char terminator)
            {
                int start = index;
                while (index < selector.Length && selector[index] != terminator)
                {
                    index++;
                }

                return selector[start..index];
            }

            private static string ReadType(string selector, ref int index)
            {
                int start = index;
                while (index < selector.Length)
                {
                    char c = selector[index];
                    if (c == '#' || c == '.' || c == '[')
                    {
                        break;
                    }
                    index++;
                }

                return selector[start..index];
            }

            private static bool MatchesAttribute(VNode node, string key, string value)
            {
                if (node.Props.TryGetValue(key, out var propValue) && string.Equals(propValue, value, StringComparison.Ordinal))
                {
                    return true;
                }

                if (node.Dataset.TryGetValue(key, out var datasetValue) && string.Equals(datasetValue, value, StringComparison.Ordinal))
                {
                    return true;
                }

                return false;
            }
        }
    }
}
