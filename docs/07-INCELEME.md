# İnceleme Notları

Planın son kontrolünün kaydı. Bu sürüm üç değişiklikten sonra yazıldı: monitör HP V214a oldu, ekran kartı RTX 3080 oldu, renk, parlaklık, mavi ışık ve üst-alt farkı için yeni yöntemler eklendi.

Önceki geçmiş: Lenovo T24i-20 planı ve onun üç turluk incelemesi `Myapplab/Lenovo-monitor` reposunda.

---

## 1. Tur: Özelliklerin yeniden kontrolü

| # | Bulunan sorun | Düzeltme |
|---|---|---|
| 1.1 | Ekran kartı 3070 yerine 3080 oldu. 3080 güçlü bir kart; oyunları 3090'da render edip 3080'den göstermek gereksiz kopya gecikmesi ekliyordu. | Oyun modu artık oyunu **doğrudan 3080'de** çalıştırıyor. 3090 aynı anda LLM için boş kalabiliyor. |
| 1.2 | 3090 + 3080 + i3-10100 tam yükte ≈ 800 W; 3090 + 3070'e göre güç payı azaldı. | 1000 W yeterli. Güç yönetimi iki karta genişletildi (T807). İki PSU kuralı eklendi: bir kartın bütün güç girişleri aynı PSU'dan. |
| 1.3 | GPU renk tablosunu dört özellik birden kullanmak istiyordu (kalibrasyon, mavi ışık, ek karartma, DDC yoksa beyaz noktası). Her biri ayrı yazarsa birbirini ezer. | Tek sahip: **renk boru hattı** (T302). Eski uyarlamadaki koşullu "GPU LUT yedek yolu" koşulsuz v1 çekirdeği oldu. |
| 1.4 | Oyunlar, Windows Gece ışığı, f.lux ve NVIDIA masaüstü renk ayarları GPU tablosunu değiştirebiliyor. | Boru hattı dış değişikliği tespit edip kullanıcının tablosunu geri yüklüyor. Kural 1'e 3. istisna olarak yazıldı (yeni değer seçmez). |
| 1.5 | Windows derin karartma ve çok sıcak renk tablolarını reddediyor. | `GdiIcmGammaRange` kaydı yönetici yardımcısının izinli komutlarına eklendi; eski değer yedeklenir, kaldırınca geri döner. |
| 1.6 | NVIDIA tam aralık RGB'de varsayılan olarak dithering yapmıyor; GPU tablosu değiştirilince bantlaşma çıkar. | GPU tablosu kullanan her özellikte dithering açık tutuluyor (T303, T704). |
| 1.7 | Ek karartma, "dijital parlaklık koruması" kuralıyla çelişiyordu. | Koruma sadece **artırma** yönünde. Ek karartma sadece azaltma yönünde, sadece arka ışık tabanının altında ve arayüzde "dijital" etiketiyle. |
| 1.8 | Uygulama çökerse koyu bir tablo ekranda kalabilir. | Karartma tabanı (beyazın %15'i), kurtarma dosyası tabloyu da sıfırlıyor, açılışta tablo kontrolü, panik kısayolu (yeni Kural 6). |
| 1.9 | Eski gece modu sadece monitörün RGB kazancıyla sınırlıydı; sıcak tonlara inemiyordu. | Kelvin tabanlı **mavi ışık filtresi** (T307): siyah cisim eğrisi, önce monitör sonra GPU. |
| 1.10 | Mavi ışık filtresi beyazı doğal olarak karartıyor (3400K'de ~%65). Telafi için arka ışığı artırmak Kural 1'i bozar. | Telafi eklenmedi; kayıp arayüzde gösteriliyor. |
| 1.11 | V214a flicker-free değil; arka ışığı kısmak PWM titremesi yaratabilir. | PWM testi ve **titreşimsiz karartma modu**: arka ışık titreşimsiz tabanın altına inmez, kalanı GPU yapar. |
| 1.12 | TN'de kalibrasyon sadece tek bir göz konumu için geçerli. Önceki plan kamerayı "ekran merkezine dik" koyuyordu. | Kamera **göz konumunda**. Konum ayarı (T401) kalibrasyondan önce. |
| 1.13 | Kullanıcı gerçek bir üst-alt ışık farkı gördü. Daha önce çıkarılan homojenlik katmanı bu sorunu çözebilirdi. | İki boyutlu katman yerine sadece **dikey** hali geri geldi: elle (v1), ölçümlü (v2), gölgelendiricili (v3). Asıl çözüm konum. |
| 1.14 | Gölgelendiricili telafi korumalı videoları siyah gösterir, tam ekran özel mod oyunlarda çalışmaz. | v3'te, varsayılan olarak kapalı; açılırken uyarı; panik kısayolu. |
| 1.15 | Katmanlar ekran görüntüsüne ve kayda girebilirdi. | Katmanlar ekran yakalamadan hariç tutuluyor. |
| 1.16 | DaVinci LUT'u, sistem genelindeki GPU kalibrasyonunu ikinci kez uygulayabilirdi. | LUT, kalibrasyon açıkken ölçülen değerlerden üretiliyor (T706). |
| 1.17 | Telefonla mutlak D65 hedefi yaklaşık; kullanıcı daha iyi renk istiyor. | "Doğal beyaz" seçeneği (telefonla en güvenilir), ölçüm doğruluğu kuralları (DNG matrisi, çoklu kare, pozlama basamaklama, tekrarlanabilirlik) ve isteğe bağlı kolorimetre modu (T905). |
| 1.18 | Eski T703 (kontrast + gamma) tek görev için büyüktü ve sıra yanlıştı: gri dengesi beyaz noktasından önce gelemez. | Sıra: kontrast ve siyah (T702) → beyaz noktası (T703) → gri dengesi ve ton eğrisi (T704). |
| 1.19 | Canlılık "doğru renk" hedefiyle çelişiyor. | Sadece Film ve Oyun presetlerinde; ölçümde ve DaVinci'de kapalı; kapanışta varsayılana döner. |
| 1.20 | Monitöre özel bilgiler (spec sınırları, DDC kodları, PWM tabanı) kod içine dağılabilirdi. | Monitör profil dosyası (T004). Yeni monitör = yeni profil dosyası. |
| 1.21 | Önceki uyarlama "3070'in HDMI'ı" diyordu; kurtarma yolu da 3070'e göreydi. | Tüm dokümanlar 3080'e göre güncellendi; kurtarma: 3090'ın HDMI'ı veya 3080'in DP çıkışı (DP→HDMI). |

---

## 2. Tur: Faz tablosu kontrolü (script ile)

- 66 görev, tekrar eden numara yok. ✔
- Hiçbir görev kendinden sonra gelen bir göreve bağımlı değil. ✔
- Her görev numarası kendi fazıyla uyumlu (T3xx → Faz 3). ✔
- A sınıfının hepsini Opus, B ve C sınıfının hepsini Sonnet yazıyor; K görevleri kullanıcıda. ✔
- Her görevde yazan ve inceleyen farklı model. ✔
- Özet tablosu sayımla birebir: 22 Opus, 41 Sonnet, 3 kontrol noktası. ✔
- Diğer dokümanlarda geçen her görev numarası faz tablosunda var. ✔

---

## 3. Tur: Son tutarlılık kontrolü

- Otomatik Hz veya otomatik parlaklık değişimi yapan bir görev yok. Mavi ışık zamanlaması sadece rengi değiştirir ve varsayılan olarak kapalı. ✔
- Firmware'e yazma hiçbir görevde yok; sadece okuma (T908, en son görev). ✔
- Donanıma veya sistem ayarına yazan her katman A sınıfı ve Opus tarafından yazılıyor: EDID (T101, T202), sürücü (T203), DDC (T301), renk boru hattı (T302), NVIDIA ayarları (T303, T304, T309), ICC (T705), içerik Hz'leri (T805), güç limiti (T807), deneyler (T901, T903, T906–T908). B görevleri bu katmanları kullanır, kendisi doğrudan yazmaz. ✔
- GPU renk tablosuna boru hattı dışında yazan görev yok. ✔
- Çıkarılan özellikler sadece "çıkarıldı" listelerinde ve genel fikirdeki "ne gerektirir" tablosunda geçiyor. ✔
- Bu repoda uygulama kodu yok, sadece plan. ✔

---

## 4. Eski → yeni görev eşlemesi

Lenovo reposundaki görev numaralarından bu repodakilere:

| Eski | Yeni | Not |
|---|---|---|
| T001–T003 | T001–T003 | — |
| — | T004 | Yeni: monitör profil dosyası |
| T101–T105 | T101–T105 | HP ve 3080'e göre güncellendi |
| T201–T208 | T201–T208 | Hz tavanı, 61 → 75, CTA-861 |
| T301 | T301 | + DDC yetenek testi |
| — | T302 | Yeni: renk boru hattı (eski koşullu Y2) |
| T305 | T303 | Dithering |
| T709 | T304 | RGB aralığı, v2'den v1'e |
| T302 | T305 | DDC paneli (+ ses) |
| — | T306 | Yeni: parlaklık ve ek karartma (koruma buraya taşındı) |
| T304 | T307 | Gece modu → mavi ışık filtresi |
| T303 | T308 | Presetler (+ Oyun) |
| — | T309 | Yeni: canlılık |
| T306 | T310 | Tepsi (+ panik kısayolu) |
| T307 | T311 | Rehber |
| Y1 (HP uyarlaması) | T401 | Açı ayar deseni + teşhis |
| — | T402 | Yeni: dikey denge katmanı |
| T401–T404 | T403–T406 | GPU (3090 + 3080) |
| T501–T503 | T501–T503 | — |
| T601–T604 | T601–T604 | Kamera göz konumunda |
| T701 | T701 | + DNG matrisi, tekrarlanabilirlik |
| T703 | T702 + T704 | Bölündü: kontrast/siyah ve gri/gamma |
| T702 | T703 | Beyaz noktası |
| T704 | T705 | ICC |
| — | T706 | Yeni: DaVinci 3D LUT |
| T705 | T707 | Hz başına kalibrasyon |
| T706 | T801 | Parlaklık + PWM |
| T707 | T802 | Panel testleri |
| — | T803 | Yeni: ölçümle dikey denge |
| T708 | T804 | Ghosting / overdrive |
| T710 | T805 | İçerik Hz'leri |
| T711, T712 | T806, T807 | İzleme, güç (iki kart) |
| T713, T714 | T808, T809 | Karne, v2 kontrol noktası |
| T801 | ✖ | FreeSync testi çıkarıldı |
| T802 | T901 | 10-bit (50 Hz) |
| T803 | T902 | RIFE |
| T804 | T903 | Kartlar arası geçiş (VGA) |
| — | T904 | Yeni: satır bazlı ton telafisi |
| — | T905 | Yeni: kolorimetre modu |
| — | T906 | Yeni: NVIDIA renk dönüşümü arka ucu |
| T805, T806 | T907, T908 | Keşif (USB kontrolü çıkarıldı), firmware okuma |
| T807 | T909 | v3 kontrol noktası |

**Toplam:** 55 eski görevden 1'i çıktı, 1'i ikiye bölündü, 11 yeni görev eklendi → 66.
