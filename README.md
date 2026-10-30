# Monitor Tuner: HP P21b G4

HP P21b G4 monitörü için bir Windows uygulamasının planı. Sistem: monitör **RTX 3080**'in HDMI'ında, **RTX 3090** ağır işler için boş.

> **Durum:** Plan aşaması, uygulama kodu yok. [İlk gün kontrolü](docs/09-ILK-GUN.md) yapıldı; sıradaki görev `docs/04-FAZLAR.md` içindeki **T001**. Kodlama Antigravity'de (Gemini, Opus, Sonnet); başlamadan önce: [`docs/05-HANDOFF.md`](docs/05-HANDOFF.md), bölüm 0 (kurulum).
>
> **Not:** Plan HP V214a için yazılmıştı; ilk gün kontrolünde monitörün **HP P21b G4** olduğu anlaşıldı. Sinyal sınırları aynı olduğu için plan uyarlandı, repo adı `hp-v214a` olarak kaldı. Ünitenin ham verileri `donanim/hp-p21b-g4/` içinde.

Uygulama fikri monitörden bağımsız tasarlandı: [fikir/UYGULAMA-FIKRI.md](fikir/UYGULAMA-FIKRI.md). Bu repo o fikrin HP P21b G4 uyarlamasıdır. İlk uyarlama (Lenovo T24i-20) `Myapplab/Lenovo-monitor` reposunda arşiv olarak duruyor.

## Ne yapacak?

Hedef: bu panelden çıkabilecek en iyi görüntü, göz konforu ve 76 Hz. Gerekli ince ayar var, aşırısı yok.

- **Yenileme hızı:** 76 Hz tavanı ilk gün bulundu. Modlar NVIDIA özel çözünürlüğüyle bir kez eklenir; uygulama 76, 75, 72, 60 ve 50 Hz arasında güvenle geçer (15 sn onay) ve gelen Hz'i monitörün kendisinden doğrular.
- **Ayar sihirbazı:** Desenlerle kontrast, parlaklık, keskinlik, RGB Full ve açı; Pixel'in ekranıyla yan yana beyaz eşleme; gözle gamma ve gri dengesi. Kamera ya da ölçüm cihazı gerekmez.
- **Renk:** GPU renk boru hattı (kalibrasyon → mavi ışık → ek karartma), dithering ile pürüzsüz geçişler, Film ve Oyun için canlılık.
- **Göz konforu:** Titreşimsiz arka ışık (PWM yok), tek kaydırıcıyla güvenli ek karartma, Kelvin tabanlı mavi ışık filtresi, oturma ve açı rehberi.
- **Renk profilleri:** Gündüz, Akşam, Gece, Kod, Film, Oyun, DaVinci zaman çizelgesi; tepsiden tek tık, panik kısayolu.

## Temel kurallar

1. **Hz ve parlaklık sadece kullanıcı değiştirdiğinde değişir.** İki güvenlik istisnası var; ikisi de kullanıcının bilinen değerine döner.
2. **Her değişiklik geri alınabilir.** DDC değerleri ve GPU tablosu yedeklenir; onaylanmayan Hz 15 saniyede geri döner; kurtarma dosyası var.
3. **Firmware'e ve EDID'e yazılmaz.**
4. **60 Hz her zaman yedekte kalır.**
5. **Her görüntü efekti tek kısayolla kapanır.**

## Dokümanlar

| Dosya | İçerik |
|---|---|
| [AGENTS.md](AGENTS.md) | Antigravity ve Claude Code ajanlarının otomatik okuduğu kurallar, öncelikler, okuma haritası ([CLAUDE.md](CLAUDE.md) bunu içe aktarır) |
| [fikir/UYGULAMA-FIKRI.md](fikir/UYGULAMA-FIKRI.md) | Monitörden bağımsız fikir: kurallar, modüller, karar kuralları, uyarlama şablonu |
| [docs/00-DONANIM.md](docs/00-DONANIM.md) | HP P21b G4 bilgileri ve ünite ölçümleri (DDC, EDID, OSD), Hz tavanı ve test sonuçları, beklentiler, bilgisayar, kablolama, kurtarma, oturma |
| [docs/01-OZELLIKLER.md](docs/01-OZELLIKLER.md) | Tüm özellikler ve onları yapan görevler |
| [docs/02-MIMARI.md](docs/02-MIMARI.md) | Teknoloji kararları, katmanlar, renk boru hattı, modül sözleşmeleri, CI korumaları |
| [docs/03-GUVENLIK.md](docs/03-GUVENLIK.md) | Değişmez kurallar, kurtarma yolları, ajan kuralları |
| [docs/04-FAZLAR.md](docs/04-FAZLAR.md) | 21 görev, modeller ve kota sırası, paralel dalgalar |
| [docs/05-HANDOFF.md](docs/05-HANDOFF.md) | Kurulum, görev döngüsü ve Antigravity / Claude Code için hazır metinler |
| [docs/06-CIKARILANLAR.md](docs/06-CIKARILANLAR.md) | Çıkarılan özellikler ve nedenleri |
| [docs/07-INCELEME.md](docs/07-INCELEME.md) | İnceleme turlarının kaydı ve eski → yeni görev eşlemeleri |
| [docs/08-GORUNTU-YONTEMLERI.md](docs/08-GORUNTU-YONTEMLERI.md) | Üst-alt farkı, renk düzeltme, parlaklığı düşürme, mavi ışık filtresi, renk boru hattı |
| [docs/09-ILK-GUN.md](docs/09-ILK-GUN.md) | Kodlamadan önce monitörde yapılan kontroller |
| [LESSONS.md](LESSONS.md) | Donanım bulguları ve geliştirme dersleri |
| [donanim/hp-p21b-g4/](donanim/hp-p21b-g4/) | Ünitenin orijinal EDID'i ve DDC dökümü |
