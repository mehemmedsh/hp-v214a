# Fazlar ve Görevler

**Sıradaki görev: T001.** T004'ten önce ilk gün kontrolü (`09-ILK-GUN.md`) bitmiş olmalı.

## Çalışma kuralları

### Görev sınıfları ve modeller

| Sınıf | Ne tür iş | Yazan | İnceleyen |
|---|---|---|---|
| **A** | Donanıma veya sistem ayarına yazan, geri dönüşü zor olabilecek veya güvenlikle ilgili iş | **Opus 5.5** (high) | **Sonnet 5.5**, `03-GUVENLIK.md` kontrol listesiyle |
| **B** | Mantık, algoritma, akış | **Sonnet 5.5** | **Opus 5.5** (high) |
| **C** | Arayüz, metin, basit ayar ekranı | **Sonnet 5.5** | **Opus 5.5** |
| **K** | Faz sonu kontrol noktası | **Kullanıcı** (HP V214a başında test) | **Claude app – Opus 5.5 – max** |

- Yazan ve inceleyen her zaman farklı model. İnceleme hiçbir görevde atlanmaz.
- Kodlama yeri: **Claude Code, bu repo.** Faz sonu değerlendirmeleri: **Claude app**.
- Uzun bir oturumun ortasında model değiştirilmez. Yazma ve inceleme ayrı oturumlardır.

### Görev kuralları

- Her görev yaklaşık **200 satır** değişiklik ve tek oturumda bitecek boyutta.
- **Önce test:** Görevin testleri önce yazılır, sonra kod.
- Sözleşmeler (T002) kodda yaşar; bir görev sözleşmeyi değiştirmek zorundaysa önce `02-MIMARI.md` güncellenir.
- Görev bitince: commit, bu dosyada durum ✅, gerekirse `LESSONS.md`.
- Kullanım limiti yaklaşınca yeni göreve başlanmaz; yarım görev bırakılmaz.
- 🔧 işaretli görevler kullanıcının gerçek monitörde test etmesini gerektirir. Test edilmeden ✅ olmaz.

Durum: ⬜ bekliyor · 🟨 sürüyor · ✅ bitti

---

## Faz 0 — Hazırlık

| No | Görev | Sınıf | Yazan → İnceleyen | Bağımlı | Bitti sayılır | Durum |
|---|---|---|---|---|---|---|
| T001 | Proje iskeleti: teknoloji kararlarının onayı, klasör yapısı, dil altyapısı (tüm metinler baştan çeviri dosyasından), CI (derleme, test, katman korumaları) | C | Sonnet → Opus | — | Boş çözüm derleniyor, CI yeşil, katman ihlali CI'da yakalanıyor | ⬜ |
| T002 | Sözleşmeler: `02-MIMARI.md`'deki modül sözleşmelerini arayüzlere ve boş (kırmızı) testlere çevir; renk boru hattı ve katman yöneticisi dahil | A | Opus → Sonnet | T001 | Her modülün arayüzü ve en az bir testi var | ⬜ |
| T003 | Sahte donanım: sahte monitör (DDC'li ve DDC'siz), sahte kayıt defteri, sahte sürücü, sahte GPU renk tablosu, sahte saat; örnek EDID seti | B | Sonnet → Opus | T002 | Donanımsız tüm testler sahte donanımla çalışıyor | ⬜ |
| T004 | Monitör profil dosyası: şema + HP V214a profili (spec sınırları, OSD yetenekleri, ilk gün sonuçları) | B | Sonnet → Opus | T002 | Profil yükleniyor; ilk gün sonuçları işlendi 🔧 | ⬜ |

## Faz 1 — Çekirdek (v1)

