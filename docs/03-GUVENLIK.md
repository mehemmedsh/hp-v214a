# Güvenlik

Bu uygulama monitörün, ekran kartının ve Windows'un ekran ayarlarına yazıyor. Aşağıdaki kurallar pazarlıksızdır; hiçbir görev bunları gevşetemez.

## 1. Değişmez kurallar

1. **Hz ve parlaklık sadece kullanıcı değiştirdiğinde değişir.** Otomatik Hz, otomatik parlaklık, zamana veya uygulamaya göre geçiş yok. Sadece üç güvenlik istisnası var; üçü de yeni bir değer seçmez, kullanıcının bilinen değerine döner:
   - Onaylanmayan bir değişiklikten sonra 15 saniyede **önceki değere geri dönüş**.
   - Sürücü güncellemesi override'ı sildiyse **kullanıcının daha önce onayladığı ayarın geri yüklenmesi**.
   - Bir oyun veya başka bir program GPU renk tablosunu değiştirirse **kullanıcının seçtiği tablonun geri yüklenmesi**.
2. **Firmware'e yazma yok.** Hiçbir sürümde, hiçbir deneysel modda.
3. **Orijinal 60 Hz mod hiçbir zaman silinmez veya değiştirilmez.** Yeni modlar yanına eklenir.
4. **Her donanım ve sistem ayarı yazması bir geri alma noktası oluşturur** ve Güvenlik katmanından geçer.
5. **Deneysel işlemler ve overclock testleri telefondan başlatılamaz.** Kullanıcının bilgisayar başında olması gerekir.
6. **Her görüntü efekti tek kısayolla kapanır.** Panik kısayolu mavi ışığı, ek karartmayı, canlılığı ve tüm katmanları kapatır; kalibrasyon kalır.

## 2. Overclock güvenliği

- İlk açılışta her bağlantının orijinal EDID'i yedeklenir. Yedek alınmadan hiçbir override yazılmaz.
- EDID'in CTA-861 uzantı bloğu (HDMI ve ses bilgisi) korunur.
- Değişiklikten sonra **15 saniyelik onay**: kullanıcı "görüntü geldi" demezse otomatik geri dönülür.
- Spec dışı adımlar (72 Hz üstü) ayrıca uyarı ve onay ister.
- NVIDIA panelinde özel çözünürlük veya CRU kaydı bulunursa kullanıcı uyarılır; iki yöntem karıştırılmaz.
- Overclock garanti dışıdır ve panel bunun için tasarlanmamıştır. Stabil bulunan değerde bile uzun vadede küçük bir risk vardır. İlk overclock'tan önce bir kez gösterilir.

### Görüntü hiç gelmezse (kolaydan zora)

1. **15 saniye bekle.** Onay gelmezse uygulama kendisi geri döner.
2. **HDMI kablosunu başka bir çıkışa tak:** 3090'ın HDMI'ı ya da 3080'in bir DP çıkışı (DP→HDMI kabloyla). Windows her bağlantı için ayrı kayıt tuttuğundan orada override yoktur ve görüntü gelir. Sonra uygulamadan "Fabrika ayarına dön".
3. **Güvenli mod + kurtarma dosyası.** Güvenli modda Windows temel ekran sürücüsünü kullanır ve görüntü gelir. `tools/recovery` içindeki kurtarma dosyası tüm override'ları siler ve GPU renk tablosunu sıfırlar. Dosya kurulumda masaüstüne ve uygulama klasörüne kopyalanır; nasıl kullanılacağı ilk overclock'tan önce gösterilir.

## 3. DDC güvenliği (monitör belleğini koruma)

