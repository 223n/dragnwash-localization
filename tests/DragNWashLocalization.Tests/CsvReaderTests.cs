using System.Collections.Generic;
using System.IO;
using System.Linq;
using Xunit;

namespace DragNWashLocalization.Tests
{
    /// <summary>
    /// <para>
    /// Tests for <see cref="CsvReader"/>: the parser every strings.csv and
    /// working copy goes through, and <see cref="CsvReader.Escape(string)"/>,
    /// which writes values the parser must read back unchanged.
    /// </para>
    /// <para>
    /// <see cref="CsvReader"/> のテスト。
    /// すべての strings.csv と作業コピーを読むパーサーと、
    /// <see cref="CsvReader.Escape(string)"/> を確かめる。
    /// <see cref="CsvReader.Escape(string)"/> は、パーサーがそのまま読み戻せる形で値を書き出す。
    /// </para>
    /// </summary>
    public class CsvReaderTests
    {
        /// <summary>
        /// <para>Writes the content to a file of its own and reads it back as rows.</para>
        /// <para>内容を専用のファイルに書き、行として読み戻す。</para>
        /// </summary>
        /// <param name="content">
        /// <para>The whole CSV text, header included.</para>
        /// <para>ヘッダーを含む CSV の全文。</para>
        /// </param>
        /// <param name="bom">
        /// <para>Whether to write a UTF-8 BOM first, as Excel does.</para>
        /// <para>Excel と同じく先頭に UTF-8 の BOM を書くかどうか。</para>
        /// </param>
        /// <returns>
        /// <para>The rows <see cref="CsvReader.ReadRows(string)"/> returned.</para>
        /// <para><see cref="CsvReader.ReadRows(string)"/> が返した行。</para>
        /// </returns>
        private static List<Dictionary<string, string>> Read(string content, bool bom = false)
        {
            using (var dir = new TempDirectory())
            {
                string path = dir.Write("strings.csv", content, bom);
                return CsvReader.ReadRows(path).ToList();
            }
        }

        /// <summary>
        /// <para>Each row is a dictionary keyed by the header's column names.</para>
        /// <para>各行はヘッダーの列名をキーにした辞書として読まれる。</para>
        /// </summary>
        [Fact]
        public void ReadsPlainRowsByHeader()
        {
            var rows = Read("key,translation\n0123456789abcdef,こんにちは\nfedcba9876543210,さようなら\n");

            Assert.Equal(2, rows.Count);
            Assert.Equal("0123456789abcdef", rows[0]["key"]);
            Assert.Equal("こんにちは", rows[0]["translation"]);
            Assert.Equal("さようなら", rows[1]["translation"]);
        }

        /// <summary>
        /// <para>A translator who capitalises a header name must not lose the column.</para>
        /// <para>翻訳者がヘッダー名を大文字にしても、列が読めなくならない。</para>
        /// </summary>
        [Fact]
        public void HeaderLookupIgnoresCase()
        {
            var rows = Read("Key,TRANSLATION\na,b\n");

            Assert.Equal("a", rows[0]["key"]);
            Assert.Equal("b", rows[0]["translation"]);
        }

        /// <summary>
        /// <para>An empty file is not an error; it simply has no rows.</para>
        /// <para>空のファイルはエラーではなく、行がないだけとして扱う。</para>
        /// </summary>
        [Fact]
        public void EmptyFileHasNoRows()
        {
            Assert.Empty(Read(string.Empty));
        }

        /// <summary>
        /// <para>The header is never returned as a row.</para>
        /// <para>ヘッダー行はデータ行として返さない。</para>
        /// </summary>
        [Fact]
        public void HeaderOnlyHasNoRows()
        {
            Assert.Empty(Read("key,translation\n"));
        }

        /// <summary>
        /// <para>Inside quotes, commas and newlines are text and <c>""</c> is one quote.</para>
        /// <para>引用符の中ではカンマと改行は文字として扱い、<c>""</c> は引用符 1 つになる。</para>
        /// </summary>
        [Fact]
        public void QuotedFieldKeepsCommasQuotesAndNewlines()
        {
            var rows = Read("key,translation\nk,\"a, \"\"b\"\"\nc\"\n");

            Assert.Single(rows);
            Assert.Equal("a, \"b\"\nc", rows[0]["translation"]);
        }

