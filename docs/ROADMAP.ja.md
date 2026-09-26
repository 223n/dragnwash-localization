# ロードマップ

[English](ROADMAP.md)

Drag'n Wash Localizationのこれからの予定です。
予定は変わることがあり、日付は近いものだけ書いています。
更新日：2026-09-26。

## リリース済み：v1.5.0（2026-09-23）

- F1 のウィンドウのアクティビティログ、翻訳、Saves、About の各タブを 1 つずつ見直しました。
- Drag'n Wash ModFramework 1.5.0 の新しい Mods 画面を、全部の言語パックで訳しました。
- zip にフレームワークを入れなくなりました。インストーラーが ModFramework 1.5.0 を専用のリリースから取ってきて、先に確かめます。

## リリース済み：v1.4.0（2026-09-20）

- 他のModが同梱する訳（[#28](https://github.com/TomXV/dragnwash-localization/issues/28)）。
  β版の実験的機能で、既定はオフです。
- 言語ごとの絵の翻訳（[#4](https://github.com/TomXV/dragnwash-localization/issues/4)）。
  仕組みだけで、絵はまだ描かれていません。
- Drag'n Wash ModFramework 1.4.0の上で動きます。

それぞれの中身は、下の2つの項目のとおりです。

### 他の Mod のテキストの翻訳（[#28](https://github.com/TomXV/dragnwash-localization/issues/28)）

新しい仕組み・UI・会話を足すModのテキストも、翻訳できるようにします。

- **今できること**:
  ModがTextMeshProで出す文字は、ゲームの文字と同じ仕組みで訳せます。
  未翻訳の行は書き出しにも出ます。
  IMGUIや古いuGUIの `Text` で出す文字は対象外です。
- **どう動くか**:
  各Modが `<Mod のフォルダー>/Translations/<locale>/strings.csv` に自分の訳を同梱し、
  このModが自分のパックのあとにそれを読み込みます。
  Modのファイルがゲーム本体の行を上書きすることはありません。
  同じ行の訳が食い違うときは、先に読んだほう（まずこのMod自身のパック、次に他のModをGUID順に毎回同じ順）を使い、
  あとのほうを衝突として記録します。
  ログとF1 → Translationに、両方の名前とどちらを使ったかが出ます。
  書き出しにはModの列が付くので、その行がどこのものか分かります。
- **β版の実験的機能で、既定はオフ**です。
  他のModとの組み合わせは未知の領域なので、承知のうえでオンにしてもらいます。
- 機械翻訳は組み込みません。
- 他のModは、その作者から頼まれたときに試します。
- 他のModの訳は、このリポジトリでは持ちません。
- 設計: [docs/MOD_TRANSLATIONS.ja.md](MOD_TRANSLATIONS.ja.md)。
  状況: **テスト用の Mod でゲーム内まで確認済み。次のリリースから入ります。**

### 絵の翻訳（[#4](https://github.com/TomXV/dragnwash-localization/issues/4)）

メニューのボタン、ロード画面の扉の札、壁の看板は絵です。
フレームワークのコンテンツポリシーに基づき、手で描いた絵やゲームの絵に手を加えたものを、言語ごとにこのリポジトリに入れます。
描いた人はクレジットに載せます。
置き場所は `Translations/<locale>/textures/` で、`fallback.txt` にたどる言語を書けます。
**Translate pictures** の設定でオフにでき、Direct3D 12では言語を変えたあと再起動で切り替わります。
土台は、フレームワークのAssets 1.2.0の言語ごとのテクスチャ差し替えです。

- 設計: [docs/TRANSLATED_TEXTURES.ja.md](TRANSLATED_TEXTURES.ja.md)。
  状況: **ゲーム内（Direct3D 12）まで確認済み。次のリリースから入ります。絵はまだ一枚もありません。**
  —— 仕組みだけ先にあり、どの言語からでも足せます。

## リリース済み：v1.3.0（2026-09-19）

- Drag'n Wash ModFramework 1.3.0の上で動きます。
  Direct3D 12でのクラッシュが減り、落ちたときはクラッシュレポートのウィンドウが出ます。

## リリース済み：v1.2.1（2026-09-19）

- 2026-09-14のゲームのアップデート以降に作ったセーブを、Savesタブがまた見つけられるように（Drag'n Wash ModFramework 1.2.1）。
- 書き出したときに開いていなかった画面の英語も、
  作業用ファイルに入るように（[#31](https://github.com/TomXV/dragnwash-localization/issues/31)）。

## リリース済み：v1.2.0（2026-09-17）

- ウクライナ語・タイ語・ベトナム語（仮翻訳）を追加し、16言語に。
- 韓国語を母語話者が校正（Hotcakeさん、ありがとうございます）。
- ゲームの更新で台詞の文面が変わっても、翻訳が消えないように。
- Mister ERIOさんによるロゴ。
- Drag'n Wash ModFramework 1.2.0の上で動きます。

## 予定していること

### 母語話者による確認

ドイツ語、フランス語、スペイン語、ブラジルポルトガル語、ロシア語、ポーランド語、ヘブライ語、ウクライナ語、タイ語、
ベトナム語、繁体字中国語は、まだ仮翻訳です。
修正のプルリクエストはいつでも歓迎します。確認してくださった方はクレジットに載せます（[CONTRIBUTING.ja.md](../CONTRIBUTING.ja.md)）。

### ほかの環境での確認

- Steam Deck: F1の窓で、画面キーボードで文字を入力できるか。
- macOS: BepInEx 5.4.23.5に入っているDoorstopは、Unity 6.3にフックできません（
  [NeighTools/UnityDoorstop#108](https://github.com/NeighTools/UnityDoorstop/issues/108)）。
  今は、実験的なインストールスクリプト（[`installer/experimental/install-macos.sh`](../installer/experimental/install-macos.sh)）で、
  UnityDoorstopの `ci` プレリリースのDoorstop（ゲームで試した4.6.0のビルドに固定）を入れ、ゲームをRosetta（x86_64）で動かし、Steamオーバーレイを引き継ぎます。
  進み具合は [223n/dragnwash-modframework#3](https://github.com/223n/dragnwash-modframework/issues/3) で確かめられます。
  オーバーレイの引き継ぎは、2026-09-25に本家にマージされました（[NeighTools/UnityDoorstop#121](https://github.com/NeighTools/UnityDoorstop/pull/121)、4.6.0に収録）。
  固定しているビルドを含め、Doorstop 4.6.0以降では、スクリプトによる書き換えは要らないので行いません。
  `ci` は同じURLのまま作り直されるので（前に固定していた4.5.0のビルドは、もうダウンロードできません）、スクリプトは4.6.0のビルドを、223nのフォークにプレリリースとして手を加えずに置いたコピー（[223n/UnityDoorstop `ci-4.6.0-97293a28`](https://github.com/223n/UnityDoorstop/releases/tag/ci-4.6.0-97293a28)）からダウンロードします。
  次は、UnityDoorstop 4.6.0の安定版が出て試せたら、コピーに代えてそれに固定します。
  4.6.0の安定版には、UnityDoorstopの修正（[NeighTools/UnityDoorstop#117](https://github.com/NeighTools/UnityDoorstop/pull/117) と [NeighTools/UnityDoorstop#114](https://github.com/NeighTools/UnityDoorstop/pull/114)）が両方とも入る見込みです。
  どちらも、固定しているビルドにすでに入っているためです。
  そのあとにスクリプトが待つのは、BepInExのarm64向けの修正（[BepInEx/BepInEx#1288](https://github.com/BepInEx/BepInEx/pull/1288) と [BepInEx/BepInEx#1402](https://github.com/BepInEx/BepInEx/pull/1402)）が入ったBepInExのリリースだけで、出たらそちらに切り替えます。
  そのリリースなら、Rosettaなしのネイティブ（arm64）で動く見込みです（まだ試していません）。

## 予定していないこと

- Mod内での機械翻訳。
- ゲームのファイルや英語の台本を、手を加えずにそのまま配布すること（
  [コンテンツポリシー](https://github.com/TomXV/dragnwash-modframework/blob/main/docs/CONTENT_POLICY.ja.md)）。
- 他のModの訳をこのリポジトリで持つこと。
