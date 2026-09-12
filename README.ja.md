# Drag'n Wash Localization

[English](README.md)

[Drag'n Wash](https://store.steampowered.com/) 用の BepInEx ベースの非公式ローカライズ Mod です。
日本語・簡体字中国語（今後他言語も歓迎）への翻訳を、コードを書かずに追加できるようにすることを
目指しています。

技術的な調査結果と実装計画は [docs/PLAN.md](docs/PLAN.ja.md) を参照してください。

## 導入方法

### かんたん導入（推奨）

1. [Releases](https://github.com/TomXV/dragnwash-localization/releases) から `DragNWashLocalization-<version>.zip` をダウンロードし、どこでもよいので展開する
2. **`Install.exe` をダブルクリック**する。ゲームフォルダは Steam から自動で見つかります（見つからなければ選択できます）
3. 言語（日本語 / 简体中文 / 英語のまま）を選んで「インストール / 更新」を押す。BepInEx が未導入なら、公式の 5.4.23.5 を自動でダウンロード（SHA-256 検証つき）して導入します
4. Steam からゲームを起動する

同じ画面に「アンインストール」ボタンもあります。セーブ履歴は既定で残し、インストーラーが入れた BepInEx は他の Mod がなければ一緒に削除できます。

手動で導入したい場合は、以下の手順に従ってください。

### 手動で導入する

### 必要なもの

- Steam版（Windows）の Drag'n Wash
- [BepInEx 5 Windows x64（Mono）版](https://github.com/BepInEx/BepInEx/releases)
- このリポジトリの [Releasesページ](https://github.com/TomXV/dragnwash-localization/releases) で配布される最新版の `DragNWashLocalization-<version>.zip`

> [!IMPORTANT]
> ReleasesのAssetsにある `DragNWashLocalization-<version>.zip` を使用してください。GitHubが自動生成する **Source code** のZIPはMod導入用ではありません。ReleasesページにModのZIPがまだない場合は、導入可能なビルドが未公開です。

### 1. ゲームフォルダを開く

Steamライブラリで **Drag'n Washを右クリック → 管理 → ローカルファイルを閲覧** を選びます。ゲームの `.exe` が置かれているフォルダがゲームルートです。

### 2. BepInExを導入する

BepInEx 5の **Windows x64（Mono）版**をダウンロードし、アーカイブの中身をゲームルートへ直接展開します。

展開後、ゲームの実行ファイルと同じ場所に `winhttp.dll`、`doorstop_config.ini`、`BepInEx` フォルダが並んでいることを確認してください。これらがもう1段内側のフォルダに入っている場合は、ゲームルートへ移します。

ゲームを一度起動し、タイトル画面まで進んだら終了します。BepInExの設定ファイルとログが生成されるので、次へ進む前に `BepInEx/LogOutput.log` が存在することを確認します。

### 3. Drag'n Wash Localizationを導入する

[Releases](https://github.com/TomXV/dragnwash-localization/releases) から `DragNWashLocalization-<version>.zip` をダウンロードし、BepInExと同じ**ゲームルート**へ展開します。`BepInEx` フォルダの統合を確認された場合は許可してください。

プラグインのDLLが次の場所にあれば正しく展開されています。

```text
<Drag'n Washのフォルダ>/BepInEx/plugins/DragNWashLocalization/DragNWashLocalization.dll
```

ZIPファイルや `DragNWashLocalization-<version>` フォルダが `plugins` とDLLの間に入らないようにしてください。

### 4. 起動して確認する

Drag'n Washを起動します。初期設定では日本語が選択されます。**F1**でローカライズメニューを開き、**Tools**から導入済みの言語へ再起動なしで切り替えられます。

`BepInEx/LogOutput.log` に `DragNWashLocalization` の起動行が記録されていれば、プラグインは読み込まれています。

起動時の言語を手動で変更する場合は、ゲームを終了して次のファイルを開きます。

```text
BepInEx/config/com.tomxv.dragnwash.localization.cfg
```

`[General]` の `TargetLocale` を `ja` や `zh-Hans` などの導入済みロケールへ変更し、ゲームを起動し直します。`en` にすると Mod を入れたまま英語の原文で遊べます（インストーラーとゲーム内 F1 メニューでも同じ選択ができます）。

### Modが読み込まれない場合

- BepInExとModの両方を、ゲームの実行ファイルがあるフォルダへ展開したか確認します。
- DLLが上記のパスにあるか確認します。
- `BepInEx/LogOutput.log` を開きます。ファイル自体がない場合はBepInExが起動していません。ファイルがある場合は `DragNWashLocalization` を検索し、周辺のエラーを確認します。
- Optionsを開いたときにDirect3D 12でクラッシュする場合は、[設定画面を開くとクラッシュする場合（Windows）](#設定画面を開くとクラッシュする場合windows) の回避策を試してください。

## 翻訳者向け

`Translations/<locale>/strings.csv` を編集するだけで翻訳を追加できます。公開ファイルは
`key,speaker,translation` の3列で、`key` は英語原文のハッシュ、`speaker` は誰の台詞かです。
おすすめの作業手順：

1. ゲーム内で **F1 → Tools → Export working copy** を押す。`Translations/_discovered/<locale>.working.csv`
   に、各行の英語原文を並べた作業用ファイル（`key,speaker,source_en,translation`）がゲーム内の実行順で書き出されます
2. `translation` 列を編集して保存する。起動中のゲームにその場で反映されます
3. コミット前に **F1 → Tools → Hash for commit**（または `tools/hash-strings.ps1`）で、英語原文を含まない `strings.csv` を作り直す

リポジトリにはゲームの英語台本を含めない方針で、**製品版を持っている人だけが翻訳できる**仕組みです。
各言語フォルダには表示名を書いた1行の `name.txt`（例: `日本語`）があり、インストーラーとゲーム内メニューに表示されます。
Unity内部のキー名などを知る必要はありません。手順は [CONTRIBUTING.md](CONTRIBUTING.md) を参照。書式タグ（`<size=70%>`など）が原文に
含まれている場合は、タグ構造をそのまま残して中の文章だけ訳してください。

### 会話文をまとめて確認したい場合

プラグイン導入後、ゲーム内（セーブをロードした後）で **F6キー** を押すと、全会話文が
`BepInEx/plugins/DragNWashLocalization/Translations/_discovered/dialogue_lines.csv`
に一括で書き出されます（実機で1839行を確認済み）。

行は**ゲーム内で実際に流れる順**に並びます。`node` 列が会話の単位で、
`Alexander_2_intro` のように「キャラクター名＿何回目＿場面」の形になっており、
`order` 列がその会話内での順番です。`kind` 列は `line`（キャラクターの台詞）と
`option`（プレイヤーが選ぶ選択肢）を区別します。誰が誰に何と答えているかが分かるので、
前後を見ながら訳せます。登場するドラゴンは Conrad / Ryan / Alexander の3体です。

訳したい行を `Translations/<locale>/strings.csv` にコピーし、`translation` 列を埋めて
PRを送ってください（`node` や `key` など余分な列が付いたままでも問題なく読み込まれます）。
すでに訳した行は `translation` 列に訳が入った状態で出力されるので、再ダンプしても
作業は失われません。

### 編集した訳を再起動なしで確認する

ゲームを起動したまま `Translations/<現在の言語>/strings.csv` または作業用ファイル
`_discovered/<locale>.working.csv` を保存すると、約2秒後に自動で読み直され、画面に出ている
テキストにもその場で反映されます。変わった行は F1 の Activity log に1行ずつ出ます。
訳を直しては画面で確かめる、を再起動なしで繰り返せます（`[Debug] HotReloadTranslations`
で無効化可能）。反映されたかは F1 の Activity log に `[reload]` で出ます。

### UI文言をまとめて確認したい場合

**F7キー** で、読み込み済みの全UIテキストが
`Translations/_discovered/ui_texts.csv` に書き出されます。**非表示のメニューも対象**なので、
ポーズメニューや確認ダイアログを開かなくても、そのシーンのUI文言が丸ごと手に入ります。
列は `key` / `source_en` / `translation`（訳済みなら既存の訳）/ `object_path`（画面上のどこか）です。

タイトル画面とゲーム中で1回ずつ押せば、ほぼ全てのUIが揃います。

未翻訳のUI文言は、プレイ中に自動的に
`Translations/_discovered/strings.csv` にも記録されます。このファイルは起動のたびに
整理され、すでに訳した行や重複は取り除かれるので、常に「残りの作業リスト」になります。

### 訳す必要のない文字列について

スライダーの数値・解像度（`1920 x 1080 @ 164.995Hz`）・ビルド番号などは、
最初から記録の対象外です。除外パターンは [Translations/ignore.txt](Translations/ignore.txt)
に追記できます（正規表現、ファイル内に記述例あり）。

除外されるのは「記録」だけです。翻訳の検索のほうが先に行われるため、
`strings.csv` に書いた行は除外パターンに一致していても必ず翻訳されます。

### ゲーム内デバッグメニュー

**F1キー** でデバッグウィンドウを開閉できます（設定で変更可能）。
タイトル部分をドラッグして移動、右下の角をドラッグしてサイズを変更できます。

- **Activity log**: 翻訳結果と処理ログを表示します。`Follow: ON/OFF` で末尾への
  自動追従を切り替えられ、手動スクロールすると追従が止まります。`Clear log` は
  表示と重複抑制をリセットします。ログは直近100件を保持します。
- **Tools**: 再起動なしで言語を切り替えます（ボタン名は各言語の `name.txt`、**English** は翻訳オフ）。
  会話の書き出し（`Export loaded dialogue`）、UI文言の書き出し（`Export UI text`）、
  原文つき作業ファイルの書き出し（`Export working copy`）、公開ファイルの作り直し（`Hash for commit`）、
  レイアウトチェックもここから行います。
- **Saves**: セーブの巻き戻し、レベル番号の変更、フラグの反転。後述。

**Check translation layout** ボタンで、レイアウト崩れリスクのある文字列を
`Translations/_discovered/layout_risks.csv` に書き出せます（しきい値は
`BepInEx/config/.../LayoutOverflowThreshold`、既定 `1.0` ＝ ちょうど収まる）。

### セーブを1つ前に戻す（翻訳確認用）

ゲームがセーブを書き込むたびに、プラグインが
`BepInEx/plugins/DragNWashLocalization/SaveHistory/<スロット>/` に世代コピーを残します
（スロットごとに既定30世代、`[Debug] SaveHistoryKeep` で変更可）。
**F1 → Saves** タブでスロットを選び、戻したい世代の **Restore** を押すと、その世代が
ゲームのセーブファイルに書き戻されます。**その後タイトル画面に戻ってスロットをロード**すると
反映されます（ゲーム内でセーブすると再び上書きされます）。復元前の状態も自動で
世代に残るので、戻しすぎても元に戻せます。

同じ場面の会話を訳し直して見比べたいときに使ってください。Restore はフラグや変数を
直接いじるわけではなく、ゲーム自身のセーブファイルを差し替えるだけです。

同じタブの **PROGRESS** 欄では、レベル番号を **-** / **+** で変えて **Apply** できます。
先に進める方向はネタバレの可能性があるので確認が出ます。**Flags...** を押すと、ゲームが使う
イベントフラグが分類（レベル進行 / ストーリー / 恋愛 / シーン発生条件 / シーン視聴済み / アイテム）と
説明つきで全部並びます。セーブにまだ存在しないフラグも「unset」として表示され、値をクリックすると
unset → true → false の順に切り替わります。検索欄で絞り込み、**Reset all to false...** で全フラグを
一括で false にできます（レベル番号は保持）。一覧は DLL の隣の `FlagCatalog.csv` から読むので、
後から見つけたフラグを行として追加できます。どの編集も直前の
状態をスナップショットに残してから行われます。

## 設定画面を開くとクラッシュする場合（Windows）

Unity 6000.3.14f1 / DirectX 12 環境で、Options を開いた際に
`D3D12ScratchAllocator::DestroyScratch` で落ちる問題がありました。同じスタックの
[Unity公式の不具合報告（UUM-140564）](https://issuetracker.unity.com/issues/10698)
があり、ネイティブ描画側のバグなので翻訳フックで例外を捕捉しても防げません。

このバグは**実行中のテクスチャ確保・アップロード**で踏みます。プラグインは
日本語グリフを起動時にまとめて生成することでゲーム中のアトラス更新をなくし、
Direct3D 12 のままクラッシュしないことを実機で確認済みです。設定は不要です。

それでも落ちる場合は、Steamライブラリで
**Drag'n Wash → プロパティ → 一般 → 起動オプション** に `-force-d3d11` を追加して
再起動すると、描画APIごとバグを回避できます
（[Unity標準の起動オプション](https://docs.unity3d.com/6000.3/Documentation/Manual/PlayerCommandLineArguments.html)。
ゲーム本体のDLL・セーブデータは変更されません）。
`BepInEx/LogOutput.log` のプラグイン起動行で、実際に使われている描画APIを
`graphics=...` として確認できます。

`BepInEx/config/com.tomxv.dragnwash.localization.cfg` の `[Font] AtlasPointSize`
を下げると、フォントアトラスの枚数が減り、上げると文字が鮮明になります（既定80）。

## 現在のステータス

v0.1.1 をリリース済み（ワンクリックインストーラー、英語のまま遊ぶ選択肢、セーブ進捗の編集）。BepInExプラグインの骨格、UI文字列・会話文の日本語/中国語差し替え、
CJKフォント表示、会話・UIの一括抽出、ゲーム内デバッグメニュー、レイアウト崩れ検出、
翻訳者向けドキュメント、リリース手順を実装・実機確認済みです。
詳細は [docs/PLAN.md](docs/PLAN.ja.md) を参照してください。

## 翻訳に参加する

コード不要で `Translations/<locale>/strings.csv` を編集するだけで参加できます。
手順・書式・未翻訳の見つけ方は [CONTRIBUTING.md](CONTRIBUTING.md) を参照してください。

## 配布・リリース

リリース zip のビルドと配布手順は [docs/RELEASING.md](docs/RELEASING.ja.md) を
参照してください（ゲーム由来の参照アセンブリをコミットできないため、リリースは
ローカルでビルドして GitHub Releases にアップロードします）。

## 開発者の方へ

本プロジェクトは非公式のファン制作物で、Gator Dragon Games とは無関係です。ゲームのアセットや台本は含まず、英語原文は SHA-256 ハッシュとしてのみ保持し、ゲームのファイルを書き換えることもありません（BepInEx が実行時にプラグインを読み込みます）。開発チームの方で懸念がある場合は、このリポジトリの Issue かメンテナーへの連絡でお知らせください。ご希望に応じて修正または公開停止します。

## ライセンス

プラグインのコードは [LICENSE](LICENSE) を参照してください。ゲーム本体の資産・コードは
含んでおらず、翻訳文はそれぞれの翻訳者の貢献として扱われます。
