# Contributing（翻訳への参加）

[English](CONTRIBUTING.md)

Drag'n Wash Localization は、コードを書かずに **CSV を編集するだけ**で翻訳に参加できる
Mod です。この文書は翻訳に参加したい方向けのガイドです。プラグイン本体の開発者向けの
ビルド・配布手順は [docs/RELEASING.md](docs/RELEASING.md) を参照してください。

## 必要なもの

- Drag'n Wash（Steam版）と本 Mod の導入（BepInEx）
- CSV を編集できるテキストエディタ（Excel / LibreOffice / VS Code など）

Unity の内部キー名やプログラミングの知識は一切不要です。作業中は**画面に表示される
英語原文そのもの**を `source_en` として書き、コミット前にそれをハッシュに変換します
（下記「コミット前にハッシュ化する」）。

### 言語の表示名（`name.txt`）

`Translations/<locale>/name.txt` に書いた文字列が、ゲーム内 F1 メニューの言語ボタンと
インストーラーの言語選択にそのまま表示されます（例: `ja/name.txt` → `日本語`、
`zh-Hans/name.txt` → `中文`）。1行だけ、UTF-8 で保存してください。ファイルがなければ
フォルダ名が表示されます。新しい言語を追加するときは、フォルダ・`strings.csv`・`name.txt`
の3つを作れば完了です。

## 基本の流れ

1. このリポジトリをフォークします。
2. `Translations/<locale>/strings.csv` に訳を追加・修正します。
3. コミットして Pull Request を送ります。

## 翻訳ファイルの形式

`Translations/<locale>/strings.csv` は公開用の CSV で、**ゲームで流れる順**に並び、`#` の見出しで区切られています。作業中は原文つきの行を**同じファイルに混在**させても読めます。

```csv
key,section,node,order,speaker,translation

# ===== Level 1: Ryan (Sunny) | sets level_1 | ends level_1_complete =====
# --- intro: Ryan_1_intro ---
5d0a…,L01 Ryan,Ryan_1_intro,1,Ryan,よお。ここが洗い屋か？
# --- phone: Ryan_1_PhoneTutorial | if $has_talked_to_ryan ---
…
# ===== UI and other text (not part of the dialogue script) =====
bc1b88907d3b748a,UI,,,UI,オプション
```

- `section` … `L01 Ryan` のようなレベル番号とドラゴン名、または `Cutscene` / `Reaction` / `Unused` / `UI`
- `node` / `order` … Yarn の会話ノード名と、その中での順番。分岐先のノードは親の直後に置かれます
- `#` 行 … 見出し。読み込み時は無視されるので自由に残せます。`| if $変数` は分岐条件のヒントです
- 並び順は `data/script_order.csv`（ノード名・行 ID・ハッシュ・話者だけ。英語なし）で決まり、
  ゲーム内 **Export game flow** で再生成できます（ゲームの更新時にメンテナーが行います）

```csv
source_en,translation
Options,オプション
```

- `key` … 原文の SHA-256 の先頭16桁。**リポジトリにはこの形式だけ**が入ります。
- `speaker` … 誰の台詞か（Conrad / Ryan / Alexander / Kobold＝選択肢 / Phone / UI）。台本の構造から自動で付きます。
  ゲームの英語台本を再配布しないためで、これにより製品版を持っていない人は
  台本を読むことも、原文なしに訳を書くこともできません。
- `source_en` … ゲームに表示される英語原文そのまま（完全一致で照合）。**作業中はこちら**で
  書くと、保存した瞬間にホットリロードで画面に反映されます。
- `translation` … 訳文。

プラグインは画面に出た英文をその場でハッシュして引くので、どちらの行も同じように動きます。
F6 / F7 の出力には `key` 列と `source_en` 列の両方が入っているので、対応はそこで分かります。

### 新しい言語を始める

