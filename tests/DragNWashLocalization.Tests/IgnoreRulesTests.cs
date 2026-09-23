using System;
using System.Linq;
using Xunit;

namespace DragNWashLocalization.Tests
{
    /// <summary>
    /// <para>
    /// Loads <see cref="IgnoreRules"/> once, for every test in
    /// <see cref="IgnoreRulesTests"/>, with an ignore.txt that exercises each
    /// branch.
    /// </para>
    /// <para>
    /// <see cref="IgnoreRulesTests"/> の全テストのために、
    /// <see cref="IgnoreRules"/> を 1 回だけ読み込む。
    /// ignore.txt には、各分岐を通すための行を入れてある。
    /// </para>
    /// </summary>
    /// <remarks>
    /// <para>
    /// <see cref="IgnoreRules"/> loads once per process, so the tests share
    /// this one load instead of loading their own.
    /// </para>
    /// <para>
    /// <see cref="IgnoreRules"/> はプロセスごとに 1 回しか読み込まないので、
    /// テストはそれぞれ読み込まずに、この 1 回の読み込みを共有する。
    /// </para>
    /// </remarks>
    public sealed class IgnoreRulesFixture : IDisposable
    {
        /// <summary>
        /// <para>A translator's own rule, written with spaces around it.</para>
        /// <para>翻訳者が追加する独自の規則（前後に空白を付けて書く）。</para>
        /// </summary>
        public const string CustomPattern = @"^Ver\. \d+$";

        /// <summary>
        /// <para>Not a valid regular expression: must be skipped with a warning.</para>
        /// <para>正規表現として不正なので、警告を出して読み飛ばされるはずの規則。</para>
        /// </summary>
        public const string InvalidPattern = @"([unclosed";

        /// <summary>
        /// <para>A rule that backtracks catastrophically on <see cref="SlowInput"/>.</para>
        /// <para><see cref="SlowInput"/> に対して壊滅的なバックトラックを起こす規則。</para>
        /// </summary>
        public const string SlowPattern = @"^(a+)+$";

        /// <summary>
        /// <para>
        /// A run of "a" that does not end the way <see cref="SlowPattern"/>
        /// wants: far beyond the 50 ms match timeout.
        /// </para>
        /// <para>
        /// 末尾が <see cref="SlowPattern"/> に合わない "a" の連続。
        /// 50 ms の照合タイムアウトを大きく超える。
        /// </para>
        /// </summary>
        public const string SlowInput = "aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa!";

        /// <summary>
        /// <para>The plugin directory the rules are loaded from, holding Translations/ignore.txt.</para>
        /// <para>規則を読み込むプラグインのディレクトリ。Translations/ignore.txt を置く。</para>
        /// </summary>
        private readonly TempDirectory _dir = new TempDirectory();

        /// <summary>
        /// <para>How many rules <see cref="IgnoreRules"/> held right after this load.</para>
        /// <para>この読み込みの直後に <see cref="IgnoreRules"/> が持っていた規則の数。</para>
        /// </summary>
        public int PatternsAfterFirstLoad { get; }

        /// <summary>
        /// <para>
        /// Writes ignore.txt with a comment, an empty line and a
        /// whitespace-only line (all skipped), then the three rules above, and
        /// loads it.
        /// </para>
        /// <para>
        /// ignore.txt に次の行を書き、読み込む。
        /// コメント行、空行、空白だけの行（いずれも読み飛ばされる）と、上の 3 つの規則。
        /// </para>
        /// </summary>
        public IgnoreRulesFixture()
        {
            _dir.Write("Translations/ignore.txt", string.Join("\n",
                "# a comment",
                "",
                "   ",
                "  " + CustomPattern + "  ",
                InvalidPattern,
                SlowPattern,
                ""));
            IgnoreRules.Load(_dir.Path);
            PatternsAfterFirstLoad = IgnoreRules.PatternCount;
        }

        /// <inheritdoc/>
        public void Dispose() => _dir.Dispose();
    }

    /// <summary>
    /// <para>
    /// Tests for <see cref="IgnoreRules"/>: which on-screen strings are never
    /// reported as untranslated (numbers, resolutions, clocks...), and how
    /// rules from ignore.txt are read and kept from freezing the game.
    /// </para>
    /// <para>
    /// <see cref="IgnoreRules"/> のテスト。
    /// 画面上の文字列のうち、未翻訳として報告しないもの（数値、解像度、時刻など）の判定と、
    /// ignore.txt の規則の読み込み、および規則がゲームを固めないための対策を確かめる。
    /// </para>
    /// </summary>
    public class IgnoreRulesTests : IClassFixture<IgnoreRulesFixture>
    {
        /// <summary>
        /// <para>The shared load, for the rule count taken right after it.</para>
        /// <para>共有の読み込み。その直後に数えた規則の数を参照するために持つ。</para>
        /// </summary>
        private readonly IgnoreRulesFixture _fixture;

