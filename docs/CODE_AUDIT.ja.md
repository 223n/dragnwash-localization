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
- [検証ラウンドの結果](#検証ラウンドの結果前半-15-件に対する反証)
- [追加監査で見つかった指摘](#追加監査で見つかった指摘28-件検証済み)
- [調査したが問題なしと判断した項目](#調査したが問題なしと判断した項目)
- [データ整合性の測定結果](#データ整合性の測定結果)
- [未検証の前提](#未検証の前提)

## サマリ

| ID | 重大度 | 対象 | 概要 |
| --- | --- | --- | --- |
| H1 | 中 | `SpeakerLookup.cs` | 話者表を作り直しすぎて「Hash for commit」が数秒〜数十秒止まる |
| H2 | 高 | `.github/workflows/comment-on-check.yml` | fork の PR が作ったアーティファクトを特権ワークフローが信用している |
| H3 | 高 | `tools/hash-strings.ps1` | ゲーム内ボタンと挙動が違い、公開済みの訳が消える |
| H4 | 高 | `WorkingCopy.cs` | 「Export working copy」の再実行で未ハッシュの訳が消える |
| M1 | 中 | `LayoutChecker.cs` | ロケール依存の数値書式が出力 CSV を壊す |
| M2 | 中 | `IgnoreRules.cs` | 正規表現にタイムアウトがなく、`ignore.txt` 次第で停止する |
| M3 | 中 | `.gitattributes` ほか | CSV の改行コードが環境で変わる |
| M4 | 中 | `TranslationStore.cs` / `WorkingCopy.cs` | 翻訳ファイルの書き込みが非アトミック |
| M5 | 中 | `check-translations.py` / `hash-strings.ps1` | コメント行の除去方法が C# と食い違う |
| M6 | 低 | `ScriptOrder.cs` | 書き出し時の検索が O(n×m)（体感は無し） |
| M7 | 低 | `DragNWashLocalization.csproj` | 手順コメントが実態と一致しない（ビルドは壊れない） |
| L1 | — | 各書き出し処理 | `_discovered` 配下だけ BOM 付き UTF-8（一貫性の整理） |
| L2 | — | 4 ファイル | カルチャ依存の `StartsWith`（一貫性の整理） |
| L5 | — | `Plugin.cs` | 起動失敗時の明示的な停止（防御的整理。不具合ではない） |
| L6 | 低 | `.csproj` / `Plugin.cs` | バージョン番号が 2 か所にあり、一致を保証する仕組みがない |

検証の結果、当初 17 件としたもののうち **2 件は誤りで取り下げ**、4 件は重大度を訂正、
3 件は「不具合」ではなく整理として位置づけ直しました。確定した指摘は
高 3 件、中 5 件、低 3 件、整理 3 件です。詳細は[検証ラウンドの結果](#検証ラウンドの結果前半-15-件に対する反証)を参照してください。

このうち **H3・H4・M4・M5 はいずれも翻訳者の作業内容が失われる経路**で、実害が最も大きいと考えます。

さらに、当初カバーできていなかった 4 領域の追加監査で
[28 件の指摘](#追加監査で見つかった指摘28-件検証済み)が出ました。こちらも同じ検証を通し、
**22 件が残り 6 件を取り下げ**ています。

macOS インストーラーの指摘は bash と node がこの環境で動くため、
**再現と修正の両方を実際に実行して確認しました**。C# の指摘は `dotnet` が無いため読解のみですが、
フレームワーク（[TomXV/dragnwash-modframework](https://github.com/TomXV/dragnwash-modframework)）の
ソースを取得して一次情報で裏を取っています。

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
- bash と node はこの環境で動くため、macOS インストーラーの指摘は該当構文を切り出して実行し、修正前後の挙動を比較しました。
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

> [!IMPORTANT]
> **前提に誤りがありました（重大度 高 → 中）。** 以下の「作業用コピーには `speaker` 列が無いことが
> 多い」という記述は誤りです。公開 `strings.csv` 13 言語で `speaker` 列が空の行は **0 件**、
> `data/script_order.csv` が知らないキーは ja で 1,680 件中 **110 件**でした。
> Mod が書き出した作業用コピーでは走査は約 110 回（数秒の引っかかり）で、数千回になるのは
> `speaker` 列を持たない入力、すなわち `CONTRIBUTING.md:54-57` が案内する手書きの
> `source_en,translation` 形式や、CI が今も受け付ける旧ヘッダーの場合です。
> 欠陥そのものと修正案は変わりません。

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

`github.event.workflow_run.pull_requests` を使う書き方もありますが、
この配列は **fork からの PR では空になります**。
今回守りたいのはまさにその経路なので、`head_sha` から引く形にしてください。

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

> [!NOTE]
> **ソートが壊れる仕組みを訂正します。** 当初「`double.TryParse` がカルチャ依存で失敗する」と
> 書きましたが、実測ではより静かな壊れ方でした。
>
> | 入力行の ratio 欄 | 修正前の `RatioOf` の戻り値 |
> | --- | --- |
> | `4.7`（不変カルチャで書かれた行） | **47**（`.` を桁区切りとして解釈） |
> | `1,23`（カレントカルチャで書かれた行） | **23**（先に `,` で分割され、断片を解釈） |
>
> いずれも失敗して 0 になるのではなく、**誤った値を返します**。指摘自体は成立し、修正案も
> 変わりません。

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

*.bundle binary
*.gif    binary
```

後半の 2 行は保険です。
`.bundle` と `.gif` は先頭 8,000 バイト以内に NUL バイトを含むため、
Git は現状でも自動的にバイナリと判定します（確認済み）。
明示しておくと、将来 `* text=auto` を足したときにも安全です。

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

**この形式は、ドキュメントが明示的に推奨しているものです**（`CONTRIBUTING.md:115`）。

```text
Wrap fields containing commas, quotes or line breaks in `"` (escape quotes as `""`),
per [RFC 4180](https://datatracker.ietf.org/doc/html/rfc4180).
```

`CONTRIBUTING.ja.md:135` も同じ内容で、RFC 4180 準拠をうたっています。
つまり、貢献者が案内どおりに書いた入力を、3 つの実装のうち 2 つが正しく扱えません。

**重大度を「中」とした理由**

現在のデータには引用フィールド内に改行を含む行が 1 件もありません。
そのため、実害はまだ発生していません。
ただし、複数行の訳文が 1 行でも追加された時点で、静かなデータ消失に変わります。

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

> [!IMPORTANT]
> **影響を過大に書いていました（重大度 中 → 低）。** 約 310 万回という計算は正しいものの、
> 16 文字のキー比較としては **10〜20 ミリ秒**で、ボタン 1 回につき 1 度だけです。
> 知覚できる停止は発生しません。不要な計算量の整理として扱ってください。

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

> [!IMPORTANT]
> **主張に誤りがありました（重大度 中 → 低）。** `<Reference>` の重複は MSBuild が同一項目として
> 解決するため、ビルドに影響しません。また `docs/RELEASING.md` が案内する `tools/pack.ps1` は
> 欠落した DLL を名指しで報告し、フレームワークの DLL は自分でコピーします。
> 実体は古くなったコメントという文書上の不備です。

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

> [!NOTE]
> **具体的な誤動作は確認できませんでした。** 読み手はすべて BOM を除去し、追記時は preamble も
> 出力されません。不具合ではなく一貫性の整理として扱ってください。

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

> [!NOTE]
> **「実害が構築できない」という当初の記述は誤りでした。** カルチャ依存の比較は無視可能文字を
> 読み飛ばすため、ゼロ幅接合子（U+200D）、ソフトハイフン（U+00AD）、BOM（U+FEFF）を前に
> 付けた名前は「`_` で始まる」と判定されます（実測で確認）。
>
> ただし、そうした名前のフォルダーや `ignore.txt` の行が実際に作られる見込みは低いため、
> 重大度は「低」のままとします。位置づけは「実害は考えにくいが、起きた場合は静かに壊れる」です。

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

> [!CAUTION]
> **この指摘は取り下げました。** `ConvertFrom-Csv` は空行を読み飛ばし、オブジェクトを生成しません。
> したがって空行が `$dropped` に計上されることはなく、前提そのものが誤りでした。
> この環境には `pwsh` が無く自身では未実測のため、以下の記述は誤った推論の記録として残します。

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

> [!CAUTION]
> **この指摘は取り下げました。** `ScriptOrder.Generate` の呼び出し元は `FlowDumper.Export:38` のみで、
> その 4 行前（`:34`）で `level_flow.csv` が同じ `_discovered/` に書かれます。`Generate` は
> `FlowDumper.ReadLevels` が失敗すれば `script_order.csv` を書かずに戻るため、両者は必ず同時に
> 成功または失敗します。片方だけ生成される経路は存在しません（自身で確認済み）。

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

> [!CAUTION]
> **主張した障害経路は到達不能でした。** `Update` が参照する 9 個の `ConfigEntry` はすべて
> `Plugin.cs:104-160` で束縛され、現実的に例外を投げうる処理（`:162-183`）より前にあります。
> それ以前の唯一の呼び出し `ModFrameworkInfo.Register` は自身で try/catch を持ちます
> （`ModFrameworkInfo.cs:14-30`）。したがって `Update` が null を参照することはありません。
> ただし `:162-183` で失敗すると、何も翻訳しないまま動き続ける中途半端な状態になります。
> **不具合の修正ではなく、任意の防御的整理**として位置づけ直します。

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

### L6. バージョン番号が 2 か所にあり、一致を保証する仕組みがない

**対象**: `src/DragNWashLocalization/DragNWashLocalization.csproj:8`、
`src/DragNWashLocalization/Plugin.cs:33`

**現象**

同じバージョンが 2 か所に書かれています。

```xml
<!-- Keep in sync with Plugin.PluginVersion; the installer shows this. -->
<Version>1.1.2</Version>
<FileVersion>1.1.2</FileVersion>
```

```csharp
public const string PluginVersion = "1.1.2";
```

コメントは同期を指示していますが、それを検査する仕組みはありません。

**根拠**

- `Plugin.cs` の値は `[BepInPlugin(PluginGuid, PluginName, PluginVersion)]` を通じて
  Mods 画面に表示されます（`Plugin.cs:21`）。
- `.csproj` の値は DLL のファイルバージョンになり、`.csproj` のコメントによれば
  インストーラーがこれを表示します。
- `tools/pack.ps1:98-104` は `Plugin.cs` からしか読みません。

```powershell
$pluginCs = Get-Content -LiteralPath (Join-Path $SrcDir 'Plugin.cs') -Raw
if ($pluginCs -match 'PluginVersion\s*=\s*"([^"]+)"') {
    $Version = $Matches[1]
}
```

現時点では両者とも `1.1.2` で一致しています（確認済み）。

**再現条件**

リリース時に片方だけ更新した場合です。
zip の名前と Mods 画面は新しいバージョンを、インストーラーは古いバージョンを表示します。
ビルドもテストも通るため、気付くのはリリース後になります。

**修正案**

`tools/pack.ps1` で一致を検査し、ずれていれば止めます。

```powershell
$csproj = Get-Content -LiteralPath $Project -Raw
if ($csproj -match '<Version>([^<]+)</Version>' -and $Matches[1] -ne $Version) {
    throw "Version mismatch: Plugin.cs is $Version but the .csproj is $($Matches[1]). Update both."
}
```

## 検証ラウンドの結果（前半 15 件に対する反証）

前半の指摘のうち 15 件について、「反証を試みる」「具体的な失敗ケースを構築する」の
2 観点から独立に検証しました。その結果、**6 件に反証が出ました。**

### 取り下げた指摘（2 件）

| ID | 取り下げの理由 |
| --- | --- |
| L3 | `ConvertFrom-Csv` は空行を読み飛ばし、オブジェクトを生成しない。したがって空行が `$dropped` に計上されることはない。前提そのものが誤りだった（`pwsh` が無く自身では未実測） |
| L4 | `script_order.csv` と `level_flow.csv` は `FlowDumper.Export` の同一呼び出しで、同じディレクトリに書かれる（`:34` と `:38`）。片方だけ生成される経路は存在しない（自身で確認済み） |

### 主張を訂正した指摘（4 件）

| ID | 訂正内容 | 重大度 |
| --- | --- | --- |
| H1 | 「作業用コピーは speaker 列が空なのが通常」は誤り。公開 `strings.csv` 13 言語で speaker 空の行は **0 件**、`script_order.csv` が知らないキーは ja で 110 件。Mod 製の作業用コピーでは走査は約 110 回で、数秒の引っかかり。数千回になるのは手書きの `source_en,translation` 形式の場合 | 高 → **中** |
| H2 | 対象は「任意の Issue / PR」ではなく **「任意の PR」**（`issues: write` は無い）。また「passed と表示させる」のは既に marker コメントがある PR の**編集のみ** | 高（据え置き） |
| M6 | 「数千 × 数千」は無い体感コストを示唆していた。約 310 万回の 16 文字比較は **10〜20 ミリ秒**で知覚できない | 中 → **低** |
| M7 | `<Reference>` の重複は MSBuild が同一項目として解決するため**ビルドに影響しない**。また `tools/pack.ps1` を使う文書化された手順なら欠落は名指しで報告される。実体は古いコメントという文書上の不備 | 中 → **低** |

### 位置づけを改めた指摘（3 件）

| ID | 内容 |
| --- | --- |
| L5 | 「`Update` が毎フレーム NRE」は到達不能。9 個の `ConfigEntry` はすべて危険な処理より前で束縛され、それ以前の `ModFrameworkInfo.Register` は自身で try/catch を持つ。**不具合ではなく任意の防御的整理** |
| L1 | 具体的な誤動作は存在しない。読み手はすべて BOM を除去し、追記時は preamble も出ない。**一貫性の整理** |
| L2 | 実際に挙動が変わる入力を構築できなかった。**一貫性の整理** |

### 補強された指摘（2 件）

| ID | 内容 |
| --- | --- |
| M1 | ソートが壊れる仕組みは「解釈の失敗」ではなかった。`RatioOf` は先に `,` で分割するため、`1,23` は断片 `23` として解釈され、**1.23 ではなく 23 が返る**。より静かな誤りだった |
| M2 | 「画面に出る文字列ごと」は、ハング側から見れば過大（最初の 1 件で止まる）だが、**呼び出し頻度としては過小**。`IgnoreRules.cs:101-102` の「呼び出し側で重複排除済み」というコメントに反し、`TmpTextPatches.cs:50` はすべてのテキストで無条件に呼ぶ |

### 実機での検証（メンテナーによる）

上記の反証ラウンドとは別に、メンテナーが実機（Windows 11 / .NET SDK 10.0.401 / PowerShell 7.6.6）で
各修正を検証しました。**そこでさらに 2 件、修正そのものの欠陥が見つかっています。**

| 項目 | 実機検証で判明したこと |
| --- | --- |
| M4 | `File.Replace` は対象の削除・改名を必要とするため、別プロセスが `FILE_SHARE_DELETE` なしで開いていると失敗する。`StreamWriter` で直接書いていた従来は成功していた経路で、**修正が退行を招いていた**。`Commit` 失敗時に `.tmp` も残っていた |
| M5 | `ConvertFrom-Csv` は行の配列を受け取ると 1 行を 1 レコードとして扱う。コメント除去を解釈のあとに回しても、**PowerShell 側では複数行フィールドが復元されず、直そうとした症状がそのまま残っていた** |
| M1 | 修正前の `RatioOf` は失敗して 0 を返すのではなく、`4.7` の `.` を桁区切りとして解釈して **47 を返す**。本レポートの記述より静かな壊れ方だった |
| L2 | 「実害が構築できない」は誤り。ゼロ幅接合子・ソフトハイフン・BOM を前置した名前で判定が分かれることが実測された |
| L6 | 当初 `<FileVersion>` を検査対象から外していたが、`<Version>` とは別の行にある独立要素であり単独でずれる。例外メッセージは両方を指示していたため、実装と食い違っていた |
| L1 | `_discovered/` の BOM 混在は実機で確認された（4 ファイルが BOM あり、2 ファイルが BOM なし）。ただし BOM を外すと Excel が UTF-8 と認識しなくなるという代償があり、方針を再検討中 |

M4 と M5 は、**この環境に `dotnet` と `pwsh` が無いまま修正を断定的に書いたことが原因**です。
本レポートの「実行できなかった検証」は注記として機能しておらず、検証が入らなければ
どちらも壊れたままになっていました。以降の指摘では、根拠が「実行して確認」か「読解のみ」かを
項目ごとに明示します。

## 追加監査で見つかった指摘（28 件・検証済み）

当初カバーできていなかった 4 領域を追加で監査し、前半 15 件と同じ敵対的検証（1 領域につき「再現」役と「反証」役の 2 系統）を通しました。

**結果: 22 件が残り、6 件を取り下げました。**

| 判定 | 件数 |
| --- | --- |
| 生き残った（PR 化済み: #22〜#43） | 22 |
| 取り下げ | 6 |

### macOS インストーラー（8 件 → 5 件）

| 判定 | 重大度 | 箇所 | 概要 | PR |
| --- | --- | --- | --- | --- |
| 採用 | 中 → **低** | `install-macos.sh:871` | `BepInEx/plugins` が無いと `set -e` + `pipefail` でアンインストールが無言で中断する。**到達経路は「ユーザーが手で `BepInEx/plugins` を削除した」場合のみ**（unzip 中断・アンインストール中断では起きないことを検証で確認） | #22 |
| 取り下げ | — | `install-macos.sh:770` | 「`run_bepinex.sh` はアーカイブの末尾にある」という前提が誤り。実際のアーカイブでは 22 件中 4 番目で、`BepInEx.dll`（7 番目）より先に展開される。unzip 中断で当該状態にはならない | — |
| 取り下げ | — | `install-macos.sh:774` | 「`-s` があるのでエラーが出ない」が誤り。`-fsSL` には `-S`（`--show-error`）が含まれており、curl はエラーを表示する。提案していた修正は no-op | — |
| 取り下げ | — | `install-macos.sh:632` | フォールバック自体は存在するが、「動作しない形式」という主張が過大 | — |
| 採用 | 低 | `install-macos.sh:492` | JXA の `String.replace` が置換文字列中の `$&` `` $` `` `$'` `$$` を特殊解釈し、起動オプションが壊れる | #23 |
| 採用 | 低 | `install-macos.sh:79` | 値を伴わないオプションが最後の引数だと、診断なしで終了コード 1 になる | #24 |
| 採用 | 低 | `install-macos.sh:806` | `$LANG_CHOICE` が `sed s///` の置換側にエスケープなしで埋め込まれる | #25 |
| 採用 | 低 | `install-macos.sh:661` | 実行開始のログ行が、常に空のコマンドラインを記録する | #26 |

この領域は **bash / node が使えるため、この環境で実際に実行して再現と修正の確認ができました**。

### ImGui ツールウィンドウ（7 件 → 5 件）

| 判定 | 重大度 | 箇所 | 概要 | PR |
| --- | --- | --- | --- | --- |
| 採用 | **高 → 中** | `Plugin.ImGui.cs:159` | About タブが導入パスをメニューフォントで描画し、「ASCII のみ」という同ファイルの明文の前提を破る。準備済み非 ASCII 文字の和集合 2759 字を実測し、**日本人の姓の上位 20 件中 12 件が未準備の漢字を含む**ことを確認。ただしクラッシュには D3D12 + 非 ASCII パス + 未準備文字の 3 条件が必要 | #30 |
| 採用 | 中 | `Plugin.ImGui.cs:406` | `GameSaves.ReadLevel` が OnGUI のたびにセーブファイルを読む。フレームワークのソースを取得し、`File.ReadAllText` + 正規表現であることを一次情報で確認 | #29 |
| 取り下げ | — | `Plugin.ImGui.cs:437` | 描画コールバック内の書き込みに try/catch が無い点は事実だが、両検証役とも「主張された失敗経路が成立しない」と判定 | — |
| 取り下げ | — | `Plugin.ImGui.cs:167` | 毎パスの再構築は事実だが、根拠に挙げた D3D12 との関連が誤り。ローカル関数はデリゲート化されずアロケーションも発生しない。提案していた修正では描画ラベル数もグリフ数も減らない | — |
| 採用 | 中 → **低** | `Plugin.ImGui.cs:370` | **当初の提案（`EventType.Layout` に限定）は no-op**（`Time.unscaledTime` はフレーム内で変化せず、既に Layout パスで実行されている）。本当の問題は**フレームをまたぐ押下〜解放**で、上限 30 件に達したスロットでは件数が変わらないまま全エントリがずれる | #31 |
| 採用 | 中 | `LayoutChecker.cs:189` | 同じメソッド内で、空の報告の書き出しは try/catch されているのに、本体の書き出しは保護されていない | #28 |
| 採用 | 低 | `LayoutChecker.cs:41` | 翻訳が未読み込みのとき、「アクティビティログを見てください」と案内した直後に何も記録せず戻る | #27 |

この領域は **`dotnet` が無いため、いずれも読解のみ**です。ただし `GameSaves` / `MenuText` / `MenuFont` については、[TomXV/dragnwash-modframework](https://github.com/TomXV/dragnwash-modframework) を実際に取得して一次情報で裏を取りました。

### データ整合性（4 件 → 3 件）

| 判定 | 重大度 | 箇所 | 概要 | PR |
| --- | --- | --- | --- | --- |
| 採用 | 中 → **低** | `Translations/es/strings.csv:945` ほか | 2 つのキーで `<i>` の閉じ方が言語間で食い違う。検証の結果、未クローズの形は**英語原文（強調語が文末で閉じ忘れ）の踏襲**で、語順の都合があるドイツ語だけが閉じていた、という構造が判明。見た目を変えずタグだけ揃える方針を採用 | #43 |
| 取り下げ | — | `data/script_order.csv:498` ほか | ハッシュを逆算した結果、4 件の正体は `==` / `---` / `Project Scale mary` という **Yarn の内部マーカーで、`Translations/ignore.txt` が明示的に翻訳対象外としているもの**だった。提案していた修正はスタイルガイドに反する | — |
| 採用 | 低 | `Translations/de/strings.csv:27` ほか | ドイツ語の引用符が開きは `U+201E` なのに、14 箇所で閉じが ASCII の `"` になっている | #33 |
| 採用 | 低 | `Translations/zh-Hans/name.txt:1` | zh-Hans の表示名が汎用の「中文」で、zh-Hant の「繁體中文」と区別しにくい | #32 |

### ドキュメント整合性（9 件 → 9 件）

9 件すべてが検証を通りました。

| 判定 | 重大度 | 箇所 | 概要 | PR |
| --- | --- | --- | --- | --- |
| 採用 | 中 | `CONTRIBUTING.md:46` | ファイル形式の例が、`Audio` のハッシュを使いながらラベルを `Options` と書いている | #34 |
| 採用 | 中 | `CONTRIBUTING.md:110` ほか | 存在しない「F1 → Tools」タブへ翻訳者を案内している（7 ファイル 21 箇所） | #35 |
| 採用 | 中 | `CONTRIBUTING.md:108` | 「作業用コピーがあればそこから生成」という説明が、文書化された `-Path` 形式では成り立たない | #36 |
| 採用 | 中 | `README.md:226` | README が案内する台詞エクスポート経由の貢献手順が、リポジトリ自身の CI が拒否するファイルを生む | #37 |
| 採用 | 低 | `.github/ISSUE_TEMPLATE/translation-fix.yml:9` | 13 の言語パックがあるのに、選択肢が ja / zh-Hans / その他の 3 つしかない | #38 |
| 採用 | 低 | `docs/RELEASING.md:49` | リリース ZIP の構成説明が、`pack.ps1` が実際に入れる ModFramework のプラグインとプリローダーを落としている | #39 |
| 採用 | 低 | `README.md:281` | フラグ分類の一覧が、`FlagCatalog.csv` の 8 分類のうち 2 つを欠いている | #40 |
| 採用 | 低 | `CONTRIBUTING.ja.md:71` | 「ハッシュのみ」の理由づけが別の項目に付いており、事実と異なる説明になっている | #41 |
| 採用 | 低 | `docs/PLAN.ja.md:59` | 公開 CSV が `source_en,translation` だけだという古い記述が残っている（`PLAN.md` は修正済み） | #42 |

### 検証で分かったこと

前半 15 件では 6 件に反証が出ました。後半 28 件でも 6 件が落ちています（誤り率としてはほぼ同じ 40% → 21%）。落ち方の内訳が示唆的でした。

- **事実の誤り（3 件）** … `-fsSL` に `-S` が含まれる、zip のエントリ順、ハッシュの正体。いずれも**一次情報を当たれば分かること**を当たらずに書いていました
- **根拠の誤り（2 件）** … 現象自体は存在するが、原因として挙げた機構が違う（ローカル関数のアロケーション、D3D12 との関連）
- **提案の誤り（採用したものも含め 3 件）** … 指摘は正しいが提案していた修正が効かない（`-S` の追加、`EventType.Layout` への限定、`CanDraw` によるガード）

3 つ目は特に、**「指摘が正しいこと」と「提案が正しいこと」を分けて検証する必要がある**ことを示しています。

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
| `.gitattributes` にバイナリ指定が無い | `.bundle` と `.gif` は先頭 8,000 バイト以内に NUL バイトを含むため、Git が自動でバイナリと判定する |
| L3: 空行が malformed dropped に計上される | `ConvertFrom-Csv` は空行を読み飛ばしオブジェクトを作らない。前提が誤りだった（`pwsh` 不在のため自身では未実測） |
| L4: `level_flow.csv` の探索先が食い違う | 両ファイルは `FlowDumper.Export` の同一呼び出しで同じディレクトリに書かれる。片方だけ存在する経路は無い |

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