1. `Translations/<locale>/` フォルダを作り（例: `ko`）、`name.txt` に表示名（例: `한국어`）を書く
2. ゲームを起動し、F1 → Tools の言語一覧で新しい言語を選ぶ（まだ訳が 0 件なので画面は英語のまま）
3. **F1 → Tools → Export working copy** を押す。`strings.csv` がなくても、ゲームが持つ全行の英語原文を並べた
   空の作業コピー `_discovered/<locale>.working.csv` ができる
4. あとは下記「原文を並べて作業する」と同じ。訳した行から順に画面へ反映される

### 原文を並べて作業する（推奨）

ゲーム内 **F1 → Tools → Export working copy** を押すと、公開用の `strings.csv` が
`Translations/_discovered/<locale>.working.csv` に展開されます：

```csv
key,section,node,order,speaker,source_en,translation
5d0a…,L01 Ryan,Ryan_1_intro,1,Ryan,Hey. This the cleaning place?,よお。ここが洗い屋か？
```

会話は**ゲーム内の実行順**に並び、`speaker` 列に**誰の台詞か**（Conrad / Ryan / Alexander、
選択肢は Kobold、本部からの電話は Phone、UI は UI）が入ります。口調を合わせるときの目印に
してください。`source_en` にはゲームが今読み込んでいる台本・UIから原文が埋まります（セーブをロードしてから押すと会話文が揃います）。このファイルを編集して
保存すれば、ホットリロードでその場で画面に反映されます。`_discovered/` 配下なのでリポジトリには入りません。

プラグインがゲームの中で動いていること自体が「製品版を持っている」証明なので、
別途の認証はありません。

### コミット前にハッシュ化する

PR を送る前に、公開用の `strings.csv` を作り直してください。作業ファイル（`_discovered/<locale>.working.csv`）が
あればそこから、なければ `strings.csv` 自身の `source_en` 行から生成されます。方法は2つ：

- ゲーム内 **F1 → Tools → Hash for commit**（現在の言語のファイルを書き換えます）
- `tools/hash-strings.ps1`（引数なしで全言語、`-Path` で1ファイル）

**英語原文が残った `strings.csv` は PR で受け付けません。** PR ごとに自動チェックが走り、形式が違う場合は理由を英語でコメントします。直してプッシュすれば同じコメントが更新されます。

