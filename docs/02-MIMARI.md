# Mimari

Uygulamanın parçaları ve birbirleriyle nasıl konuştukları. Kod içermez; T002 görevinde buradaki sözleşmeler koda (arayüzler ve testler) çevrilir.

## Teknoloji kararları (T001'de onaylanır)

| Parça | Önerilen | Neden |
|---|---|---|
| Windows uygulaması | **C# / .NET 10 (LTS), WPF** | Kayıt defteri, DDC, NVIDIA arayüzleri, tepsi simgesi ve katman pencereleri için en kolay yol. .NET 8'in desteği 10 Kasım 2026'da bitiyor; .NET 10 Kasım 2028'e kadar destekli |
| Proje ayrımı | `MonitorTuner.*` projeleri. Core, Color, Safety, Services ve donanım arayüzleri `net10.0` (platformdan bağımsız); App, Overlay, Hardware, Elevated, Patterns `net10.0-windows` + `EnableWindowsTargeting` | Bulut oturumu (Linux) çözümün tamamını derler ve platformdan bağımsız testleri kendisi çalıştırır; Windows'a özel testler CI'da. Denendi: WPF projesi Linux'ta derleniyor. Proje adı sadece `App` olursa WPF'nin `App` sınıfıyla çakışıyor |
| Yönetici yardımcısı | Aynı çözümde ayrı küçük bir .NET süreci | Sadece yetki isteyen birkaç komutu çalıştırır |
| Test desenleri | DirectX tabanlı tam ekran pencere (vsync'e kilitli) | Kare atlama testi için her yenilemede tam bir kare değişmeli |
| Katmanlar | Katmanlı, tıklama geçirgen, en üstte duran pencereler; ekran yakalamadan hariç tutulur | Dikey denge ve ekran bilgisi |
| Satır bazlı telafi (v3) | Windows ekran yakalama + DirectX 11 gölgelendirici | ShaderGlass'ın kullandığı yaklaşım |
| Pixel uygulaması | **Kotlin, Camera2** | CinePixel'deki manuel kamera kontrolü yeniden kullanılabilir |
| Fotoğraf analizi | .NET içinde görüntü işleme (ör. OpenCV sarmalayıcısı) + DNG çözücü (ör. LibRaw) | RAW fotoğraf PC'de analiz edilir, telefon sadece çeker |
| Hareket yumuşatma (v3) | Harici: mpv + VapourSynth + RIFE | Motor sıfırdan yazılmaz, kurulur ve yönetilir |
| FPS ölçümü | Harici: PresentMon | Windows'ta güvenilir FPS ölçümünün standart yolu |
| Kolorimetre (v3, isteğe bağlı) | Harici: ArgyllCMS | Kolorimetreyle ölçüm |
| Ayarlar | `%AppData%` altında JSON; yedekler ayrı klasörde; monitör profili ayrı bir veri dosyası | Okunabilir, dışa aktarması kolay |
| Derleme ve test çıktısı | `tools/dev/` içinde sessiz derleme ve test betikleri (bash + PowerShell): sadece hata ve özet | Oturumların okuduğu çıktı kısalır, bilgi kaybı yok |
| 🔧 testleri için çalıştırma | **Önerilen:** CI her push'ta kendi içinde (self-contained) Windows paketi üretir, kısa saklama süresiyle; kullanıcı indirip çalıştırır. **Alternatif:** PC'de .NET 10 SDK ile derleme | PC'ye geliştirme aracı kurmak gerekmez |

## Süreçler

1. **Ana uygulama** (normal kullanıcı): Arayüz, tepsi, profiller, DDC, renk boru hattı, katmanlar, analiz, Pixel bağlantısı.
2. **Yönetici yardımcısı** (yönetici yetkisi): Sadece şu komutları kabul eder, listede olmayanı reddeder:
   - EDID override yaz / sil
   - Ekran sürücüsünü yeniden başlat
   - GPU güç limitini değiştir (sadece varsayılan ve altı)
   - GPU tablo aralığı kaydını (`GdiIcmGammaRange`) yaz / eski haline döndür
   Ana uygulamayla yerel bir kanal (named pipe) üzerinden konuşur.
3. **Pixel uygulaması:** Yerel ağ üzerinden ana uygulamaya bağlanır.
4. **Harici araçlar:** PresentMon (v1), mpv + RIFE (v3), ArgyllCMS (v3, isteğe bağlı).

## Katmanlar

Kural: **donanıma veya sistem ayarına yazan her işlem Güvenlik katmanından geçer.** Arayüz hiçbir zaman doğrudan donanım katmanını çağırmaz.

```
Arayüz (WPF, tepsi, katman pencereleri)
        │
Uygulama servisleri (overclock akışı, preset, mavi ışık, karartma, modlar, kalibrasyon akışı, karne)
        │
Güvenlik katmanı (yedek, geri alma noktası, 15 sn onay, DDC yazma sınırı, karartma tabanı, deneysel uyarıları)
        │
Donanım katmanı ── EDID · DDC · NVIDIA · GPU renk tablosu · Windows ekran · Sürücü
        │
Saf mantık (donanımsız test edilir) ── EDID · timing · Kelvin → RGB · karartma planı · kalibrasyon çözücü · LUT/ICC üretimi · fotoğraf analizi
```

## Renk boru hattı

Ayrıntılı anlatım: `08-GORUNTU-YONTEMLERI.md`, bölüm 5.

```
İçerik → masaüstü (katmanlar) → GPU renk tablosu [kalibrasyon → mavi ışık → ek karartma] → dithering
       → HDMI (Full RGB) → monitör RGB kazancı [beyaz noktası + mavi ışık] → arka ışık → panel
```

- **İki yarı:** Monitör yarısı (DDC: arka ışık, RGB kazancı) ve GPU yarısı (renk tablosu, dithering, RGB aralığı). Bir efekt önce monitör yarısında yapılır (seviye kaybı yok), sınıra gelince GPU yarısında devam eder.
- **Tek sahip:** GPU renk tablosunu sadece renk boru hattı yazar. Diğer modüller kendi katmanını boru hattına verir; boru hattı katmanları sırayla birleştirip tek tabloyu yazar.
- **Dış değişiklik izleme:** Tablo değiştiyse (oyun, Windows Gece ışığı, f.lux, NVIDIA masaüstü renk ayarları) kullanıcının tablosu geri yüklenir ve uyarı verilir.
- **Arka uçlar:** v1–v2'de Windows gamma tablosu; v3'te isteğe bağlı NVIDIA renk dönüşümü (T906). Aynı anda tek arka uç çalışır.

## Katman yöneticisi

| Katman | Sürüm | Davranış |
|---|---|---|
| Dikey denge | v1 | Tıklama geçirgen, ekran yakalamaya girmez, en üstte; ölçüm, test deseni ve tam ekran oyunda gizlenir |
| Ekran bilgisi | v1 | Köşede Hz, FPS, sıcaklıklar; ölçümde gizlenir |
| Satır bazlı ton telafisi | v3 | Varsayılan kapalı; korumalı video uyarısı; panik kısayoluyla anında kapanır |

Panik kısayolu tüm katmanları ve efektleri (mavi ışık, ek karartma, canlılık) tek tuşla kapatır; kalibrasyon kalır.

## Modül sözleşmeleri

| Modül | Sorumluluk | Girdi | Çıktı | Yazar mı? | Nasıl test edilir |
|---|---|---|---|---|---|
| **EDID ayrıştırıcı** | EDID ↔ yapı; checksum; CTA-861 uzantısı | EDID baytları | Monitör bilgisi, timing'ler, aralık sınırı, uzantılar | Hayır | Gidiş-dönüş testi (gerçek HP P21b G4 EDID'i dahil: `donanim/hp-p21b-g4/edid-original.bin`) |
| **Timing hesaplayıcı** | Standart, CVT-RB ve sıkı timing; Hz tavanı | Çözünürlük, Hz, yöntem, spec sınırları | Pixel clock, yatay frekans, spec içi/dışı | Hayır | `00-DONANIM.md` tablosuyla birebir |
| **EDID override yazıcı** | Yeni modu ekleyen EDID; CTA bloğunu korur | Orijinal EDID, timing | Override kaydı | **Evet** (yardımcı) | Sahte kayıt defteriyle |
| **Sürücü yeniden başlatıcı** | Ekran sürücüsünü yeniden başlatır | Kart | Başarı/başarısızlık | **Evet** | Sahte sistem + kullanıcı onaylı gerçek test |
| **Onay zamanlayıcısı** | 15 sn onay, yoksa geri al | Geri alma noktası | Onaylandı / geri alındı | Dolaylı | Sahte saatle |
| **DDC katmanı** | Yetenek testi, okuma/yazma, yazma sınırı | Monitör, kod, değer | Değer / hata | **Evet** | Sahte monitör (DDC'li ve DDC'siz) |
| **Renk boru hattı** | Katmanları birleştirir, GPU tablosunu yazar, dış değişikliği izler | Kalibrasyon, mavi ışık, karartma katmanları | GPU tablosu | **Evet** | Sahte GPU tablosuyla |
| **Kelvin hesaplayıcı** | Sıcaklık → monitör ve GPU çarpanları | Kelvin, monitör kazanç sınırları | Çarpanlar | Hayır | Bilinen değer tablosuyla |
| **Karartma planlayıcı** | Kaydırıcı → arka ışık + GPU ölçeği; titreşimsiz taban | Kaydırıcı, taban, mod | DDC değeri + ölçek | Hayır | Sınır değerleriyle |
| **NVIDIA katmanı** | Dithering, RGB aralığı, canlılık, GPU bilgisi, güç limiti, (v3) renk dönüşümü | Kart, ayar | Durum | **Evet** | Sahte sürücü + gerçek donanım |
| **Windows ekran katmanı** | Mod listesi ve seçimi, ekranı aç/kapat, GPU tercihi, ICC yükleme | Ekran, mod | Durum | **Evet** | Sahte sistem + elle test |
| **Katman yöneticisi** | Katman pencereleri, sıra, gizleme kuralları, kayıttan hariç tutma | Katman durumu | Pencereler | Hayır | Elle |
| **Monitör profili** | Monitöre özel verileri okur | Profil dosyası | Spec sınırları, yetenekler | Hayır | Örnek dosyalarla |
| **Profil deposu** | Kullanıcı profillerini okur/yazar, dışa/içe aktarır | Profil | Dosya | Sadece kendi dosyaları | Geçici klasörle |
| **Test desenleri motoru** | Tam ekran, vsync'e kilitli desenler | Desen türü, Hz | Görüntü | Hayır | Kare sayacıyla |
| **Fotoğraf analizi** | Kare atlama, yama ölçümü (yaklaşık XYZ), homojenlik, PWM bantları, ghosting | DNG | Ölçümler | Hayır | Örnek fotoğraf setiyle |
| **Kalibrasyon çözücü** | Ölçümlerden kanal eğrileri, beyaz noktası önerisi | Ölçümler, hedef | Eğriler, DDC önerisi | Hayır | Sentetik ölçümlerle |
| **LUT / ICC üretici** | ICC (matris + eğri + VCGT) ve Resolve için `.cube` | Kalibrasyon sonucu | Dosyalar | Hayır | Bilinen girdilerle |
| **Pixel bağlantısı** | Eşleştirme, komut, fotoğraf alma | Komut | Fotoğraf / durum | Hayır | Sahte istemciyle |

