# strings.csv をゲームの進行順に並べる計画（事前調査と設計）

作成日: 2026-09-12。実機から書き出した `level_flow.csv`（LevelFlow アセット）と
`dialogue_graph.csv`（Yarn コンパイル済みプログラム 8,619 命令 / 195 ノード）に基づく。
書き出しは F1 → Tools → **Export game flow** で再現できる（セーブをロードした状態で押す）。

## 1. 調査でわかったこと

### 1.1 ゲームの進行はすべて `LevelFlow` が決めている

本体の `LevelFlow`（ScriptableObject）に 15 レベル分の `LevelConfiguration` が並び、
各レベルは **どのドラゴンが来て、どの Yarn ノードをいつ走らせるか** を持っている。

| # | ドラゴン | 天候 | intro | 洗っている間 (progress) | 電話 | outro | 立てるフラグ / 終了フラグ |
|---|---|---|---|---|---|---|---|
| 0 | Ryan | Sunny | Ryan_1_intro | Ryan_1_required（nag: Ryan_1_nag） | Ryan_1_PhoneTutorial | Ryan_1_Outro | level_1 / level_1_complete |
| 1 | Conrad | Sunny | Conrad_Intro_1 | Conrad_required_1 | | Conrad_Outro_1 | |
| 2 | Alexander | Sunny | Alexander_1_intro | Alexander_1_required | | Alexander_1_Outro | |
| 3 | Ryan | Sunny | Ryan_2_intro | Ryan_2_required | | Ryan_2_Outro | |
| 4 | Conrad | Rainy | Conrad_Intro_2 | Conrad_required_2 | | Conrad_Outro_2 | level_5 / MedkitCompleted, level_5_complete |
| 5 | Alexander | Night | Alexander_2_intro | Alexander_2_required | | Alexander_2_Outro | level_6_started / |
| 6 | Ryan | Sunny | Ryan_3_intro | Ryan_3_required | | Ryan_3_Outro | |
| 7 | Conrad | Sunny | Conrad_Intro_3 | （idle: Conrad_idle_3_1, _2） | Conrad_3_PhoneTutorial | Conrad_Outro_3 | DeliveredMountFrame / （spawn: PlacedMount） |
| 8 | Alexander | Sunny | Alexander_3_intro | Alexander_3_required | | Alexander_3_Outro | |
| 9 | Ryan | Sunny | Ryan_4_intro | （ピクニック。食べ物ノード） | | | / PicnicCompleted |
| 10 | Conrad | Sunny | Conrad_Intro_4 | Conrad_required_4 | | Conrad_Outro_4 | |
| 11 | Alexander | Rainy | Alexander_4_intro | Alexander_4_required | | Alexander_4_Outro | |
| 12 | Ryan | Sunny | Ryan_5_intro | Ryan_5_Required | | Ryan_5_Outro | |
| 13 | Conrad | Night | Conrad_Intro_5 | Conrad_required_5 | | Conrad_Outro_5 | |
| 14 | Alexander | Sunny | Alexander_5_intro | Alexander_5_required | | Alexander_5_Outro | |

各レベルにはさらに **手コキ開始 / 射精 / マウント設置 / マウント終了** のノードが紐づく
（例: レベル 7 は Conrad_started_jerkoff_3 / Conrad_finished_jerkoff_3 / Conrad_placed_mount_3 /
Conrad_finished_mount_3。断る場合は `ryan_mount_no` などの共通ノード）。

つまり **1 レベル内の会話順**は固定で、次のとおり：

```
intro → （電話チュートリアル）→ 洗っている間の会話 (required)
      → 待機中 (idle) / 催促 (nag)
      → 手コキ開始 → 射精 / マウント設置 → マウント終了
      → outro
```

### 1.2 Yarn ノードは 3 種類に分かれる

195 ノードの内訳：

- **LevelFlow から直接呼ばれる**: 83
- **他ノードから `run_node` で呼ばれる分岐サブノード**: 51
  （例: Conrad_Outro_4 → Conrad_Outro_4_Ryan_Conrad_Romanced など、恋愛ルート別）
