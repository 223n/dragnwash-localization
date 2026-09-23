using System;
using System.IO;
using System.Linq;
using System.Text;
using Xunit;

namespace DragNWashLocalization.Tests
{
    /// <summary>
    /// <para>
    /// Tests for <see cref="SafeFile"/>: saving a translator's file must leave
    /// either the old content or the new one, never half of each, and never a
    /// stray .tmp beside it.
    /// </para>
    /// <para>
    /// <see cref="SafeFile"/> のテスト。
    /// 翻訳者のファイルの保存では、古い内容か新しい内容のどちらかが必ず残る。
    /// 両者が混ざったり、横に .tmp が残ったりしてはいけない。
    /// </para>
    /// </summary>
    public class SafeFileTests
    {
        /// <summary>
        /// <para>UTF-8 without a BOM, as the repository keeps its CSVs.</para>
        /// <para>リポジトリの CSV と同じ BOM なし UTF-8。</para>
        /// </summary>
        private static readonly Encoding Utf8 = new UTF8Encoding(false);

        /// <summary>
        /// <para>The temporary files <see cref="SafeFile"/> may have left in the directory.</para>
        /// <para><see cref="SafeFile"/> がディレクトリに残した可能性のある一時ファイル。</para>
        /// </summary>
        /// <param name="dir">
        /// <para>The test's directory.</para>
        /// <para>テストのディレクトリ。</para>
        /// </param>
        /// <returns>
        /// <para>The paths of every <c>*.tmp</c> file in it.</para>
        /// <para>その中にあるすべての <c>*.tmp</c> ファイルのパス。</para>
        /// </returns>
        private static string[] TempFilesBeside(TempDirectory dir)
        {
            return Directory.GetFiles(dir.Path, "*.tmp");
        }

        /// <summary>
        /// <para>A file that did not exist is created, and the temp file is gone.</para>
        /// <para>存在しなかったファイルは作成され、一時ファイルは残らない。</para>
        /// </summary>
        [Fact]
        public void CreatesAFileThatDidNotExist()
        {
            using (var dir = new TempDirectory())
            {
                string path = dir.File("strings.csv");

                SafeFile.Write(path, Utf8, w => w.Write("new"));

                Assert.Equal("new", File.ReadAllText(path));
                Assert.Empty(TempFilesBeside(dir));
            }
        }

        /// <summary>
        /// <para>
        /// An existing file is replaced whole: nothing of the longer old
        /// content remains after the shorter new one.
        /// </para>
        /// <para>
        /// 既存のファイルは丸ごと置き換わる。
        /// 新しい内容が短くても、古い内容の残りが後ろに残らない。
        /// </para>
        /// </summary>
        [Fact]
        public void ReplacesAnExistingFile()
        {
            using (var dir = new TempDirectory())
            {
                string path = dir.Write("strings.csv", "old content that is longer");

                SafeFile.Write(path, Utf8, w => w.Write("new"));

                Assert.Equal("new", File.ReadAllText(path));
                Assert.Empty(TempFilesBeside(dir));
            }
        }

        /// <summary>
        /// <para>The bytes are in the encoding asked for.</para>
        /// <para>指定したエンコーディングで書かれる。</para>
        /// </summary>
        [Fact]
        public void WritesWithTheGivenEncoding()
        {
            using (var dir = new TempDirectory())
            {
                string path = dir.File("strings.csv");

                SafeFile.Write(path, Utf8, w => w.Write("訳"));

                Assert.Equal(Utf8.GetBytes("訳"), File.ReadAllBytes(path));
            }
        }

        /// <summary>
        /// <para>
        /// An exception partway through the write keeps the old content, is
        /// passed on to the caller, and cleans up the temp file.
        /// </para>
        /// <para>
        /// 書き込みの途中で例外が出ると、古い内容はそのまま残り、
        /// 例外は呼び出し元へ伝わり、一時ファイルは片付けられる。
        /// </para>
        /// </summary>
        [Fact]
        public void AFailedWriteLeavesTheOldContentAndNoTempFile()
        {
            using (var dir = new TempDirectory())
            {
                string path = dir.Write("strings.csv", "old");

                Assert.Throws<InvalidOperationException>(() =>
                    SafeFile.Write(path, Utf8, w =>
                    {
                        w.Write("half of the new");
                        throw new InvalidOperationException("boom");
                    }));

                Assert.Equal("old", File.ReadAllText(path));
                Assert.Empty(TempFilesBeside(dir));
            }
        }

