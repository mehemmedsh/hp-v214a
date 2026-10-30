# Fazlar ve Görevler

**Sıradaki görev: T001** (Dalga 1, Opus hattı). Kapsam 2026-10-30'da 66 görevden 21'e indi (`07-INCELEME.md`, 7. tur; plan dışı kalanlar `06-CIKARILANLAR.md`).

## Çalışma kuralları

### Araçlar, modeller ve kota sırası

| Kısaltma | Nerede | Model | Düşünme seviyesi | Kota |
|---|---|---|---|---|
| **Opus** | Antigravity | Claude Opus 5.5 | seçenek varsa en yüksek | Antigravity Claude havuzu |
| **Sonnet** | Antigravity | Claude Sonnet 5.5 | seçenek varsa en yüksek | Antigravity Claude havuzu |
| **Gemini** | Antigravity | Gemini 4 Argon (seçicide yoksa Gemini 3.8 Flash) | **High** | Antigravity Gemini havuzu |
| **Flash** | Antigravity | Gemini 3.8 Flash | **Medium** | Antigravity Gemini havuzu |

- **Önce Antigravity.** Antigravity'nin Claude havuzu biterse Opus ve Sonnet işleri Claude Code'da aynı modelle sürer (effort high). Gemini havuzu biterse Gemini işleri havuz yenilenene kadar bekler (5 saatte bir yenilenir, haftalık tavanı var).
- Görevin ortasında kota biterse model değiştirilmez: havuz yenilenince aynı konuşmada "devam et" yazılır.
- Antigravity'de Claude ve GPT aynı havuzu kullanır. Codex yok; tek GPT modeli (GPT-OSS-120B) 2 Kasım 2026'da kalkıyor. O havuzda sadece Opus ve Sonnet kullanılır.

### Görev sınıfları

| Sınıf | Ne tür iş | Yazan | İnceleyen |
|---|---|---|---|
| **A** | Donanıma veya sistem ayarına yazan, güvenlikle ilgili ya da her şeyin temeli olan iş | Opus | Gemini, `03-GUVENLIK.md` kontrol listesiyle |
| **B** | Mantık, algoritma, akış | Gemini (T003'ü temel olduğu için Opus yazar) | Opus (T003'ü Gemini) |
| **C** | Arayüz, metin | Gemini; metin ve çeviride Flash | Sonnet |
| **K** | Son kontrol noktası | Kullanıcı (monitör başında) | Claude app, Opus (yeni sohbet) |

- Yazan ve inceleyen her zaman farklı model. İnceleme hiçbir görevde atlanmaz.
- Yazma, inceleme ve düzeltme ayrı konuşmalardır. Düzeltmeyi yazan model yapar (`05-HANDOFF.md` → 4).

### Görev kuralları

- Her görev yaklaşık **200 satır** değişiklik ve tek konuşmada bitecek boyutta. **Önce test**, sonra kod.
- Sözleşmeler (T002) kodda yaşar; bir görev sözleşmeyi değiştirmek zorundaysa önce `02-MIMARI.md` güncellenir.
- Görev bitince: commit (`T___: ...`), durum 🟨, gerekirse `LESSONS.md`. Commit gövdesinde ne yapıldı, neler test edildi ve inceleyicinin bakması gereken 3 nokta yazar.
- 🔧 işaretli görev, kullanıcı gerçek monitörde test etmeden ✅ olmaz. Yazan, test adımlarını commit gövdesine ve kullanıcıya verdiği özete yazar.
- Bir görev, bağımlı olduğu görevler ✅ ya da 🔧 (kodu onaylı, monitör testi bekliyor) olmadan başlamaz.
- Kullanım limiti yaklaşınca yeni göreve başlanmaz; yarım görev bırakılmaz.

Durum: ⬜ bekliyor · 🟨 yazıldı, incelemede · 🔧 onaylandı, monitör testi bekliyor · ✅ bitti

### Paralel çalışma (hatlar)

