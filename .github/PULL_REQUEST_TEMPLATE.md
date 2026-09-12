## What / 内容

<!-- What you translated or fixed: the language, and for dialogue the node or scene (e.g. Conrad_1_intro).
     何を訳した／直したか。言語と、会話なら node 名や場面（例: Conrad_1_intro） -->

## Checklist / チェックリスト

- [ ] `Translations/<locale>/strings.csv` is **hashed**: columns `key,section,node,order,speaker,translation`, no `source_en` rows left (F1 → Tools → Hash for commit, or `tools/hash-strings.ps1`) / **ハッシュ化済み**で `source_en` の行が残っていない
- [ ] Checked in the game (switch language in F1 → Tools, or save and let it hot-reload) / 実機で表示を確認した
- [ ] Formatting tags (`<size=…>` `<i>` `<gradient=…>`) keep the same structure as the source / 書式タグの構造を原文と同じに保った
- [ ] Character voices follow [docs/TRANSLATION_STYLE.md](../docs/TRANSLATION_STYLE.md) / キャラクターの口調がスタイルガイドに沿っている
- [ ] Nothing from `Translations/_discovered/` (working copies, exports) is included / `_discovered/` のファイルを含めていない
- [ ] If this is a native-speaker review of a provisional pack: the header comment and README table are updated / 仮翻訳のネイティブレビューなら、先頭コメントと README の一覧も更新した
