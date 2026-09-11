using System;
using System.Security.Cryptography;
using System.Text;

namespace DragNWashLocalization
{
    // The published translation files carry no English. Each row is keyed by a
    // hash of the exact source string instead, so the repository does not
    // redistribute the game's script and only someone who can see the text on
    // screen - which is to say, someone who owns the game - can translate it.
    //
    // The key is the first 16 hex digits of SHA-256 over the UTF-8 bytes of the
    // string, exactly as TMP received it (no trimming, tags included). Sixteen
    // digits is 64 bits: no risk of collision across a few thousand lines, and
    // short enough to read in a diff. tools/hash-strings.ps1 computes the same.
    internal static class TranslationKey
    {
        public const int Length = 16;

        [ThreadStatic] private static SHA256 _sha;

        public static string Hash(string source)
        {
            if (source == null)
            {
                return string.Empty;
            }

            if (_sha == null)
            {
                _sha = SHA256.Create();
            }

            byte[] digest = _sha.ComputeHash(Encoding.UTF8.GetBytes(source));
            var sb = new StringBuilder(Length);
            for (int i = 0; i < Length / 2; i++)
            {
                sb.Append(digest[i].ToString("x2"));
            }
            return sb.ToString();
        }

        // A key column value: 16 lowercase hex digits. Anything else is
        // treated as not-a-key so a mistyped row is reported, not silently
        // matched against nothing.
        public static bool LooksLikeKey(string value)
        {
            if (value == null || value.Length != Length)
            {
                return false;
            }
            foreach (char c in value)
            {
                bool hex = (c >= '0' && c <= '9') || (c >= 'a' && c <= 'f');
                if (!hex)
                {
                    return false;
                }
            }
            return true;
        }
    }
}
