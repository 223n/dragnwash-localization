using System;
using System.IO;
using System.Text;

namespace DragNWashLocalization.Tests
{
    /// <summary>
    /// <para>A directory of its own for each test, removed afterwards.</para>
    /// <para>テストごとの専用ディレクトリ。終わったら削除する。</para>
    /// </summary>
    /// <remarks>
    /// <para>Tests never touch the repository's files or each other's.</para>
    /// <para>テストがリポジトリのファイルや他のテストのファイルに触れないようにするため。</para>
    /// </remarks>
    internal sealed class TempDirectory : IDisposable
    {
        /// <summary>
        /// <para>The directory's full path.</para>
        /// <para>ディレクトリのフルパス。</para>
        /// </summary>
        public string Path { get; }

        /// <summary>
        /// <para>Creates a new, empty directory under the system's temp directory.</para>
        /// <para>システムの一時ディレクトリの下に、空のディレクトリを新しく作る。</para>
        /// </summary>
        public TempDirectory()
        {
            Path = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "dwloc-tests-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(Path);
        }

        /// <summary>
        /// <para>The full path of a file in this directory, which may not exist yet.</para>
        /// <para>このディレクトリ内のファイルのフルパス（まだ存在しなくてもよい）。</para>
        /// </summary>
        /// <param name="name">
        /// <para>The file's path relative to this directory.</para>
        /// <para>このディレクトリからのファイルの相対パス。</para>
        /// </param>
        /// <returns>
        /// <para>The full path.</para>
        /// <para>フルパス。</para>
        /// </returns>
        public string File(string name) => System.IO.Path.Combine(Path, name);

        /// <summary>
        /// <para>Writes a file in this directory, creating its folders as needed.</para>
        /// <para>このディレクトリにファイルを書く。途中のフォルダーは必要に応じて作る。</para>
        /// </summary>
        /// <param name="name">
        /// <para>The file's path relative to this directory.</para>
        /// <para>このディレクトリからのファイルの相対パス。</para>
        /// </param>
        /// <param name="content">
        /// <para>The text to write.</para>
        /// <para>書き込む文字列。</para>
        /// </param>
        /// <param name="bom">
        /// <para>Whether to write a UTF-8 BOM; without one, as the repository keeps its CSVs.</para>
        /// <para>UTF-8 の BOM を書くかどうか。既定はリポジトリの CSV と同じ BOM なし。</para>
        /// </param>
        /// <returns>
        /// <para>The file's full path.</para>
        /// <para>ファイルのフルパス。</para>
        /// </returns>
        public string Write(string name, string content, bool bom = false)
        {
            string path = File(name);
            Directory.CreateDirectory(System.IO.Path.GetDirectoryName(path));
            System.IO.File.WriteAllText(path, content, new UTF8Encoding(bom));
            return path;
        }

        /// <summary>
        /// <para>Deletes the directory and everything in it.</para>
        /// <para>ディレクトリを中身ごと削除する。</para>
        /// </summary>
        /// <remarks>
        /// <para>A file still held open must not fail the test that used it, so failures are ignored.</para>
        /// <para>
        /// まだ開かれているファイルがあっても使ったテストを失敗させないよう、削除の失敗は無視する。
        /// </para>
        /// </remarks>
        public void Dispose()
        {
            try
            {
                Directory.Delete(Path, recursive: true);
            }
            catch (IOException)
            {
            }
            catch (UnauthorizedAccessException)
            {
            }
        }
    }
}
