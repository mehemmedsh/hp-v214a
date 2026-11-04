# Mimari

Uygulamanın parçaları ve birbirleriyle nasıl konuştukları. Kod içermez; T002 görevinde buradaki sözleşmeler koda (arayüzler ve testler) çevrilir.

## Teknoloji kararları (kullanıcı onayladı, 2026-10-30)

| Parça | Karar | Neden |
|---|---|---|
| Windows uygulaması | **C# / .NET 10 (LTS), WPF** | Kayıt defteri, DDC, NVIDIA arayüzleri ve tepsi için en kolay yol. .NET 8'in desteği 10 Kasım 2026'da bitiyor; .NET 10 Kasım 2028'e kadar destekli |
| Proje ayrımı | `MonitorTuner.*`. Core, Color, Hardware.Abstractions, Safety, Services `net10.0` (platformdan bağımsız); Hardware, Composition, App `net10.0-windows` + `EnableWindowsTargeting` | Linux (bulut, CI) çözümün tamamını derler ve platformdan bağımsız testleri çalıştırır; Windows'a özel testler PC'de ve CI'da. Proje adı sadece `App` olursa WPF'nin `App` sınıfıyla çakışıyor |
| Donanım arayüzleri | Ayrı `MonitorTuner.Hardware.Abstractions` (`net10.0`) | Güvenlik ve servisler donanımı arayüzden kullanır ve Linux'ta sahte donanımla test edilir; gerçek Windows kodu `Hardware`'de |
| Bağlama (composition root) | Ayrı `MonitorTuner.Composition` projesi | App, `Hardware`'i hiç görmez; katman ihlali CI'da derlenmiş derlemelerin referanslarından yakalanır |
| Yönetici yetkisi | Ayrı süreç yok. `GdiIcmGammaRange` için tek seferlik yönetici onayıyla tek kayıt yazması (eski değer önce yedeklenir) | Yetki isteyen tek iş bu |
| Hz modları | Kullanıcı NVIDIA özel çözünürlüğüyle bir kez ekler; uygulama Windows ekran API'siyle modlar arasında geçer | EDID override yazmaktan çok daha az riskli; ilk gün testleri bu yolla yapıldı |
| Test desenleri | WPF tam ekran pencereler (ayar sihirbazının içinde) | Kare atlama testi testufo ile yapıldığı için DirectX motoruna gerek yok |
| Ayarlar | `%AppData%` altında JSON; yedekler ayrı klasörde; monitör profili ayrı veri dosyası | Okunabilir, yedeklemesi kolay |
| Çeviri | `.resx`; özellik başına ayrı dosya (`src/MonitorTuner.App/Resources/Strings/<Özellik>.resx`); XAML'de `{loc:Tr Özellik.Ad}`, kodda `Loc.Get("Özellik.Ad")`; Türkçe nötr dil, Azerbaycanca ve İngilizce T502'de (`<Özellik>.az.resx`, `<Özellik>.en.resx`) | Paralel görevler aynı dosyada çakışmaz |
| Satır sonları | `.gitattributes`: metin dosyaları LF | PC (Windows) ve bulut (Linux) aynı repoda çalışıyor; bash betikleri bozulmaz |
| Derleme ve test çıktısı | `tools/dev/` içinde sessiz betikler (bash + PowerShell): sadece hata ve özet | Ajanların okuduğu çıktı kısalır, bilgi kaybı yok |
| 🔧 testleri için çalıştırma | CI her push'ta self-contained `win-x64` paket üretir (7 gün saklama); PC'de .NET 10 SDK ile yerel derleme de mümkün | Kullanıcı istediği yolu seçer |
| Geliştirme ortamı | Claude Code (PC, Windows, masaüstü uygulaması) ana araç; Claude Code bulut (Linux) yedek; Qwen yerel | Kurallar `AGENTS.md`'de |
| Harici | testufo.com (kare atlama ve hareket testi), NVIDIA Denetim Masası (Hz modu ekleme) | Var olan araçlar yeniden yazılmaz |

## Süreç

Tek süreç: ana uygulama, normal kullanıcı yetkisiyle. Arayüz, tepsi, ayar sihirbazı, DDC, renk boru hattı, NVIDIA ayarları ve Hz paneli bu süreçte. Yönetici onayı sadece `GdiIcmGammaRange` yazılırken ve geri alınırken istenir.

## Katmanlar

