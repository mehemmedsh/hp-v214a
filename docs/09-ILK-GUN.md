# İlk Gün Kontrolü

Kodlamadan önce, monitör masadayken yapılacak kısa kontroller (20–30 dakika). Sonuçlar `LESSONS.md` → "Donanım bulguları" bölümüne yazılır ve T004'te monitör profil dosyasına işlenir.

| # | Kontrol | Nasıl | Neyi belirler |
|---|---|---|---|
| 1 | **Üst-alt ışık farkının sebebi** | Ekranı düz %50 gri yap; başını 10–15 cm yukarı-aşağı oynat. Fark değişiyorsa sebep açı, değişmiyorsa arka ışık. Emin olmak için 1.5–2 m uzaktan, zoomla, ekranın ortasına dik bir fotoğraf çek | T401, T402, T803 |
| 2 | **Oturma ölçüleri** | Göz yüksekliği ile ekranın üst kenarı arasındaki fark (cm), ekrana mesafe (cm) | T401 |
| 3 | DDC/CI açık mı? | OSD → Management → DDC/CI Support → aç | T301 |
| 4 | Hangi DDC kodları çalışıyor? | ControlMyMonitor (NirSoft, ücretsiz) ile listele: 10 parlaklık, 12 kontrast, 14 renk ön ayarı, 16/18/1A RGB kazancı, 60 giriş, 62 ses. Parlaklığı bir kez değiştirip geri al | T301, T305, T306, T307 |
| 5 | NVIDIA RGB aralığı | NVIDIA Denetim Masası → Ekran → Çözünürlüğü değiştir → "Çıkış dinamik aralığı": ne yazdığını not et, Full yap | T304 |
| 6 | Overdrive ayarı var mı? | OSD → Image Control | T804 |
| 7 | Renk ön ayarları | OSD → Color Control: hangi seçenekler var (Warm, Neutral, Cool, sRGB, Low Blue Light, Custom RGB) | T307, T703 |
| 8 | Bilgi ekranı Hz'i gösteriyor mu? | OSD → Information | T205, T206 |
| 9 | **PWM ve titreşimsiz taban** | Pixel kamerası manuel modda, 1/2000 s enstantane; parlaklık 100, 50, 20 ve 0'da ekrana tut. Yatay bantların ilk göründüğü parlaklığı not et (hiç görünmüyorsa PWM yok) | T306, T801 |
| 10 | Çakışan programlar | Windows Gece ışığı kapalı; f.lux benzeri program yok; NVIDIA "Masaüstü renk ayarları" varsayılan | T302 |
| 11 | EDID'i kaydet | CRU'yu aç, **sadece Export** ile EDID'i dosyaya kaydet. Hiçbir değişikliği uygulama | T101 |
| 12 | (İsteğe bağlı) Hızlı tavan denemesi | NVIDIA özel çözünürlük ile 65 → 70 → 72 Hz dene, testufo.com/frameskipping ile kontrol et. Bitince özel çözünürlükleri **sil** (uygulamayla çakışmasın) | Beklentiyi netleştirir |

## Açık sorular (bu kontrol cevaplar)

- [ ] Üst-alt farkının sebebi: açı mı, arka ışık mı
- [ ] DDC/CI desteği ve çalışan kodlar
- [ ] OSD'de overdrive ayarı var mı
- [ ] Renk ön ayarlarının listesi
- [ ] PWM var mı; varsa titreşimsiz taban
- [ ] OSD Information gelen Hz'i gösteriyor mu
- [ ] EDID'deki orijinal timing standart 2200×1125 mi
- [ ] NVIDIA bu monitörde varsayılan olarak Limited mi gönderiyor
- [ ] Gerçek Hz tavanı (T206'da kesinleşir)
- [ ] Panel modeli
- [ ] Servis menüsü (bilinen yol yok)