- Her hat ayrı bir klasör (ayrı `git clone`) ve Antigravity'de ayrı bir penceredir: `hp-opus` (Opus yazma ve düzeltme), `hp-gemini` (Gemini yazma ve düzeltme), `hp-gemini2` (aynı dalgadaki ikinci Gemini işi), `hp-inceleme` (incelemeler; sadece okur, derler, test eder). Kurulum: `05-HANDOFF.md` → 0.
- Aynı dalgadaki görevler aynı anda yazılır, sonra çapraz incelenir: Opus'un görevini Gemini, Gemini'nin görevini Opus (B) ya da Sonnet (C) inceler. Bir dalga, görevlerinin hepsi onaylanınca biter.
- Kalite için: ilk iki dalgada tek yazma hattı; alışınca iki; rahat edince üç. Hat sayısı toplam kullanımı artırmaz, sadece işi kısaltır.

## Dalgalar

| Dalga | Opus hattı | Gemini hattı | 2. Gemini hattı |
|---|---|---|---|
| 1 | T001 | — | — |
| 2 | T002 | T311 (Flash) | — |
| 3 | T003 | T004 | — |
| 4 | T103 | T102 | T307 |
| 5 | T301 | T305 | T306 |
| 6 | T302 | T312 | — |
| 7 | T303 | T313 | — |
| 8 | T209 | T308 | — |
| 9 | T104 | T310 | — |
| 10 | — | T502 (Flash) | — |
| 11 | T503: kullanıcı + Claude app | | |

---

## Faz 0 — Hazırlık