- **どちらでもない**: 62。さらに分類すると
  - 本体コードから起動されるカットシーン: `Ryan_SexScene`, `Conrad_SexScene`, `Alexander_SexScene`, `Ryan_Conrad_SexScene`（各 `_Loop` / `_Cum` 付き）
  - ドラゴンの反応（バーク）: `DragonAlmostClean`, `DragonAnnoyedByBuzzing`, `DragonColdBalls`, `DragonConfusedAboutDismissal`, `DragonSatisfiedAndLeaves`
  - ピクニックの食べ物（レベル 9、アイテム使用で起動）: `Ryan_egg` / `Ryan_grapes` / `Ryan_ham` / `Ryan_melon` → `ryan_food_dialogue`
  - 個別トリガー: `Ryan_3_wont_use_mount`, `Mount_Alexander_2_6`, `Conrad_5_Mount_Start/Finish`
  - **未使用（旧版・デモの残り）**: `RyanMuddy_*`（17）, `ConradBeatup_*`（6）, `RyanDate_*`（6）, `RyanExploded_*`（2）, `Alexander_1/2/3`, `Alexander_Idle1-4`, `Alexander_Intro/Outro`, `Conrad_1/2`, `Conrad_Idle1`, `Conrad_Intro`, `Start`, `Declarations`

### 1.3 分岐に使う変数

恋愛ルートの分岐はレベル 10 以降に集中している：

- `$ryan_romanced` / `$conrad_romanced` / `$ryan_conrad_romanced` を読んで
  `*_ryan_romanced` / `*_conrad_romanced` / `*_ryan_conrad_romanced` のサブノードへ分岐
  （Conrad_Intro_5, Conrad_required_4/5, Conrad_Outro_4/5, Ryan_5_intro/Required/Outro, Alexander_5_intro/required）
- シーン発生条件: `$ryan_sex_scene` は Ryan_5_Outro_ryan_romanced、`$conrad_sex_scene` は Conrad_5_*_conrad_romanced、
  `$ryan_conrad_sex_scene` は Conrad_Outro_4_Ryan_Conrad_Romanced、`$alexander_sex_scene` は Alexander_5_Outro で立つ
- レベル 7（Conrad 3 回目）は `$conrad_jerked_off_3` / `$conrad_used_mount_3` で idle と終了の会話が変わる
- レベル 1 の冒頭は `$has_talked_to_ryan`（Ryan_1_intro が立て、Ryan_1_PhoneTutorial が読む）
- ピクニックは `$food_number` で食べ物ごとの会話を数える

`FlagCatalog.csv` の内容は、この結果と一致している（追加すべき変数はなし）。

## 2. 設計

### 2.1 公開ファイルの形式

```csv
key,section,node,order,speaker,translation
# ===== Level 1: Ryan (Sunny) | sets level_1 | ends level_1_complete =====
# --- intro: Ryan_1_intro ---
bc1b88907d3b748a,L01 Ryan,Ryan_1_intro,1,Ryan,よお。ここが洗い屋か？
...
# --- phone: Ryan_1_PhoneTutorial | if not $has_talked_to_ryan ---
...
# --- while washing: Ryan_1_required ---
# --- nag: Ryan_1_nag ---
# --- outro: Ryan_1_Outro ---
# ===== Level 2: Conrad (Sunny) =====
...
# ===== Cutscenes (started by game code) =====
# --- Ryan_SexScene -> Ryan_SexScene_Loop -> Ryan_SexScene_Cum ---
# ===== Dragon reactions =====
# ===== Picnic food (Level 10) =====
# ===== Unused nodes (not reachable in the current game) =====
# ===== UI =====
a7c0…,UI,,,UI,オプション
```

- `section`: 「L01 Ryan」「Cutscene」「Reaction」「Unused」「UI」。並び替えやフィルタ用
- `node` / `order`: Yarn ノード名とノード内の順番。分岐サブノードは親の直後に置く
- `#` 行はコメント。プラグインの `CsvReader` と CI は読み飛ばす。**英語は一切書かない**
  （ノード名・変数名・フラグ名はゲーム内部の識別子で、台本ではない）
