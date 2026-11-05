# Fazlar ve Görevler

**Sıradaki (2026-11-05, tek model Opus, inceleme yok): kullanıcının 🔧 monitör testleri, sonra T503.** T502 ✅ (2026-11-05; Azerbaycanca ve İngilizce çeviriler, dil seçimi ana pencerede, varsayılan Türkçe). T314 ✅ (inceleme atlandı, kullanıcı kararı 2026-11-05; testler CI'da ve bulutta geçti). T315 🔧 (yazıldı 2026-11-05; sayfa ViewModel ve çizgili yama testli; monitörde sihirbaz baştan sona kullanıcıda). Kullanıcının monitörde yapacağı 🔧 testler: T314 (açılış, tepsi ve kapanış), T004 (14 eşlemesi ve 75/76 Hz AE), T104 (kurtarma dosyasını bir kez çalıştırma), T209 (60 ↔ 76 geçişi), T301 (okuma ve tek parlaklık yazması), T303 (gradyanda bant yok, `RealNvidiaTests`), T312 + T315 (sihirbaz baştan sona, tek test; `05-HANDOFF.md` → 5). Diğer yazılmış görevler ✅. Kapsam 2026-10-30'da 66 görevden 21'e indi (`07-INCELEME.md`, 7. tur; plan dışı kalanlar `06-CIKARILANLAR.md`).

## Çalışma kuralları

### Araçlar, modeller ve kota sırası

Düzen 2026-11-05'ten beri: **tek model, Opus; ayrı inceleme yok** (kullanıcı kararı). Sonnet hattı iptal edildi; Qwen 2026-11-04'te çıkarıldı; Antigravity (Gemini, Flash) kullanılmıyor.

| Kısaltma | Nerede | Model | Düşünme seviyesi (effort) | Kota |
|---|---|---|---|---|
| **Opus** | Claude Code, PC (masaüstü uygulaması, Windows) | Claude Opus 5.5 | **High**; sadece metin işinde (T311, T502, sonuç bildirme) **Medium** | Claude aboneliği |

- **Kısa kural:** kod işi **High**, sadece metin işi **Medium**; hiçbir işte Low yok.
- **Yedek:** Claude Code bulut oturumu (claude.ai/code, Linux), PC'ye ulaşılamadığında. Orada Windows'a özel testler çalışmaz (CI'da).
- **Kullanım sınırı dolarsa** (`/usage` ne zaman yenileneceğini gösterir): yenilenene kadar bekle.
- Görevin ortasında sınır dolarsa model değiştirilmez: yenilenince aynı oturumda "devam et" yazılır.

### Görev sınıfları

| Sınıf | Ne tür iş | Yazan |
|---|---|---|
| **A** | Donanıma veya sistem ayarına yazan, güvenlikle ilgili ya da her şeyin temeli olan iş | Opus (`03-GUVENLIK.md` kontrol listesine kendisi bakar) |
| **B** | Mantık, algoritma, akış | Opus |
| **C** | Arayüz, metin | Opus (metin ve çeviride Medium) |
| **K** | Son kontrol noktası | Kullanıcı (monitör başında) + Claude app, Opus (yeni sohbet) |

- **İnceleme yok:** yazan, bitirmeden önce `05-HANDOFF.md` → 3'teki kontrol listesine kendisi bakar. Kalite testlerle ve 🔧 görevlerde kullanıcının monitör testiyle korunur.
- Bir oturumda sırayla birkaç görev yapılabilir; her görev ayrı commit'tir.
- 2026-11-05'ten önceki görevler iki modelle (yazan ve farklı inceleyen) yapıldı; kaydı commit mesajlarında (`git log --oneline --grep "inceleme"`).
- 🔧 testi geçmezse düzeltmeyi Opus yapar (`05-HANDOFF.md` → 4).

### Görev kuralları

- Her görev yaklaşık **200 satır** değişiklik ve tek konuşmada bitecek boyutta. **Önce test**, sonra kod.
- Sözleşmeler (T002) kodda yaşar; bir görev sözleşmeyi değiştirmek zorundaysa önce `02-MIMARI.md` güncellenir.
- Kırmızı testler `[Pending("T___")]` ile atlanır (`tests/MonitorTuner.Tests/Contracts`). Görevi yazan kendi testlerinde `[Pending]`'i `[Fact]` yapar ve testi doldurur; görev bitince kendi numarasıyla `[Pending]` kalmaz.
- Görev bitince: derleme ve testler geçerse durum ✅ (🔧 görevde 🔧), commit (`T___: ...`), gerekirse `LESSONS.md`. Commit gövdesinde ne yapıldı, neler test edildi ve dikkat edilmesi gereken 3 nokta yazar.
- 🔧 işaretli görev, kullanıcı gerçek monitörde test etmeden ✅ olmaz. Yazan, test adımlarını commit gövdesine ve kullanıcıya verdiği özete yazar.
- Bir görev, bağımlı olduğu görevler ✅ ya da 🔧 (kodu bitti, monitör testi bekliyor) olmadan başlamaz.
- Kullanım limiti yaklaşınca yeni göreve başlanmaz; yarım görev bırakılmaz.

Durum: ⬜ bekliyor · 🟨 yazıldı, incelemede (2026-11-05'ten beri kullanılmıyor) · 🔧 kod bitti, monitör testi bekliyor · ✅ bitti

### Hat

- Tek hat: `hp-opus` klasörü, Claude Code'da Opus. Görevler sırayla yazılır.
- `hp-inceleme` klasörü sadece monitör testi için yerel derlemede (`05-HANDOFF.md` → 5, B yolu) ve yardım oturumlarında (`05-HANDOFF.md` → 7) kullanılır. `hp-sonnet` ve `hp-sonnet2` artık gerekmez.

## Dalgalar

Dalgalar eski (iki modelli) düzenin kaydı. 2026-11-05'ten beri kalan görevler tek hatta sırayla yapılır.

| Dalga | Görevler |
|---|---|
| 1 | T001 |
| 2 | T002, T311 (Flash) |
| 3 | T003, T004 |
| 4 | T103, T102, T307 |
| 5 | T301, T305, T306 |
| 6 | T302, T312 |
| 7 | T303, T313, T314, T315 (sırayla) |
| 8 | T209, T308 |
| 9 | T104, T310 |
| 10 | T502 |
| 11 | T503: kullanıcı + Claude app |

---

## Faz 0 — Hazırlık

| No | Görev | Sınıf | Model | Bağımlı | Dalga | Bitti sayılır | Durum |
|---|---|---|---|---|---|---|---|
| T001 | Proje iskeleti: `02-MIMARI.md` → Klasör yapısı'ndaki 8 `MonitorTuner.*` projesi ve test projeleri; dil altyapısı (tüm metinler baştan çeviri dosyasından, özellik başına ayrı `.resx`, ana dil Türkçe); sessiz derleme/test betikleri (`tools/dev`, bash + PowerShell); CI: Ubuntu (derleme + platformdan bağımsız testler) ve Windows (derleme + tüm testler + çalıştırılabilir self-contained `win-x64` paket, 7 gün saklama); mimari testleri (`02-MIMARI.md` → CI korumaları); `05-HANDOFF.md` → 5'e test paketinin nasıl açılacağı | A | Opus | — | 1 | Boş çözüm PC'de (Windows) ve CI'da (Ubuntu + Windows) derleniyor; platformdan bağımsız testler Linux'ta da çalışıyor; CI yeşil ve Windows paketi indirilebiliyor; mimari testleri bilerek ihlal eden bir örneği yakalıyor (testte kanıtlı) | ✅ |
| T002 | Sözleşmeler: `02-MIMARI.md`'deki modül sözleşmelerini arayüzlere ve boş (kırmızı) testlere çevir; donanım yazma metotları geri alma noktası ister; renk boru hattı dahil | A | Opus | T001 | 2 | Her modülün arayüzü ve en az bir testi var | ✅ |
| T003 | Sahte donanım: sahte monitör (DDC'li ve DDC'siz, bellek yazma sayacı), sahte GPU renk tablosu, sahte NVIDIA ayarları (dithering, canlılık), sahte ekran modları, sahte kayıt defteri, sahte saat; örnek EDID | B | Opus | T002 | 3 | Donanımsız tüm testler sahte donanımla çalışıyor | ✅ |
| T004 | Monitör profil dosyası: şema + HP P21b G4 profili (Hz modları ve timing'leri: 50, 60, 72, 74, 75, 76 ve test durumları; ölçülen sınır; OSD yetenekleri; DDC kod tablosu ve 14 kodunun ön ayar eşlemesi; AE/AC okuma kuralları; PWM yok; kaynak `donanim/hp-p21b-g4/`) | B | Opus | T002 | 3 | Profil yükleniyor; ilk gün sonuçları işlendi 🔧 | 🔧 |

## Faz 1 — Çekirdek

| No | Görev | Sınıf | Model | Bağımlı | Dalga | Bitti sayılır | Durum |
|---|---|---|---|---|---|---|---|
| T102 | Monitör tanıma (model, seri no, bağlantı tipi, bağlı olduğu kart; VGA'daysa uyarı) ve ayar deposu (`%AppData%` altında JSON, yedekler ayrı klasörde) | B | Opus | T003 | 4 | Sahte sistemle tanıma ve depo testleri geçiyor | ✅ |
| T103 | Güvenlik katmanı: DDC ve GPU tablosu yedeği, geri alma noktaları, 15 sn onay zamanlayıcısı, "fabrika ayarına dön", kaldırma temizliği | A | Opus | T003 | 4 | Sahte donanımla her yazma geri alınabiliyor; onaylanmayan değişiklik 15 sn'de dönüyor | ✅ |
| T104 | Kurtarma dosyası (GPU tablosunu sıfırlar, `GdiIcmGammaRange`'i eski haline döndürür, dithering ve canlılığı varsayılana alır) ve "görüntü gelmezse" talimatı (başka port, güvenli mod, NVIDIA özel çözünürlüğünü silme) | A | Opus | T103, T302, T303 | 9 | Kurtarma dosyası sahte sistemde her şeyi varsayılana döndürüyor; gerçek PC'de bir kez çalıştırıldı 🔧 | 🔧 |

## Faz 2 — Yenileme hızı

| No | Görev | Sınıf | Model | Bağımlı | Dalga | Bitti sayılır | Durum |
|---|---|---|---|---|---|---|---|
| T209 | Hz paneli: Windows mod listesi; onaylı modlar (76, 75, 72, 60, 50) arasında elle geçiş; 15 sn onay; monitörden (DDC AE) gerçek Hz doğrulaması; sürücü güncellemesi modu silerse uyarı ve NVIDIA'ya girilecek değerler (profilden); spec dışı moda ilk geçişte uyarı | A | Opus | T004, T103, T301 | 8 | Sahte sistemde geçiş, geri dönüş ve doğrulama testleri geçiyor; gerçek monitörde 60 ↔ 76 geçişi ve geri dönüş 🔧 | 🔧 |

## Faz 3 — Görüntü, renk ve göz konforu

| No | Görev | Sınıf | Model | Bağımlı | Dalga | Bitti sayılır | Durum |
|---|---|---|---|---|---|---|---|
| T301 | DDC katmanı: yetenek testi (DDC var mı, hangi kodlar), okuma/yazma, yazma izin listesi (04/05/08 ve E6–FF asla), yazma sınırı (bırakınca tek yazma, tekrar engeli), AC değerinin taşma düzeltmesi | A | Opus | T004, T103 | 5 | Sahte monitörde sınırlar çalışıyor; gerçek monitörde okuma ve tek bir parlaklık yazması 🔧 | 🔧 |
| T302 | Renk boru hattı: GPU tablosunun tek sahibi; katman sırası kalibrasyon → mavi ışık (GPU kısmı) → ek karartma; `GdiIcmGammaRange` (tek seferlik yönetici onayı, eski değer yedeklenir); dış değişiklik tespiti, geri yükleme ve uyarı; kapanışta tabloda sadece kalibrasyon kalır | A | Opus | T103 | 6 | Sahte tabloda birleştirme, geri yükleme ve kapanış testleri geçiyor; gerçek PC'de tablo yazılıp geri alındı 🔧 | ✅ |
| T303 | NVIDIA ayarları: dithering (GPU tablosu kullanılırken açık) ve canlılık (sadece Film ve Oyun profillerinde); kapanışta ikisi de varsayılana döner | A | Opus | T302 | 7 | Sahte sürücüyle aç/kapa ve geri dönüş testleri geçiyor; gerçek monitörde gradyanda bant yok 🔧 | 🔧 |
| T305 | DDC paneli: parlaklık, kontrast, renk ön ayarı, RGB kazancı, keskinlik, giriş, hoparlör sesi; dijital parlaklık koruması; DDC yoksa OSD talimatı | C | Opus | T102 | 5 | Sahte monitörle panel çalışıyor | ✅ |
| T306 | Parlaklık ve ek karartma: tek kaydırıcı (arka ışık 100 → 0, sonra GPU ile %15 tabana kadar); kaydırıcı bırakılınca tek yazma | B | Opus | T003 | 5 | Karartma planı sınır değer testleri geçiyor | ✅ |
| T307 | Mavi ışık filtresi: Kelvin hesabı (6500K → 1900K); önce monitör RGB kazancı, sınırda GPU; kalibrasyona fark olarak; Low Blue Light kısayolu; isteğe bağlı yavaş akşam geçişi (varsayılan kapalı, sadece renk); Gece ışığı/f.lux uyarısı | B | Opus | T003 | 4 | Kelvin tablosu (`08-GORUNTU-YONTEMLERI.md` → 4) birebir; monitör/GPU paylaşımı testli | ✅ |
| T308 | Renk profilleri (presetler): Gündüz, Akşam, Gece, Kod, Film, Oyun, DaVinci zaman çizelgesi (nötr, efektsiz) ve kullanıcı profilleri; parlaklık, renk, mavi ışık, ek karartma ve canlılığı bir kez ayarlar; Hz'e dokunmaz; sadece elle | B | Opus | T303, T306, T307 | 8 | Her profil sahte donanımda doğru değerleri yazıyor; Hz değişmiyor | ✅ |
| T310 | Sistem tepsisi, kısayollar, panik kısayolu (mavi ışık, ek karartma, canlılık kapanır; kalibrasyon kalır) | C | Opus | T308 | 9 | Tepsiden profil seçimi ve panik testleri geçiyor | ✅ |
| T311 | Rehber (metin): oturma ve açı (TN), Hz kurulumu (NVIDIA özel çözünürlük değerleri, `00-DONANIM.md`), kare atlama ve stabilite testi (testufo, ışığa duyarlılık uyarısıyla), RTX Video Super Resolution, DLDSR, ClearType, ortam ışığı, 20-20-20 kuralı, Gece ışığı/f.lux kapalı | C | Opus | T001 | 2 | Metinler çeviri dosyasında; uygulamada rehber sayfasında açılıyor | ✅ |
| T312 | Ayar sihirbazı 1 (desenler ve monitör ayarları): ısınma ve konum hatırlatması; siyah/beyaz kırpma ile kontrast ve parlaklık; keskinlik (yazı kenarında hale yok); RGB Full kontrolü; TN açı deseni; ölü piksel ve homojenlik; Pixel ekranıyla yan yana beyaz eşleme (DDC RGB kazancı); sonucu "temel ayar" olarak kaydeder | B | Opus | T305 | 6 | Desenler açılıyor, adımlar sahte monitörde değer yazıyor; gerçek monitörde sihirbaz baştan sona 🔧 (sayfa T315'te geldi; bu test T315'inkiyle birlikte yapılır, adımlar `05-HANDOFF.md` → 5, T312 + T315) | 🔧 |
| T313 | Ayar sihirbazı 2, mantık ve servis (gözle kalibrasyon): gamma (çizgili alan ile düz gri desenleri, hedef 2.2) ve gri dengesi (koyu, orta ve açık gri) eğri hesabı; `SetCalibration` sonucu renk boru hattının kalibrasyon katmanına, dithering açık; DDC yoksa beyaz eşleme GPU'da; sihirbazda canlılık kapalı (`02-MIMARI.md` → T312). Sayfa ve bağlama T314–T315'e bölündü (2026-11-05, kullanıcı onayı: görev ~1000 satır çıkıyordu) | B | Opus | T302, T312 | 7 | Eğri hesabı, desenler ve sihirbaz akışı sahte donanımla testli (kullanıcı testi yok; 🔧 T315'te) | ✅ |
| T314 | Bağlama (`MonitorTuner.Composition`) ve uygulama açılışı: gerçek donanımı servislere bağlar (Güvenlik, DDC, renk boru hattı, NVIDIA, parlaklık, mavi ışık, DDC paneli, sihirbaz, ayar deposu, monitör seçimi); açılışta kalibrasyonu uygular, `EnsureFullGammaRange`'i çağırır ve tepsiyi bağlar (`TrayHost`, `02-MIMARI.md` → T310); kapanışta `Shutdown` sırası; `App` sadece arayüz arayüzlerini görür (mimari testler). T310 de Composition'a dokunur: kendi bölümünde tut | B | Opus | T313 | 7 | Bağlama sahte donanımla testli; mimari testler (App `Hardware`'i görmez) geçiyor. İnceleme atlandı, kullanıcı kararı (2026-11-05). Gerçek monitörde açılış, tepsi ve kapanış denemesi kullanıcıda 🔧 | ✅ |
| T315 | Ayar sihirbazı sayfası (`MainWindow` sekmesi): adımlar, `PatternWindow` ile desenler (çizgili yama dahil), DDC değerleri ve kalibrasyon seçimleri, iptal ve bitiş; T312'nin bekleyen monitör testi bu sayfayla yapılır | C | Opus | T314 | 7 | Sayfa ViewModel testli; gerçek monitörde sihirbaz baştan sona, gri basamaklar nötr, gradyanda bant yok 🔧 (adımlar `05-HANDOFF.md` → 5, T312 + T315; T312'nin monitör testi de bununla yapılır) | 🔧 |

## Faz 5 — Tamamlama

(Faz 4 tamamen plan dışında kaldı.)

| No | Görev | Sınıf | Model | Bağımlı | Dalga | Bitti sayılır | Durum |
|---|---|---|---|---|---|---|---|
| T502 | Çeviriler: Azerbaycanca ve İngilizce (Türkçe ana dil) | C | Opus | T209, T305–T315 | 10 | Tüm ekranlar üç dilde (çeviri dosyaları eksiksiz, testli; dil ana pencereden seçilir, varsayılan Türkçe) | ✅ |
| T503 | **Son kontrol noktası:** uçtan uca test, `LESSONS.md`, plan dışı fikirler için karar | K | Kullanıcı + Claude app Opus | hepsi | 11 | Kullanıcı onayı | ⬜ |

## Özet

| Faz | Görev | Kontrol noktası |
|---|---|---|
| 0 | 4 | — |
| 1 | 3 | — |
| 2 | 1 | — |
| 3 | 11 | — |
| 5 | 2 | T503 |
| **Toplam** | **21** | **1** |

**Kalan iş (2026-11-05):** kullanıcının 🔧 monitör testleri; T503 (Claude app, Opus).
