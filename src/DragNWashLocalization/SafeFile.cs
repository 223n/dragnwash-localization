using System;
using System.IO;
using System.Text;

namespace DragNWashLocalization
{
    // Writing a translator's file by truncating it and streaming into it costs
    // them their work when anything throws partway through: the previous
    // content is already gone and only half the new content is there.
    //
    // Write() puts the new content in a temporary file beside the target and
    // moves it into place once the write has finished, so the target holds
    // either the old content or the new one, never a truncated mixture.
    internal static class SafeFile
    {
        // Beside the target on purpose: the move is only cheap and atomic when
        // both are on the same volume, which a temp directory cannot promise.
        private static string TempFor(string path)
        {
            return path + ".tmp";
        }

        public static void Write(string path, Encoding encoding, Action<StreamWriter> write)
        {
            string temp = TempFor(path);
            try
            {
                using (var writer = new StreamWriter(temp, append: false, encoding))
                {
                    write(writer);
                }
            }
            catch
            {
                Discard(temp);
                throw;
            }
            Commit(temp, path);
        }

        private static void Commit(string temp, string path)
        {
            if (!File.Exists(path))
            {
                File.Move(temp, path);
                return;
            }
            try
            {
                // Keeps the target's identity, so an editor holding the file
                // open sees the new content rather than a dangling handle.
                File.Replace(temp, path, null, ignoreMetadataErrors: true);
            }
            catch (PlatformNotSupportedException)
            {
                // Some Mono builds do not implement File.Replace. The window
                // here is a single delete rather than a whole file write.
                File.Delete(path);
                File.Move(temp, path);
            }
        }

        private static void Discard(string temp)
        {
            try
            {
                if (temp != null && File.Exists(temp))
                {
                    File.Delete(temp);
                }
            }
            catch
            {
                // A leftover .tmp is untidy, not harmful; never mask the
                // original failure with a cleanup failure.
            }
        }
    }
}
