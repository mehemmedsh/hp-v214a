# Güvenlik

Bu uygulama monitörün, ekran kartının ve Windows'un ekran ayarlarına yazıyor. Aşağıdaki kurallar pazarlıksızdır; hiçbir görev bunları gevşetemez.

## 1. Değişmez kurallar

1. **Hz ve parlaklık sadece kullanıcı değiştirdiğinde değişir.** Otomatik Hz, otomatik parlaklık, zamana veya uygulamaya göre geçiş yok. Sadece iki güvenlik istisnası var; ikisi de yeni bir değer seçmez, kullanıcının bilinen değerine döner:
   - Onaylanmayan bir Hz değişikliğinden sonra 15 saniyede **önceki moda dönüş**.
   - Bir oyun veya başka bir program GPU renk tablosunu değiştirirse **kullanıcının tablosunun geri yüklenmesi**.
2. **Firmware'e yazma yok.** EDID override da yazılmaz; Hz modlarını kullanıcı NVIDIA panelinden ekler.
3. **Orijinal 60 Hz mod hiçbir zaman silinmez veya değiştirilmez.**
4. **Her donanım ve sistem ayarı yazması bir geri alma noktası oluşturur** ve Güvenlik katmanından geçer.
5. **Her görüntü efekti tek kısayolla kapanır.** Panik kısayolu mavi ışığı, ek karartmayı ve canlılığı kapatır; kalibrasyon kalır.

## 2. Hz güvenliği

- Uygulama Hz modu eklemez; sadece Windows'un listesindeki modlar arasında geçer. Modlar NVIDIA özel çözünürlüğüyle kullanıcı tarafından eklenir; değerler profilde ve rehberde (T311).
- Her geçişten sonra **15 saniyelik onay**: kullanıcı "görüntü geldi" demezse önceki moda dönülür.
- Spec dışı modlara (72 Hz üstü) ilk geçişte uyarı gösterilir: overclock garanti dışıdır ve panel bunun için tasarlanmamıştır; stabil bulunan değerde bile uzun vadede küçük bir risk vardır.
- Gelen Hz monitörün kendisinden (DDC AE) okunarak doğrulanır.

### Görüntü hiç gelmezse (kolaydan zora)

1. **15 saniye bekle.** Onay gelmezse uygulama kendisi geri döner. NVIDIA panelindeki test de kendi kendine geri döner.
2. **HDMI kablosunu başka bir çıkışa tak:** 3090'ın HDMI'ı ya da 3080'in bir DP çıkışı (DP→HDMI kabloyla). Özel çözünürlük çıkışa bağlı olduğu için orada 60 Hz gelir. Sonra NVIDIA panelinden sorunlu modu sil.
3. **Güvenli mod + kurtarma dosyası.** Güvenli modda Windows temel ekran sürücüsünü kullanır ve görüntü gelir. Paketteki `Kurtarma.cmd` (`tools/recovery`) GPU renk tablosunu sıfırlar, `GdiIcmGammaRange`'i eski haline, dithering ve canlılığı varsayılana döndürür (T104); Hz'e, DDC'ye ve özel çözünürlüklere dokunmaz. Güvenli modda NVIDIA sürücüsü yüklü olmadığından dithering ve canlılık, normal açılışta ikinci çalıştırmada döner. Adım adım talimat: `tools/recovery/GORUNTU-GELMEZSE.txt` (pakette exe'nin yanında).
4. **NVIDIA özel çözünürlüğünü sil:** NVIDIA Denetim Masası → Ekran → Çözünürlüğü değiştir → Özelleştir → sorunlu mod → Kaldır. Orijinal 60 Hz mod silinmez.

## 3. DDC güvenliği (monitör belleğini koruma)

- Monitör DDC ile gelen bazı ayarları kalıcı belleğine yazar; çok sık yazma bu belleği yıpratır.
- Kaydırıcı sürüklenirken yazma yapılmaz, **bırakıldığında tek yazma** yapılır.
- Aynı ayara kısa sürede tekrar tekrar yazma engellenir.
- Mavi ışığın isteğe bağlı akşam geçişi birkaç adımda ve uzun aralıklarla ilerler.
- Sadece profildeki çalışan kodlara yazılır. 04, 05, 08 ve E6–FF'ye hiçbir zaman yazılmaz; bilinmeyen kodlara yazılmaz.

## 4. Renk boru hattı güvenliği

- GPU renk tablosunu sadece renk boru hattı yazar.
- **Karartma tabanı:** Ek karartma beyazı en fazla %15'e indirir; ekran hiçbir zaman tamamen kararmaz.
- `GdiIcmGammaRange` değiştirilmeden önce eski değeri yedeklenir (tek seferlik yönetici onayı); kaldırınca eski haline döner.
- Uygulama normal kapanırken tabloda sadece kalibrasyon kalır, efektler kaldırılır; dithering ve canlılık varsayılana döner.
- Uygulama çökerse son tablo ekranda kalabilir. Kurtarma dosyası tabloyu sıfırlar; uygulama yeniden açılınca tabloyu kontrol eder.

## 5. GPU güvenliği

- Dithering ve canlılık dışında NVIDIA ayarı yazılmaz. Güç limiti, saat hızı, voltaj ve fan eğrisi değiştirilmez.
- PSU: 3090 + 3080 + i3-10100 ≈ 800 W tam yük; EVGA 1000 W yeterli. Her 8-pin girişe ayrı kablo. İki PSU kullanılıyorsa bir kartın bütün güç girişleri (ve varsa riser'ı) aynı PSU'dan beslenir.

## 6. Ajanlar ve geliştirme ortamı

Claude Code ajanı (masaüstü uygulaması) kullanıcının bilgisayarında, gerçek monitörün yanında çalışır.

- Ajan uygulamayı ve donanıma yazan kodu çalıştırmaz. Testler sahte donanımla çalışır; 🔧 testlerini sadece kullanıcı yapar.
- Ajan kayıt defterine, ekran ayarlarına, NVIDIA ayarlarına veya monitöre (DDC) dokunan komut çalıştırmaz.
- Serbest komutlar: `git`, `gh`, `dotnet restore/build/test/format`, `tools/dev/` betikleri. Başka bir komut ya da program kurulumu kullanıcının onayıyla.
- Claude Code'da izin modu varsayılan kalır: her komut kullanıcının onayını ister; otomatik onay modları (auto, bypass) PC'de kullanılmaz (`05-HANDOFF.md` → 0.3).

## 7. Işığa duyarlılık uyarısı

Uygulamanın desenleri yanıp sönmez. Rehberdeki testufo kare atlama ve hareket testlerinden önce ışığa duyarlı epilepsi uyarısı gösterilir.

## 8. Kaldırma

Uygulama kaldırılınca:
- DDC değerleri yedekten geri yüklenir.
- GPU renk tablosu sıfırlanır, `GdiIcmGammaRange` eski haline döner.
- Dithering ve canlılık ilk haline döner.
- Yedekler kullanıcı isterse saklanır.
- NVIDIA'daki özel çözünürlükler kullanıcınındır; silinmez, nasıl silineceği gösterilir.
