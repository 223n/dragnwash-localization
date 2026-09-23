using System.Collections.Generic;

namespace DragNWashLocalization
{
    /// <summary>
    /// <para>Stands in for the plugin's LogKind in Plugin.ActivityLog.cs.</para>
    /// <para>Plugin.ActivityLog.cs にあるプラグインの LogKind の代わり。</para>
    /// </summary>
    /// <remarks>
    /// <para>
    /// The original lives in a file that needs Unity. Keep the members in
    /// step with it.
    /// </para>
    /// <para>
    /// 元のファイルは Unity を必要とするため取り込めない。
    /// メンバーは元と一致させておくこと。
    /// </para>
    /// </remarks>
    internal enum LogKind
    {
        /// <summary>
        /// <para>Startup notices and details.</para>
        /// <para>起動時の通知と詳細。</para>
        /// </summary>
        Info,

        /// <summary>
        /// <para>What came of something the player did.</para>
        /// <para>プレイヤーの操作の結果。</para>
        /// </summary>
        Result,

        /// <summary>
        /// <para>Worth a look, though nothing failed.</para>
        /// <para>失敗ではないが、確認したほうがよいもの。</para>
        /// </summary>
        Warning,

        /// <summary>
        /// <para>Something failed.</para>
        /// <para>何かが失敗した。</para>
        /// </summary>
        Error,

        /// <summary>
        /// <para>A replaced text, from the verbose text log.</para>
        /// <para>詳細テキストログの、置き換えた文字列。</para>
        /// </summary>
        Text,
    }

    /// <summary>
    /// <para>Stands in for the BepInEx plugin class.</para>
    /// <para>BepInEx のプラグインクラスの代わり。</para>
    /// </summary>
    /// <remarks>
    /// <para>
    /// The code under test only calls <see cref="Log(string, LogKind)"/>, and
    /// the tests read back what it wrote through <see cref="Logged"/>.
    /// </para>
    /// <para>
    /// テスト対象のコードが呼ぶのは <see cref="Log(string, LogKind)"/> だけで、
    /// テストは書き込まれた内容を <see cref="Logged"/> で読み戻して確かめる。
    /// </para>
    /// </remarks>
    internal static class Plugin
    {
        /// <summary>
        /// <para>Every line logged so far, oldest first, as (kind, text).</para>
        /// <para>これまでに記録された全行。古い順で、(種類, 文字列) の組。</para>
        /// </summary>
        /// <remarks>
        /// <para>Locked on every access: xUnit runs test classes in parallel.</para>
        /// <para>xUnit はテストクラスを並列に実行するので、読み書きのたびにロックする。</para>
        /// </remarks>
        private static readonly List<KeyValuePair<LogKind, string>> Lines = new List<KeyValuePair<LogKind, string>>();

        /// <summary>
        /// <para>Records a log line instead of sending it to BepInEx.</para>
        /// <para>ログの行を BepInEx に送る代わりに記録する。</para>
        /// </summary>
        /// <param name="message">
        /// <para>The line's text.</para>
        /// <para>行の文字列。</para>
        /// </param>
        /// <param name="kind">
        /// <para>What the line is.</para>
        /// <para>行の種類。</para>
        /// </param>
        internal static void Log(string message, LogKind kind = LogKind.Info)
        {
            lock (Lines)
            {
                Lines.Add(new KeyValuePair<LogKind, string>(kind, message));
            }
        }

        /// <summary>
        /// <para>A copy of every line logged so far in this test run.</para>
        /// <para>このテスト実行でこれまでに記録された全行のコピー。</para>
        /// </summary>
        internal static List<KeyValuePair<LogKind, string>> Logged
        {
            get { lock (Lines) { return new List<KeyValuePair<LogKind, string>>(Lines); } }
        }
    }
}