- Monitör DDC ile gelen bazı ayarları kalıcı belleğine yazar; çok sık yazma bu belleği yıpratır.
- Kaydırıcı sürüklenirken yazma yapılmaz, **bırakıldığında tek yazma** yapılır.
- Aynı ayara kısa sürede tekrar tekrar yazma engellenir.
- Mavi ışık zamanlaması (açıksa) birkaç adımda ve uzun aralıklarla ilerler.
- Keşif taraması **sadece okur** ve kodlar arasında bekler. Bilinmeyen bir koda yazma için eski değer yedeklenir, kullanıcı ayrı ayrı onaylar.

## 4. Renk boru hattı güvenliği

- GPU renk tablosunu sadece renk boru hattı yazar.
- **Karartma tabanı:** Ek karartma beyazı en fazla %15'e indirir; ekran hiçbir zaman tamamen kararmaz.
- `GdiIcmGammaRange` kayıt değeri değiştirilmeden önce eski değeri yedeklenir; kaldırınca eski haline döner.
- Uygulama normal kapanırken tabloda sadece kalibrasyon kalır, efektler kaldırılır.
- Uygulama çökerse son tablo ekranda kalabilir. Bu durumda kurtarma dosyası tabloyu sıfırlar; uygulama yeniden açılınca tabloyu kontrol eder.
- Canlılık, uygulama kapanınca varsayılana döner.
- v3 NVIDIA renk dönüşümü ile Windows gamma tablosu aynı anda çalışmaz; uygulama kapanınca dönüşüm kapatılır.

## 5. Katman güvenliği

- Katmanlar tıklamaları geçirir ve ekran görüntüsüne veya kayda girmez.
- Ölçüm, test deseni ve tam ekran oyun sırasında gizlenir.
- Satır bazlı ton telafisi (v3) varsayılan olarak kapalıdır. Açılırken korumalı videoların (Netflix vb.) siyah görüneceği uyarısı gösterilir.
- Uygulama süreci kapanınca katmanlar da kapanır.

## 6. GPU güvenliği

- Güç limiti sadece **düşürülebilir** ve varsayılana döndürülebilir; varsayılanın üstüne çıkarma yok.
- Saat hızı, voltaj ve fan eğrisi değiştirilmez.
- PSU: 3090 + 3080 + i3-10100 ≈ 800 W tam yük; EVGA 1000 W yeterli. Her 8-pin girişe ayrı kablo. İki PSU kullanılıyorsa bir kartın bütün güç girişleri (ve varsa riser'ı) aynı PSU'dan beslenir.

## 7. Deneysel modül (v3)

- Her deney ayrı bir uyarı ekranından sonra açılır ve tek tek çalıştırılır.
- Her deney kendi geri alma noktasını oluşturur; kabul edilmeyen deney otomatik geri alınır.
- **Firmware okuma:** Monitörü fişten çekip takana kadar dondurabilir. Kullanıcıya önceden söylenir. Sadece okuma.

## 8. Pixel bağlantısı

- Sadece yerel ağ. İnternete açık bir port yok.
- İlk bağlantıda PC ekranında gösterilen eşleştirme kodu. Eşleşmemiş cihaz hiçbir komut gönderemez.
- Telefondan izin verilen komutlar: preset, parlaklık ve ek karartma, mavi ışık, mod seçimi, **önceden onaylanmış** Hz değerleri arasında geçiş, ölçüm sırasında fotoğraf gönderme.

## 9. Işığa duyarlılık uyarısı

Bazı test desenleri hızlı yanıp sönen görüntüler içerir. Bu testlerden önce ışığa duyarlı epilepsi uyarısı gösterilir.

## 10. Kaldırma

Uygulama kaldırılınca:
- Tüm EDID override'ları silinir.
- DDC değerleri yedekten geri yüklenir.
- GPU renk tablosu sıfırlanır, `GdiIcmGammaRange` eski haline döner.
- Dithering, canlılık ve RGB aralığı ayarları ilk haline döner.
- GPU tercihleri ve güç limitleri varsayılana döner.
- Katmanlar kapanır.
- Yedekler kullanıcı isterse saklanır.