## Profil yapısı

**Monitör profili** (veri dosyası, monitör başına): spec sınırları, OSD yetenekleri (overdrive var mı, renk ön ayarları), çalışan DDC kodları, titreşimsiz taban, konum sonucu, bilinen beklenti aralıkları.

**Kullanıcı profili** (anahtar: monitör + seri numarası + port + kart):
- Orijinal EDID'in ve GPU tablosunun yedek konumu
- Onaylı Hz listesi (timing yöntemi, spec durumu, test sonuçları, onay tarihi)
- Hz başına RGB aralığı
- DDC presetleri
- Mavi ışık ayarı, ek karartma ayarı ve modu, zamanlama
- Dithering ve canlılık durumu
- Dikey denge katmanının eğrisi
- v2: Hz başına kalibrasyon (eğriler, beyaz noktası, ICC, Resolve LUT), overdrive önerisi

## Ayar sahipliği (çakışma kuralı)

1. **Kullanıcının elle yaptığı değişiklik** her zaman geçerlidir ve temel değeri günceller.
2. **Preset veya mod seçimi** değerleri bir kez ayarlar; sonra kullanıcı istediği gibi değiştirebilir.
3. **Mavi ışık ve ek karartma** temel değerlerin üstüne fark ya da katman olarak uygulanır; kapatınca temel değerlere dönülür.
4. **Ölçüm ve testler** başlarken efektler ve katmanlar geçici olarak kapanır, bitince geri gelir.
5. **Dış bir program** GPU tablosunu değiştirirse kullanıcının tablosu geri yüklenir (Kural 1, 3. istisna).

