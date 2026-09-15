## What / 内容

<!-- What you translated or fixed: the language, and for dialogue the node or scene (e.g. Conrad_1_intro).
     何を訳した／直したか。言語と、会話なら node 名や場面（例: Conrad_1_intro） -->

## Checklist / チェックリスト

- [ ] `Translations/<locale>/strings.csv` is **hashed**: columns `key,section,node,order,speaker,translation`, no `source_en` rows left (F1 → Translation → Hash for commit, or `tools/hash-strings.ps1`) / **ハッシュ化済み**で `source_en` の行が残っていない
- [ ] Checked in the game (switch language in Options → Language (Mod) or F1 → Translation, or save and let it hot-reload) / 実機で表示を確認した
- [ ] Formatting tags (`<size=…>` `<i>` `<gradient=…>`) keep the same structure as the source / 書式タグの構造を原文と同じに保った
- [ ] Character voices follow [docs/TRANSLATION_STYLE.md](../blob/main/docs/TRANSLATION_STYLE.md) / キャラクターの口調がスタイルガイドに沿っている
- [ ] Nothing from `Translations/_discovered/` (working copies, exports) is included / `_discovered/` のファイルを含めていない
- [ ] If this is a native-speaker review of a provisional pack: the header comment and README table are updated, or I asked the maintainer to do it / 仮翻訳のネイティブレビューなら、先頭コメントと README の一覧も更新した（またはメンテナーに依頼した）

## Credit / クレジット表記

<!-- Would you like to be credited for this translation (README, the pack's header, the in-game About)?
     Tick one. If yes, write the name and optional link exactly as you want it shown.
     この翻訳への協力者として、クレジットの表記を希望しますか？（README、言語パックの先頭、ゲーム内の About など）
     どちらかにチェックしてください。希望する場合は、表示してほしい名前と、必要ならリンクを書いてください。 -->

- [ ] Yes, credit me as / 希望する（表記）: 
- [ ] No credit, please / 希望しない
