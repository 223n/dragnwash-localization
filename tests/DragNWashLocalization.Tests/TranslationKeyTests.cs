using Xunit;

namespace DragNWashLocalization.Tests
{
    /// <summary>
    /// <para>
    /// Tests for <see cref="TranslationKey"/>: what counts as a hash key and
    /// what counts as a Yarn line ID in a row's key column.
    /// </para>
    /// <para>
    /// <see cref="TranslationKey"/> のテスト。
    /// 行の key 列の値を、ハッシュキーまたは Yarn の行 ID として判定する規則を確かめる。
    /// </para>
    /// </summary>
    /// <remarks>
    /// <para>
    /// A value that is neither is reported as malformed, so these rules decide
    /// which rows are loaded at all.
    /// </para>
    /// <para>
    /// どちらでもない値は不正として報告されるので、この規則がどの行を読み込むかを決める。
    /// </para>
    /// </remarks>
    public class TranslationKeyTests
    {
        /// <summary>
        /// <para>Exactly 16 lowercase hex digits is a key.</para>
        /// <para>小文字の 16 進数ちょうど 16 桁はキーとして扱う。</para>
        /// </summary>
        /// <param name="value">
        /// <para>A value that must be accepted.</para>
        /// <para>受け付けられるべき値。</para>
        /// </param>
        [Theory]
        [InlineData("0123456789abcdef")]
        [InlineData("ffffffffffffffff")]
        [InlineData("e3b0c44298fc1c14")]
        public void SixteenLowercaseHexDigitsAreAKey(string value)
        {
            Assert.True(TranslationKey.LooksLikeKey(value));
        }

        /// <summary>
        /// <para>Wrong length, uppercase, non-hex, whitespace or a line ID is not a key.</para>
        /// <para>長さ違い、大文字、16 進数以外の文字、空白、行 ID はキーではない。</para>
        /// </summary>
        /// <remarks>
        /// <para>Uppercase is rejected here; callers lowercase first.</para>
        /// <para>大文字はここでは弾く（呼び出し側が先に小文字にする）。</para>
        /// </remarks>
        /// <param name="value">
        /// <para>A value that must be rejected.</para>
        /// <para>弾かれるべき値。</para>
        /// </param>
        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData("0123456789abcde")]
        [InlineData("0123456789abcdef0")]
        [InlineData("0123456789ABCDEF")]
        [InlineData("0123456789abcdeg")]
        [InlineData(" 123456789abcdef")]
        [InlineData("line:6046bedf")]
        public void AnythingElseIsNotAKey(string value)
        {
            Assert.False(TranslationKey.LooksLikeKey(value));
        }

        /// <summary>
        /// <para><c>line:</c> followed by letters, digits, <c>_</c>, <c>-</c> or <c>.</c> is a line ID.</para>
        /// <para>
        /// <c>line:</c> の後に英数字、<c>_</c>、<c>-</c>、<c>.</c> が続く値は行 ID として扱う。
        /// </para>
        /// </summary>
        /// <param name="value">
        /// <para>A value that must be accepted.</para>
        /// <para>受け付けられるべき値。</para>
        /// </param>
        [Theory]
        [InlineData("line:6046bedf")]
        [InlineData("line:a")]
        [InlineData("line:Node_1-2.3")]
        [InlineData("line:ABCdef")]
        public void YarnLineIdsAreRecognised(string value)
        {
            Assert.True(TranslationKey.LooksLikeLineId(value));
        }

        /// <summary>
        /// <para>
        /// The prefix is case-sensitive and must come first, the ID must not be
        /// empty, and spaces, punctuation or non-ASCII letters are not allowed.
        /// </para>
        /// <para>
        /// 接頭辞は大文字小文字を区別し、先頭に置く必要がある。
        /// ID は空にできず、空白、記号、ASCII 以外の文字は使えない。
        /// </para>
        /// </summary>
        /// <param name="value">
        /// <para>A value that must be rejected.</para>
        /// <para>弾かれるべき値。</para>
        /// </param>
        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData("line:")]
        [InlineData("Line:6046bedf")]
        [InlineData(" line:6046bedf")]
        [InlineData("line:6046 bedf")]
        [InlineData("line:6046bedf,")]
        [InlineData("line:行")]
        [InlineData("0123456789abcdef")]
        public void OtherTextIsNotALineId(string value)
        {
            Assert.False(TranslationKey.LooksLikeLineId(value));
        }

        /// <summary>
        /// <para>64 characters, prefix included, is the longest line ID accepted.</para>
        /// <para>接頭辞を含めて 64 文字が、受け付ける行 ID の最大長。</para>
        /// </summary>
        [Fact]
        public void LineIdsAreCappedAtSixtyFourCharacters()
        {
            string longest = TranslationKey.LinePrefix + new string('a', 64 - TranslationKey.LinePrefix.Length);

            Assert.True(TranslationKey.LooksLikeLineId(longest));
            Assert.False(TranslationKey.LooksLikeLineId(longest + "a"));
        }
    }
}