カンマ・引用符・改行を含む場合は、フィールドを `"` で囲んでください（引用符は `""` と
エスケープ）。詳細は [RFC 4180](https://datatracker.ietf.org/doc/html/rfc4180) 準拠です。

### 書式タグについて

原文に `<size=70%>` / `<gradient="gold">` / `<i>` のような TMP 書式タグが含まれる場合は、
**タグ構造はそのまま残して、中の文章だけ**訳してください。タグを壊すと表示が崩れます。

```csv
"<gradient=""gold""><b> ...English... </b></gradient><size=70%> (hint)","<gradient=""gold""><b> ……訳文…… </b></gradient><size=70%>（ヒントの訳）"
```

## 未翻訳の原文を探す

ゲームをプレイしながら、訳すべき原文を手に入れる方法が3つあります。どれもゲーム内の
Fキーや自動記録で `Translations/_discovered/` に CSV として出力されます。

| 方法 | 出力先 | 内容 |
|---|---|---|
| **F6** | `_discovered/dialogue_lines.csv` | 全会話文を**ゲーム内の実行順**で（`yarn_project,node,order,kind,speaker,line_id,key,source_en,translation,tags`）。セーブをロードした後に押す |
| **F7** | `_discovered/ui_texts.csv` | 全UIテキスト（非表示メニュー含む。`key,source_en,translation,object_path`） |
| 自動記録 | `_discovered/strings.csv` | プレイ中に見つかった未翻訳の原文（`source_en,translation`） |

`_discovered/` はゲーム本体の著作物（会話文そのもの）を含むため、**リポジトリには
コミットしないでください**（`.gitignore` 対象）。各自がローカルで生成します。

- `dialogue_lines.csv` から訳したい行を `strings.csv` へコピーし、`translation` 列だけ
  埋めれば OK です（`node` や `line_id` など余分な列が付いたままでも読み込まれます）。
- `node` 列は「キャラクター名＿何回目＿場面」（例: `Conrad_1_intro`）、`order` 列はその
  会話内での順番、`kind` 列は `line`（台詞）か `option`（プレイヤーの選択肢）です。
  会話単位でまとめて訳すと、口調や文脈が揃えやすくなります。
  ドラゴンは Conrad / Ryan / Alexander の3体で、`RyanMuddy` のような接尾辞付きは
  同じキャラクターの状態違いです。
- 訳済みの行は `translation` 列に訳が入った状態で出力されるので、再ダンプしても
  作業は失われません。
- `ui_texts.csv` の `object_path` 列で、その文言が画面のどこにあるかが分かります。
  タイトル画面とゲーム中で1回ずつ F7 を押せば、ほぼ全UIが揃います。

## 訳す必要のない文字列

スライダーの数値・解像度（`1920 x 1080 @ 164.995Hz`）・ビルド番号などは最初から
記録の対象外です。さらに除外したいものは `Translations/ignore.txt` に正規表現で追記
できます（ファイル内に記述例あり）。

除外が効くのは「記録」だけです。翻訳の検索はこれより先に行われるため、`strings.csv`
に書いた行は除外パターンに一致していても**必ず翻訳されます**。

## ロケール（言語）の追加

`Translations/<locale>/` にフォルダを追加して `strings.csv` を置くだけです。ロケール名は
既存に合わせて BCP 47 形式にしてください（例: `ja`、`zh-Hans`、`zh-Hant`、`pt-BR`、`ko`）。プラグインはフォルダ名を自動検出します。

フォントはファイル内の文字から自動で選ぶので、たいていの文字体系は追加作業不要です。日本語・中国語（簡体字・繁体字）・韓国語・キリル文字・アクセント付きラテン文字・ヘブライ文字は、Windows と Steam Deck でフォントが見つかります。右から左に書く言語（`he`・`ar`・`fa`・`ur`・`yi`）は自動で右から左に表示します。ファイルは普段の入力順のまま書き、行の中にラテン文字の単語や数字を混ぜないでください（逆順に表示されます）。対応するフォントがない文字体系は、プラグイン DLL の隣の `fonts/` フォルダに `.ttf` / `.otf` を置いてください。

## 仮翻訳の言語を改善する

日本語と簡体字中国語以外の言語パックはすべて仮翻訳です（全行そろっていますが、ネイティブの確認を受けていません）。その言語を話せる方のレビューが、いちばんありがたい貢献です。行の修正は PR でお願いします。パック全体をネイティブが通して確認できたら、同じ PR で `strings.csv` 先頭のコメントと、README の言語一覧の状態を書き換えてください。

## 動作確認

- ゲーム内 **F1** でデバッグウィンドウを開き、**Tools** タブで言語を切り替えると
  再起動なしで表示が切り替わります。
- Tools の **Check translation layout** で、レイアウト崩れリスクのある文字列が
  `_discovered/layout_risks.csv`（`source_en,translation,axis,required_px,available_px,ratio,object_path`）
  に出力されます。`ratio` が大きいものほどはみ出しが大きいので、訳文を短くする等で調整してください。

## Pull Request を送る前に

- `source_en` が実際に画面に表示される英文と**完全一致**しているか（大文字小文字・
  前後の空白・書式タグまで）。
- **ハッシュ化済みか**（`strings.csv` の列が `key,section,node,order,speaker,translation` で、`source_en` の行が残っていないか）。
- 書式タグの構造が原文と一致しているか。
- 重複行や `translation` が空の行を入れていないか。
- 1つの PR は1言語・まとまりのある範囲に絞ってください。

## ルール

- ゲーム本体のアセット・コードはコミットしない（著作権保護のため）。
- `Translations/_discovered/` はコミットしない。
- 翻訳文はそれぞれの翻訳者の貢献として扱います（ライセンスは [LICENSE](LICENSE) 参照）。
