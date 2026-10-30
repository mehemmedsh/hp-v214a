# Monitor Tuner: HP V214a

HP V214a monitörü için bir Windows uygulaması ve ona bağlı bir Pixel yardımcı uygulamasının planı. Sistem: monitör **RTX 3080**'in HDMI'ında, **RTX 3090** ağır işler için boş.

> **Durum:** Plan aşaması, uygulama kodu yok. Önce [ilk gün kontrolü](docs/09-ILK-GUN.md), sonra `docs/04-FAZLAR.md` içindeki **T001**.

Uygulama fikri monitörden bağımsız tasarlandı: [fikir/UYGULAMA-FIKRI.md](fikir/UYGULAMA-FIKRI.md). Bu repo o fikrin HP V214a uyarlamasıdır. İlk uyarlama (Lenovo T24i-20) `Myapplab/Lenovo-monitor` reposunda arşiv olarak duruyor.

## Ne yapacak?

- **Yenileme hızı:** 60 Hz'in üstünü güvenle dener; monitörün belgeli sınırlarına göre tavanı hesaplar (spec içinde ~72 Hz) ve gerçekten çalışıp çalışmadığını test eder.
- **Renk:** Telefonla ölçüp griyi nötr, gammayı doğru yapar; Windows için ICC, DaVinci için 3D LUT üretir. İstenirse göze hoş "canlılık".
- **Parlaklık:** Tek kaydırıcıyla önce arka ışık, sonra güvenli ek karartma; PWM varsa titreşimsiz mod.
- **Mavi ışık filtresi:** Kelvin tabanlı (6500K → 1900K) doğal sıcak beyaz; kalibrasyonu bozmaz.
- **Üst-alt ışık farkı:** Teşhis, oturma rehberi ve dikey denge katmanı (TN panelin bakış açısı sorunu).
- **İki GPU:** Hangi uygulamanın hangi kartta çalışacağı; AI, Oyun ve Render modları.
- **Deneysel:** 10-bit testi, RIFE, kartlar arası geçiş, satır bazlı ton telafisi, kolorimetre modu, NVIDIA renk dönüşümü, Keşif; hepsi güvenli testlerle.

## Temel kurallar

1. **Hz ve parlaklık sadece kullanıcı değiştirdiğinde değişir.** Üç güvenlik istisnası var; üçü de kullanıcının bilinen değerine döner.
2. **Her değişiklik geri alınabilir.** Orijinal EDID ve GPU tablosu yedeklenir; görüntü gelmezse 15 saniyede geri dönülür; kurtarma dosyası var.
3. **Firmware'e asla yazılmaz.**
4. **60 Hz her zaman yedekte kalır.**
5. **Her görüntü efekti tek kısayolla kapanır.**

## Dokümanlar

| Dosya | İçerik |
|---|---|
| [fikir/UYGULAMA-FIKRI.md](fikir/UYGULAMA-FIKRI.md) | Monitörden bağımsız fikir: kurallar, modüller, karar kuralları, uyarlama şablonu |
| [docs/00-DONANIM.md](docs/00-DONANIM.md) | HP V214a araştırması, Hz tavanı, beklentiler, bilgisayar (3090 + 3080), kablolama, kurtarma, oturma |
| [docs/01-OZELLIKLER.md](docs/01-OZELLIKLER.md) | Tüm özellikler, modüllere ve sürümlere göre |
| [docs/02-MIMARI.md](docs/02-MIMARI.md) | Süreçler, katmanlar, renk boru hattı, katman yöneticisi, modül sözleşmeleri |
| [docs/03-GUVENLIK.md](docs/03-GUVENLIK.md) | Değişmez kurallar, kurtarma yolları |
| [docs/04-FAZLAR.md](docs/04-FAZLAR.md) | 66 numaralı görev, Sonnet/Opus dağılımı, bağımlılıklar |
| [docs/05-HANDOFF.md](docs/05-HANDOFF.md) | Claude Code ve Claude app için hazır metinler |
| [docs/06-CIKARILANLAR.md](docs/06-CIKARILANLAR.md) | Çıkarılan özellikler ve nedenleri |
| [docs/07-INCELEME.md](docs/07-INCELEME.md) | Son kontrolün kaydı ve eski → yeni görev eşlemesi |
| [docs/08-GORUNTU-YONTEMLERI.md](docs/08-GORUNTU-YONTEMLERI.md) | Üst-alt farkı, renk düzeltme, parlaklığı düşürme, mavi ışık filtresi, renk boru hattı |
| [docs/09-ILK-GUN.md](docs/09-ILK-GUN.md) | Kodlamadan önce monitörde yapılacak kontroller |
| [LESSONS.md](LESSONS.md) | Donanım bulguları ve geliştirme dersleri |

## Sürümler

| Sürüm | Odak | Çıktı |
|---|---|---|
| **v1** | Temel | Overclock + güvenlik, DDC, renk boru hattı, ek karartma, mavi ışık filtresi, presetler, üst-alt dengesi, GPU yönetimi |
| **v2** | Ölçüm | Pixel uygulaması, kalibrasyon, ICC, DaVinci LUT, PWM ve panel testleri, monitör karnesi |
| **v3** | Deneysel | 10-bit, RIFE, kartlar arası geçiş, satır bazlı telafi, kolorimetre, NVIDIA renk dönüşümü, Keşif |
