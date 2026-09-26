# Drag'n Wash Localization

[English](README.md) | [한국어](README.ko.md)

[Drag'n Wash](https://store.steampowered.com/app/4739660/) を多くの言語で遊べるようにする、BepInExベースの非公式・多言語ローカライズModです
（対応している言語は[言語パック](#言語パック)を見てください）。
どの言語も、コードを書かずにCSVを編集するだけで追加したり直したりできます。

技術的に調べたことと実装の計画は [docs/PLAN.ja.md](docs/PLAN.ja.md) にまとめてあります。

> [!WARNING]
>
> ## ⚠️ ネタバレ注意 ⚠️
>
> **`Translations/` の CSV には、ゲームの全会話がストーリーの進行順で入っています。**
> 中身を見るのはネタバレ注意です！ まずはゲームを何度か周回してから開くことをおすすめします！

## 導入方法

### かんたん導入（推奨）

導入方法はすっごく簡単です。

1. [Releases](https://github.com/TomXV/dragnwash-localization/releases) からzipをダウンロードして展開する
2. **`Install.exe` をダブルクリック**
3. 言語を選んで、**「インストール」をポチッ**（入っていれば「更新」）

> [!TIP]
> 同じ手順は Steam のガイドにも載せています（[日本語](https://steamcommunity.com/sharedfiles/filedetails/?id=3801418794) / [English](https://steamcommunity.com/sharedfiles/filedetails/?id=3801420947)）。Drag'n Wash には Steam ワークショップがないので、Mod 本体は GitHub の Releases からダウンロードしてください。

## ね？ 簡単でしょ？ ( ･´ｰ･｀)ドヤッ

ゲームフォルダーはSteamから勝手に見つけてくれます。見つからなかったときも、自分で選ぶだけです。
BepInExが入っていなければ、公式の5.4.23.5を自動でダウンロードし、SHA-256で確かめてから入れます。
このModが動く土台のDrag'n Wash ModFrameworkも、ゲームのフォルダーに新しいものが入っていなければ、同じようにフレームワークのGitHubのリリースから取ってきます。
ダウンロードする前には、ちゃんと確認が出ます。
あとはSteamからゲームを起動するだけです。

言語は「日本語」「简体中文」「English（翻訳しない）」に加えて、
ネイティブが校正した韓国語・ネイティブが翻訳したトルコ語・仮翻訳の繁体字中国語・ドイツ語・フランス語・スペイン語・ブラジルポルトガル語・
ロシア語・ポーランド語・ヘブライ語・ウクライナ語・タイ語・ベトナム語、面白枠のエスペラント・トキポナから選べます
（詳しくは[言語パック](#言語パック)で）。
同じ画面には「アンインストール」ボタンもあって、セーブ履歴は既定では消さずに残します。
BepInExも、ほかにModが入っていなければ、選べば一緒に消せます。
ゲームの中の **Options → Mods → Drag'n Wash Localization → Uninstall** からもアンインストールでき、その場合は次にゲームを起動したときに消えます。

手動で入れたいときは、下の手順を見てください。

> [!NOTE]
> **`Install.exe` を押しても何も起きない、または「Windows によって PC が保護されました」と出る場合**
> `Install.exe` は署名のない小さなプログラムなので、初回だけ Windows SmartScreen が止めることがあります。
>
> - 警告画面が出たら **「詳細情報」→「実行」** を押してください
> - 何も出ないときは、`Install.exe` を右クリック → プロパティ → 一番下の **「許可する」** にチェック → OK のあと、もう一度ダブルクリック

> [!WARNING]
> **Windows セキュリティ（Microsoft Defender）が `Install.exe` を「Trojan:Script/Wacatac.B!ml」と検出する場合、または展開したフォルダーに `Install.exe` が見当たらない場合**
> これは誤検知です。末尾の `!ml` は、機械学習で「怪しいパターンに似ている」と推定されたという意味で、既知のウイルスと合ったわけではありません。v1.0.0 までのインストーラーは PowerShell のスクリプトを黒い窓を出さずに起動していて、その起動のしかたがマルウェアの手口に似ていました。v1.1.0 からの `Install.exe` は Drag'n Wash ModFramework の共通インストーラーで、スクリプトを動かさない普通の署名なしプログラムです。それでも、新しい署名なしのファイルは検出されることがあります。中身はフレームワークのリポジトリの [`installer/`](https://github.com/TomXV/dragnwash-modframework/tree/main/installer) で公開しています。
>
> 自動で隔離されたときは検出名が出ず、展開したフォルダーから `Install.exe` が消えているように見えるだけです。何が消されたかは、Windows セキュリティの「保護の履歴」で確かめられます。
>
> - まず、ダウンロードした zip が本物か確かめてください。PowerShell で `(Get-FileHash "<zip のパス>").Hash -eq ("<Releases の sha256>" -replace '^sha256:')` を実行して `True` と出れば、このリポジトリで配布しているファイルです。`False` なら使わずに削除してください
> - `sha256` は [Releases](https://github.com/TomXV/dragnwash-localization/releases) のページで `DragNWashLocalization-<version>.zip` の下に出ています。自動で付く「Source code」の2行にはハッシュがないので、そちらは使わないでください
> - ハッシュが合って分かるのは「配布しているファイルと同じもの」ということだけで、安全だという証明にはなりません。中身は上のソースで確かめられます
> - 合っていれば、Windows セキュリティの「保護の履歴」でその検出を開き、「操作」→「デバイスで許可」を選ぶと使えます。許可するのはこのファイルだけにしてください。フォルダーの除外を追加したり、Windows セキュリティを無効にしたりする必要はありません
> - 何も許可したくない場合は、下の「手動で導入する」の手順でも入れられます
> - GitHub の Releases 以外から手に入れたファイルは使わないでください

### Windows on ARM（動作確認済み）

Snapdragon XなどのARM版Windowsでも、上と同じ `Install.exe` の手順で入れられて、Modも動きます。
ゲームのほうは、x64版がエミュレーションで動いています。

> [!IMPORTANT]
> **ゲーム本体が DirectX 12 では正しく動かないため、Steam の起動オプションに `-force-d3d11` を入れてください。**
> Mod を入れていなくても起きる、ゲーム側の問題です。
>
> - GPU ドライバが古いと、スプラッシュ画面のあとにゲームが落ちます
> - 最新のドライバでは落ちなくなりますが、3D が描画されません
>
> Steam でゲームを右クリック → **プロパティ** → **起動オプション** に `-force-d3d11` と入力すると、DirectX 11 で起動して普通に遊べます。
> 確認環境: ASUS ProArt PZ13（Snapdragon X Plus / Adreno X1-45）

### Steam Deck / Linux（動作確認済み）

> [!IMPORTANT]
> Steam Deck で動くのは **v0.3.0 以降**です。
> それより前のバージョンでも起動はしますが、Deck では F1 メニューを操作できません。

Linuxネイティブ版のゲームとLinux版BepInExで動きます。
`Install.exe` はWindows用なので、Deckではインストールスクリプトを使います。

おすすめは**インストールスクリプト**です。作業はデスクトップモードで行います。

1. [Releases ページ](https://github.com/TomXV/dragnwash-localization/releases)から
   zipをダウンロードして展開する（右クリック → 展開）
2. 展開したフォルダーを開き、何もない所を右クリックして **ここでターミナルを開く** を選ぶ
3. 次の1行を入力してEnter

   ```bash
   bash install-steamdeck.sh
   ```

4. **インストール / 更新** を選び、言語を選ぶ。
   起動オプションを設定するのにSteamを一度終了させる必要があるので、その前に確認が出ます（設定が済んだらSteamを起動し直します）
5. ゲームモードに戻ってゲームを起動する。言語はあとから **Options → 言語（Mod）** で変えられます

スクリプトはSteamのライブラリからゲームを探し（SDカードの中も探します）、
公式のLinux版BepInEx 5.4.23.5をダウンロードしてSHA-256で確かめます。
Drag'n Wash ModFrameworkも、新しいものが入っていなければ同じように取ってきます。
それから `run_bepinex.sh` の `executable_name="DragNWash"` を設定してModをコピーし、
ゲームの起動オプションに `./run_bepinex.sh %command%` を足します。前から設定してあるオプションはそのまま残ります。
更新や削除も同じコマンドで行い、**インストール / 更新** か **アンインストール** を選ぶだけです。
アンインストールしてもセーブ履歴は残します。BepInExを使うModがほかになければ、起動オプションから `./run_bepinex.sh` を外して、
BepInExも消すかどうか聞いてきます。`--install` や `--uninstall` を付けると、この質問を飛ばせます。

Steamは起動中に起動オプションを上書きしてしまうので、起動オプションを変えるときはSteamを一度終了してから書き換え、
Steamを起動し直します。終了する前に確認が出ますが、`--close-steam` を付ければ飛ばせます。
うまくいかなかった手順があれば最後のダイアログに出して、手で変える内容を案内します。
スクリプトがやったことは `~/.local/state/dragnwash-installer/installer.log` に記録されます。

<details>
<summary>Deck に手動で導入する場合</summary>

1. [BepInEx_linux_x64_5.4.23.5.zip](https://github.com/BepInEx/BepInEx/releases/download/v5.4.23.5/BepInEx_linux_x64_5.4.23.5.zip) を
   ゲームフォルダー（`~/.local/share/Steam/steamapps/common/Drag'n Wash/`）に展開する。
2. このModのzipの `BepInEx/` を同じ場所に重ねる。
   Drag'n Wash ModFrameworkも重ねる（下の「手動で導入する」の3.を参照）。
3. `run_bepinex.sh` を開き、`executable_name="DragNWash"` にして保存。
   `chmod +x run_bepinex.sh` で実行権限を付ける。
4. Steamのゲームのプロパティ → 起動オプションに `./run_bepinex.sh %command%`
5. 起動する。
   言語は **Options → 言語（Mod）** で変えられます。

</details>

フォントの準備は要りません。
ゲーム本編の文字は、SteamOSに最初から入っているNoto Sans CJK（日本語・中国語・韓国語の各書体）をファイルから直接読みます。
F1メニューのほうは同梱のNoto Sans JP（`dragnwash-menufont.bundle`）で描いています。
SteamのLinuxランタイムの中だと、Unityのメニュー描画からCJKフォントが見えないからです。
このメニュー用フォントにはハングルとヘブライ文字がないので、DeckのF1の言語ボタンではこの2言語だけロケールコードで表示されます。
ゲーム本編のほうは普通に表示されます。

Deckで言語を変えるときは、コントローラーで **Options → 言語（Mod）** を使えばF1キーは要りません。
翻訳者向けのF1メニューをゲーミングモードで使うときは、次のように操作します。

- **F1** をSteam Inputでボタンに割り当てて開く
- 右トラックパッドかタッチ画面でポインターを合わせ、
  **A**・**R2**・トラックパッド押し込みのどれかで押す（Steam Inputはトラックパッド押し込みを
  マウスクリックではなくスティック押し込みとして送るので、Mod側で対応しています）
- タイトルバーでボタンを押したまま動かすと移動、右下の角で同じようにすると大きさを変えられます
- スティックと十字キーで、ポインターが乗っている一覧をスクロール

> [!WARNING]
> **macOS では今のところ、実験的な方法でしか動きません。**
> Drag'n Wash は Unity 6.3 で作られていて、macOS 版 BepInEx 5.4.23.5 に入っている読み込み役（Doorstop）は、
> Unity 6.3 のゲームに割り込めません（[NeighTools/UnityDoorstop#108](https://github.com/NeighTools/UnityDoorstop/issues/108)）。
> Doorstop 自体はゲームに読み込まれますが BepInEx が起動せず、`BepInEx/LogOutput.log` も `BepInEx/config` も作られないまま、
> ゲームは英語で始まります。
> Apple M3 Pro / macOS 26.6 で、Apple シリコンのままでも Rosetta でも同じ結果になることを確かめました。
> この不具合の修正（[NeighTools/UnityDoorstop#117](https://github.com/NeighTools/UnityDoorstop/pull/117)）が入っているのは、今は UnityDoorstop の `ci` プレリリースだけです。
> また、Apple シリコンのネイティブ実行では BepInEx 5.4.23.5 がパッチを当てられず、
> その修正（[BepInEx/BepInEx#1402](https://github.com/BepInEx/BepInEx/pull/1402)）もまだリリースされていません。
>
> 今のところの回避策を入れる**実験的な** macOS 用インストールスクリプトを、リポジトリに置いてあります
> （[`installer/experimental/install-macos.sh`](installer/experimental/install-macos.sh)）。
> リリースの zip には入っていません。
> macOS 版 BepInEx 5.4.23.5 を、UnityDoorstop の `ci` プレリリースの Doorstop（ゲームで試した 4.6.0 のビルドに固定）と組み合わせて入れ、
> ゲームを Rosetta（x86_64）で動かします。
> また、Steam オーバーレイをゲームへ引き継ぎ、Shift+Tab で開けるようにします。
> この引き継ぎは、2026-09-25 に UnityDoorstop にも取り込まれました（[NeighTools/UnityDoorstop#121](https://github.com/NeighTools/UnityDoorstop/pull/121)、4.6.0 に収録）。
> 固定しているビルドを含め、Doorstop 4.6.0 以降では、スクリプトによる書き換えは要らないので行いません。
> Steam Deck 用スクリプトと同じく、Drag'n Wash ModFramework・Mod・言語・Steam の起動オプションも設定し、
> ゲームを起動したあとで Mod が読み込まれたかを確かめる **動作確認** もできます。
> `ci` プレリリースは、master ブランチが変わるたびに同じ URL のまま作り直されるので、そこに固定したファイルは変わったり、なくなったりすることがあります。
> 前に固定していた 4.5.0 のビルドは、2026-09-25 に `ci` が 4.6.0 に替わってから、もうダウンロードできません。
> そのため、スクリプトは 4.6.0 のビルドを、223n のフォークにプレリリースとして手を加えずに置いたコピー（[223n/UnityDoorstop `ci-4.6.0-97293a28`](https://github.com/223n/UnityDoorstop/releases/tag/ci-4.6.0-97293a28)）からダウンロードし、SHA-256 を確かめます。
> UnityDoorstop 4.6.0 の安定版が出て試せたら、そちらに切り替えます。
> 進み具合は [TomXV/dragnwash-modframework#85](https://github.com/TomXV/dragnwash-modframework/issues/85) で確かめられます。
> ダウンロードした Doorstop が一致しないときや、もうダウンロードできない（HTTP 404）ときは、スクリプトはゲームのフォルダーを変えずに止まり、この issue を案内します。
> 安定版の 4.6.0 には、UnityDoorstop の修正（[NeighTools/UnityDoorstop#117](https://github.com/NeighTools/UnityDoorstop/pull/117) と [NeighTools/UnityDoorstop#114](https://github.com/NeighTools/UnityDoorstop/pull/114)）が両方とも入る見込みです。
> どちらも、固定しているビルドにすでに入っているためです。
> そのあとにスクリプトが待つのは、BepInEx の arm64 向けの修正（[BepInEx/BepInEx#1288](https://github.com/BepInEx/BepInEx/pull/1288) と [BepInEx/BepInEx#1402](https://github.com/BepInEx/BepInEx/pull/1402)）が入った BepInEx のリリースだけで、出たらそちらに切り替えます。
> そのリリースなら、Rosetta なしのネイティブで動く見込みです（まだ試していません）。
> この組み合わせは、固定している 4.6.0 のビルドも含めて、Apple A18 Pro / macOS 27.2 で手作業で確かめました。
> スクリプトは、同じ Mac の実際のゲームで、手作業で入れた環境の上から 1 回実行しました（すでに入っていたので、何もダウンロードしていません）。
> 何も入っていない状態からのインストールは、ゲームのフォルダーのテスト用コピーでしか動かしていません。
> ゲームは Steam から起動してください。ターミナルから起動すると、セーブが見つかりません。
> 起動中にゲームのウィンドウが前面にないと、タイトル画面の前で止まったように見えることがあります。ウィンドウをクリックすると進みます（同じ Mac で確かめました）。

### 手動で導入する

### 必要なもの

- Steam版（Windows）のDrag'n Wash
- [BepInEx 5 Windows x64（Mono）版](https://github.com/BepInEx/BepInEx/releases)
- このリポジトリの [Releasesページ](https://github.com/TomXV/dragnwash-localization/releases) で配布している
  最新版の `DragNWashLocalization-<version>.zip`
- Drag'n Wash ModFrameworkの [Releasesページ](https://github.com/TomXV/dragnwash-modframework/releases) の
  `DragNWash.ModFramework-<version>.zip`（Modのzipの `mod-install.json` にある `framework.version` の版か、それより新しいもの）

> [!IMPORTANT]
> ReleasesのAssetsにある `DragNWashLocalization-<version>.zip` を使ってください。
> GitHubが自動で作る **Source code** のZIPは、Modを入れるためのものではありません。
> ReleasesページにModのZIPがまだないなら、入れられるビルドはまだ公開されていません。

### 1. ゲームフォルダーを開く

Steamライブラリで **Drag'n Washを右クリック → 管理 → ローカルファイルを閲覧** を選びます。
ゲームの `.exe` が置いてあるフォルダーがゲームルートです。

### 2. BepInExを導入する

BepInEx 5の **Windows x64（Mono）版**をダウンロードし、アーカイブの中身をゲームルートにそのまま展開します。

展開したら、ゲームの実行ファイルと同じ場所に `winhttp.dll`、`doorstop_config.ini`、`BepInEx` フォルダーが並んでいるか確かめてください。
もう1段内側のフォルダーに入ってしまっていたら、ゲームルートに移します。

ゲームを一度起動して、タイトル画面まで進んだら終了します。
これでBepInExの設定ファイルとログができるので、次に進む前に `BepInEx/LogOutput.log` があるか確かめます。

### 3. Drag'n Wash Localizationを導入する

[Releases](https://github.com/TomXV/dragnwash-localization/releases) から `DragNWashLocalization-<version>.zip` をダウンロードして、
BepInExと同じ**ゲームルート**に展開します。`BepInEx` フォルダーを統合するか聞かれたら、許可してください。

プラグインのDLLが次の場所にあれば、ちゃんと展開できています。

```text
<Drag'n Washのフォルダー>/BepInEx/plugins/DragNWashLocalization/DragNWashLocalization.dll
```

このModが動く土台のDrag'n Wash ModFrameworkは、このzipには入っていません。
フレームワークのzipから、次のものも同じゲームルートにコピーしてください。
フォルダー `BepInEx/plugins/DragNWash.ModFramework`、`DragNWash.ModFramework.Text`、
`.Dialogue`、`.ToolWindow`、`.Assets`、`.Saves` と、`BepInEx/patchers/DragNWash.ModFramework.Preloader.dll` です。
ほかのModがもっと新しいModFrameworkを入れているときは、新しいほうを残してください。

`plugins` とDLLの間に、ZIPファイルや `DragNWashLocalization-<version>` フォルダーが挟まらないようにしてください。

### 4. 起動して確認する

Drag'n Washを起動します。手動で入れた場合は日本語で始まります（インストーラーを使った場合は、そこで選んだ言語）。

言語を変えるときは **Options** を開き、ゲームプレイの項目の最後にある **言語（Mod）** を使います。
選んだ時点でその言語に切り替わり、**Save** で確定、**Back** なら保存済みの言語に戻ります。
マウスでもゲームパッドでも操作できて、Steam Deckでも使えます。**F1** ウィンドウの **Translation** タブからも切り替えられ、
こちらはその場で確定します。

`BepInEx/LogOutput.log` に `DragNWashLocalization` の起動行が出ていれば、プラグインは読み込まれています。

起動時の言語を手で変えたいときは、ゲームを終了してから次のファイルを開きます。

```text
BepInEx/config/com.tomxv.dragnwash.localization.cfg
```

`[General]` の `TargetLocale` を `ja` や `zh-Hans` など、入っているロケールに変えて、
ゲームを起動し直します。`en` にすると、Modを入れたまま英語の原文で遊べます。
同じことはインストーラーやOptionsの言語（Mod）、F1メニューでも選べます。

### Modが読み込まれない場合

- BepInExとModを両方とも、ゲームの実行ファイルがあるフォルダーに展開したか確かめてください。
- DLLが上のパスにあるか見てください。
- `BepInEx/LogOutput.log` を開いてみてください。ファイル自体がなければ、BepInExが起動していません。
  ファイルがあれば `DragNWashLocalization` で検索して、その近くに出ているエラーを見ます。
- Optionsを開いたときにDirect3D 12でクラッシュする場合は、
  [設定画面を開くとクラッシュする場合（Windows）](#設定画面を開くとクラッシュする場合windows) の回避策を試してください。
- ゲームのファイルを上書きするタイプの翻訳（`DragNWash_Data` にファイルをコピーするものなど）を入れていた場合、
  ゲームの英文がもう置き換わっているので、このModは訳す相手を見つけられません。
  先にSteamでゲームを右クリック → **プロパティ** → **インストール済みファイル** → **ゲームファイルの整合性を確認** で
  元のファイルに戻してから、このModを入れ直してください。
  そうした翻訳はゲームのアップデートで消えたり壊れたりしますが、このModはゲームのファイルを一切書き換えません。

## 言語パック

翻訳ファイルはTomXVが作っていて、同じzipに入っています。
言語パックを良くしてくれた協力者は、下の表のその言語の行に載せます（[協力者のクレジット](CONTRIBUTING.ja.md#協力者のクレジット)も見てください）。
インストーラーとF1メニューは、`Translations/` の下にあるフォルダーを自動で一覧にします。

| ロケール  | 言語               | 状態                                                                 |
|-----------|--------------------|----------------------------------------------------------------------|
| `ja`      | 日本語             | 作者が監修                                                           |
| `zh-Hans` | 简体中文           | 作者が監修                                                           |
| `zh-Hant` | 繁體中文           | 仮翻訳（監修済みの簡体字版を台湾の言い回しで繁体字化）               |
| `de`      | Deutsch            | 仮翻訳                                                               |
| `fr`      | Français           | 仮翻訳                                                               |
| `es`      | Español            | 仮翻訳                                                               |
| `pt-BR`   | Português (Brasil) | 仮翻訳                                                               |
| `ko`      | 한국어             | ネイティブ校正済み、Hotcake（ゲームで使われない行はそのまま）        |
| `ru`      | Русский            | 仮翻訳                                                               |
| `pl`      | Polski             | 仮翻訳                                                               |
| `he`      | עברית              | 仮翻訳（右から左に表示）                                             |
| `uk`      | Українська         | 仮翻訳                                                               |
| `th`      | ไทย                | 仮翻訳（単語の間にゼロ幅スペースを入れて折り返せるようにしています） |
| `vi`      | Tiếng Việt         | 仮翻訳                                                               |
| `tr`      | Türkçe             | ネイティブが翻訳、[DarkBloodKing](https://github.com/Darkbloodking00) |
| `eo`      | Esperanto          | 仮翻訳（面白枠）                                                     |
| `tok`     | toki pona          | 仮翻訳（面白枠。単語が 137 個しかない言語なので、かなりざっくり）    |
| `en`      | English            | ゲーム本来の英語（翻訳なし）                                         |

> [!NOTE]
> **仮翻訳**は、ネイティブスピーカーに監修してもらっていません。
> 全行そろっていて通しで遊べますが、言い回しが不自然だったり、ジョークを取りこぼしていたりするかもしれません。
> 作者が監修したのは日本語と簡体字中国語だけです。
> ネイティブの方からの修正 PR は大歓迎です（[CONTRIBUTING.ja.md](CONTRIBUTING.ja.md)）。
> 各 `strings.csv` の先頭にも、同じ注意書きをコメントで入れてあります。

## 翻訳者向け

翻訳は `Translations/<locale>/strings.csv` を編集するだけで追加できます。公開ファイルの列は
`key,section,node,order,speaker,translation` です。
`key` は英語原文のハッシュで、`section` / `node` / `order` はその行がゲームのどこ（レベルと会話）で流れるかをプレイ順で表し、
`speaker` は誰の台詞かを表します。
`#` で始まる行は `# ===== Level 1: Ryan (Sunny) =====` のような見出しで、ファイルを上から読むと台本のように流れが追えます。

おすすめの進め方はこうです。

1. ゲーム内で **F1 → Translation → Export working copy** を押す。`Translations/_discovered/<locale>.working.csv`に、
   各行の英語原文を並べた作業用ファイル（`key,section,node,order,speaker,source_en,translation`）が、
   同じ見出しつきでゲーム内で流れる順に書き出されます
2. `translation` 列を編集して保存する。起動中のゲームにその場で反映されます
3. コミット前に **F1 → Translation → Hash for commit**（または `tools/hash-strings.ps1`）で、
   英語原文を含まない `strings.csv` を作り直す

リポジトリにはゲームの英語台本を入れない方針なので、**製品版を持っている人だけが翻訳できる**仕組みになっています。
各言語フォルダーには表示名を1行だけ書いた `name.txt`（例: `日本語`）があって、それがインストーラーとゲーム内メニューに出ます。
Unity内部のキー名などは知らなくても大丈夫です。手順は [CONTRIBUTING.ja.md](CONTRIBUTING.ja.md) にあります。
原文に書式タグ（`<size=70%>`など）が入っているときは、タグはそのまま残して中の文章だけ訳してください。

### 会話文をまとめて確認したい場合

ここからは開発者向けの機能なので、
先に **Options → Mods → Drag'n Wash ModFramework → Developer tools** をオンにしてください
（遊ぶだけの人向けにオフになっていて、オフのままだと以下は何も動きません）。
そのうえで、セーブをロードしてからゲーム内で **F6キー** を押すと、
全会話文が`BepInEx/plugins/DragNWashLocalization/Translations/_discovered/dialogue_lines.csv`に
まとめて書き出されます（実機で1839行出ることを確認済み）。

行は**ゲーム内で実際に流れる順**に並びます。
`node` 列が会話の単位で、`Alexander_2_intro` のように「キャラクター名＿何回目＿場面」の形になっていて、
`order` 列がその会話の中での順番です。
`kind` 列を見ると、`line`（キャラクターの台詞）か`option`（プレイヤーが選ぶ選択肢）かが分かります。
誰が誰に何と答えているかがわかるので、前後を見ながら訳せます。
登場するドラゴンはConrad / Ryan / Alexanderの3体です。

訳したい行を `Translations/<locale>/strings.csv` にコピーして `translation` 列を埋め、
実機で確かめてください（`node` や `key` など余分な列が付いたままでも、プラグインはちゃんと読み込みます）。

**PR を送る前に、公開用ファイルを作り直してください。**
ゲーム内の **Hash for commit** ボタンか `tools/hash-strings.ps1` を使います。
自動チェックが受け付けるのは公開形式のヘッダーだけで、
*Hash for commit* が書く`key,section,node,order,speaker,translation` のほかに、
短い `key,speaker,translation` と `key,translation`も通ります。
作業ファイルのヘッダーはどれにも当てはまらないので、`source_en` などダンプから来た列が残ったファイルは弾かれます。
英語原文を含むPRを作らないというのは、このリポジトリの
一番大事な前提でもあります。詳しくは [CONTRIBUTING.ja.md](CONTRIBUTING.ja.md#コミット前にハッシュ化する) を見てください。

すでに訳した行は `translation` 列に訳が入った状態で書き出されるので、
ダンプし直しても作業は消えません。

### 編集した訳を再起動なしで確認する

ゲームを起動したまま `Translations/<現在の言語>/strings.csv`
か作業用ファイル `_discovered/<locale>.working.csv` を保存すると、
2秒ほどで自動的に読み直されて、画面に出ているテキストにもその場で反映されます。
変わった行はF1のActivity logに1行ずつ出ます。
訳を直しては画面で確かめる、というのを再起動なしで繰り返せます（`[Debug] HotReloadTranslations`で無効にできます）。
反映されたかどうかは、F1のActivity logに `[reload]` として出ます。

### UI文言をまとめて確認したい場合

**F7キー** を押すと、読み込まれている全UIテキストが`Translations/_discovered/ui_texts.csv` に書き出されます。
**非表示のメニューも対象**なので、ポーズメニューや確認ダイアログを開かなくても、そのシーンのUI文言が丸ごと手に入ります。
列は `key` / `source_en` / `translation`（訳済みなら既存の訳）/ `object_path`（画面上のどこか）です。

タイトル画面とゲーム中で1回ずつ押せば、ほぼすべてのUIが揃います。

まだ訳していないUI文言は、遊んでいるあいだに自動で`Translations/_discovered/strings.csv` にも記録されます。
このファイルは起動するたびに整理されて、訳した行や重複は取り除かれるので、いつも「残りの作業リスト」になっています。

### 訳す必要のない文字列について

スライダーの数値や解像度（`1920 x 1080 @ 164.995Hz`）、ビルド番号などは、はじめから記録しません。
除外パターンは [Translations/ignore.txt](Translations/ignore.txt)に書き足せます（正規表現で書きます。ファイルの中に例があります）。

除外されるのは「記録」だけです。
訳を探すほうが先なので、`strings.csv` に書いた行は、除外パターンに合っていても必ず翻訳されます。

### ゲーム内デバッグメニュー

**F1キー** で、Drag'n Wash ModFrameworkを使うほかのModと共通のツールウィンドウを開いたり閉じたりできます
（キーは `BepInEx/config/com.tomxv.dragnwash.modframework.toolwindow.cfg` の `[General] ToggleKey`）。
タイトル部分をドラッグすると移動、右下の角をドラッグするとサイズを変えられます。

このModは、次の4つのタブを足します。

- **Activity log**:
  翻訳結果と処理のログが出ます。
  `Follow: ON/OFF` で末尾に自動でついていくかを切り替えられ、自分でスクロールするとついていくのが止まります。
  `Clear log` で表示と重複抑制をリセットします。ログは直近100件まで残ります。
- **Translation**:
  再起動なしで言語を切り替えられます（ボタンの名前は各言語の `name.txt` から取っていて、**English** は翻訳オフ）。
  会話の書き出し（`Export loaded dialogue`）、UI文言の書き出し（`Export UI text`）、
  原文つき作業ファイルの書き出し（`Export working copy`）、公開ファイルの作り直し（`Hash for commit`）、
  レイアウトチェックもここでできます。
- **Saves**:
  セーブの巻き戻し、レベル番号の変更、フラグの反転ができます。
  詳しくは下で説明します。
- **About**:
  動いている版とビルド、制作者、ライセンス、今回の起動で読み込んだ内容が見られます。
  不具合を報告するときに、そのまま貼り付けられます。

**Check translation layout** ボタンを押すと、レイアウトが崩れそうな文字列を
`Translations/_discovered/layout_risks.csv` に書き出せます
（しきい値は`BepInEx/config/.../LayoutOverflowThreshold`で、既定の `1.0` がちょうど収まる大きさです）。

### セーブを1つ前に戻す（翻訳確認用）

ゲームがセーブを書き込むたびに、プラグインが`BepInEx/SaveHistory/<スロット>/` に世代ごとのコピーを残します
（スロットごとに既定で30世代。`BepInEx/config/com.tomxv.dragnwash.modframework.saves.cfg` の `[History] Keep` かMods画面で変えられます）。
前の版が `BepInEx/plugins/DragNWashLocalization/SaveHistory` に残したコピーは、最初に起動したときにそこへ移されます。
**F1 → Saves** タブでスロットを選び、戻したい世代の **Restore** を押すと、
その世代がゲームのセーブファイルに書き戻されます。
**その後タイトル画面に戻ってスロットをロード**すると反映されます。ゲーム内でセーブすると、また上書きされます。
戻す前の状態も自動で世代として残るので、戻しすぎてもやり直せます。

同じ場面の会話を訳し直して見比べたいときに使ってください。
Restoreはフラグや変数を直接いじるわけではありません。ゲーム自身のセーブファイルを差し替えるだけです。

同じタブの **PROGRESS** 欄では、レベル番号を **-** / **+** で変えて **Apply** できます。
先に進めるほうはネタバレになるかもしれないので、確認が出ます。
**Flags...** を押すと、ゲームが使うイベントフラグが分類
（レベル進行 / ストーリー / 恋愛 / シーン発生条件 / シーン視聴済み / 洗浄セッション / アイテム / デバッグ）と
説明つきで全部並びます。
セーブにまだないフラグも「unset」として出て、値をクリックするとunset → true → falseの順に切り替わります。
検索欄で絞り込めて、**Reset all to false...** で全フラグをまとめてfalseにできます（レベル番号はそのまま）。
一覧はDLLの隣にある `FlagCatalog.csv` から読んでいるので、あとで見つけたフラグは行を足せば追加できます。
どの編集も、直前の状態をスナップショットに残してから行います。

## 設定画面を開くとクラッシュする場合（Windows）

Unity 6000.3.14f1 / DirectX 12の環境で、Optionsを開いたときに `D3D12ScratchAllocator::DestroyScratch` で落ちる問題がありました。
同じスタックの[Unity公式の不具合報告（UUM-140564）](https://issuetracker.unity.com/issues/10698)も出ていて、
ネイティブの描画側のバグなので、翻訳フックで例外を捕まえても防げません。

このバグを踏むのは**実行中のテクスチャ確保・アップロード**のときです。
なのでDirect3D 12では、入っている全言語のフォントを起動時に用意しておき、
ゲーム中も、OptionsやF1メニューで言語を切り替えたときも、フォントアトラスに何も足さないようにしています。
各文字はその言語が使うフォント1つにだけ焼き込むので、起動時の処理は軽く済みます。
言語を何度切り替えても落ちないことは実機で確かめてあります。設定は要りません。

Direct3D 11やSteam DeckのVulkanなど、ほかの描画APIは実行中のアップロードでも大丈夫なので、
使っている言語だけを用意して、ほかの言語は選んだときに読み込みます。
そちらでも起動時に全部用意しておきたいなら、`[Font] PreloadAllLocales = true` にしてください。

それでも落ちる場合は、Steamライブラリで **Drag'n Wash → プロパティ → 一般 → 起動オプション** に
`-force-d3d11` を足して再起動すると、描画APIごと切り替わるのでバグを避けられます
（[Unity標準の起動オプション](https://docs.unity3d.com/6000.3/Documentation/Manual/PlayerCommandLineArguments.html)で、
ゲーム本体のDLLやセーブデータは変わりません）。
実際に使われている描画APIは、`BepInEx/LogOutput.log` のプラグイン起動行に`graphics=...` として出ます。

`BepInEx/config/com.tomxv.dragnwash.modframework.assets.cfg` の `[Fonts] AtlasPointSize` を下げると
フォントアトラスの枚数が減り、上げると文字がくっきりします（既定は80）。

## 排他的フルスクリーンで画面を切り替えると固まる場合（Windows）

DirectX 12で **ウィンドウモード** を **排他的フルスクリーン** にしていると、
別のウィンドウに切り替えて（Alt+Tabや、ほかのウィンドウをクリック）戻ったときにゲームが固まり、そのまま落ちることがあります。
クラッシュの記録を見ると、Windowsが排他的フルスクリーンを解除・再開するあいだに、UnityのDirectX 12の画面表示が止まっています
（`D3D12SwapChain::Present` がエラー `887a0001` で失敗していて、その前のログに `D3D12Fence::Wait ... May cause crash` が出ていることが多いです）。
このときModのコードは動いていないので、このModではなく、ゲームの描画処理の問題です。

次のどちらかで避けられます。

- **ウィンドウモード** を **排他的フルスクリーン** ではなく **フルスクリーン** にする。
  画面を切り替えたときに表示モードが変わるのは排他的フルスクリーンだけです。
  2026年9月20日に試したところ、DirectX 12でSteamオーバーレイも有効のまま、
  この設定で何度切り替えても固まらず、ログにも画面表示まわりのエラーは1行も出ませんでした。
- Steamライブラリで **Drag'n Wash → プロパティ → 一般 → 起動オプション** に `-force-d3d11` を追加する。
  こちらも試してあり、排他的フルスクリーンのまま切り替えても固まらなくなります。

> [!NOTE]
> 2026 年 9 月 20 日には、**排他的フルスクリーン**でも固まりませんでした。
> 最初に見つかったときと同じ PC で、グラフィックドライバーも Steam オーバーレイもそのままです。
> 何が変わったのかは分かっていません（フレームワーク 1.3.0 の DirectX 12 の修正は、これとは別のクラッシュを直したものです）。
> なので、この項目は残しておきます。
> 画面を切り替えて固まるようなら、上の 2 つのどちらかで避けてください。

## 現在のステータス

最新のリリースは **v1.5.0** です。
v1.5.0 では、F1 のウィンドウのアクティビティログ、翻訳、Saves、About の各タブを 1 つずつ見直し、
新しい Mods 画面の文言も全部の言語パックで訳しました。
zip には Drag'n Wash ModFramework を入れなくなり、
`Install.exe` と `install-steamdeck.sh` が ModFramework 1.5.0 を専用のリリースから取ってきて、確かめてから入れます。

**v1.4.0** では、ほかのModのテキストも翻訳できるようになり
（実験的なβ版で、既定はオフです。[#28](https://github.com/TomXV/dragnwash-localization/issues/28)）、
言語ごとに絵を差し替えられるようにもなりました
（[#4](https://github.com/TomXV/dragnwash-localization/issues/4)。
仕組みだけで、絵はまだありません）。
土台のDrag'n Wash ModFrameworkは **1.4.0** で、コードのないMod、操作の登録簿、Bridgeが入っています。

**v1.3.0** では、Direct3D 12でのクラッシュが減り、落ちたときは何が起きたかをウィンドウで知らせるようになりました
（Drag'n Wash ModFramework 1.3.0を同梱）。

**v1.2.1** では、2026年9月14日のゲームのアップデート以降に作ったセーブがSavesタブにまた出るようにしました。
あわせて、翻訳者の作業用ファイルに、開いていなかった画面の英語も入るようにしています。

**v1.2.0** では、ウクライナ語・タイ語・ベトナム語が加わって16言語になり、
韓国語のネイティブ校正、ゲームの更新で消えにくい翻訳、ロゴ、Drag'n Wash ModFramework 1.2.0が入りました。

**v1.1.2** では、Drag'n Wash ModFramework 1.1.2を同梱し、Mods画面のアイコンを手作りのロゴに差し替えました。

**v1.1.1** では、ModFramework 1.1.1を同梱し、Mods画面にフレームワークのアイコンを出すようにしました。

**v1.1.0** では、ModFramework 1.1.0と合わせて、このModの新しいリリースが出るとMods画面とタイトル画面で知らせるようにしました。

**v1.0.0** で [Drag'n Wash ModFramework](https://github.com/TomXV/dragnwash-modframework) の上で動くようになり、Mods画面が加わりました。

**v0.6.2** では、古い作業用ファイルで「Hash for commit」をしても行が消えないように直し、「Really Delete Save?」も訳しました。

**v0.6.1** では、ヘブライ語で訳のない名前などが反転して表示される不具合を直しました。

**v0.6.0** で台詞IDごとに訳せるようになり、複数のキャラが話す同じ英文を、キャラごとに訳し分けられるようになりました
（2026年9月14日のゲームのアップデートで動作確認済み）。

**v0.5.0** で、ゲームのOptions画面から言語を変えられるようになりました。

**v0.4.0** で13言語になり、言語ごとのフォント準備、Aboutタブ、日英中に切り替えられるインストーラーが入りました。

**v0.3.0** でSteam Deckに対応しました。Windows on ARMでも動くことを確かめてあります（ゲーム本体の都合で `-force-d3d11` が要ります）。
macOSは、BepInEx側の既知の不具合のため、今は実験的な方法でしか動きません（[Steam Deck / Linux](#steam-deck--linux動作確認済み) の下の注意書きを見てください）。
BepInExプラグインの骨格、UI文字列・会話文の日本語/中国語差し替え、CJKフォント表示、会話・UIの一括抽出、
ゲーム内デバッグメニュー、レイアウト崩れ検出、翻訳者向けドキュメント、リリース手順も、作って実機で確かめてあります。

詳しくは [docs/PLAN.ja.md](docs/PLAN.ja.md) にあります。

これからの予定（他のModのテキストの翻訳を含む）は [docs/ROADMAP.ja.md](docs/ROADMAP.ja.md) にあります。

## Drag'n Wash ModFramework

**v1.0.0** から、このModは前提Mod **Drag'n Wash ModFramework** の上で動いています。
ほかのDrag'n WashのModも、この上に作れます。

このModがゲームに入り込むために作ってきた仕組みの多くは、ほかのModにも役立つので、フレームワークに移しました。
入っているModを設定やオン・オフと一緒に一覧できる **Mods** 画面（Options → Mods）、ゲームのOptions画面の言語の行、
表示前のテキストの書き換え、台詞や選択肢のイベント、共通のF1ツールウィンドウ、Direct3D 12で安全なフォント、セーブ履歴です。
こうしておくと、ゲームがアップデートされても追いかける必要があるのはフレームワークだけになり、その上に乗るModは動き続けられます。
2026年9月14日のアップデートのような変更も、1か所で吸収できます。

v1.1.0からは、このModやフレームワークの新しいリリースが出ると、タイトル画面に **1 update available in Mods** と出て、
**Options → Mods** からリリースページを開けます。
フレームワークが1日1回、GitHubに最新リリースを問い合わせるだけで、あなたやゲームについての情報は送らず、ダウンロードもしません。
止めたいときは **Mods → Drag'n Wash ModFramework → 設定 → 更新を確認する** をオフにしてください。

プレイヤーの方向けに言うと、リリースのzipにはフレームワークは入っていません。
ゲームのフォルダーに新しいものがなければ、インストーラーがこのModを作ったときの版をGitHubから取ってきます。
v1.1.0からのインストーラーは、どのDrag'n WashのModも同梱できるフレームワークの共通インストーラーです。
このModをアンインストールしても、ほかのModが入っていればフレームワークは残します。

翻訳者の方にとっては、CSVの形式も翻訳用のツールも変わりません。
今までの翻訳パックや協力は、そのまま使えます。

説明書は、フレームワークの [Wiki](https://github.com/TomXV/dragnwash-modframework/wiki/Home-ja) にあります。
Mods画面、クラッシュレポート、F1の開発者ツール、それにこのModには入っていない
[Inspector](https://github.com/TomXV/dragnwash-modframework/wiki/Inspector-ja)（入れ方もWikiにあります）の説明です。

Drag'n WashのModを作っていて、フレームワークに欲しい機能があれば、Issueで教えてください。

## 翻訳に参加する

コードは要らず、`Translations/<locale>/strings.csv` を編集するだけで参加できます。
手順や書式、まだ訳されていない行の見つけ方は [CONTRIBUTING.ja.md](CONTRIBUTING.ja.md) にまとめてあります。

参加するみなさんには[行動規範](docs/CODE_OF_CONDUCT.ja.md)に沿ってもらいます。
セキュリティの問題を見つけたときは、Issueではなく非公開で報告してください（[SECURITY.ja.md](SECURITY.ja.md)）。
それと、もし余裕があれば [GitHub Sponsors](https://github.com/sponsors/TomXV) もあります。
どちらにしても、Modは無料のままですし、翻訳のほうがずっと価値があります。

## 配布・リリース

リリースzipのビルドと配布の手順は [docs/RELEASING.ja.md](docs/RELEASING.ja.md) にあります。
ゲーム由来の参照アセンブリはここにコミットできないので、zipはGitHub Actionsの **Build** ワークフローが、非公開のリポジトリからそれを読み込んでビルドしています。
`v*` のタグをpushするとzipの付いた下書きのリリースができるので、あとは人がノートを書いて公開します。

## 開発者の方へ

このプロジェクトはファンが作った非公式のもので、**Gator Dragon Gamesとは無関係** です。
Drag'n Wash ModFrameworkの[コンテンツポリシー](https://github.com/TomXV/dragnwash-modframework/blob/main/docs/CONTENT_POLICY.ja.md)に沿って、
ゲームのアセットや台本はそのままの形では入れず、英語原文もSHA-256ハッシュとしてしか持っていません。
ゲームのファイルを書き換えることもなく、プラグインはBepInExが実行時に読み込みます。
開発チームの方で気になる点があれば、このリポジトリのIssueかメンテナーへの連絡でお知らせください。
ご希望に合わせて、直すか公開を止めます。

## クレジット

- このModの**ロゴ**（Mods画面のアイコン、`icon.png`）は **Mister ERIO** さん（[@mistererio](https://github.com/mistererio)）が描いたもので、
  許可をいただいて使っています。
- Drag'n Wash ModFrameworkに同梱されているOptions画面の **Mods ボタン**も、Mister ERIOさんの作品です。
- Drag'n Wash ModFrameworkの**ロゴとアイコン**（アイコンはフレームワークに入っています）は、
  **NotaGames** さん（[@NotaGames](https://github.com/NotaGames)）の作品です。
- 韓国語パックは **Hotcake** さんに校正していただきました。
- 言語パックを良くしてくださった翻訳者の方は、[言語パック](#言語パック)の表に載せています。

## ライセンス

プラグインのコードと翻訳文は MIT ライセンスです（[LICENSE](LICENSE)）。
翻訳文を書いたのはそれぞれの翻訳者の方で、お名前は言語パックの表と「クレジット」に載せています。
「クレジット」に挙げた絵は作者のもので、このライセンスの対象外です。
ゲーム本体のアセットやコードは入っていません。
