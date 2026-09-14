# Translation Style Guide by Character

[日本語](TRANSLATION_STYLE.ja.md)

Maintaining a **consistent voice for each character** is the highest priority in every language. Inconsistent speech patterns quickly reduce the quality of a translation. The first part of this guide applies to all languages; language-specific wording follows.

## Every language

- **Kobold** (the player, a washer): cheerful, eager, polite to customers, a little naive. Warms up to regulars as the levels go on. The bark `Yip!` stays a bark.
- **Conrad** (large red dragon): brash, loud, crude and blunt, swears freely; shy and kind underneath, awkward with romance.
- **Ryan** (small drake): young, friendly, casual and gentle, a little shy; self-conscious about his size; embarrassed by sexual jokes but plays along.
- **Alexander** (lavender professor): old-fashioned, grandiose and verbose; very formal; never crude, even in intimate scenes, where he uses euphemism; obsessed with his reputation and crest.
- **Phone** (the franchise head office): dry, corporate and impersonal.
- Where a language distinguishes formal and informal address, the kobold starts polite with customers, Ryan and Conrad speak informally, and Alexander and the Phone stay formal.
- There are no humans in this world. Avoid words that mean "human" for `people`, `someone` or `guy`.
- Pick one word for `mount` (the stand a dragon mounts) and use it everywhere.

### Formatting tags

Preserve the structure of TMP tags such as `<size=70%>`, `<gradient="gold">`, `<i>`, and `<b>`, and translate only the text inside them. Also translate parenthetical choice hints such as `(Will lead to no romance!)`.

### Lines shared by several characters

A row whose `speaker` lists several characters (`Ryan/Alexander`) is used for all of them. Prefer a wording that fits every voice. If none does, translate the shared row for the most common speaker and add a line-ID row for the others, as described in [CONTRIBUTING.md](../CONTRIBUTING.md#lines-said-by-more-than-one-character).

### Do not translate Yarn internal markers

Lines beginning with `title:`, `Test line` entries, flags such as `player hooked up with …`, development messages, and the markers `---` and `===` are not displayed text. Do not translate them. They are already excluded in `Translations/ignore.txt`.

### Conventions the current packs use

Reviewers: these are the choices the provisional packs were written with. Change them if a native speaker would choose differently, but change them throughout the file.

| Locale | Address | mount | kobold |
|---|---|---|---|
| `de` | du; Alexander and the Phone: Sie | Bock | Kobold |
| `fr` | tu; Alexander and the Phone: vous | chevalet | kobold |
| `es` | tú; Alexander and the Phone: usted | potro | kóbold |
| `pt-BR` | você; Alexander: o senhor | cavalete | kobold |
| `ko` | kobold 해요체, Ryan and Conrad 반말, Alexander 하십시오체, Phone 합니다체 | 마운트 | 코볼트 |
| `ru` | ты; Alexander and the Phone: вы | станок | кобольд |
| `pl` | ty; Alexander: pan | kozioł | kobold |
| `he` | masculine forms; names written in Hebrew letters (ריאן, קונרד, אלכסנדר) | מתקן | קובולד |
| `eo` | vi throughout | la stablo | koboldo |
| `tok` | no formality; characters differ by rhythm; names as jan Lajan, jan Kolan, jan Alekante | supa unpa | jan lili |
| `zh-Hant` | follows the supervised `zh-Hans` wording, with Taiwan vocabulary | — | — |

## Japanese

### Characters and voices

#### Kobold (protagonist and washer)

- Uses a cheerful, friendly customer-service voice. Usually speaks in polite `です・ます` forms, but remains approachable and occasionally relaxes into `〜だね` or `〜だよ`.
- The signature call is **「イップ！」** for `Yip!`. Translate an explanatory form such as `Yip! (Yes)` as **「イップ！（うん）」**, retaining the parenthetical explanation.
- As the character who welcomes and sees off customers, the kobold often sounds attentive and helpful, as in 「ゲートを開けてくるね」.

#### Conrad (large red dragon)

- Uses rough, short-tempered **masculine speech**, including `〜だぜ`, `〜だろ`, and `〜だな`.
- Translate `Damn right` as 「あったりめぇだ」. Adapt `Hell yeah` to the scene instead of transliterating it, using expressions such as 「よっしゃあ！」, 「そうこなくっちゃ」, 「最高だぜ」, or 「たまんねえ」.
- He is foul-mouthed but shy and kind underneath, and somewhat awkward with romantic partners.

#### Ryan (small drake)

- Uses a soft, boyish masculine voice. He is affectionate and shy, with endings such as `〜だよ` and `〜なんだ`.
- He is self-conscious about being small. Translate `vertically challenged` as 「縦に小さい」.
- He is embarrassed by sexual jokes but readily plays along.

#### Alexander (lavender professor)

- Uses academic, **grandiose polite language**, including `〜でございます`, `〜でありまして`, `いかが`, and `お見事`.
- Uses formal vocabulary, such as 「格式高い」 for `prestigious` and 「装飾的な」 for `ornamental`.
- He is excessively concerned with his reputation, honor, and crest. He burns and returns in phoenix-like fashion.

#### Other characters

- Max, an irritable acquaintance of the kobold, and Igwyx, a fellow professor of Alexander, appear only briefly.

### Avoid words that imply humans

There are no humans in this setting; its inhabitants include dragons, kobolds, and griffins. Translating English words such as `people`, `someone`, `folks`, or `guy` with the Japanese word 「人」 can incorrectly imply that everyone is human. Rephrase the noun instead.

- `someone` / `new people` → 「誰か」「新しい相手」「新しい出会い」
- `folks` / `those around me` → 「みんな」「周りのみんな」「相手」
- `big guy` / `little guy` as forms of address → 「おっきいの」「ちっちゃいの」
- `you two` for a kobold and a dragon → 「君たち」「お二方」; for two dragons, 「お二竜」
- When everyone being counted is a dragon, use 「一竜」「二竜」 as intentional wordplay.
- The 「人」 in established words such as 「恋人」「有名人」「素人」「人目」「個人的」「本人」「人生」 does not specifically mean a human and is acceptable.

### Standard terminology

| English | Japanese |
|---|---|
| Yip / Yip! | イップ！ |
| gate | ゲート |
| mount | マウント |
| wash / clean | 洗い／洗浄 |
| spotless | ピカピカ |
| kobold | コボルド |
| drake | ドレイク |
| dragon | ドラゴン |
| handjob | 手コキ |
| phoenix feather | フェニックスの羽根 |
| crest | 紋章 |
| "Burnswick Community College of Thaumaturgy" | バーンズウィック・コミュニティ・サマタージー大学 |

### Character names

- Conrad → コンラッド
- Ryan → ライアン
- Alexander → アレクサンダー
- Igwyx → イグウィックス
- Max → マックス

When a line starts with a name prefix such as `Conrad: ...`, retain the prefix as `コンラッド：...` using a full-width Japanese colon.

