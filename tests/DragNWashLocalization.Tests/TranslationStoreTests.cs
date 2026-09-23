using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Web.Script.Serialization;
using Xunit;

namespace DragNWashLocalization.Tests
{
    /// <summary>
    /// <para>
    /// Tests for the part of <see cref="TranslationStore"/> that needs no
    /// loaded locale: the key a string gets, and the lookups when nothing is
    /// loaded.
    /// </para>
    /// <para>
    /// <see cref="TranslationStore"/> のうち、言語を読み込まなくても動く部分のテスト。
    /// 対象は、文字列から作るキーと、何も読み込んでいないときの検索。
    /// </para>
    /// </summary>
    /// <remarks>
    /// <para>
    /// Loading lives in TranslationStore.Loading.cs, which reaches Unity and is
    /// not compiled into the tests.
    /// </para>
    /// <para>
    /// 読み込み処理は Unity に依存する TranslationStore.Loading.cs にあり、テストには含めていない。
    /// </para>
    /// </remarks>
    public class TranslationStoreTests
    {
        /// <summary>
        /// <para>Each vector in ci/linekey-vectors.json.</para>
        /// <para>ci/linekey-vectors.json の各ベクトル。</para>
        /// </summary>
        /// <returns>
        /// <para>One (text, expected key) pair per vector.</para>
        /// <para>ベクトルごとの (文字列, 期待するキー) の組。</para>
        /// </returns>
        public static IEnumerable<object[]> Vectors()
        {
            string path = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "linekey-vectors.json");
            var list = (object[])new JavaScriptSerializer().DeserializeObject(File.ReadAllText(path));
            foreach (Dictionary<string, object> v in list.Cast<Dictionary<string, object>>())
            {
                yield return new object[] { (string)v["text"], (string)v["key"] };
            }
        }

        /// <summary>
        /// <para>
        /// <see cref="TranslationStore.KeyFor(string)"/> gives the key
        /// ci/linekey-vectors.json expects for each text.
        /// </para>
        /// <para>
        /// <see cref="TranslationStore.KeyFor(string)"/> は、
        /// 各文字列に対して ci/linekey-vectors.json が期待するキーを返す。
        /// </para>
        /// </summary>
        /// <remarks>
        /// <para>
        /// The vectors are what tools/linekeys.py and tools/hash-strings.ps1
        /// agree on; the plugin must hash the same, or no published row would
        /// ever match the text on screen.
        /// </para>
        /// <para>
        /// このベクトルは tools/linekeys.py と tools/hash-strings.ps1 が一致を確認している値。
        /// プラグインも同じハッシュを出さないと、
        /// 公開ファイルの行が画面の文字列に一つも一致しなくなる。
        /// </para>
        /// </remarks>
        /// <param name="text">
        /// <para>The source text, exactly as on screen.</para>
        /// <para>画面に表示されるとおりの原文。</para>
        /// </param>
        /// <param name="key">
        /// <para>The key the vectors give for <paramref name="text"/>.</para>
        /// <para>ベクトルが <paramref name="text"/> に対して示すキー。</para>
        /// </param>
        [Theory]
        [MemberData(nameof(Vectors))]
        public void KeyForMatchesTheSharedVectors(string text, string key)
        {
            Assert.Equal(key, TranslationStore.KeyFor(text));
        }

        /// <summary>
        /// <para>The vectors file holds vectors to check against.</para>
        /// <para>ベクトルのファイルに、照合するベクトルが入っている。</para>
        /// </summary>
        /// <remarks>
        /// <para>
        /// Guards <see cref="KeyForMatchesTheSharedVectors(string, string)"/>:
        /// an empty or unreadable file would otherwise let it pass with nothing
        /// checked.
        /// </para>
        /// <para>
        /// <see cref="KeyForMatchesTheSharedVectors(string, string)"/> の保険。
        /// ファイルが空だったり読めなかったりすると、何も確認しないまま通ってしまう。
        /// </para>
        /// </remarks>
        [Fact]
        public void TheVectorsFileIsNotEmpty()
        {
            Assert.True(Vectors().Count() >= 10);
        }