## Klasör yapısı (planlanan)

```
/src/App          Arayüz, tepsi
/src/Services     Uygulama servisleri (akışlar)
/src/Safety       Güvenlik katmanı
/src/Hardware     EDID, DDC, NVIDIA, GPU tablosu, Windows ekran, sürücü adaptörleri
/src/Color        Renk boru hattı, Kelvin, karartma, kalibrasyon çözücü, LUT/ICC
/src/Overlay      Katman yöneticisi ve katman pencereleri
/src/Core         Saf mantık: EDID, timing, analiz
/src/Elevated     Yönetici yardımcısı
/src/Patterns     Test desenleri motoru
/profiles         Monitör profil dosyaları (HP P21b G4)
/android          Pixel yardımcı uygulaması
/tests            Birim testleri, örnek EDID ve fotoğraf setleri
/tools/recovery   Güvenli mod kurtarma dosyası
/docs             Bu dokümanlar
```

## CI korumaları (T001)

- Her push'ta derleme ve birim testleri (GitHub Actions, Windows çalıştırıcısı). Platformdan bağımsız testler oturumda (Linux) push'tan önce de çalıştırılır.
- Saf mantık katmanı donanım katmanını kullanamaz.
- Arayüz donanım katmanını doğrudan kullanamaz; her yazma Güvenlik katmanından geçer.
- GPU renk tablosuna sadece renk boru hattı yazabilir.
- EDID kodlayıcıda gidiş-dönüş testi zorunlu.
- `LESSONS.md` her faz sonunda güncellenir.