Kural: **donanıma veya sistem ayarına yazan her işlem Güvenlik katmanından geçer.** Arayüz hiçbir zaman donanım katmanını görmez.

```
Arayüz (WPF, tepsi, ayar sihirbazı ve desenler)                        MonitorTuner.App
        │                                    (bağlama: MonitorTuner.Composition)
Uygulama servisleri (Hz paneli, renk boru hattı, profiller,            MonitorTuner.Services
  mavi ışık, karartma, sihirbaz akışı)
        │
Güvenlik katmanı (yedek, geri alma noktası, 15 sn onay,                MonitorTuner.Safety
  DDC yazma sınırı, karartma tabanı)
        │
Donanım katmanı: DDC · GPU renk tablosu · NVIDIA (dithering,            arayüzler: MonitorTuner.Hardware.Abstractions
  canlılık) · Windows ekran · kayıt defteri                             Windows: MonitorTuner.Hardware
        │
Saf mantık (donanımsız test edilir): profil, DDC kuralları, AE/AC,      MonitorTuner.Core
  Kelvin → RGB, karartma planı, gamma ve gri dengesi eğrileri,          MonitorTuner.Color
  tablo birleştirme
```

**Yazmaların derleme zamanında zorlanması:** Donanım yazma metotları, sadece Güvenlik katmanının oluşturabildiği bir geri alma noktası nesnesi ister (T002). Böylece Güvenlik'i atlayan bir yazma derlenmez.

## Renk boru hattı

Ayrıntılı anlatım: `08-GORUNTU-YONTEMLERI.md`, bölüm 5.

```
İçerik → masaüstü → GPU renk tablosu [kalibrasyon → mavi ışık → ek karartma] → dithering
       → HDMI (Full RGB) → monitör RGB kazancı [beyaz noktası + mavi ışık] → arka ışık → panel
```

- **İki yarı:** Monitör yarısı (DDC: arka ışık, RGB kazancı) ve GPU yarısı (renk tablosu, dithering). Bir efekt önce monitör yarısında yapılır (seviye kaybı yok), sınıra gelince GPU yarısında devam eder.
- **Tek sahip:** GPU renk tablosunu sadece renk boru hattı (`Services` içindeki `ColorPipeline`) yazar. Diğer modüller kendi katmanını boru hattına verir; boru hattı katmanları sırayla birleştirip tek tabloyu yazar. Güvenlik katmanı tablonun yedeğini tutar, geri yüklemeyi boru hattından ister.
- **Dithering:** Tabloda kalibrasyondan başka bir şey varken ya da kalibrasyon katmanı doluyken açık tutulur (T303).
- **Dış değişiklik izleme:** Tablo değiştiyse (oyun, Windows Gece ışığı, f.lux, NVIDIA masaüstü renk ayarları) kullanıcının tablosu geri yüklenir ve uyarı verilir.
- **Arka uç:** Windows gamma tablosu. Tek arka uç.

## Modül sözleşmeleri