| No | Görev | Sınıf | Yazan → İnceleyen | Bağımlı | Dalga | Bitti sayılır | Durum |
|---|---|---|---|---|---|---|---|
| T001 | Proje iskeleti: `02-MIMARI.md` → Klasör yapısı'ndaki 8 `MonitorTuner.*` projesi ve test projeleri; dil altyapısı (tüm metinler baştan çeviri dosyasından, özellik başına ayrı `.resx`, ana dil Türkçe); sessiz derleme/test betikleri (`tools/dev`, bash + PowerShell); CI: Ubuntu (derleme + platformdan bağımsız testler) ve Windows (derleme + tüm testler + çalıştırılabilir self-contained `win-x64` paket, 7 gün saklama); mimari testleri (`02-MIMARI.md` → CI korumaları); `05-HANDOFF.md` → 5'e test paketinin nasıl açılacağı | A | Opus → Gemini | — | 1 | Boş çözüm PC'de (Windows) ve CI'da (Ubuntu + Windows) derleniyor; platformdan bağımsız testler Linux'ta da çalışıyor; CI yeşil ve Windows paketi indirilebiliyor; mimari testleri bilerek ihlal eden bir örneği yakalıyor (testte kanıtlı) | ⬜ |
| T002 | Sözleşmeler: `02-MIMARI.md`'deki modül sözleşmelerini arayüzlere ve boş (kırmızı) testlere çevir; donanım yazma metotları geri alma noktası ister; renk boru hattı dahil | A | Opus → Gemini | T001 | 2 | Her modülün arayüzü ve en az bir testi var | ⬜ |
| T003 | Sahte donanım: sahte monitör (DDC'li ve DDC'siz, bellek yazma sayacı), sahte GPU renk tablosu, sahte NVIDIA ayarları (dithering, canlılık), sahte ekran modları, sahte kayıt defteri, sahte saat; örnek EDID | B | Opus → Gemini | T002 | 3 | Donanımsız tüm testler sahte donanımla çalışıyor | ⬜ |
| T004 | Monitör profil dosyası: şema + HP P21b G4 profili (Hz modları ve timing'leri: 50, 60, 72, 74, 75, 76 ve test durumları; ölçülen sınır; OSD yetenekleri; DDC kod tablosu ve 14 kodunun ön ayar eşlemesi; AE/AC okuma kuralları; PWM yok; kaynak `donanim/hp-p21b-g4/`) | B | Gemini → Opus | T002 | 3 | Profil yükleniyor; ilk gün sonuçları işlendi 🔧 | ⬜ |

## Faz 1 — Çekirdek

| No | Görev | Sınıf | Yazan → İnceleyen | Bağımlı | Dalga | Bitti sayılır | Durum |
|---|---|---|---|---|---|---|---|
| T102 | Monitör tanıma (model, seri no, bağlantı tipi, bağlı olduğu kart; VGA'daysa uyarı) ve ayar deposu (`%AppData%` altında JSON, yedekler ayrı klasörde) | B | Gemini → Opus | T003 | 4 | Sahte sistemle tanıma ve depo testleri geçiyor | ⬜ |
| T103 | Güvenlik katmanı: DDC ve GPU tablosu yedeği, geri alma noktaları, 15 sn onay zamanlayıcısı, "fabrika ayarına dön", kaldırma temizliği | A | Opus → Gemini | T003 | 4 | Sahte donanımla her yazma geri alınabiliyor; onaylanmayan değişiklik 15 sn'de dönüyor | ⬜ |
| T104 | Kurtarma dosyası (GPU tablosunu sıfırlar, `GdiIcmGammaRange`'i eski haline döndürür, dithering ve canlılığı varsayılana alır) ve "görüntü gelmezse" talimatı (başka port, güvenli mod, NVIDIA özel çözünürlüğünü silme) | A | Opus → Gemini | T103, T302, T303 | 9 | Kurtarma dosyası sahte sistemde her şeyi varsayılana döndürüyor; gerçek PC'de bir kez çalıştırıldı 🔧 | ⬜ |

## Faz 2 — Yenileme hızı

| No | Görev | Sınıf | Yazan → İnceleyen | Bağımlı | Dalga | Bitti sayılır | Durum |
|---|---|---|---|---|---|---|---|
| T209 | Hz paneli: Windows mod listesi; onaylı modlar (76, 75, 72, 60, 50) arasında elle geçiş; 15 sn onay; monitörden (DDC AE) gerçek Hz doğrulaması; sürücü güncellemesi modu silerse uyarı ve NVIDIA'ya girilecek değerler (profilden); spec dışı moda ilk geçişte uyarı | A | Opus → Gemini | T004, T103, T301 | 8 | Sahte sistemde geçiş, geri dönüş ve doğrulama testleri geçiyor; gerçek monitörde 60 ↔ 76 geçişi ve geri dönüş 🔧 | ⬜ |

## Faz 3 — Görüntü, renk ve göz konforu

| No | Görev | Sınıf | Yazan → İnceleyen | Bağımlı | Dalga | Bitti sayılır | Durum |
|---|---|---|---|---|---|---|---|
| T301 | DDC katmanı: yetenek testi (DDC var mı, hangi kodlar), okuma/yazma, yazma izin listesi (04/05/08 ve E6–FF asla), yazma sınırı (bırakınca tek yazma, tekrar engeli), AC değerinin taşma düzeltmesi | A | Opus → Gemini | T004, T103 | 5 | Sahte monitörde sınırlar çalışıyor; gerçek monitörde okuma ve tek bir parlaklık yazması 🔧 | ⬜ |
| T302 | Renk boru hattı: GPU tablosunun tek sahibi; katman sırası kalibrasyon → mavi ışık (GPU kısmı) → ek karartma; `GdiIcmGammaRange` (tek seferlik yönetici onayı, eski değer yedeklenir); dış değişiklik tespiti, geri yükleme ve uyarı; kapanışta tabloda sadece kalibrasyon kalır | A | Opus → Gemini | T103 | 6 | Sahte tabloda birleştirme, geri yükleme ve kapanış testleri geçiyor; gerçek PC'de tablo yazılıp geri alındı 🔧 | ⬜ |
| T303 | NVIDIA ayarları: dithering (GPU tablosu kullanılırken açık) ve canlılık (sadece Film ve Oyun profillerinde); kapanışta ikisi de varsayılana döner | A | Opus → Gemini | T302 | 7 | Sahte sürücüyle aç/kapa ve geri dönüş testleri geçiyor; gerçek monitörde gradyanda bant yok 🔧 | ⬜ |
| T305 | DDC paneli: parlaklık, kontrast, renk ön ayarı, RGB kazancı, keskinlik, giriş, hoparlör sesi; dijital parlaklık koruması; DDC yoksa OSD talimatı | C | Gemini → Sonnet | T102 | 5 | Sahte monitörle panel çalışıyor | ⬜ |
| T306 | Parlaklık ve ek karartma: tek kaydırıcı (arka ışık 100 → 0, sonra GPU ile %15 tabana kadar); kaydırıcı bırakılınca tek yazma | B | Gemini → Opus | T003 | 5 | Karartma planı sınır değer testleri geçiyor | ⬜ |
| T307 | Mavi ışık filtresi: Kelvin hesabı (6500K → 1900K); önce monitör RGB kazancı, sınırda GPU; kalibrasyona fark olarak; Low Blue Light kısayolu; isteğe bağlı yavaş akşam geçişi (varsayılan kapalı, sadece renk); Gece ışığı/f.lux uyarısı | B | Gemini → Opus | T003 | 4 | Kelvin tablosu (`08-GORUNTU-YONTEMLERI.md` → 4) birebir; monitör/GPU paylaşımı testli | ⬜ |
| T308 | Renk profilleri (presetler): Gündüz, Akşam, Gece, Kod, Film, Oyun, DaVinci zaman çizelgesi (nötr, efektsiz) ve kullanıcı profilleri; parlaklık, renk, mavi ışık, ek karartma ve canlılığı bir kez ayarlar; Hz'e dokunmaz; sadece elle | B | Gemini → Opus | T303, T306, T307 | 8 | Her profil sahte donanımda doğru değerleri yazıyor; Hz değişmiyor | ⬜ |
| T310 | Sistem tepsisi, kısayollar, panik kısayolu (mavi ışık, ek karartma, canlılık kapanır; kalibrasyon kalır) | C | Gemini → Sonnet | T308 | 9 | Tepsiden profil seçimi ve panik testleri geçiyor | ⬜ |
| T311 | Rehber (metin): oturma ve açı (TN), Hz kurulumu (NVIDIA özel çözünürlük değerleri, `00-DONANIM.md`), kare atlama ve stabilite testi (testufo, ışığa duyarlılık uyarısıyla), RTX Video Super Resolution, DLDSR, ClearType, ortam ışığı, 20-20-20 kuralı, Gece ışığı/f.lux kapalı | C | Flash → Sonnet | T001 | 2 | Metinler çeviri dosyasında; uygulamada rehber sayfasında açılıyor | ⬜ |
| T312 | Ayar sihirbazı 1 (desenler ve monitör ayarları): ısınma ve konum hatırlatması; siyah/beyaz kırpma ile kontrast ve parlaklık; keskinlik (yazı kenarında hale yok); RGB Full kontrolü; TN açı deseni; ölü piksel ve homojenlik; Pixel ekranıyla yan yana beyaz eşleme (DDC RGB kazancı); sonucu "temel ayar" olarak kaydeder | B | Gemini → Opus | T305 | 6 | Desenler açılıyor, adımlar sahte monitörde değer yazıyor; gerçek monitörde sihirbaz baştan sona 🔧 | ⬜ |
| T313 | Ayar sihirbazı 2 (gözle kalibrasyon): gamma (dama desenleriyle, hedef 2.2) ve gri dengesi (koyu, orta ve açık gride renk kayması kalmayacak şekilde); sonuç renk boru hattının kalibrasyon katmanına; dithering açık | B | Gemini → Opus | T302, T312 | 7 | Eğri hesabı testli; gerçek monitörde gri basamaklar nötr, gradyanda bant yok 🔧 | ⬜ |

## Faz 5 — Tamamlama

(Faz 4 tamamen plan dışında kaldı.)

| No | Görev | Sınıf | Yazan → İnceleyen | Bağımlı | Dalga | Bitti sayılır | Durum |
|---|---|---|---|---|---|---|---|
| T502 | Çeviriler: Azerbaycanca ve İngilizce (Türkçe ana dil) | C | Flash → Sonnet | T209, T305–T313 | 10 | Tüm ekranlar üç dilde | ⬜ |
| T503 | **Son kontrol noktası:** uçtan uca test, `LESSONS.md`, plan dışı fikirler için karar | K | Kullanıcı → Claude app Opus | hepsi | 11 | Kullanıcı onayı | ⬜ |

## Özet

| Faz | Görev | Opus yazar | Gemini / Flash yazar | Kontrol noktası |
|---|---|---|---|---|
| 0 | 4 | 3 | 1 | — |
| 1 | 3 | 2 | 1 | — |
| 2 | 1 | 1 | 0 | — |
| 3 | 11 | 3 | 8 | — |
| 5 | 2 | 0 | 1 | T503 |
| **Toplam** | **21** | **9** | **11** | **1** |

**Tahmini kullanım:** Antigravity Claude havuzu yaklaşık 20 konuşma (9 Opus yazma, 7 Opus inceleme, 4 Sonnet inceleme). Antigravity Gemini havuzu yaklaşık 20 konuşma (11 yazma, 9 inceleme). Düzeltmeler %20–40 ekler. Claude Code sadece Antigravity'nin Claude havuzu bitince kullanılır.
