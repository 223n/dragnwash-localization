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
        // Build() walks every loaded object (Resources.FindObjectsOfTypeAll),
        // so it must run once per operation, not once per row. Testing the
        // table for emptiness instead would rebuild on every call whenever no
        // project is loaded - which is exactly when a translator presses
        // "Hash for commit" from the title screen.
        private static bool _built;

        // Forget the table so the next lookup rebuilds it. Callers that batch
        // many lookups call this once up front; a project loaded since the last
        // build is then picked up, without paying for a scan per row.
        public static void Reset()
        {
            _byKey = null;
            _built = false;
        }

        private static void Build()
        {
            _built = true;
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
            if (!_built)
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