- 同じ台詞が複数箇所で使われる場合は最初に出る場所に 1 行だけ（キーは 1 つなので）
- 列が増えても読み込み側は `key` と `translation` しか見ないので互換性は保たれる

### 2.2 並び順の決め方（`ScriptOrder`）

1. `LevelFlow` を反射で読む（`FlowDumper` と同じ経路）。レベル 0..14 の順
2. 各レベルで intro → phone → progress → idle → nag → jerkoff → cum → mount_start → mount_finish → outro の順にノードを訪ねる
3. ノードを訪ねたら、命令列を上から読み、`line` / `option` を順番に出力。`run_node` / `detour` が出たら
   その先のノードを **その場で** 深さ優先で展開（重複は出力しない）
4. `jump_if_false` の直前に読まれた変数名を拾い、サブノードの見出しに `if $var` として添える
5. LevelFlow から到達できなかったノードを名前で分類して後ろに置く（Cutscene → Reaction → Picnic → Unused）
6. 最後に UI（Yarn 由来でない文字列）

**進捗に関係なく全レベルが取れる。** `LevelFlow` は 15 レベル分を 1 つのアセットに持っているので、
どのレベルをロードしていても（レベル 1 のセーブでも）全レベルの構成が読める。Yarn プロジェクトも
全ノードを含む。今回の書き出しはレベル 1 の状態で行い、レベル 15 までそろっていることを確認した。

ただし LevelFlow はタイトル画面では読み込まれていない。そこで並び順そのものをリポジトリに
**データとして同梱**する：

- `data/script_order.csv`（`section,phase,node,order,line_id,key,speaker`）を新設。ゲーム内の
  Export game flow が生成し、コミットする。含まれるのはノード名・行 ID・ハッシュ・話者だけで、
  英語の台本は含まれない
- `Hash for commit`、`Export working copy`、`tools/hash-strings.ps1` はこのファイルを読んで並べる。
  ゲーム内でもタイトル画面でも、ゲームの外（PowerShell）でも同じ順序になる
- ゲームが更新されてノードが増減したら、レベルをロードして Export game flow を押し直し、
  `data/script_order.csv` を更新する

### 2.3 変更するもの

| ファイル | 変更 |
|---|---|
| `src/.../ScriptOrder.cs`（新規） | `data/script_order.csv` を生成（ゲーム内、任意のレベルで可）し、読み込んで並び順を返す |
| `data/script_order.csv`（新規） | 並び順の定義。ゲームの更新時に再生成 |
| `src/.../TranslationStore.HashFileInPlace` | `key,section,node,order,speaker,translation` とコメント行を出力 |
| `src/.../WorkingCopy.Export` | 同じ順序・見出しで `source_en` 付きを出力 |
| `src/.../CsvReader` | `#` で始まる行を読み飛ばす |
| `src/.../SpeakerLookup` | ノード種別（Cutscene / Reaction）の話者判定を追加 |
| `tools/hash-strings.ps1` | `data/script_order.csv` を読んで同じ順序・見出しで書き出す（ゲーム不要） |
| `tools/check-translations.py` | 新ヘッダーを許可、`#` 行を無視、`section`/`node` に英文が入っていないかの簡易チェック |
| `CONTRIBUTING.md` / README | 形式の説明を更新 |

### 2.4 やらないこと

- 未使用ノード（RyanMuddy_* など）の翻訳を削る：ゲームの更新で復活する可能性があるので残す。
  「Unused」の見出しの下にまとめるだけ
- 台詞の英文をコメントに書く：公開ファイルの方針に反する
- 話者の自動判定を変える：現行のルールで十分（Kobold＝選択肢、Phone、UI）

## 3. 手順

1. `ScriptOrder` を実装し、F1 → Tools の Export working copy で新形式を書き出して目視確認
2. `Hash for commit` を新形式に切り替え、ja / zh-Hans の `strings.csv` を再生成（内容は変わらず並びと列だけ変わる）
3. `CsvReader` / CI / `hash-strings.ps1` を対応させ、テスト PR なしで CI を手元の Python で確認
4. ドキュメント更新、コミット、次のリリース（0.2.0）に含める

所要の見込み: 実装と実機確認で半日程度。
