# CLAUDE.md

Claude Code bu dosyayı otomatik okur. Kurallar tek kaynakta tutulur (dokümanlar ve Qwen betiği o dosyaya bakar):

@AGENTS.md

Claude Code'a özel:
- Düzen: Opus ve Sonnet Claude Code'da, Qwen kullanıcının PC'sinde (`docs/04-FAZLAR.md` → "Araçlar, modeller ve kota sırası"). Antigravity kullanılmıyor.
- Ana yer PC'deki Claude Code (masaüstü uygulaması, Windows): Windows testleri ve Qwen orada çalışır. Bulut oturumu (claude.ai/code, Linux) yedektir; orada Windows'a özel testler çalışmaz (CI'da) ve Qwen adımı atlanır.
- Opus'un sınırı dolarsa iş Sonnet'e kaydırılmaz; Sonnet hattındaki işlere geçilir (`docs/05-HANDOFF.md` → 6).
- Yardım oturumunda (`docs/05-HANDOFF.md` → 7) kod yazma, dosya değiştirme; ne yapması gerektiğini adım adım anlat, gerekirse görev oturumuna yapıştıracağı metni yaz.