        /// <summary>
        /// <para>A file saved on Windows (CRLF) reads the same as one with LF.</para>
        /// <para>Windows で保存したファイル（CRLF）も LF と同じように読める。</para>
        /// </summary>
        [Fact]
        public void CrLfLineEndingsAreAccepted()
        {
            var rows = Read("key,translation\r\nk1,t1\r\nk2,t2\r\n");

            Assert.Equal(2, rows.Count);
            Assert.Equal("t1", rows[0]["translation"]);
            Assert.Equal("k2", rows[1]["key"]);
        }

        /// <summary>
        /// <para>Section headers (<c># ...</c>) and blank lines in the published files are not rows.</para>
        /// <para>公開ファイルにある見出し（<c># ...</c>）と空行は行として扱わない。</para>
        /// </summary>
        [Fact]
        public void CommentAndBlankLinesAreSkipped()
        {
            var rows = Read("key,translation\n# ===== Section =====\n\nk1,t1\n\n# note\nk2,t2\n");

            Assert.Equal(new[] { "k1", "k2" }, rows.Select(r => r["key"]));
        }

        /// <summary>
        /// <para>Only a <c>#</c> at the start of a record makes a comment; one later in the record is ordinary text.</para>
        /// <para>
        /// コメントになるのは行頭の <c>#</c> だけで、行の途中の <c>#</c> は普通の文字。
        /// </para>
        /// </summary>
        [Fact]
        public void HashInsideARecordIsNotAComment()
        {
            var rows = Read("key,translation\nk,#1 位\n");

            Assert.Equal("#1 位", rows[0]["translation"]);
        }

        /// <summary>
        /// <para>A quoted <c>#</c> at the start of a record is data, so such a row survives.</para>
        /// <para>行頭でも引用符で囲んだ <c>#</c> はデータなので、その行は残る。</para>
        /// </summary>
        /// <remarks>
        /// <para><see cref="CsvReader.Escape(string)"/> relies on this.</para>
        /// <para><see cref="CsvReader.Escape(string)"/> はこの挙動を前提にしている。</para>
        /// </remarks>
        [Fact]
        public void QuotedLeadingHashIsARowNotAComment()
        {
            var rows = Read("key,translation\n\"#k\",t\n");

            Assert.Single(rows);
            Assert.Equal("#k", rows[0]["key"]);
        }

        /// <summary>
        /// <para>A file that does not end with a newline still has its last row.</para>
        /// <para>末尾に改行がないファイルでも、最後の行が読める。</para>
        /// </summary>
        [Fact]
        public void LastRowWithoutNewlineIsRead()
        {
            var rows = Read("key,translation\nk,t");

            Assert.Single(rows);
            Assert.Equal("t", rows[0]["translation"]);
        }

        /// <summary>
        /// <para><c>k,</c> is a row whose translation is empty, not a row without one.</para>
        /// <para><c>k,</c> は訳が空の行であり、訳の列がない行ではない。</para>
        /// </summary>
        [Fact]
        public void TrailingEmptyFieldIsKept()
        {
            var rows = Read("key,translation\nk,\n");

            Assert.Single(rows);
            Assert.Equal(string.Empty, rows[0]["translation"]);
        }

        /// <summary>
        /// <para>Columns a row does not reach read as empty, not as missing keys.</para>
        /// <para>行に足りない列は、キーなしではなく空文字として読む。</para>
        /// </summary>
        [Fact]
        public void ShortRowFillsMissingColumnsWithEmpty()
        {
            var rows = Read("key,speaker,translation\nk\n");

            Assert.Equal("k", rows[0]["key"]);
            Assert.Equal(string.Empty, rows[0]["speaker"]);
            Assert.Equal(string.Empty, rows[0]["translation"]);
        }

        /// <summary>
        /// <para>Fields beyond the header have no name and are dropped.</para>
        /// <para>ヘッダーより多い列は名前がないので捨てる。</para>
        /// </summary>
        [Fact]
        public void ExtraColumnsBeyondTheHeaderAreIgnored()
        {
            var rows = Read("key,translation\nk,t,extra\n");

            Assert.Equal(2, rows[0].Count);
            Assert.Equal("t", rows[0]["translation"]);
        }