        /// <summary>
        /// <para>A failed first save does not leave an empty or partial file behind.</para>
        /// <para>初回の保存に失敗しても、空のファイルや途中までのファイルは作られない。</para>
        /// </summary>
        [Fact]
        public void AFailedWriteOfANewFileCreatesNothing()
        {
            using (var dir = new TempDirectory())
            {
                string path = dir.File("strings.csv");

                Assert.Throws<InvalidOperationException>(() =>
                    SafeFile.Write(path, Utf8, w => throw new InvalidOperationException("boom")));

                Assert.False(File.Exists(path));
                Assert.Empty(TempFilesBeside(dir));
            }
        }

        /// <summary>
        /// <para>
        /// A leftover .tmp that another program holds open makes
        /// <see cref="SafeFile"/> try the next name, and the leftover itself is
        /// left alone.
        /// </para>
        /// <para>
        /// 別のプログラムが開いたままの .tmp が残っていると、
        /// <see cref="SafeFile"/> は次の名前を試す。
        /// 残っていた .tmp には手を付けない。
        /// </para>
        /// </summary>
        [Fact]
        public void ALockedTempFileNameFallsBackToTheNextOne()
        {
            using (var dir = new TempDirectory())
            {
                string path = dir.Write("strings.csv", "old");
                string leftover = dir.Write("strings.csv.tmp", "someone else's");
                using (new FileStream(leftover, FileMode.Open, FileAccess.Read, FileShare.None))
                {
                    SafeFile.Write(path, Utf8, w => w.Write("new"));
                }

                Assert.Equal("new", File.ReadAllText(path));
                Assert.False(File.Exists(dir.File("strings.csv.1.tmp")));
                Assert.Equal("someone else's", File.ReadAllText(leftover));
            }
        }

        /// <summary>
        /// <para>
        /// With every temp name taken, the file is written in place rather
        /// than not saved at all: atomicity is lost, the work is not.
        /// </para>
        /// <para>
        /// 一時ファイル名がすべて使えないときは、保存を諦めずにファイルへ直接書く。
        /// 失うのは原子性であって、翻訳者の作業ではない。
        /// </para>
        /// </summary>
        [Fact]
        public void EveryTempFileNameLockedStillSavesInPlace()
        {
            using (var dir = new TempDirectory())
            {
                string path = dir.Write("strings.csv", "old");
                string[] names = { "strings.csv.tmp", "strings.csv.1.tmp", "strings.csv.2.tmp", "strings.csv.3.tmp" };
                var locks = names.Select(n => new FileStream(dir.Write(n, "x"), FileMode.Open, FileAccess.Read, FileShare.None)).ToList();
                try
                {
                    SafeFile.Write(path, Utf8, w => w.Write("new"));
                }
                finally
                {
                    locks.ForEach(l => l.Dispose());
                }

                Assert.Equal("new", File.ReadAllText(path));
            }
        }

        /// <summary>
        /// <para>
        /// A target another program holds open without delete sharing is still
        /// saved, by copying over it, and the temp file is removed.
        /// </para>
        /// <para>
        /// 別のプログラムが削除を共有せずに開いているファイルでも、
        /// 上書きコピーで保存し、一時ファイルは消す。
        /// </para>
        /// </summary>
        /// <remarks>
        /// <para>
        /// An editor that keeps the file open (read/write share, no delete
        /// share) blocks <see cref="File.Replace(string, string, string, bool)"/>.
        /// </para>
        /// <para>
        /// エディターがファイルを開いたまま（読み書きは共有、削除は共有しない）だと、
        /// <see cref="File.Replace(string, string, string, bool)"/> は失敗する。
        /// </para>
        /// </remarks>
        [Fact]
        public void ATargetHeldOpenWithoutDeleteShareIsOverwritten()
        {
            using (var dir = new TempDirectory())
            {
                string path = dir.Write("strings.csv", "old content");
                using (new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
                {
                    SafeFile.Write(path, Utf8, w => w.Write("new"));
                }

                Assert.Equal("new", File.ReadAllText(path));
                Assert.Empty(TempFilesBeside(dir));
            }
        }
    }
}
