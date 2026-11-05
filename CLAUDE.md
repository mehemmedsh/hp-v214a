# CLAUDE.md

Claude Code bu dosyayı otomatik okur. Kurallar tek kaynakta tutulur (dokümanlar o dosyaya bakar):

@AGENTS.md

Claude Code'a özel:
- Düzen: tek model, Opus, Claude Code'da; inceleme yok (kullanıcı kararı 2026-11-05; `docs/04-FAZLAR.md` → "Araçlar, modeller ve kota sırası"). Sonnet ve Antigravity kullanılmıyor.
- Ana yer PC'deki Claude Code (masaüstü uygulaması, Windows): Windows testleri orada çalışır. Bulut oturumu (claude.ai/code, Linux) yedektir; orada Windows'a özel testler çalışmaz (CI'da).
- Opus'un sınırı dolarsa yenilenene kadar beklenir (`docs/05-HANDOFF.md` → 6).
- Yardım oturumunda (`docs/05-HANDOFF.md` → 7) kod yazma, dosya değiştirme; ne yapması gerektiğini adım adım anlat, gerekirse görev oturumuna yapıştıracağı metni yaz.
