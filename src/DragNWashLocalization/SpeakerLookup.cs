using System;
using System.Collections.Generic;

namespace DragNWashLocalization
{
    // key -> speaker, built lazily from the loaded Yarn project so a published
    // row can carry who says it. Empty when no project is loaded (title
    // screen); the row then keeps whatever speaker it already had.
    internal static class SpeakerLookup
    {
        private static Dictionary<string, string> _byKey;

        private static void Build()
        {
            _byKey = new Dictionary<string, string>(StringComparer.Ordinal);
            foreach (KeyValuePair<string, string> line in DialogueDumper.EnumerateOrderedLines())
            {
                string key = TranslationStore.KeyFor(line.Key);
                if (!_byKey.ContainsKey(key) && !string.IsNullOrEmpty(line.Value))
                {
                    _byKey[key] = line.Value;
                }
            }
        }

        public static string ForKey(string key)
        {
            if (_byKey == null || _byKey.Count == 0)
            {
                Build();
            }
            return _byKey.TryGetValue(key, out string s) ? s : string.Empty;
        }

        public static string For(string source)
        {
            return ForKey(TranslationStore.KeyFor(source));
        }
    }
}