        /// <summary>
        /// <para>
        /// Whatever the text, its key is one
        /// <see cref="TranslationKey.LooksLikeKey(string)"/> accepts, so a row
        /// the plugin writes is never rejected when read back.
        /// </para>
        /// <para>
        /// どんな文字列でも、
        /// <see cref="TranslationKey.LooksLikeKey(string)"/> が受け付けるキーになる。
        /// そのため、プラグインが書いた行が読み戻しで弾かれることはない。
        /// </para>
        /// </summary>
        [Fact]
        public void KeyForIsAlwaysAWellFormedKey()
        {
            foreach (string s in new[] { "", " ", "<b>tag</b>", "改行\nあり", new string('x', 10000) })
            {
                Assert.True(TranslationKey.LooksLikeKey(TranslationStore.KeyFor(s)), s);
            }
        }

        /// <summary>
        /// <para>
        /// The key is of the exact string: leading spaces and case make a
        /// different key, and the same string always gives the same one.
        /// </para>
        /// <para>
        /// キーは文字列そのものから作る。
        /// 先頭の空白や大文字小文字が違えば別のキーになり、同じ文字列からは常に同じキーになる。
        /// </para>
        /// </summary>
        [Fact]
        public void KeyForDoesNotTrimOrFoldCase()
        {
            string key = TranslationStore.KeyFor("Hi");

            Assert.NotEqual(key, TranslationStore.KeyFor(" Hi"));
            Assert.NotEqual(key, TranslationStore.KeyFor("hi"));
            Assert.Equal(key, TranslationStore.KeyFor("Hi"));
        }

        /// <summary>
        /// <para>A line ID is readable as it is, so it is shown unchanged.</para>
        /// <para>行 ID はそのままで読めるので、変えずに表示する。</para>
        /// </summary>
        [Fact]
        public void DescribeKeyShowsALineIdAsItIs()
        {
            Assert.Equal("line:6046bedf", TranslationStore.DescribeKey("line:6046bedf"));
        }

        /// <summary>
        /// <para>A key with no known English is marked with <c>#</c> so it is obviously not text.</para>
        /// <para>
        /// 英語の原文が分からないキーは、文字列ではないと分かるよう <c>#</c> を付ける。
        /// </para>
        /// </summary>
        [Fact]
        public void DescribeKeyMarksAKeyWithoutKnownEnglish()
        {
            Assert.Equal("#0123456789abcdef", TranslationStore.DescribeKey("0123456789abcdef"));
        }

        /// <summary>
        /// <para>Before any locale is loaded, every lookup finds nothing and the tables are empty.</para>
        /// <para>言語を読み込む前は、どの検索でも何も見つからず、表も空。</para>
        /// </summary>
        [Fact]
        public void LookupsFindNothingWhenNothingIsLoaded()
        {
            Assert.False(TranslationStore.TryGetTranslation("Hello, world!", out _));
            Assert.False(TranslationStore.TryGetKeyTranslation("315f5bdb76d078c4", out _));
            Assert.False(TranslationStore.TryGetLineTranslation("line:6046bedf", out _));
            Assert.Equal(0, TranslationStore.EntryCount);
            Assert.Equal(0, TranslationStore.LineEntryCount);
            Assert.Empty(TranslationStore.Entries);
        }

        /// <summary>
        /// <para>A null key or line ID returns false with a null translation instead of throwing.</para>
        /// <para>キーや行 ID が null のときは、例外を投げずに false と null の訳を返す。</para>
        /// </summary>
        [Fact]
        public void NullKeysAreNotLookedUp()
        {
            Assert.False(TranslationStore.TryGetKeyTranslation(null, out string byKey));
            Assert.Null(byKey);
            Assert.False(TranslationStore.TryGetLineTranslation(null, out string byLine));
            Assert.Null(byLine);
        }

        /// <summary>
        /// <para>
        /// The verbose log reports a string the first time only, until
        /// <see cref="TranslationStore.ResetAppliedOnceTracking"/> resets the
        /// tracking.
        /// </para>
        /// <para>
        /// 詳細ログは文字列を初回だけ記録し、
        /// <see cref="TranslationStore.ResetAppliedOnceTracking"/> で記録状態をリセットするまでは、
        /// 同じ文字列を再び記録しない。
        /// </para>
        /// </summary>
        /// <remarks>
        /// <para>The Activity log's "Clear" button calls the reset.</para>
        /// <para>リセットはアクティビティログの「Clear」ボタンから呼ばれる。</para>
        /// </remarks>
        [Fact]
        public void FirstApplicationIsReportedOnceUntilReset()
        {
            string text = "first-application-" + Guid.NewGuid();

            Assert.True(TranslationStore.IsFirstApplication(text));
            Assert.False(TranslationStore.IsFirstApplication(text));

            TranslationStore.ResetAppliedOnceTracking();

            Assert.True(TranslationStore.IsFirstApplication(text));
        }
    }
}
