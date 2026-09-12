## 内容

<!-- 何を訳した／直したか。言語と、会話なら node 名や場面（例: Conrad_1_intro） -->

## チェックリスト

- [ ] `Translations/<locale>/strings.csv` は **ハッシュ化済み**（`key,section,node,order,speaker,translation` の列で、`source_en` の行が残っていない。F1 → Tools → Hash for commit か `tools/hash-strings.ps1` で作る）
- [ ] 実機で表示を確認した（F1 → Tools で言語を切り替え、または保存してホットリロード）
- [ ] 書式タグ（`<size=…>` `<i>` `<gradient=…>`）の構造を原文と同じに保った
- [ ] キャラクターの口調は [docs/TRANSLATION_STYLE.md](../docs/TRANSLATION_STYLE.md) に沿っている
- [ ] `Translations/_discovered/`（作業コピーや書き出しファイル）は含めていない
