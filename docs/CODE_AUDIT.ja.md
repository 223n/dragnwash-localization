# コード監査レポート

対象コミット: `003ed1e`（v1.1.2）

このレポートは、リポジトリ全体を静的に監査した結果を問題点ごとに整理したものです。
各項目は「現象」「根拠」「再現条件」「修正案」の順に記載します。
コードは変更していません。修正の採否はメンテナーの判断に委ねます。

## 目次

- [サマリ](#サマリ)
- [監査の範囲と方法](#監査の範囲と方法)
- [重大度: 高](#重大度-高)
- [重大度: 中](#重大度-中)
- [重大度: 低](#重大度-低)
- [調査したが問題なしと判断した項目](#調査したが問題なしと判断した項目)
- [データ整合性の測定結果](#データ整合性の測定結果)
- [未検証の前提](#未検証の前提)

## サマリ

| ID | 重大度 | 対象 | 概要 |
| --- | --- | --- | --- |
| H1 | 高 | `SpeakerLookup.cs` | 話者表を毎行作り直し、「Hash for commit」でゲームが固まる |
| H2 | 高 | `.github/workflows/comment-on-check.yml` | fork の PR が作ったアーティファクトを特権ワークフローが信用している |
| H3 | 高 | `tools/hash-strings.ps1` | ゲーム内ボタンと挙動が違い、公開済みの訳が消える |
| H4 | 高 | `WorkingCopy.cs` | 「Export working copy」の再実行で未ハッシュの訳が消える |
| M1 | 中 | `LayoutChecker.cs` | ロケール依存の数値書式が出力 CSV を壊す |
| M2 | 中 | `IgnoreRules.cs` | 正規表現にタイムアウトがなく、`ignore.txt` 次第で停止する |
| M3 | 中 | `.gitattributes` ほか | CSV の改行コードが環境で変わる |
| M4 | 中 | `TranslationStore.cs` / `WorkingCopy.cs` | 翻訳ファイルの書き込みが非アトミック |
| M5 | 中 | `check-translations.py` / `hash-strings.ps1` | コメント行の除去方法が C# と食い違う |
| M6 | 中 | `ScriptOrder.cs` | 書き出し時の検索が O(n×m) |
| M7 | 中 | `DragNWashLocalization.csproj` | 参照が重複し、手順コメントと一致しない |
| L1 | 低 | 各書き出し処理 | `_discovered` 配下だけ BOM 付き UTF-8 |
| L2 | 低 | 4 ファイル | カルチャ依存の `StartsWith` |
| L3 | 低 | `tools/hash-strings.ps1` | 空行を「malformed dropped」と数える |
| L4 | 低 | `ScriptOrder.cs` | `level_flow.csv` の探索先がツール間で食い違う |
| L5 | 低 | `Plugin.cs` | `Awake` の失敗が毎フレームの例外になる |

高が 4 件、中が 7 件、低が 5 件です。
このうち H3・H4・M4 はいずれも翻訳者の作業内容が失われる経路で、実害が最も大きいと考えます。

## 監査の範囲と方法

### 範囲

| 領域 | 規模 | 主な確認対象 |
| --- | --- | --- |
| C# プラグイン | 20 ファイル / 約 4,400 行 | Harmony パッチ、CSV 入出力、IMGUI、ホットリロード |
| macOS インストーラー | 946 行（bash + JXA） | ダウンロード検証、削除処理、Steam 設定の書き換え |
| Python ツール | 3 ファイル / 約 310 行 | CI の検証ロジック、終了コード |
| PowerShell ツール | 2 ファイル / 約 410 行 | ハッシュ化、パッケージング |
| GitHub Actions | 2 ワークフロー | 権限、信頼境界 |
| データ | 13 言語 × `strings.csv` ほか | 文字コード、列数、タグ整合性 |
| ドキュメント | 14 ファイル | コードとの整合、日英の対応 |

### 方法

- ソースを直接読み、指摘ごとにファイルと行番号を確認しました。
- Python ツールは実際に実行し、`ruff` を通しました（いずれも警告なし、終了コード 0）。
- データ整合性は目視ではなく、使い捨ての Python スクリプトで測定しました。
- 各指摘は「反証を試みる」「具体的な失敗ケースを構築する」の 2 つの観点から個別に検証しました。

### 実行できなかった検証

- `dotnet` が無いため、C# のコンパイルと動的解析は行っていません。
- `pwsh` が無いため、PowerShell は読解のみです。
- `shellcheck` が無いため、シェルは読解と `bash -n` のみです。
- ゲーム本体が無いため、実機での再現は行っていません。

## 重大度: 高

### H1. 話者表を毎行作り直し、「Hash for commit」でゲームが固まる

**対象**: `src/DragNWashLocalization/SpeakerLookup.cs:28`

**現象**

`ForKey` は、話者表が空のときに毎回 `Build()` を呼び直します。

```csharp
public static string ForKey(string key)
{
    if (_byKey == null || _byKey.Count == 0)
    {
        Build();
    }
    return _byKey.TryGetValue(key, out string s) ? s : string.Empty;
}
```

判定が「未構築か」ではなく「空か」であるため、構築しても空のままになる状況では、
呼ばれるたびに再構築が走ります。

**根拠**

- `Build()` は `DialogueDumper.EnumerateOrderedLines()` を列挙します（`SpeakerLookup.cs:16`）。
- その実体は `Resources.FindObjectsOfTypeAll<YarnProject>()` で、ロード済みオブジェクトを全走査します
  （`DialogueDumper.cs:255`）。
- `TranslationStore.HashFileInPlace` は、`speaker` 列が空の行ごとに `SpeakerLookup` を呼びます
  （`TranslationStore.cs:383-386`）。

```csharp
row.TryGetValue("speaker", out string speaker);
if (string.IsNullOrEmpty(speaker))
{
    speaker = source != null ? SpeakerLookup.For(source) : SpeakerLookup.ForKey(key);
}
```

**再現条件**

`YarnProject` が読み込まれていない状態、つまりタイトル画面や Options 画面で
F1 → Translation → 「Hash for commit」を押した場合です。
このとき話者表は空のまま構築されるため、CSV の行数だけ全オブジェクト走査が繰り返されます。
公開ファイルは 1,700 行前後あるので、走査は 1,700 回に達します。
`speaker` 列を持たない作業用コピーでは、ほぼ全行が該当します。

**修正案**

構築済みかどうかを専用のフラグで持ちます。

```csharp
private static Dictionary<string, string> _byKey;
private static bool _built;

private static void Build()
{
    _built = true;
    _byKey = new Dictionary<string, string>(StringComparer.Ordinal);
    // ...
}

public static string ForKey(string key)
{
    if (!_built)
    {
        Build();
    }
    return _byKey.TryGetValue(key, out string s) ? s : string.Empty;
}
```

ロケール切り替え時に作り直したい場合は、`TranslationStore.Load` から `_built = false` を設定する
リセット用のメソッドを呼びます。

### H2. fork の PR が作ったアーティファクトを特権ワークフローが信用している

**対象**: `.github/workflows/comment-on-check.yml:30`

**現象**

`comment-on-check.yml` は `workflow_run` で起動し、`pull-requests: write` を持ちます。
そのうえで、コメント先の PR 番号をアーティファクト内のファイルから読み取っています。

```yaml
permissions:
  pull-requests: write
  actions: read
```

```javascript
const pr = Number(fs.readFileSync('pr-number.txt', 'utf8').trim());
const status = fs.readFileSync('status.txt', 'utf8').trim();
const report = fs.readFileSync('report.txt', 'utf8').trim();
```

このアーティファクトを作るのは `check-translations.yml` であり、こちらは `pull_request` で起動します。

**根拠**

`pull_request` イベントでは、GitHub は PR の head 側にあるワークフロー定義を実行します。
これは fork からの PR でもトークンが読み取り専用になる理由そのものです。
したがって fork の PR は `check-translations.yml` 自体を書き換えて、
`pr-number.txt`・`status.txt`・`report.txt` を任意の内容にできます。

その結果、特権側のワークフローは次の操作を攻撃者の指定どおりに実行します。

- 同リポジトリの**任意の** Issue / PR に `github-actions[bot]` 名義でコメントを投稿する。
- 既存の翻訳チェックコメントを `updateComment` で書き換え、失敗しているのに
  「The translation check passed.」と表示させる。

加えて、更新対象のコメントを投稿者で絞っていません（`comment-on-check.yml:71`）。

```javascript
const mine = comments.find(c => c.body && c.body.startsWith(marker));
```

人間が `<!-- translation-check -->` で始まるコメントを書くと、bot がそれを上書きします。

**再現条件**

公開リポジトリの既定では、初回貢献者のワークフロー実行に承認が要ります。
したがって前提は「一度でも PR がマージされた貢献者」または
「メンテナーが実行を承認した PR」です。
翻訳 PR は承認されるのが通常運用なので、この前提は満たされやすいと考えます。

**修正案**

PR 番号をアーティファクトではなく `workflow_run` のペイロードから導出します。
`head_sha` は GitHub が設定する値であり、PR 側からは改竄できません。

```javascript
const run = context.payload.workflow_run;
const { owner, repo } = context.repo;
const open = await github.paginate(github.rest.pulls.list, { owner, repo, state: 'open', per_page: 100 });
const match = open.find(p => p.head.sha === run.head_sha);
if (!match) return;            // 対応する PR が無ければ何もしない
const pr = match.number;
```

あわせて次の 4 点を推奨します。

1. `check-translations.yml` の `pr-number.txt` を廃止する（不要になるため）。
2. `report` はコードフェンスを破れないようサニタイズし、長さを制限する。
   例: `const safe = report.replace(/`/g, 'ˋ').slice(0, 20000);`
3. 更新対象を bot 自身のコメントに限定する。
   例: `comments.find(c => c.user?.login === 'github-actions[bot]' && c.body?.startsWith(marker))`
4. `check-translations.yml` にも最小権限を明示する（`permissions: contents: read`）。

### H3. `tools/hash-strings.ps1` がゲーム内ボタンと挙動が違い、公開済みの訳が消える

**対象**: `tools/hash-strings.ps1:91`

**現象**

`hash-strings.ps1` は、作業用コピーがあればそれを入力に選び、公開ファイルを丸ごと作り直します。

```powershell
$work = Join-Path $root ('Translations/_discovered/' + $_.Name + '.working.csv')
$in = if (Test-Path $work) { $work } else { $out }
```

しかし、作業用コピーに含まれない公開済みの行を引き継ぐ処理がありません。

**根拠**

ゲーム内の「Hash for commit」に相当する `TranslationStore.HashFileInPlace` には、
その処理が明示的に存在します（`TranslationStore.cs:391-428`）。

```csharp
// A working copy only holds the rows it was written with. Rows the
// published file gained since (a pack update, keys re-made after a
// game update) would otherwise be lost, so keep every published row
// the working copy does not have. The working copy wins where both do.
```

一方 `hash-strings.ps1` の先頭コメントは、両者が同じであると述べています（`tools/hash-strings.ps1:22`）。

```text
Identical to the in-game "Hash for commit" button.
```

さらに `README.md:308` には、この種の消失を v0.6.2 で修正したと書かれています。
修正されたのはゲーム内ボタンだけで、PowerShell ツールには反映されていません。

**再現条件**

1. 翻訳者が作業用コピーを書き出す。
2. その後、公開ファイルに新しい行が追加される（パックの更新、ゲーム更新後のキー再生成など）。
3. 古い作業用コピーを残したまま `pwsh tools/hash-strings.ps1` を実行する。

手順 2 で増えた行が、公開ファイルから消えます。
ゲーム内ボタンでは消えません。

**修正案**

公開ファイルの行をマージする処理を移植します。

```powershell
# 作業用コピーを入力にした場合だけ、公開ファイルの行を引き継ぐ
if ($t.Input -ne $t.Output -and (Test-Path $t.Output)) {
  foreach ($r in Read-Csv $t.Output) {
    $k = ([string]$r.key).Trim().ToLowerInvariant()
    if ($k -eq '' -or $rows.ContainsKey($k)) { continue }
    $tr = [string]$r.translation
    if ($tr -eq '') { continue }
    if ($k -cmatch $lineIdPattern) {
      if (-not $lineRows.Contains($k)) { $lineRows[$k] = $tr }
      continue
    }
    $rows[$k] = @{ Speaker = [string]$r.speaker; Translation = $tr }
    $inputOrder.Add($k)
  }
}
```

移植しない場合は、少なくとも `:22` の「Identical」という記述を撤回し、
差異と危険性を明記してください。

### H4. 「Export working copy」の再実行で未ハッシュの訳が消える

**対象**: `src/DragNWashLocalization/WorkingCopy.cs:51`

**現象**

`WorkingCopy.Export` は、既存の作業用コピーを一切読まずに上書きします。
訳文の供給元は公開ファイルだけです。

```csharp
bool fresh = !File.Exists(published);
// ...
foreach (var row in fresh ? new List<Dictionary<string, string>>() : CsvReader.ReadRows(published))
```

**根拠**

`Export` の内部で `PathFor`（作業用コピーのパス）が現れるのは 1 箇所だけで、
書き込み先としての用途のみです（`WorkingCopy.cs:143`、`WorkingCopy.cs:146`）。

```csharp
string path = PathFor(pluginDirectory, locale);
Directory.CreateDirectory(Path.GetDirectoryName(path));
ScriptOrder.Data order = ScriptOrder.Load(pluginDirectory);
using (var writer = new StreamWriter(path, append: false, new UTF8Encoding(false)))
```

`_discovered/strings.csv` も読みますが、これは原文（`source_en`）を集めるためだけです
（`WorkingCopy.cs:117-132`）。

一方 `TranslationStore.Load` は公開ファイルと作業用コピーの両方を読み、作業用コピーを優先します
（`TranslationStore.cs:178-179`）。
つまり編集内容はホットリロードで画面に反映されますが、
ディスク上は作業用コピーにしか存在せず、「Hash for commit」を通すまでどこにも保存されません。

UI 側は確認を挟みません（`Plugin.ImGui.cs:332`）。

```csharp
if (GUI.Button(new Rect(12, y, innerWidth - 12, RowHeight), _pendingWorkingCopy ? "Export queued..." : "Export working copy (English beside each line)", S.Button))
```

**再現条件**

1. 作業用コピーを書き出し、いくつかの行を訳す。
2. 「Hash for commit」を押さずに、もう一度「Export working copy」を押す。
   （例: ゲームが新たに発見した行を取り込みたい、という自然な動機で起こります。）

手順 1 で入力した訳がすべて失われます。バックアップも確認もありません。

**修正案**

既存の作業用コピーを読み、その `translation` 列を公開ファイルより優先してマージします。
H3 と同じ「既にある訳は捨てない」という方針です。

```csharp
// 公開ファイルを読んだ後、既存の作業用コピーで上書きする
string existing = PathFor(pluginDirectory, locale);
if (File.Exists(existing))
{
    foreach (var row in CsvReader.ReadRows(existing))
    {
        row.TryGetValue("key", out string k);
        row.TryGetValue("translation", out string tr);
        k = k?.Trim();
        if (string.IsNullOrEmpty(k) || string.IsNullOrEmpty(tr)) continue;
        if (TranslationKey.LooksLikeLineId(k)) { lineTranslations[k] = tr; continue; }
        k = k.ToLowerInvariant();
        if (!translations.ContainsKey(k)) fileOrder.Add(k);
        translations[k] = tr;
    }
}
```

あわせて、上書き前に `<locale>.working.csv.bak` を残すことを推奨します。

## 重大度: 中

### M1. ロケール依存の数値書式が出力 CSV を壊す

**対象**: `src/DragNWashLocalization/LayoutChecker.cs:143-145`

**現象**

レイアウト検査の結果を CSV に書く際、数値をカレントカルチャで文字列化しています。

```csharp
rows.Add(string.Concat(
    CsvReader.Escape(source), ",",
    CsvReader.Escape(translation), ",",
    axis, ",",
    required.ToString("0.#"), ",",
    available.ToString("0.#"), ",",
    (required / available).ToString("0.##"), ",",
    CsvReader.Escape(path)));
```

**根拠**

小数点をカンマで表記するロケールでは、`1.5` が `1,5` になります。
この値は `CsvReader.Escape` を通っていないため引用符が付かず、CSV の列が 1 つ増えます。
該当するロケールは de、fr、es、pt-BR、ru、pl など、本 MOD が対象とする言語そのものです。

同じ理由で、並べ替えに使う `RatioOf` も壊れます（`LayoutChecker.cs:210`）。

```csharp
if (double.TryParse(parts[i], out value))
```

`double.TryParse` もカレントカルチャで解釈するため、
「悪い順に並べる」という `LayoutChecker.cs:162` の意図が働かなくなります。

**再現条件**

OS の地域設定が上記いずれかの言語である PC で、F1 → Translation → レイアウト検査を実行した場合です。

**修正案**

書式化と解釈の双方で不変カルチャを明示します。

```csharp
using System.Globalization;

required.ToString("0.#", CultureInfo.InvariantCulture), ",",
available.ToString("0.#", CultureInfo.InvariantCulture), ",",
(required / available).ToString("0.##", CultureInfo.InvariantCulture), ",",
```

```csharp
if (double.TryParse(parts[i], NumberStyles.Float, CultureInfo.InvariantCulture, out value))
```

### M2. 正規表現にタイムアウトがなく、`ignore.txt` 次第で停止する

**対象**: `src/DragNWashLocalization/IgnoreRules.cs:93`

**現象**

`Translations/ignore.txt` から読み込んだパターンを、タイムアウトなしでコンパイルしています。

```csharp
Patterns.Add(new Regex(pattern, RegexOptions.CultureInvariant));
```

**根拠**

照合側には `try/catch` がありますが（`IgnoreRules.cs:121-134`）、
破滅的バックトラックは例外を投げません。単に返ってこなくなります。

```csharp
try
{
    if (Patterns[i].IsMatch(trimmed))
    {
        return true;
    }
}
catch (Exception)
{
    // A pathological pattern must not break text rendering.
}
```

`IsIgnored` は画面に出る文字列ごとに Unity のメインスレッドで呼ばれるため、
1 つの病的なパターンでゲームが応答しなくなります。

**再現条件**

`ignore.txt` に `^(a+)+$` のようなパターンを書き、`aaaaaaaaaaaaaaaaaaaaaaaaaX` のような
文字列が画面に出た場合です。
`ignore.txt` は MOD に同梱されて配布されるため、既定では安全です。
問題になるのは、翻訳者が自分で編集したときです。

**修正案**

コンストラクタにタイムアウトを渡します。
これで既存の `catch` が機能し、コメントの意図どおりの動作になります。

```csharp
private static readonly TimeSpan MatchTimeout = TimeSpan.FromMilliseconds(50);

private static void TryAdd(string pattern, string origin)
{
    try
    {
        Patterns.Add(new Regex(pattern, RegexOptions.CultureInvariant, MatchTimeout));
    }
    catch (ArgumentException ex)
    {
        Plugin.Log($"Skipping invalid ignore pattern from {origin}: {pattern} ({ex.Message})");
    }
}
```

### M3. CSV の改行コードが環境で変わる

**対象**: `.gitattributes:1`

**現象**

`.gitattributes` の内容は 1 行だけで、CSV に対する規則がありません。

```text
*.sh text eol=lf
```

一方、CSV を書き出す 2 つの経路はいずれも環境依存の改行を使います。

- `TranslationStore.cs:450-503` の `writer.WriteLine` は Windows で CRLF になります。
- `tools/hash-strings.ps1` の `StringBuilder.AppendLine` も Windows で CRLF になります
  （`tools/hash-strings.ps1:137` ほか、`:188` で書き出し）。

**根拠**

リポジトリ内の CSV は現在すべて LF です（確認済み）。
規則が無いため、結果は貢献者の `core.autocrlf` 設定に依存します。
`core.autocrlf` が `false` の Windows 環境でハッシュ化を実行すると、
全行が変更された差分になります。

引用フィールド内の改行が CRLF 化された場合、C# の読み取り側は引用符の内側の文字を
そのまま保持するため（`CsvReader.cs:68`）、訳文に `\r` が混入します。
現時点では引用フィールド内に改行を含む行は 0 件なので、これは潜在的な問題です。

**再現条件**

Windows の貢献者が `tools/hash-strings.ps1` またはゲーム内の「Hash for commit」を実行し、
`core.autocrlf` が `false` の場合です。

**修正案**

`.gitattributes` に規則を追加します。
現在の CSV はすべて LF なので、内容は変わりません。

```text
*.sh  text eol=lf
*.csv text eol=lf
*.txt text eol=lf
```

あわせて、書き出し側でも改行を固定すると環境差に依存しなくなります。

```csharp
using (var writer = new StreamWriter(path, append: false, new UTF8Encoding(false)))
{
    writer.NewLine = "\n";
    // ...
}
```

```powershell
$out = New-Object System.Text.StringBuilder
# AppendLine のかわりに Append("...`n") を使う、または最後に置換する
[System.IO.File]::WriteAllText($t.Output, ($out.ToString() -replace "`r`n", "`n"), (New-Object System.Text.UTF8Encoding $false))
```

### M4. 翻訳ファイルの書き込みが非アトミック

**対象**: `src/DragNWashLocalization/TranslationStore.cs:450`、`src/DragNWashLocalization/WorkingCopy.cs:146`

**現象**

いずれも対象ファイルを切り詰めてから内容を書きます。

```csharp
using (var writer = new StreamWriter(path, append: false, new UTF8Encoding(false)))
```

**根拠**

書き込み中に例外が発生すると、ファイルは途中まで書かれた状態で残ります。
`WorkingCopy.Export` は全体を `try/catch` で包んでメッセージを返すだけなので
（`WorkingCopy.cs:212-215`）、翻訳者には「失敗した」としか伝わらず、
その時点でファイルは壊れています。

```csharp
catch (Exception ex)
{
    return $"[working] Failed: {ex.Message}";
}
```

対象はどちらも翻訳者の作業内容そのものです。

**再現条件**

書き込み中のディスク満杯、ウイルス対策ソフトによるロック、ゲームの強制終了などです。
`ScriptOrder.WriteOrdered` に渡すコールバック内で例外が出た場合も同様です。

**修正案**

同一ディレクトリの一時ファイルに書いてから置き換えます。

```csharp
string temp = path + ".tmp";
using (var writer = new StreamWriter(temp, append: false, new UTF8Encoding(false)))
{
    writer.NewLine = "\n";
    // ... 既存の書き込み処理 ...
}
if (File.Exists(path))
{
    File.Replace(temp, path, path + ".bak", ignoreMetadataErrors: true);
}
else
{
    File.Move(temp, path);
}
```

### M5. コメント行の除去方法が C# と食い違う

**対象**: `tools/check-translations.py:46`、`tools/hash-strings.ps1:53`

**現象**

Python も PowerShell も、CSV として解釈する**前**に物理行単位でコメント行と空行を捨てています。

```python
kept = [(i, line) for i, line in enumerate(f, start=1) if line.strip() and not line.startswith("#")]
```

```powershell
$lines = [System.IO.File]::ReadAllLines($file, [System.Text.Encoding]::UTF8) | Where-Object { -not $_.StartsWith('#') }
```

**根拠**

C# 側の実装は正しく、引用符の外かつレコードの先頭にある `#` だけをコメントとして扱います
（`CsvReader.cs:75`）。

```csharp
// A '#' at the very start of a record is a comment line
// (section headers in the published files); skip to EOL.
if (c == '#' && fields.Count == 0 && field.Length == 0)
```

したがって、改行を含む訳文の 2 行目が `#` で始まる、あるいは空行である場合、
CI とゲームで解釈が食い違います。
Python 側では引用が閉じないまま以降の行を巻き込み、行番号の対応も崩れます。

**再現条件と実証**

引用フィールド内に改行を持つ訳文が追加された場合です。
現在のデータには該当行が 0 件なので、まだ表面化していません。
CONTRIBUTING は改行を含む値を引用符で囲むよう案内しているため、
いずれ書かれる可能性のある形です。

実際に再現しました。次の 2 行の CSV を用意します。

```csv
key,section,node,order,speaker,translation
0123456789abcdef,UI,,,UI,"第一段落です。

第二段落です。"
fedcba9876543210,UI,,,UI,normal
```

同じファイルを 3 つの実装が読むと、値が一致しません。

| 読み手 | `0123456789abcdef` の値 |
| --- | --- |
| ゲーム（`CsvReader.cs`） | `第一段落です。\n\n第二段落です。` |
| `check-translations.py` | `第一段落です。\n第二段落です。` |
| `hash-strings.ps1` | `第一段落です。\n第二段落です。` |

段落の区切りである空行が、検証ツールとハッシュ化ツールの側だけ消えています。

問題は 2 つあります。

1. `check-translations.py` はこのファイルに対して `translations OK` を返し、終了コード 0 で通します。
   つまり CI は、**実際に配布される値とは違う値**を検証しています。
2. `hash-strings.ps1` は解釈した値でファイルを書き戻すため、
   空行が**恒久的に削除**されます。翻訳者には何も表示されません。

先頭が `#` の行でも同じことが起こります。
こちらは中間行がまるごと消えます。

**修正案**

CSV として解釈してからコメント行を落とします。
Python 側は、行番号の対応を保つために `csv.reader` の `line_num` を使う現在の方式を維持しつつ、
フィルタ条件を「引用の外にいるとき」に限定する必要があります。
簡潔にするなら、コメントを保持したまま `csv.reader` に渡し、
先頭フィールドが `#` で始まる**行**を読み飛ばす形にします。

```python
with io.open(path, encoding="utf-8-sig", newline="") as f:
    reader = csv.reader(f)
    rows = []
    for row in reader:
        if not row or (row[0].startswith("#") and len(row) == 1):
            continue
        rows.append((reader.line_num, row))
```

この形なら、引用フィールド内の `#` 行や空行は行として切り出されないため、
C# と同じ解釈になります。

### M6. 書き出し時の検索が O(n×m)

**対象**: `src/DragNWashLocalization/ScriptOrder.cs:304`

**現象**

台本順に書き出すループの中で、`ICollection<string>` に対する線形検索を行っています。

```csharp
foreach (Entry e in data.Entries)
{
    bool hashRow = keysPresent.Contains(e.Key) && !done.Contains(e.Key);
```

**根拠**

呼び出し側はいずれも `List<string>` を渡します。

- `TranslationStore.cs:466` は `inputOrder`（`List<string>`）
- `WorkingCopy.cs:173` は `all`（`List<string>`）

`List<T>.Contains` は O(n) なので、全体は エントリ数 × キー数 になります。
実測値は `data/script_order.csv` が 1,839 行、キーが 1,700 件前後なので、
1 回の書き出しで約 300 万回の文字列比較が発生します。

**再現条件**

「Hash for commit」または「Export working copy」を実行するたびに発生します。
体感できるほどの停止ではない可能性もありますが、
メインスレッド上の無駄であり、H1 と重なると悪化します。

**修正案**

メソッドの先頭で `HashSet` を作ります。

```csharp
public static void WriteOrdered(TextWriter w, Data data, ICollection<string> keysPresent, Action<string, Entry> emit,
    Func<Entry, bool> wantLine, Action<Entry> emitLine, out List<string> leftovers)
{
    var present = keysPresent as HashSet<string> ?? new HashSet<string>(keysPresent, StringComparer.Ordinal);
    var done = new HashSet<string>(StringComparer.Ordinal);
    // 以降 keysPresent.Contains を present.Contains に置き換える
```

なお末尾の `leftovers` は `keysPresent` の順序を使うため、
そちらは元のコレクションを走査したままにしてください。

### M7. 参照が重複し、手順コメントと一致しない

**対象**: `src/DragNWashLocalization/DragNWashLocalization.csproj`

**現象**

3 つの不整合があります。

1. `<Reference Include="UnityEngine.UI">` が 2 回記述されています。
2. コメントの「game install からコピーする DLL」一覧に
   `UnityEngine.TextCoreFontEngineModule.dll` がありませんが、`<Reference>` は存在します。
3. コメント一覧に `UnityEngine.UI.dll` が 2 回書かれており、
   `DragNWash.ModFramework*.dll` の 6 個はまったく記載がありません。

**根拠**

`grep` で参照の重複を確認しました。

```text
2 Reference Include="UnityEngine.UI"
```

`tools/pack.ps1:45-53` の必須 DLL 一覧には `UnityEngine.TextCoreFontEngineModule.dll` が含まれており、
こちらが正です。

**再現条件**

新しい貢献者が `.csproj` のコメントどおりに DLL をコピーしてビルドすると、
`UnityEngine.TextCoreFontEngineModule` が見つからず失敗します。
フレームワークの DLL の入手方法も `.csproj` からは分かりません。

**修正案**

1. 重複した `<Reference Include="UnityEngine.UI">` を 1 つ削除します。
2. コメント一覧を `<Reference>` と一対一で対応させ、
   `UnityEngine.TextCoreFontEngineModule.dll` を追加し、重複行を削除します。
3. `DragNWash.ModFramework*.dll` は `tools/pack.ps1` がフレームワークのリポジトリからビルドして
   `libs/` にコピーする旨を、コメントに 1 行加えます。

長期的には、コメントではなく `tools/pack.ps1` の `$Required` を単一の情報源にし、
`.csproj` からはそちらを参照する形にすると、ずれが起きにくくなります。

## 重大度: 低

### L1. `_discovered` 配下だけ BOM 付き UTF-8

**対象**: `TranslationStore.cs:581`、`TranslationStore.cs:669`、`UiTextDumper.cs:102`、
`LayoutChecker.cs:175`、`LayoutChecker.cs:189`

**現象**

公開ファイルは BOM なしで書きます。

```csharp
using (var writer = new StreamWriter(path, append: false, new UTF8Encoding(false)))
```

`_discovered` 配下は BOM 付きで書きます。

```csharp
using (var writer = new StreamWriter(filePath, append: false, Encoding.UTF8))
```

**根拠**

`Encoding.UTF8` は BOM を出力するプロパティです。
読み取り側（C#、PowerShell、Python いずれも）は BOM を除去するため、実害は限定的です。

**再現条件**

翻訳者が `_discovered` 配下のファイルを、BOM を扱わない外部ツールで開いた場合です。

**修正案**

`new UTF8Encoding(false)` に統一します。

### L2. カルチャ依存の `StartsWith`

**対象**: `TranslationStore.cs:90`、`TranslationStore.cs:125`、`Plugin.cs:433`、`IgnoreRules.cs:76`

**現象**

引数 1 つの `StartsWith` はカレントカルチャで比較します。

```csharp
if (name.StartsWith("_"))
```

**根拠**

同じコードベースの他の箇所は `StringComparison.Ordinal` を明示しています
（`TranslationStore.cs:529`、`TranslationKey.cs:51` など）。
ロケールフォルダーの判定という用途では、カルチャに依存すべきではありません。

**再現条件**

実害が出る具体的なケースは構築できませんでした。
一貫性と、意図しないカルチャ依存を避けるための指摘です。

**修正案**

`StringComparison.Ordinal` を明示します。

```csharp
if (name.StartsWith("_", StringComparison.Ordinal))
```

### L3. 空行を「malformed dropped」と数える

**対象**: `tools/hash-strings.ps1:53`

**現象**

`Read-Csv` は `#` 行だけを除去し、空行は残します。

```powershell
$lines = [System.IO.File]::ReadAllLines($file, [System.Text.Encoding]::UTF8) | Where-Object { -not $_.StartsWith('#') }
```

**根拠**

`ConvertFrom-Csv` は空行から、全プロパティが `$null` のオブジェクトを作ります。
そのオブジェクトは `key` も `source_en` も空なので、`:126` の `$dropped++` に到達します。

```powershell
} else {
  $dropped++; continue
}
```

公開ファイルはセクションの区切りに空行を多く使うため、
`:189` が表示する「malformed dropped」の件数が実態より大幅に大きくなります。

**再現条件**

公開済みの `strings.csv` を入力にして `tools/hash-strings.ps1` を実行した場合です。

**修正案**

空行も除去します。

```powershell
$lines = [System.IO.File]::ReadAllLines($file, [System.Text.Encoding]::UTF8) |
  Where-Object { $_.Trim() -ne '' -and -not $_.StartsWith('#') }
```

### L4. `level_flow.csv` の探索先がツール間で食い違う

**対象**: `src/DragNWashLocalization/ScriptOrder.cs:258`

**現象**

`level_flow.csv` を、採用した `script_order.csv` と同じディレクトリから探します。

```csharp
string flow = Path.Combine(Path.GetDirectoryName(path), "level_flow.csv");
```

`path` は `_discovered/script_order.csv` が存在すればそちらが優先されます（`ScriptOrder.cs:238`）。

**根拠**

`tools/hash-strings.ps1:61` は常にリポジトリの `data/level_flow.csv` を読みます。

```powershell
$flowFile = Join-Path $root 'data/level_flow.csv'
```

**再現条件**

メンテナーが `script_order.csv` だけをゲーム内で再生成した場合です。
`data.Levels` が空になり、セクション見出しが `Level 1: Ryan` ではなく `L01 Ryan` のままになります。
その結果、ゲーム内ボタンと PowerShell ツールが同じデータから異なるファイルを作ります。

**修正案**

隣に無ければ `data/level_flow.csv` にフォールバックします。

```csharp
string flow = Path.Combine(Path.GetDirectoryName(path), "level_flow.csv");
if (!File.Exists(flow))
{
    flow = Path.Combine(pluginDirectory, "data", "level_flow.csv");
}
```

### L5. `Awake` の失敗が毎フレームの例外になる

**対象**: `src/DragNWashLocalization/Plugin.cs:98`

**現象**

`Awake` 全体に例外処理がありません。

**根拠**

Unity は `Awake` の例外をログに記録したうえで `Update` を呼び続けます。
`Update` は `Awake` が設定するフィールドを参照します。

- `Plugin.cs:355` `HotReloadTranslations.Value`
- `Plugin.cs:360` `DumpDialogueKey.Value`
- `Plugin.cs:406` `LayoutRiskThreshold.Value`

初期化が途中で止まると、以降は毎フレーム `NullReferenceException` が発生します。

**再現条件**

`GameFonts.AddFontFolder` や `GameFlags.AddCatalog` が想定外の例外を投げた場合、
あるいはフレームワークのバージョン差で API が例外を返した場合です。

**修正案**

初期化完了フラグで `Update` を守ります。

```csharp
private bool _ready;

private void Awake()
{
    try
    {
        // ... 既存の処理 ...
        _ready = true;
    }
    catch (Exception ex)
    {
        Logger.LogError($"DragNWashLocalization failed to start: {ex}");
    }
}

private void Update()
{
    if (!_ready) return;
    // ...
}
```

## 調査したが問題なしと判断した項目

誤検知を避けるため、検討したうえで指摘しないと判断した項目を残します。

| 項目 | 判断 |
| --- | --- |
| `.github/PULL_REQUEST_TEMPLATE.md` の `../blob/main/...` リンク | 正しい。GitHub は Issue / PR 本文の相対リンクをリポジトリ URL 基準で解決する |
| 各言語の `<size=70%>` が閉じていない（1 言語あたり約 53 行） | 原文の記法。13 言語すべてで同一であり、TMP は未閉じタグを行末まで適用する |
| `ja` と `ko` にだけ存在するキー（各 10 件 / 9 件） | すべて `line:` 形式の台詞 ID 別訳。`data/script_order.csv` にも存在し、意図どおり |
| `data/level_flow.csv` の BOM | C# / PowerShell / Python のいずれの読み取り経路も BOM を除去する |
| `HotReload` のスレッド競合 | `FileSystemWatcher` ではなくメインスレッドのポーリング。設計上そのように選択されている（`HotReload.cs:17-20`） |
| `TranslationKey.LooksLikeLineId` の長さ制限 | Python 側の正規表現と一致（いずれも 6〜64 文字） |
| `Translations/_discovered/` の混入 | `.gitignore:16` と `check-translations.py:109-113` の二重で防いでいる |

## データ整合性の測定結果

使い捨てスクリプトで実測した結果です。**不整合は検出されませんでした。**

### 検証ツールの実行結果

```text
$ python3 tools/check-translations.py
translations OK
exit=0

$ python3 tools/check-game-files.py
OK: 84 tracked files, none of them a game file
   (fingerprints of Windows 9/12/2026_a93aa21a, Linux 9/13/2026_2a0da92f).
exit=0

$ ruff check tools/
All checks passed!
```

### 言語別の収録行数

キーの和集合は 1,734 件です。

| 言語 | 行数 | 和集合との差 |
| --- | --- | --- |
| ja | 1,721 | 13 |
| ko | 1,713 | 21 |
| zh-Hans | 1,703 | 31 |
| zh-Hant | 1,703 | 31 |
| de | 1,687 | 47 |
| pt-BR | 1,686 | 48 |
| ru | 1,686 | 48 |
| pl | 1,684 | 50 |
| es | 1,682 | 52 |
| fr | 1,682 | 52 |
| eo | 1,681 | 53 |
| he | 1,681 | 53 |
| tok | 1,680 | 54 |

差分は仮翻訳パックの未収録分であり、README の説明と整合します。

### 書式タグの整合性

`ja` を基準に、各言語の書式タグの構成を比較しました。
差異があったのは 3 行だけです。

| キー | 言語 | 値 |
| --- | --- | --- |
| `6267e98361c404ca` | 多数派（11 言語） | `... <i>impeccable.`（閉じない） |
| `6267e98361c404ca` | de | `... <i>makellos</i> bist.`（閉じる） |
| `6267e98361c404ca` | ko | `<i>티끌 하나 없이</i> 만들어 드릴게요.`（閉じる） |
| `84da55b8523dba73` | 多数派（12 言語） | `... <i>impeccable.`（閉じない） |
| `84da55b8523dba73` | de | `Du bist <i>makellos</i>.`（閉じる） |

原文は `<i>` を閉じない記法であり、行末まで斜体になります。
de と ko の 3 行だけが語単位で閉じているため、表示が他言語と異なります。
組版としては閉じるほうが自然とも言えるため、**不具合ではなく意図の確認事項**として記載します。

### その他

- 全 CSV が有効な UTF-8 です。BOM を持つのは `data/level_flow.csv` のみです。
- 全 CSV が LF です。CR を含むファイルはありません。
- 引用フィールド内に改行を含む行は 0 件です。
- 列数が不揃いな行、キーの重複は検出されませんでした。
- `line:` 形式の行は 54 件で、すべて `data/script_order.csv` に存在します。
- `data/script_order.csv` に無いキーは 110 件で、いずれも台詞ではない UI 文字列です。

## 未検証の前提

このレポートの限界を明示します。

- **ビルドしていません。** `dotnet` が無いため、C# の指摘はすべて読解に基づきます。
  コンパイルエラーや警告の有無は確認していません。
- **実機で動かしていません。** ゲーム本体が無いため、H1 のフリーズや M1 の CSV 破損は
  コード上の論理から導いたものであり、実測ではありません。
- **PowerShell を実行していません。** `pwsh` が無いため、H3・L3 は読解に基づきます。
  特に `ConvertFrom-Csv` の空行の扱いは、PowerShell の版によって差がある可能性があります。
- **macOS で動かしていません。** インストーラーは `bash -n` による構文確認と読解のみです。
- **H2 の攻撃は実行していません。** 公開リポジトリに対する実証は行っていません。
  `pull_request` イベントのワークフロー定義が PR の head から取られるという GitHub の仕様に基づく評価です。