| Modül | Sorumluluk | Girdi | Çıktı | Yazar mı? | Nasıl test edilir |
|---|---|---|---|---|---|
| **Monitör tanıma** | Model, seri no, bağlantı tipi, bağlı kart | Sistem | Monitör bilgisi | Hayır | Sahte sistemle |
| **Monitör profili** | Monitöre özel verileri okur | Profil dosyası | Hz modları ve timing'leri, sınırlar, DDC kodları, yetenekler | Hayır | Örnek dosyalarla |
| **Ayar deposu** | Kullanıcı ayarlarını okur/yazar | Ayarlar | JSON dosyası | Sadece kendi dosyaları | Geçici klasörle |
| **Güvenlik katmanı** | Yedek, geri alma noktası, 15 sn onay, fabrika ayarı, kaldırma temizliği | Yazma isteği | Geri alma noktası / geri alındı | Dolaylı | Sahte donanım + sahte saat |
| **DDC katmanı** | Yetenek testi, okuma/yazma, izin listesi, yazma sınırı | Monitör, kod, değer | Değer / hata | **Evet** | Sahte monitör (DDC'li ve DDC'siz) |
| **Windows ekran katmanı** | Mod listesi, mod geçişi | Ekran, mod | Durum | **Evet** | Sahte sistem + 🔧 |
| **Hz paneli** | Onaylı modlar arasında geçiş, AE ile doğrulama, kaybolan mod uyarısı | Mod | Durum | Dolaylı | Sahte sistem + sahte saat |
| **Renk boru hattı** | Katmanları birleştirir, GPU tablosunu yazar, dış değişikliği izler | Kalibrasyon, mavi ışık, karartma katmanları | GPU tablosu | **Evet** | Sahte GPU tablosuyla |
| **NVIDIA katmanı** | Dithering ve canlılık: okuma, yazma, varsayılana dönüş | Kart, ayar | Durum | **Evet** | Sahte sürücü + 🔧 |
| **Kelvin hesaplayıcı** | Sıcaklık → monitör ve GPU çarpanları | Kelvin, monitör kazanç sınırları | Çarpanlar | Hayır | Bilinen değer tablosuyla |
| **Karartma planlayıcı** | Kaydırıcı → arka ışık + GPU ölçeği; %15 taban | Kaydırıcı | DDC değeri + ölçek | Hayır | Sınır değerleriyle |
| **Gözle kalibrasyon** | Gamma ve gri dengesi seçimlerinden kanal eğrileri | Kullanıcının desen seçimleri | Kalibrasyon katmanı | Hayır | Bilinen girdilerle |
| **Renk profilleri** | Profil → DDC, mavi ışık, karartma ve canlılık değerleri | Profil | Ayar listesi | Dolaylı | Sahte donanımla |
| **Ayar sihirbazı** | Desenler, adım sırası, temel ayarı kaydetme | Kullanıcı seçimleri | Temel ayar | Dolaylı | Sahte monitörle + 🔧 |
| **Kurtarma** | GPU tablosu, `GdiIcmGammaRange`, dithering ve canlılığı varsayılana döndürür | — | Durum | **Evet** | Sahte sistem + 🔧 |

### Sözleşmeler kodda (T002)

| Modül | Arayüz | Proje |
|---|---|---|
| Monitör tanıma | `IMonitorIdentifier`; donanım yarısı `IDisplayEnumerator` | Services; Hardware.Abstractions |
| Monitör profili | `IMonitorProfileReader` → `MonitorProfile` | Core |
| Ayar deposu | `ISettingsStore` (özellik başına bölüm) | Services |
| Güvenlik katmanı | `ISafetyLayer`; GPU tablosu geri yüklemesi için `IGpuTableOwner` | Safety |
| DDC katmanı | `IDdcControl` (izin listesi, yazma sınırı); ham `IDdcMonitor` | Services; Hardware.Abstractions |
| Windows ekran katmanı | `IDisplayModes` | Hardware.Abstractions |
| Hz paneli | `IRefreshRatePanel` | Services |
| Renk boru hattı | `IColorPipeline` / `ColorPipeline` (iskelet); `IGpuGammaTable`, `IGdiIcmGammaRange`; `IGammaTableCombiner` | Services; Hardware.Abstractions; Color |
| NVIDIA katmanı | `INvidiaSettings` | Hardware.Abstractions |
| Kelvin hesaplayıcı · Karartma planlayıcı · Gözle kalibrasyon | `IKelvinCalculator` · `IDimmingPlanner` · `ICalibrationCurves` | Color |
| Renk profilleri · Ayar sihirbazı · Kurtarma | `IColorProfiles` · `ISetupWizard` · `IRecovery` | Services |

- **Geri alma noktası:** `RestorePoint` (Hardware.Abstractions) kurucusu `internal`; sadece Safety (ve platformdan bağımsız test projesi) `InternalsVisibleTo` ile görür. Hardware.Abstractions'taki her yazma metodu `RestorePoint` ister; okuma metotları `Read`, `Get`, `List`, `Enumerate` ile başlar. Testler: `tests/MonitorTuner.Tests/Contracts`.
- Donanım arayüzlerinde mod ekleme/silme, firmware, EDID yazma, güç, saat, voltaj ve fan üyesi yoktur (testli).
- Ortak sayılar `Core.SafetyLimits`'te (15 sn, %15 beyaz, 60 Hz, yasak DDC kodları). Zaman `TimeProvider` ile; sahte saat T003'te.
- **Sahte donanım (T003):** `tests/MonitorTuner.Tests/Fakes`. `FakeHardware` kullanıcının sistemini kurar (RTX 3080'in HDMI'ında HP P21b G4, örnek EDID, kaydedilmiş DDC değerleri, sahte saat `FakeTimeProvider`); Hardware.Abstractions'taki her arayüzün bir sahtesi vardır (testli). Sahteler başka hedef için alınmış geri alma noktasıyla yazmayı hata sayar.