| No | Görev | Sınıf | Yazan → İnceleyen | Bağımlı | Bitti sayılır | Durum |
|---|---|---|---|---|---|---|
| T101 | EDID ayrıştırıcı ve kodlayıcı, checksum, CTA-861 uzantı bloğu | A | Opus → Sonnet | T003 | Gidiş-dönüş testi tüm örneklerde aynı baytları veriyor; gerçek HP V214a EDID'i test setinde 🔧 | ⬜ |
| T102 | Monitör tanıma, çoklu monitör, kullanıcı profili sistemi (bağlantı başına anahtar; Hz ve RGB aralığı alanları), profil deposu | B | Sonnet → Opus | T101, T004 | HP V214a 3080'in HDMI'ında doğru tanınıyor 🔧 | ⬜ |
| T103 | Güvenlik katmanı: EDID, DDC ve GPU tablosu yedeği, geri alma noktaları, 15 sn onay zamanlayıcısı, "fabrika ayarına dön" | A | Opus → Sonnet | T102 | Sahte donanımda her yazma geri alınabiliyor; onay gelmezse geri dönülüyor | ⬜ |
| T104 | Kurtarma dosyası (override'ları siler, GPU tablosunu sıfırlar), güvenli mod talimatı (3090 HDMI veya 3080 DP→HDMI), kaldırma temizliği | A | Opus → Sonnet | T103 | Güvenli modda kurtarma tüm değişiklikleri siliyor 🔧 | ⬜ |
| T105 | Yönetici yardımcısı: izinli komutlar (EDID override yaz/sil, sürücü yeniden başlat, güç limiti, `GdiIcmGammaRange` yaz/geri al), yerel kanal | A | Opus → Sonnet | T103 | Listede olmayan komut reddediliyor; ana uygulama normal kullanıcıda | ⬜ |

## Faz 2 — Yenileme hızı (v1)

| No | Görev | Sınıf | Yazan → İnceleyen | Bağımlı | Bitti sayılır | Durum |
|---|---|---|---|---|---|---|
| T201 | Timing hesaplayıcı (standart, CVT-RB, sıkı) ve Hz tavanı; spec içi/dışı işareti | A | Opus → Sonnet | T101, T004 | `00-DONANIM.md`'deki tablo birebir üretiliyor | ⬜ |
| T202 | EDID override yazıcı: CTA-861 korunur, orijinal mod yerinde, aralık sınırı güncellenir; NVIDIA özel çözünürlük / CRU çakışma kontrolü; VGA uyarısı | A | Opus → Sonnet | T201, T105 | Override sahte kayıtta doğru; orijinal mod korunuyor | ⬜ |
| T203 | Sürücü yeniden başlatma ve 15 sn onay akışı (gerçek donanımda ilk overclock) | A | Opus → Sonnet | T202 | 65 Hz eklendi, onaysız geri dönüş çalıştı 🔧 | ⬜ |
| T204 | Test desenleri motoru: tam ekran, vsync'e kilitli, kare sayacıyla kendini doğrulama; epilepsi uyarısı | B | Sonnet → Opus | T002 | Her yenilemede tam bir kare değişiyor 🔧 | ⬜ |
| T205 | Kare atlama testi: desen + elle çekilen fotoğrafın analizi + talimat ekranı | B | Sonnet → Opus | T204 | 60 Hz'de "gerçek", bilerek atlatılan örnekte "sahte" 🔧 | ⬜ |
| T206 | Maksimum Hz bulucu: 61 → 75, her adımda onay ve kare atlama testi, spec dışı adımda ayrı onay, sonuç onaysız kalıcı olmaz | B | Sonnet → Opus | T203, T205 | Gerçek tavan bulundu, `LESSONS.md`'ye yazıldı 🔧 | ⬜ |
| T207 | Stabilite testi (20–30 dk hareketli desen, kullanıcı işaretlemesi) | B | Sonnet → Opus | T206 | Onaylı Hz'de test tamamlandı 🔧 | ⬜ |
| T208 | Sürücü güncellemesi koruması (silinen override'ı onaylı değerle geri yükleme) | B | Sonnet → Opus | T202, T102 | Override silinmiş senaryoda doğru geri yükleme | ⬜ |

## Faz 3 — Görüntü ve renk boru hattı (v1)

| No | Görev | Sınıf | Yazan → İnceleyen | Bağımlı | Bitti sayılır | Durum |
|---|---|---|---|---|---|---|
| T301 | DDC katmanı: yetenek testi (DDC var mı, hangi kodlar), okuma/yazma, yazma sınırı (bırakınca tek yazma, tekrar engeli) | A | Opus → Sonnet | T003, T103 | Gerçek monitörün DDC yetenekleri profile yazıldı 🔧 | ⬜ |
| T302 | Renk boru hattı: GPU tablosunun tek sahibi; katman sırası (kalibrasyon → mavi ışık → ek karartma); tablo aralığı kaydı; çakışma tespiti (Windows Gece ışığı, f.lux, NVIDIA masaüstü renk ayarları); değişen tabloyu geri yükleme; kapanışta sadece kalibrasyon | A | Opus → Sonnet | T103, T105 | Tablo uygulanıp geri alınıyor; dış değişiklik tespit edilip geri yükleniyor 🔧 | ⬜ |
| T303 | GPU dithering (bağlantı başına aç/kapat) | A | Opus → Sonnet | T102, T103 | Gradyan deseninde bantlaşma azalıyor 🔧 | ⬜ |
| T304 | RGB Full/Limited: tespit, Full'e sabitleme, her onaylı Hz'de doğrulama | A | Opus → Sonnet | T303 | 60 Hz ve özel Hz'lerde aynı siyah seviyesi 🔧 | ⬜ |
| T305 | DDC paneli arayüzü: parlaklık, kontrast, renk ön ayarı, RGB, giriş, hoparlör sesi; DDC yoksa OSD talimatları | C | Sonnet → Opus | T301 | Desteklenen tüm kontroller çalışıyor | ⬜ |
| T306 | Parlaklık ve ek karartma: tek kaydırıcı (arka ışık → GPU), %15 taban, titreşimsiz mod, dijital parlaklık koruması | B | Sonnet → Opus | T302, T305 | Arka ışık tabanından sonra karartma GPU'da, dithering açık 🔧 | ⬜ |
| T307 | Mavi ışık filtresi: Kelvin hesabı, monitör + GPU karma uygulama, kalibrasyona fark, Low Blue Light kısayolu, isteğe bağlı yavaş zamanlama (sadece renk) | B | Sonnet → Opus | T302, T305 | 6500K → 1900K arası nötr sıcak beyaz; arka ışık değişmiyor 🔧 | ⬜ |
| T308 | Presetler: Gece (mavi ışık + düşük parlaklık), Kod, DaVinci, Film, Oyun; sadece elle | B | Sonnet → Opus | T306, T307 | Preset sadece seçilince uygulanıyor | ⬜ |
| T309 | Canlılık (NVIDIA Digital Vibrance): sadece Film ve Oyun presetlerinde; kapanışta varsayılana dönüş | A | Opus → Sonnet | T308 | DaVinci ve ölçümde kapalı 🔧 | ⬜ |
| T310 | Sistem tepsisi ve kısayollar; panik kısayolu (tüm efektler ve katmanlar kapanır, kalibrasyon kalır) | C | Sonnet → Opus | T308 | Tepsiden preset, mavi ışık, karartma; panik kısayolu çalışıyor | ⬜ |
| T311 | Görüntü iyileştirme rehberi (RTX VSR, DLDSR, DLSS, DaVinci Super Scale) | C | Sonnet → Opus | T001 | Metinler ve kısayollar hazır | ⬜ |

## Faz 4 — Üst-alt ışık farkı ve GPU (v1)

| No | Görev | Sınıf | Yazan → İnceleyen | Bağımlı | Bitti sayılır | Durum |
|---|---|---|---|---|---|---|
| T401 | Açı ayar deseni, teşhis testi (açı mı arka ışık mı) ve oturma rehberi (göz yüksekliği, mesafe, eğim) | C | Sonnet → Opus | T204 | Desenin eşit göründüğü konum bulunuyor 🔧 | ⬜ |
| T402 | Dikey denge katmanı (elle): tıklama geçirgen, kayda girmeyen, en üstte; güç kaydırıcısı; testlerde ve tam ekranda gizlenme | B | Sonnet → Opus | T401 | Üst-alt fark gözle azalıyor 🔧 | ⬜ |
| T403 | GPU algılama: 3090 + 3080 (3070 takılırsa üçüncü kart), monitörün bağlı olduğu kart, temel sıcaklık okuma, kablo taşınınca profil taşıma | B | Sonnet → Opus | T102 | Kartlar doğru tanınıyor 🔧 | ⬜ |
| T404 | GPU yönlendirme paneli (uygulama → kart) | B | Sonnet → Opus | T403 | DaVinci 3090'da çalışıyor, görüntü 3080'den 🔧 | ⬜ |
| T405 | Modlar: AI (3090 boş), Oyun (oyun doğrudan 3080'de, onaylı Hz), Render (DaVinci 3090'da veya iki kart); sadece elle | B | Sonnet → Opus | T404, T308, T206 | Her mod doğru ayarları uyguluyor | ⬜ |
| T406 | Ekran bilgi katmanı: Hz, FPS (PresentMon), iki kartın sıcaklığı; testlerde gizlenme | B | Sonnet → Opus | T403 | Oyunda FPS görünüyor 🔧 | ⬜ |

## Faz 5 — v1 tamamlama

| No | Görev | Sınıf | Yazan → İnceleyen | Bağımlı | Bitti sayılır | Durum |
|---|---|---|---|---|---|---|
| T501 | Kayıt defteri ve ayarları dışa/içe aktarma | C | Sonnet → Opus | T102, T103 | Dışa aktarılan dosya temiz kurulumda geri yükleniyor | ⬜ |
| T502 | Çeviriler: Türkçe, Azerbaycanca, İngilizce | C | Sonnet → Opus | T001 | Tüm ekranlar üç dilde | ⬜ |
| T503 | **v1 kontrol noktası:** uçtan uca test, `LESSONS.md`, v2'ye geçiş kararı | K | Kullanıcı → Claude app Opus max | Faz 0–5 | Kullanıcı v1'i onayladı 🔧 | ⬜ |

## Faz 6 — Pixel yardımcı uygulaması (v2)

| No | Görev | Sınıf | Yazan → İnceleyen | Bağımlı | Bitti sayılır | Durum |
|---|---|---|---|---|---|---|
| T601 | Android iskeleti, eşleştirme ve iletişim protokolü (yerel ağ, eşleştirme kodu, izinli komut listesi) | A | Opus → Sonnet | T503 | Eşleşmemiş cihaz komut gönderemiyor | ⬜ |
| T602 | Kamera kilidi: RAW, sabit ISO/enstantane/beyaz dengesi/odak, pozlama ≥ 1/15 s, çoklu kare, göz konumu talimatı (CinePixel'den) | B | Sonnet → Opus | T601 | Arka arkaya çekimler aynı ayarlarla 🔧 | ⬜ |
| T603 | PC tetiklemeli otomatik çekim ve aktarım | B | Sonnet → Opus | T602 | PC deseni gösterip fotoğrafı alıyor 🔧 | ⬜ |
| T604 | Uzaktan kontrol ekranı: preset, parlaklık/karartma, mavi ışık, mod, onaylı Hz'ler; deneysel komut yok | C | Sonnet → Opus | T601, T308, T405 | Telefondan tüm izinli komutlar çalışıyor 🔧 | ⬜ |

## Faz 7 — Kalibrasyon (v2)

| No | Görev | Sınıf | Yazan → İnceleyen | Bağımlı | Bitti sayılır | Durum |
|---|---|---|---|---|---|---|
| T701 | Fotoğraf analiz çekirdeği: DNG çözme, renk matrisiyle yaklaşık XYZ, çoklu kare ortancası, pozlama basamaklama, sensör siyah seviyesi, tekrarlanabilirlik (T205'in analizini de buraya taşır) | B | Sonnet → Opus | T603, T205 | Örnek setle tutarlı ölçüm; tekrarlanabilirlik raporlanıyor | ⬜ |
| T702 | Kontrast ve siyah seviyesi | B | Sonnet → Opus | T701, T301 | Doğru kontrast noktası öneriliyor 🔧 | ⬜ |
| T703 | Beyaz noktası döngüsü: DDC RGB kazancı (D65 ya da doğal beyaz); DDC yoksa GPU | B | Sonnet → Opus | T702, T302 | Beyaz hedefe yaklaşıyor 🔧 | ⬜ |
| T704 | Gri dengesi ve ton eğrisi: 17 basamak, yumuşatma, koyu tonlarda kademeli azaltma, GPU tablosunun kalibrasyon katmanı; dithering zorunlu | B | Sonnet → Opus | T703, T303 | Gri yamalarda renk kayması azalıyor, bantlaşma yok 🔧 | ⬜ |
| T705 | ICC profili (matris + ton eğrisi + VCGT) oluşturma ve Windows'a yükleme | A | Opus → Sonnet | T704 | Profil yükleniyor, kaldırılabiliyor 🔧 | ⬜ |
| T706 | DaVinci görüntüleyici 3D LUT (`.cube`, Rec.709 / gamma 2.4) ve Resolve talimatı | B | Sonnet → Opus | T705 | LUT Resolve'da yükleniyor; kalibrasyon iki kez uygulanmıyor 🔧 | ⬜ |
| T707 | Hz başına kalibrasyon (RGB aralığı dahil); mavi ışık ve karartma fark olarak üstüne | B | Sonnet → Opus | T705, T304 | Hz değiştirilince doğru değerler yükleniyor | ⬜ |

## Faz 8 — Testler ve izleme (v2)

| No | Görev | Sınıf | Yazan → İnceleyen | Bağımlı | Bitti sayılır | Durum |
|---|---|---|---|---|---|---|
| T801 | Parlaklık ölçümü (yaklaşık nit; ek karartma kademelerinin nit karşılığı) ve PWM testi (titreşimsiz taban) | B | Sonnet → Opus | T701, T306 | Titreşimsiz taban profile yazıldı 🔧 | ⬜ |
| T802 | Panel testleri: ölü piksel, ışık sızması, homojenlik teşhisi (uzaktan, zoomla), ghosting | B | Sonnet → Opus | T701, T204 | Homojenlik haritası üretiliyor 🔧 | ⬜ |
| T803 | Ölçümle dikey denge (göz konumundan üst/orta/alt) | B | Sonnet → Opus | T802, T402 | Katman eğrisi ölçümden geliyor 🔧 | ⬜ |
| T804 | Ghosting ve overdrive Hz testi (OSD'de overdrive yoksa sadece ghosting) | B | Sonnet → Opus | T701, T206 | Her onaylı Hz için sonuç var 🔧 | ⬜ |
| T805 | İçerik Hz'leri: 50 (öncelikli), 72, 48; test edilerek ekleme, elle seçim | A | Opus → Sonnet | T206 | Kabul edilenler listede, edilmeyenler yok 🔧 | ⬜ |
| T806 | GPU izleme paneli (iki kart; tepside ve telefonda) | C | Sonnet → Opus | T403, T604 | Değerler canlı görünüyor | ⬜ |
| T807 | Güç yönetimi: 3090 ve 3080 güç limitini düşürme / varsayılana dönme | A | Opus → Sonnet | T806, T105 | Varsayılanın üstüne çıkılamıyor 🔧 | ⬜ |
| T808 | Monitör karnesi (önce/sonra karşılaştırmalı rapor) | C | Sonnet → Opus | T707, T801, T802, T803, T804, T805, T806, T807 | Tüm sonuçlar tek raporda | ⬜ |
| T809 | **v2 kontrol noktası** | K | Kullanıcı → Claude app Opus max | Faz 6–8 | Kullanıcı v2'yi onayladı 🔧 | ⬜ |

## Faz 9 — Deneysel (v3)

| No | Görev | Sınıf | Yazan → İnceleyen | Bağımlı | Bitti sayılır | Durum |
|---|---|---|---|---|---|---|
| T901 | 10-bit sinyal testi (sadece 50 Hz CVT-RB) | A | Opus → Sonnet | T202, T809 | Kabul edilmezse otomatik geri alınıyor 🔧 | ⬜ |
| T902 | Hareket yumuşatma: RIFE + mpv kurulumu ve yönetimi (hesap 3090'da) | B | Sonnet → Opus | T403, T809 | Yerel video onaylı Hz'de yumuşatılıyor 🔧 | ⬜ |
| T903 | Kartlar arası geçiş: 3090 → aktif DP→VGA → VGA; görünmeyen ekranı kapatma | A | Opus → Sonnet | T301, T403, T809 | Geçişte pencere kaybolmuyor 🔧 | ⬜ |
| T904 | Satır bazlı ton telafisi: masaüstü yakalama + gölgelendirici; varsayılan kapalı, korumalı video uyarısı, panik kısayolu | B | Sonnet → Opus | T803, T809 | Orta tonlarda üst-alt fark azalıyor 🔧 | ⬜ |
| T905 | Kolorimetre modu (ArgyllCMS üzerinden; isteğe bağlı, kolorimetre alınırsa v2'ye çekilir) | B | Sonnet → Opus | T705, T809 | Kolorimetreyle kalibrasyon tamamlanıyor 🔧 | ⬜ |
| T906 | NVIDIA renk dönüşümü arka ucu (LUT-matris-LUT, deneysel) | A | Opus → Sonnet | T302, T705, T809 | Açılıp kapanıyor; gamma tablosuyla aynı anda çalışmıyor 🔧 | ⬜ |
| T907 | Keşif: yavaş, salt okunur DDC taraması; onaylı yazma; servis modu görünümü | A | Opus → Sonnet | T301, T809 | Tarama monitörü dondurmadan bitiyor 🔧 | ⬜ |
| T908 | Keşif: firmware **sadece okuma** (en son görev, önce uyarı) | A | Opus → Sonnet | T907 | Okuma denendi, sonuç `LESSONS.md`'de; yazma kodu yok 🔧 | ⬜ |
| T909 | **v3 kontrol noktası** | K | Kullanıcı → Claude app Opus max | Faz 9 | Kullanıcı v3'ü onayladı 🔧 | ⬜ |

---

## Özet

| Faz | Görev | Opus yazar | Sonnet yazar | Kontrol noktası |
|---|---|---|---|---|
| 0 | 4 | 1 | 3 | — |
| 1 | 5 | 4 | 1 | — |
| 2 | 8 | 3 | 5 | — |
| 3 | 11 | 5 | 6 | — |
| 4 | 6 | 0 | 6 | — |
| 5 | 3 | 0 | 2 | T503 |
| 6 | 4 | 1 | 3 | — |
| 7 | 7 | 1 | 6 | — |
| 8 | 9 | 2 | 6 | T809 |
| 9 | 9 | 5 | 3 | T909 |
| **Toplam** | **66** | **22** | **41** | **3** |