        /// <summary>
        /// <para>Receives the shared load from xUnit.</para>
        /// <para>共有の読み込みを xUnit から受け取る。</para>
        /// </summary>
        /// <param name="fixture">
        /// <para>The load every test in this class shares.</para>
        /// <para>このクラスの全テストが共有する読み込み。</para>
        /// </param>
        public IgnoreRulesTests(IgnoreRulesFixture fixture)
        {
            _fixture = fixture;
        }

        /// <summary>
        /// <para>Empty or whitespace-only text has nothing to translate.</para>
        /// <para>空、または空白だけの文字列には翻訳するものがない。</para>
        /// </summary>
        /// <param name="text">
        /// <para>Text that must be ignored.</para>
        /// <para>無視されるべき文字列。</para>
        /// </param>
        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData("   ")]
        [InlineData("\t\n")]
        public void EmptyTextIsIgnored(string text)
        {
            Assert.True(IgnoreRules.IsIgnored(text));
        }

        /// <summary>
        /// <para>Numbers on their own, with a sign, separators or a percent sign.</para>
        /// <para>符号、区切り記号、パーセント記号付きを含む、数値だけの文字列。</para>
        /// </summary>
        /// <param name="text">
        /// <para>Text that must be ignored.</para>
        /// <para>無視されるべき文字列。</para>
        /// </param>
        [Theory]
        [InlineData("0")]
        [InlineData("-3")]
        [InlineData("+7")]
        [InlineData("0.001")]
        [InlineData("1,250")]
        [InlineData("45%")]
        [InlineData("45 %")]
        [InlineData(".5")]
        [InlineData("  12  ")]
        public void BareNumbersAreIgnored(string text)
        {
            Assert.True(IgnoreRules.IsIgnored(text));
        }

        /// <summary>
        /// <para>
        /// The other built-in rules: resolutions, refresh rates, framerates,
        /// build stamps, dates, clocks and timers.
        /// </para>
        /// <para>
        /// その他の組み込み規則。
        /// 解像度、リフレッシュレート、フレームレート、ビルド日時の表記、日付、時計とタイマー。
        /// </para>
        /// </summary>
        /// <param name="text">
        /// <para>Text that must be ignored.</para>
        /// <para>無視されるべき文字列。</para>
        /// </param>
        [Theory]
        [InlineData("1920x1080")]
        [InlineData("1920 × 1080")]
        [InlineData("2560x1440 @ 144Hz")]
        [InlineData("1280 x 720 @ 59.94 Hz")]
        [InlineData("60Hz")]
        [InlineData("144 Hz")]
        [InlineData("30 FPS")]
        [InlineData("9/9/2026_ee944596")]
        [InlineData("12/31/26")]
        [InlineData("1:23")]
        [InlineData("00:05:12")]
        public void ResolutionsRatesStampsAndClocksAreIgnored(string text)
        {
            Assert.True(IgnoreRules.IsIgnored(text));
        }

        /// <summary>
        /// <para>
        /// Text that only contains a number, or looks close to a rule, is still
        /// game text a translator must see.
        /// </para>
        /// <para>
        /// 数字を含むだけの文字列や規則に似ているだけの文字列は、
        /// 翻訳者が見るべきゲームの文字列として扱う。
        /// </para>
        /// </summary>
        /// <remarks>
        /// <para>"fps" in lowercase is left to the translator on purpose.</para>
        /// <para>小文字の "fps" は意図的に対象外にしている。</para>
        /// </remarks>
        /// <param name="text">
        /// <para>Text that must not be ignored.</para>
        /// <para>無視されてはいけない文字列。</para>
        /// </param>
        [Theory]
        [InlineData("Start")]
        [InlineData("Level 3")]
        [InlineData("3 apples")]
        [InlineData("%")]
        [InlineData(".,")]
        [InlineData("1080p")]
        [InlineData("60 fps")]
        [InlineData("123:4")]
        public void OrdinaryTextIsNotIgnored(string text)
        {
            Assert.False(IgnoreRules.IsIgnored(text));
        }

        /// <summary>
        /// <para>A long run of digits and separators fails fast, well inside the match timeout.</para>
        /// <para>数字と区切り記号の長い連続も、照合タイムアウトよりずっと早く不一致になる。</para>
        /// </summary>
        /// <remarks>
        /// <para>The number rule once backtracked quadratically on text like this.</para>
        /// <para>
        /// 数値の規則は以前、このような文字列で 2 乗オーダーのバックトラックを起こしていた。
        /// </para>
        /// </remarks>
        [Fact]
        public void ALongRunOfDigitsAndSeparatorsDoesNotBacktrack()
        {
            string text = string.Concat(Enumerable.Repeat("1,2.", 5000)) + "x";

            Assert.False(IgnoreRules.IsIgnored(text));
        }