        /// <summary>
        /// <para>Excel saves UTF-8 with a BOM; it must not end up in the first column's name.</para>
        /// <para>
        /// Excel は BOM 付き UTF-8 で保存する。
        /// BOM が先頭列の名前に混ざってはいけない。
        /// </para>
        /// </summary>
        [Fact]
        public void Utf8BomIsNotPartOfTheFirstHeader()
        {
            var rows = Read("key,translation\nk,t\n", bom: true);

            Assert.True(rows[0].ContainsKey("key"));
        }

        /// <summary>
        /// <para>A file another program holds open for writing can still be read.</para>
        /// <para>別のプログラムが書き込み用に開いているファイルでも読める。</para>
        /// </summary>
        /// <remarks>
        /// <para>
        /// Translators keep the file open in an editor while the game runs,
        /// so reading must not need exclusive access.
        /// </para>
        /// <para>
        /// 翻訳者はゲーム実行中もファイルをエディターで開いたままにするので、
        /// 読み込みに排他アクセスを要求してはいけない。
        /// </para>
        /// </remarks>
        [Fact]
        public void FileHeldOpenForWritingCanStillBeRead()
        {
            using (var dir = new TempDirectory())
            {
                string path = dir.Write("strings.csv", "key,translation\nk,t\n");
                using (new FileStream(path, FileMode.Open, FileAccess.ReadWrite, FileShare.ReadWrite))
                {
                    Assert.Single(CsvReader.ReadRows(path).ToList());
                }
            }
        }

        /// <summary>
        /// <para>
        /// <see cref="CsvReader.Escape(string)"/> quotes a value only when the
        /// parser would otherwise misread it: a comma, a quote, a line break,
        /// or a leading <c>#</c>.
        /// </para>
        /// <para>
        /// <see cref="CsvReader.Escape(string)"/> は、
        /// そのままではパーサーが読み違える値だけを引用符で囲む。
        /// 対象は、カンマ、引用符、改行のいずれかを含む値と、<c>#</c> で始まる値。
        /// </para>
        /// </summary>
        /// <param name="value">
        /// <para>The value to escape.</para>
        /// <para>エスケープする値。</para>
        /// </param>
        /// <param name="expected">
        /// <para>The text expected in the CSV.</para>
        /// <para>CSV に書かれるべき文字列。</para>
        /// </param>
        [Theory]
        [InlineData(null, "")]
        [InlineData("", "")]
        [InlineData("plain", "plain")]
        [InlineData("a,b", "\"a,b\"")]
        [InlineData("say \"hi\"", "\"say \"\"hi\"\"\"")]
        [InlineData("two\nlines", "\"two\nlines\"")]
        [InlineData("cr\rhere", "\"cr\rhere\"")]
        [InlineData("#1", "\"#1\"")]
        [InlineData("a#1", "a#1")]
        public void EscapeQuotesOnlyWhatNeedsIt(string value, string expected)
        {
            Assert.Equal(expected, CsvReader.Escape(value));
        }

        /// <summary>
        /// <para>
        /// Whatever <see cref="CsvReader.Escape(string)"/> writes, in the first
        /// or a later column, reads back as the original value.
        /// </para>
        /// <para>
        /// <see cref="CsvReader.Escape(string)"/> が書いた値は、
        /// 先頭列でも後ろの列でも元の値として読み戻せる。
        /// </para>
        /// </summary>
        /// <param name="value">
        /// <para>The value written and read back.</para>
        /// <para>書いてから読み戻す値。</para>
        /// </param>
        [Theory]
        [InlineData("plain")]
        [InlineData("a, b")]
        [InlineData("say \"hi\"")]
        [InlineData("two\nlines")]
        [InlineData("#leading hash")]
        [InlineData("\"")]
        public void EscapedValueReadsBackUnchanged(string value)
        {
            var rows = Read("key,translation\nk," + CsvReader.Escape(value) + "\n" + CsvReader.Escape(value) + ",t\n");

            Assert.Equal(2, rows.Count);
            Assert.Equal(value, rows[0]["translation"]);
            Assert.Equal(value, rows[1]["key"]);
        }
    }
}