## Profil yapısı

**Monitör profili** (veri dosyası, monitör başına): Hz modları ve timing'leri (test durumlarıyla), spec ve ölçülen sınır, OSD yetenekleri (renk ön ayarları, overdrive yok), çalışan DDC kodları ve 14 kodunun eşlemesi, AE/AC okuma kuralları, PWM durumu (yok).

**Kullanıcı ayarları** (anahtar: monitör + seri numarası + port + kart):
- DDC değerlerinin ve GPU tablosunun yedek konumu
- Onaylı Hz modları
- Temel ayar: sihirbazın DDC değerleri (parlaklık, kontrast, RGB kazancı, keskinlik) ve kalibrasyon katmanı
- Renk profilleri
- Mavi ışık ve ek karartma ayarları, isteğe bağlı akşam geçişi
- Dithering ve canlılık durumu

## Ayar sahipliği (çakışma kuralı)

1. **Kullanıcının elle yaptığı değişiklik** her zaman geçerlidir ve temel değeri günceller.
2. **Renk profili seçimi** değerleri bir kez ayarlar; sonra kullanıcı istediği gibi değiştirebilir.
3. **Mavi ışık ve ek karartma** temel değerlerin üstüne fark ya da katman olarak uygulanır; kapatınca temel değerlere dönülür.
4. **Ayar sihirbazı** başlarken efektler geçici olarak kapanır, bitince geri gelir.
5. **Dış bir program** GPU tablosunu değiştirirse kullanıcının tablosu geri yüklenir (Kural 1, 2. istisna).

## Klasör yapısı

```
/src/MonitorTuner.App                     Arayüz, tepsi, ayar sihirbazı ve desenler (net10.0-windows, WPF)
/src/MonitorTuner.Composition             Gerçek donanımı servislere bağlar (net10.0-windows)
/src/MonitorTuner.Services                Uygulama servisleri, renk boru hattı (net10.0)
/src/MonitorTuner.Safety                  Güvenlik katmanı (net10.0)
/src/MonitorTuner.Color                   Kelvin, karartma planı, kalibrasyon eğrileri, tablo birleştirme (net10.0)
/src/MonitorTuner.Core                    Saf mantık: profil, DDC kuralları, AE/AC (net10.0)
/src/MonitorTuner.Hardware.Abstractions   Donanım arayüzleri (net10.0)
/src/MonitorTuner.Hardware                DDC, GPU tablosu, NVIDIA, Windows ekran, kayıt defteri (net10.0-windows)
/profiles                                 Monitör profil dosyaları (HP P21b G4)
/tests                                    Birim ve mimari testleri (platformdan bağımsız + Windows'a özel)
/tools/dev                                Sessiz derleme ve test betikleri
/tools/recovery                           Kurtarma dosyası
/docs                                     Bu dokümanlar
```

## CI korumaları (T001)

- Her push'ta GitHub Actions: **Ubuntu** (çözümün tamamı derlenir, platformdan bağımsız testler) ve **Windows** (derleme, tüm testler, self-contained `win-x64` paket). Platformdan bağımsız testler push'tan önce yerelde de çalıştırılır.
- Mimari testleri derlenmiş derlemelerin referanslarını okur:
  - Core ve Color hiçbir donanım derlemesine bağlı değil.
  - App, Hardware ve Hardware.Abstractions'a bağlı değil; donanıma Services üzerinden ulaşır.
  - GPU renk tablosuna yazan arayüz üyesini sadece `ColorPipeline` kullanıyor. Üye `[OnlyColorPipelineMayCall]` (Hardware.Abstractions) ile işaretlenir; onu uygulayan sınıf metodu da aynı kurala girer.
  - Windows'un gamma tablosu, DDC (`dxva2`), NVIDIA (`nvapi`) ve ekran modu fonksiyonları (P/Invoke) ve `NativeLibrary` sadece Hardware'de. Kurallar `tests/Shared/ArchitectureRules.cs`'te; bilerek ihlal eden örnek `tests/MonitorTuner.ArchitectureFixtures`.
- Donanım yazma metotları geri alma noktası ister (T002, derleme zamanında).
- `LESSONS.md` her faz sonunda güncellenir.