        /// <summary>
        /// <para>A rule from ignore.txt applies, with the spaces around it trimmed.</para>
        /// <para>ignore.txt の規則は前後の空白を取り除いたうえで適用される。</para>
        /// </summary>
        [Fact]
        public void PatternsFromIgnoreTxtAreAddedTrimmed()
        {
            Assert.True(IgnoreRules.IsIgnored("Ver. 12"));
            Assert.False(IgnoreRules.IsIgnored("Version 12"));
        }

        /// <summary>
        /// <para>An invalid rule is reported, naming the rule and its file.</para>
        /// <para>不正な規則は、その規則とファイル名を示して報告される。</para>
        /// </summary>
        /// <remarks>
        /// <para>
        /// Only the message is checked, not its <see cref="LogKind"/>: main logs
        /// it as Info and dev as Warning, and the test must pass on both.
        /// </para>
        /// <para>
        /// 確かめるのはメッセージだけで、<see cref="LogKind"/> は見ない。
        /// main は Info、dev は Warning で記録しており、どちらでも通る必要があるため。
        /// </para>
        /// </remarks>
        [Fact]
        public void AnInvalidPatternIsSkippedAndReported()
        {
            Assert.Contains(Plugin.Logged, l => l.Value.Contains(IgnoreRulesFixture.InvalidPattern) && l.Value.Contains("ignore.txt"));
        }

        /// <summary>
        /// <para>
        /// Five built-in rules, then the custom and the slow one; the comment,
        /// the blank lines and the invalid rule add nothing.
        /// </para>
        /// <para>
        /// 組み込みの 5 規則に、独自の規則と遅い規則が加わる。
        /// コメント、空行、不正な規則は何も追加しない。
        /// </para>
        /// </summary>
        [Fact]
        public void OnlyTheValidPatternsAreKept()
        {
            Assert.Equal(7, _fixture.PatternsAfterFirstLoad);
        }

        /// <summary>
        /// <para>
        /// A rule that times out counts as no match instead of freezing the
        /// game, and it is reported only once however often it times out.
        /// </para>
        /// <para>
        /// タイムアウトした規則はゲームを止めずに「一致しない」として扱う。
        /// 何度タイムアウトしても、報告は 1 回だけ。
        /// </para>
        /// </summary>
        /// <remarks>
        /// <para>
        /// As in <see cref="AnInvalidPatternIsSkippedAndReported"/>, the
        /// <see cref="LogKind"/> is not checked.
        /// </para>
        /// <para>
        /// <see cref="AnInvalidPatternIsSkippedAndReported"/> と同じく、<see cref="LogKind"/> は見ない。
        /// </para>
        /// </remarks>
        [Fact]
        public void ATimedOutPatternCountsAsNoMatchAndIsReportedOnce()
        {
            Assert.False(IgnoreRules.IsIgnored(IgnoreRulesFixture.SlowInput));
            Assert.False(IgnoreRules.IsIgnored(IgnoreRulesFixture.SlowInput + "a"));

            Assert.Single(Plugin.Logged, l => l.Value.Contains("timed out") && l.Value.Contains(IgnoreRulesFixture.SlowPattern));
        }

        /// <summary>
        /// <para>A second <see cref="IgnoreRules.Load(string)"/> reads nothing new.</para>
        /// <para>2 回目の <see cref="IgnoreRules.Load(string)"/> では何も読み込まない。</para>
        /// </summary>
        /// <remarks>
        /// <para>The rules do not depend on the locale, so a language switch keeps them.</para>
        /// <para>規則は言語に依存しないので、言語を切り替えてもそのまま使う。</para>
        /// </remarks>
        [Fact]
        public void LoadingAgainChangesNothing()
        {
            using (var other = new TempDirectory())
            {
                other.Write("Translations/ignore.txt", "^anything$\n");
                IgnoreRules.Load(other.Path);
            }

            Assert.Equal(_fixture.PatternsAfterFirstLoad, IgnoreRules.PatternCount);
            Assert.False(IgnoreRules.IsIgnored("anything"));
        }

        /// <summary>
        /// <para>
        /// Whole strings the mod shows itself (language names) are ignored
        /// exactly, after trimming; null and empty strings are not added.
        /// </para>
        /// <para>
        /// Mod 自身が表示する文字列（言語名など）は、前後の空白を除いて完全一致で無視する。
        /// null と空文字は追加しない。
        /// </para>
        /// </summary>
        [Fact]
        public void ExactStringsAreIgnoredAfterTrimming()
        {
            IgnoreRules.AddExact("  日本語  ");
            IgnoreRules.AddExact(null);
            IgnoreRules.AddExact(string.Empty);

            Assert.True(IgnoreRules.IsIgnored("日本語"));
            Assert.True(IgnoreRules.IsIgnored(" 日本語\n"));
            Assert.False(IgnoreRules.IsIgnored("日本"));
        }
    }
}
