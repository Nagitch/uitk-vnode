using System;
using System.Collections.Generic;

namespace RuntimeUIVDOM.Sync
{
    /// <summary>
    /// Provides mappings from dataset string values to integers for GPU consumption.
    /// </summary>
    public static class DatasetEncoder
    {
        private static readonly Dictionary<string, Dictionary<string, int>> Presets = new(StringComparer.Ordinal)
        {
            {
                "state", new Dictionary<string, int>(StringComparer.Ordinal)
                {
                    ["ready"] = 1,
                    ["running"] = 2,
                    ["disabled"] = 3
                }
            },
            {
                "role", new Dictionary<string, int>(StringComparer.Ordinal)
                {
                    ["primary"] = 1,
                    ["secondary"] = 2
                }
            }
        };

        public static int Encode(string key, string? value)
        {
            if (string.IsNullOrEmpty(key) || value == null)
            {
                return 0;
            }

            if (Presets.TryGetValue(key, out var map) && map.TryGetValue(value, out var encoded))
            {
                return encoded;
            }

            return 0;
        }
    }
}
