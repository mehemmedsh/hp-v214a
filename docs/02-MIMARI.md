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
| Geliştirme ortamı | Claude Code (PC, Windows, masaüstü uygulaması) ana araç; Claude Code bulut (Linux) yedek | Kurallar `AGENTS.md`'de |
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
| Güvenlik katmanı | `ISafetyLayer` / `SafetyLayer`; GPU tablosu geri yüklemesi için `IGpuTableOwner`; ilk açılış yedeğinin kalıcı yeri `IFactoryBackupStore` (dosyası T102'nin yedek klasöründe) | Safety |
| DDC katmanı | `IDdcControl` (izin listesi, yazma sınırı); ham `IDdcMonitor` | Services; Hardware.Abstractions |
| Windows ekran katmanı | `IDisplayModes` | Hardware.Abstractions |
| Hz paneli | `IRefreshRatePanel` | Services |
| Renk boru hattı | `IColorPipeline` / `ColorPipeline`; `IGpuGammaTable`, `IGdiIcmGammaRange`; `IGammaTableCombiner` / `GammaTableCombiner` | Services; Hardware.Abstractions; Color |
| NVIDIA katmanı | `INvidiaSettings`; servis `INvidiaControl` / `NvidiaControl`, boru hattının haber verdiği `IDitheringPolicy` | Hardware.Abstractions; Services |
| Kelvin hesaplayıcı · Karartma planlayıcı · Gözle kalibrasyon | `IKelvinCalculator` · `IDimmingPlanner` · `ICalibrationCurves` | Color |
| DDC paneli | `IDdcPanel` / `DdcPanel` (kontrast tavanı ayar deposunda, "DdcPanel") | Services |
| Mavi ışık filtresi | `IBlueLightFilter` / `BlueLightFilter` (baz kazançlar ayar deposunda, "BlueLightBase"); Gece ışığı / f.lux tespiti `IColorConflictProbe` | Services |
| Parlaklık ve ek karartma (servis) | `IBrightnessControl` / `BrightnessControl`; plan `DimmingPlanner : IDimmingPlanner` (Color) | Services; Color |
| Renk profilleri · Ayar sihirbazı · Kurtarma | `IColorProfiles` · `ISetupWizard` · `IRecovery` | Services |

- **Geri alma noktası:** `RestorePoint` (Hardware.Abstractions) kurucusu `internal`; sadece Safety (ve platformdan bağımsız test projesi) `InternalsVisibleTo` ile görür. Hardware.Abstractions'taki her yazma metodu `RestorePoint` ister; okuma metotları `Read`, `Get`, `List`, `Enumerate` ile başlar. Testler: `tests/MonitorTuner.Tests/Contracts`.
- Donanım arayüzlerinde mod ekleme/silme, firmware, EDID yazma, güç, saat, voltaj ve fan üyesi yoktur (testli).
- Ortak sayılar `Core.SafetyLimits`'te (15 sn, %15 beyaz, 60 Hz, yasak DDC kodları). Zaman `TimeProvider` ile; sahte saat T003'te.
- **Güvenlik katmanı (T103):** Geri alma noktası hedefin şu anki değerini yedekler; okunamıyorsa, yasak DDC kodu ya da başka ekran istenirse nokta verilmez. İlk noktadan önce ilk açılış yedeği alınır (profildeki yazılabilir DDC kodları, GPU tablosu, dithering ve canlılık, `GdiIcmGammaRange`); o an okunamayan bir hedef, ilk yazmasından önceki değeriyle eklenir. Geri alma değeri zaten yerindeyse yazmaz. Onay gelmezse 15 sn'de geri alır; geri dönemezse `RollBackFailed` olayı. `Confirm` süresi içinde gelen onayda `true` döner (T209 buna göre kalıcı kaydeder), geç onayda `false`. "Fabrika ayarına dön" ve kaldırma Hz'e dokunmaz; `GdiIcmGammaRange` sadece kaldırmada döner. En fazla 100 nokta saklanır, onay bekleyen atılmaz.
- **DDC katmanı (T301):** `DdcControl` (Services). Yetenek testi ilk kullanımda bir kez (`Probe` yeniler); yetenek dizesi `DdcCapabilities` (Core) ile çözülür, `vcp(...)` listesi yoksa profildeki kodlardan okunabilenler desteklenmiş sayılır. İzin listesi: profilde yazılabilir ∩ yetenek dizesi; 04/05/08 ve E6–FF hiçbir zaman. Değer profilin ve monitörün sınırında olmalı, 14 sadece `confirmed` eşlemeyle; aksi `Forbidden`. Monitördeki değer zaten isteneneyse yazmaz (`Written`). Yazma sınırı kod başına: 1 sn içinde ikinci yazma ve dakikada 10'dan fazlası `Throttled` (başarısız deneme de sayılır); kaydırıcı bırakılınca tek `Write` arayüzün işi. Yedek alınamazsa ya da monitör reddederse `Failed`. `Read` ham değer verir; AE → Hz ve AC'nin 16 bit taşma düzeltmesi `FrequencyReadoutRules.RefreshHz` / `HorizontalHz` (Core, profil), çünkü düzeltilmiş AC `DdcValue`'ya sığmaz. Windows yarısı `WindowsDdcMonitor` (Hardware, dxva2): her çağrıda fiziksel monitörü açıp kapatır, ekranda tek fiziksel monitör yoksa dokunmaz, okumayı 3 kez dener, yazmayı bir kez. 🔧 testi `MonitorTuner.Windows.Tests` → `RealDdcTests`, ortam değişkeni verilmezse atlanır.
- **Hz paneli (T209):** `RefreshRatePanel` (Services) sadece onaylı modlar (76, 75, 72, 60, 50; profilde olanlar) arasında ve sadece `SwitchTo` çağrılınca geçer; Hz'i kendiliğinden hiç değiştirmez. Geçiş iki aşamalı: `IDisplayModes.SwitchMode` anlık geçer (Windows'a kaydetmez), Güvenlik 15 sn onay bekler; onay süresi içinde gelirse `PersistMode` yeni bir geri alma noktasıyla şu anki modu kaydeder. Böylece onay beklerken uygulama çökerse yeniden başlatmada önceki (kayıtlı) mod gelir; onaylanmayan geçişte Güvenlik anlık geçişle önceki moda döner (`Reverted(true)`), dönemezse `Reverted(false)` ve arayüz "görüntü gelmezse" adımlarını gösterir. Onay beklerken yeni geçiş `Busy`, aynı Hz `Unchanged` (yazmaz), listede olmayan `ModeMissing`. Spec dışı (`IsBeyondSpec`, 72 üstü) modda uyarı kabulü mod başına ayar deposunda ("RefreshRates") saklanır; kabul edilmemişse `NeedsOutOfSpecWarning`. `GetMissingRatesHz` daha önce Windows listesinde görülmüş (depoda) ama artık olmayan onaylı modları verir (sürücü güncellemesi); hiç eklenmemiş mod uyarı vermez. `GetNvidiaValues` NVIDIA'ya girilecek timing'i profilden verir (EDID modlarında null). Gerçek Hz DDC AE'den (`FrequencyReadout.RefreshHz`); `VerifyWithMonitor` Windows'un Hz'iyle karşılaştırır, ne zaman çağrılacağı (geçişten birkaç sn sonra) arayüzün işi. Windows yarısı `WindowsDisplayModes` (Hardware, user32 `EnumDisplaySettingsEx`/`ChangeDisplaySettingsEx`): liste masaüstünün renk derinliğinde, taramalı modlar hariç; geçişten önce `CDS_TEST`; kalıcı kayıt `CDS_UPDATEREGISTRY` ve kayıtlı mod (`ENUM_REGISTRY_SETTINGS`) okunarak doğrulanır. 🔧 testi `MonitorTuner.Windows.Tests` → `RealRefreshRateTests`, ortam değişkeni verilmezse atlanır. Hz paneli ekranı (App) bu görevde yok.
- **Renk boru hattı (T302):** `ColorPipeline` (Services) katmanları `ColorLayer` sırasıyla (kalibrasyon → mavi ışık → ek karartma) `GammaTableCombiner`'a (Color) verir; birleştirici her katmanı aktarım fonksiyonu olarak uygular (önceki çıktı sonraki tabloda doğrusal aradeğerlemeyle aranır, yuvarlama sadece en sonda). `SetLayer` donanıma dokunmaz, `Apply` yazar; tablo zaten ekrandaysa yazmaz. Her yazma Güvenlik'ten GPU tablosu geri alma noktası alır; tablo okunamazsa yazılmaz. **Kullanıcının tablosu** uygulamanın son yazdığı ya da Güvenlik'in `Restore` ile geri yüklediği tablodur. Yazmadan hemen sonra okunan tablo saklanır ve dış değişiklik ona göre aranır (sürücü değerleri yuvarlasa da kendi yazması dış değişiklik sayılmaz). `RestoreIfChangedExternally` uygulama henüz yazmadıysa ya da okunamıyorsa dokunmaz; değişiklik varsa kullanıcının tablosunu yazar ve `true` (uyarı) döner. Ne zaman çağrılacağı (zamanlayıcı) arayüzün işi. `EnsureFullGammaRange` değer zaten 256 ise yazmaz (onay bir kez); eski değeri Güvenlik yedekler, kaldırmada döner. `Shutdown` efekt katmanlarını kaldırıp sadece kalibrasyonu, `ResetTable` birim tabloyu yazar ve bütün katmanları siler. Açılışta (çökme sonrası dahil) bağlama kökü kalibrasyonu verip `Apply` çağırır; ekrandaki eski tablo böylece düzelir. Windows yarısı (Hardware): `WindowsGpuGammaTable` (gdi32 `Get/SetDeviceGammaRamp`, her çağrıda aygıt bağlamını açıp kapatır) ve `WindowsGdiIcmGammaRange` (okuma normal yetkiyle; yazma `reg.exe`'nin yönetici onayıyla çalıştırılmasıyla, onay verilmezse false). 🔧 testi `MonitorTuner.Windows.Tests` → `RealGammaTests`, ortam değişkeni verilmezse atlanır.
- **NVIDIA ayarları (T303):** `NvidiaControl` (Services) dithering ve canlılığı sadece `INvidiaSettings` üzerinden, her yazmada Güvenlik'ten NVIDIA geri alma noktası alarak yazar; değer zaten yerindeyse yazmaz. **Dithering:** `ColorPipeline` kullanıcının tablosu her değiştiğinde (yazma, sahiplenme, Güvenlik geri yüklemesi) `IDitheringPolicy.TableInUse` ile haber verir; tablo nötr değilse (bir 8 bit seviye, üst 8 bite göre, kendine gitmiyorsa; birim ve Windows'un i×256 tablosu nötr) dithering açık, nötre dönünce kullanıcının değeri. **Canlılık:** `SetVibrance` (0–100, NVIDIA panelindeki %; Film ve Oyun, T308), `ResetVibrance` (panik ve diğer profiller; dithering'e dokunmaz), sihirbaz için `Suspend`/`Resume` (askıdayken istek saklanır). **Kullanıcının değeri** ilk açılış yedeğindeki NVIDIA durumudur (çökmeden kalan değer sayılmaz), yoksa ilk okunan. Uygulama sadece kendi açtığını geri alır. `Shutdown` ikisini de kullanıcının değerine döndürür (kapanışta tabloda kalibrasyon kalsa da, `03-GUVENLIK.md` → 4); sıra: boru hattı `Shutdown`, sonra bu. Sürücü okunamazsa yazma yok, hata yok. Windows yarısı `WindowsNvidiaSettings` (Hardware, `nvapi64` `nvapi_QueryInterface`): dithering belgesiz `GPU_Get/SetDitherControl` ile 8 bit uzamsal sabit desen (6 bit + FRC panelin zamansal titreşimine ikinci zamansal desen eklenmez); canlılık eski DVC arayüzüyle, seviye 0 = %50 (varsayılan), en yüksek = %100; %50 altı yazılmaz. Sihirbaza bağlanması (`Begin`'de `Suspend`, bitince `Resume`) T313'le aynı dosyada çakışmasın diye sonraya kaldı. 🔧 testi `MonitorTuner.Windows.Tests` → `RealNvidiaTests`, ortam değişkeni verilmezse atlanır.
- **Mavi ışık (T307):** `KelvinCalculator` (Color) Planck eğrisinden doğrusal ışık çarpanı üretir ve önce monitör tabanına kadar, kalanı GPU'ya böler; `BlueLightFilter` (Services) bölüşümü uygular. Monitör payı DDC 16/18/1A'ya baz değerin oranı olarak yazılır (kod = baz × çarpan^(1/2,2), en düşük %60); sadece DDC varsa, üç kod yazılabilirse ve 14 kodu profildeki `rgbGainPreset`'teyse kullanılır, yoksa hepsi GPU'da. Yazma sınırlandırılırsa GPU eksik kalan payı alır. GPU payı `ColorLayer.BlueLight` katmanıdır. Arka ışığa (10) ve Hz'e dokunmaz. Low Blue Light kısayolu profildeki eşleme `confirmed` değilse yazmaz. Akşam geçişi varsayılan kapalı, en az 5 dakikalık adımlarla, sadece ısıtır, kullanıcı elle değiştirdiği akşam dokunmaz. Gece ışığı/f.lux tespiti `IColorConflictProbe` arkasında (Windows tarafı sonra yazılır).
- **Parlaklık ve ek karartma (T306):** `DimmingPlanner` (Color) kaydırıcıyı (0–1) iki kademeye böler: `TransitionPoint` (0,2) üstü arka ışık 0 → 100 (GPU ölçeği 1), altı arka ışık 0 ve GPU beyaz seviyesi 1 → `SafetyLimits.MinWhiteLevel` (%15, doğrusal ışık); iki kademe birlikte çalışmaz. `BrightnessControl` (Services) planı uygular: sürüklerken `Preview` donanıma dokunmaz, bırakılınca `Commit` DDC 10'a tek yazma yapar (aynı değer tekrar yazılmaz; değer monitörün en büyük değerine ölçeklenir) ve GPU'ya sadece `ColorLayer.Dimming` katmanını `IColorPipeline` üzerinden verir. Karartırken önce GPU, açarken önce arka ışık yazılır; geçici DDC hatasında (sınır, başarısız) katman kalkmaz. Panik kısayolu `DisableDimming()` çağırır (katmanı kapatmanın tek yeri, arka ışık kalır; kullanıcı kaydırıcıyı yeniden bırakana kadar kapalı). Ayar sihirbazı `Suspend`/`Resume`, kapanış `Shutdown`. Kaydırıcı açılışta `ReadSlider` ile DDC'den kurulur; karartma durumu kalıcı saklanmaz (profiller T308'de).
- **DDC paneli (T305):** `DdcPanel` (Services) 10, 12, 14, 16, 18, 1A, 60, 62, 87'ye sadece `IDdcControl` üzerinden yazar; arayüz (`DdcPanelViewModel`, App) yalnız `IDdcPanel`'i görür. Kaydırıcı bırakılınca tek `Set`; aynı değer yazılmaz. **Dijital parlaklık koruması:** kontrast ilk okunan değerin (kalıcı tavan) üstüne çıkmaz, RGB kazancı monitörün en yükseğini aşmaz ve en çok %60'a iner, parlaklık artırma yolu arka ışıktır. Kontrast `SafetyLimits.MinWhiteLevel`'in (%15) altına yazılmaz, ekran kararmaz. **Ayar sahipliği:** yazmadan önce ayar yeniden okunur; kullanıcı OSD'den değiştirmişse yazılmaz (`ExternallyChanged`), yeni değer alınır ve bildirilir; OSD'den yükseltilen kontrast yeni tavandır. RGB kazancı sadece profildeki ön ayarda (Custom RGB) açılır ve mavi ışık filtresi açıkken kilitlidir (16/18/1A'yı filtre yazıyor; kapanınca kullanıcının değeri geri gelir, panel o değişikliği "dış" saymaz). Arka ışık (10) ek karartma (T306) açıkken kilitlidir; bağlama kökü `() => brightness.DimmingActive` verir. Renk ön ayarında yalnızca `confirmed` eşleme yazılır. Giriş, kablonun bağlı olduğu porttan başkasına ancak açık uyarı kabulüyle geçer (`IDdcControl` 15 sn onay sunmadığı için geri dönüş monitörün Input düğmesidir). DDC yoksa ya da cevap vermiyorsa arayüz OSD talimatını gösterir.
- **Ayar sihirbazı 1 (T312):** `SetupWizard` (Services) yalnız `IDdcPanel` üzerinden yazar (dijital parlaklık koruması, OSD değişikliği ve Custom RGB kuralları, Güvenlik, yazma sınırı geçerli). `Begin` mavi ışığı ve ek karartmayı askıya alır (`Suspend`), sonra sihirbazın ayarlarını (arka ışık, kontrast, keskinlik, RGB kazancı) iptal için saklar; monitöre yazmaz. `SetDdc(PanelSetting, int)` `PanelSetResult` döner; giriş, ses ve renk ön ayarı sihirbazın değildir (`NotSupported`; Custom RGB profilde `estimated` olduğundan iptalde geri yazılamazdı, beyaz eşleme için kullanıcı OSD'den seçer, değilse kazanç `Locked`). `Finish` değerleri `Baseline` olarak ayar deposuna ("Baseline" bölümü, kodlar onaltılık anahtar) yazar ve efektleri geri getirir (mavi ışık yeni kazançları taban alır); `Cancel` yalnız yazdığı ayarları eski değerine döndürür, geri dönemeyenleri listeler, yine de efektleri geri getirir. `LoadBaseline` kaydı okur. Isınma (30 dk) engellemez, `WarmUpRemaining` Begin'den sayar. `SetCalibration` T313'te (T312'de `NotSupported`); Baseline'ın kalibrasyonu o zamana dek birim tablo ya da eski kayıt. Desenler saf veridir (`WizardPatterns` → `Pattern` → kareler → yamalar, ekran yüzdesiyle): kırpma (1–25 siyah, 230–254 beyaz), keskinlik yazısı, RGB Full (1–16), açı (üç şerit %50 gri), ölü piksel (siyah, beyaz, R, G, B), homojenlik (%20, %50, %80 gri) ve beyaz alan. Zamanlayıcı ya da yanıp sönme yok; `PatternWindow` (App) tam ekran çizer, kareleri kullanıcı ok tuşlarıyla gezer. Canlılığı kapatıp geri getirme, T303 `INvidiaSettings` yazılınca sihirbaza eklenir.
- **Sahte donanım (T003):** `tests/MonitorTuner.Tests/Fakes`. `FakeHardware` kullanıcının sistemini kurar (RTX 3080'in HDMI'ında HP P21b G4, örnek EDID, kaydedilmiş DDC değerleri, sahte saat `FakeTimeProvider`); Hardware.Abstractions'taki her arayüzün bir sahtesi vardır (testli). Sahteler başka hedef için alınmış geri alma noktasıyla yazmayı hata sayar.

## Profil yapısı

**Monitör profili** (veri dosyası, monitör başına): Hz modları ve timing'leri (test durumlarıyla), spec ve ölçülen sınır, OSD yetenekleri (renk ön ayarları, overdrive yok), çalışan DDC kodları ve 14 kodunun eşlemesi, AE/AC okuma kuralları, PWM durumu (yok).

Dosya: `profiles/hp-p21b-g4.json` (şema sürümü 1); `MonitorProfileReader` (Core) okur ve doğrular. Şema sıkıdır (bilinmeyen ya da eksik alan hata). Kurallar: orijinal 60 Hz doğrulanmış olarak durur; kullanılabilir modlar ölçülen yatay frekans sınırının altında, reddedilenler üstünde; `specMaxRefreshHz` ve ölçülen tavan modlardan türetilenle aynı; 04/05/08 ve E6–FF yazılabilir işaretlenemez. Reddedilen timing'ler `rejectedModes`'ta; kullanılabilir modlarda Hz başına tek mod var. `colorPresetMap`'te yalnızca `confirmed` değer yazılmalı; `estimated` olanlar monitörde doğrulanana kadar sadece okunur.

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
