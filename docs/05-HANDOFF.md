# Handoff

Yeni bir Claude Code oturumuna veya Claude app sohbetine yapıştırılacak hazır metinler. Claude Code oturumları `CLAUDE.md`'yi otomatik okur; kurallar, okuma haritası ve commit biçimi orada olduğu için buradaki metinler kısa.

## 0. Kodlamadan önce

✅ İlk gün kontrolü yapıldı (2026-10-30): `docs/09-ILK-GUN.md`, sonuçlar `LESSONS.md` → "Donanım bulguları", ham veriler `donanim/hp-p21b-g4/`. Monitör **HP P21b G4** çıktı; dokümanlar buna göre uyarlandı.

**Ortam (bir kez):** Claude Code bulut ortamının kurulum betiğine (setup script) şu satırı ekle. Böylece her oturum kodu kendisi derler ve testleri çalıştırır; CI'a gidip gelme azalır. Windows'ta yerel Claude Code kullanılıyorsa bunun yerine .NET 10 SDK kurulu olmalı.

```bash
apt-get update && apt-get install -y dotnet-sdk-10.0
```

**Monitörde kalan kontroller** (T001'i beklemez):
- 75 Hz (2080×1100) ve 76 Hz (2080×1092): Windows aktif sinyal, DDC AE = 7500 / 7600 ve testufo kare atlama. **T206'dan önce.**
- DDC 14 kodunun eşlemesi: OSD'de her renk ön ayarını sırayla seç, ControlMyMonitor'da 14'ün değerini oku (12–15 ve tahmin edilen 2, 4, 8, 11). **T004'ten önce.**

## 1. İlk görev (T001)

**Nerede:** Claude Code, `mehemmedsh/hp-v214a` · **Model:** Sonnet 5.5 · **Sonra inceleme:** Opus 5.5 (high), ayrı oturum

Göndermeden önce "Kararlarım" satırlarını kontrol et; kararlar baştan verilince oturum soru sorup beklemez.

```text
Görev: T001 (Faz 0). CLAUDE.md'deki okuma haritasına göre gerekenleri oku;
04-FAZLAR.md'deki tanım ve "Bitti sayılır" kriterine göre yap. Sadece T001.

Kararlarım (02-MIMARI.md → Teknoloji kararları):
- Önerilen teknoloji kararları onaylı (.NET 10, WPF, MonitorTuner.* proje ayrımı).
- 🔧 testleri için CI çalıştırılabilir Windows paketi üretsin.
- Arayüzün ana dili Türkçe; Azerbaycanca ve İngilizce T502'de.

Bu kararlarla çelişen ya da benim karar vermemi gerektiren yeni bir şey çıkarsa
koda başlamadan önce hepsini tek mesajda sor.
```

## 2. Herhangi bir görev için şablon

**Nerede:** Claude Code · **Model:** görev tablosundaki "Yazan"

```text
Görev: T___ . CLAUDE.md'deki okuma haritasına göre gerekenleri oku;
04-FAZLAR.md'deki tanım, bağımlılıklar ve "Bitti sayılır" kriterine göre yap.
Bağımlı görevlerin hepsi ✅ değilse başlama, bana söyle.
- Sadece bu görev. Donanıma veya sistem ayarına yazan her işlem Güvenlik katmanından geçer.
- GPU renk tablosuna sadece renk boru hattı yazar.
- 🔧 işaretliyse gerçek monitörde benim yapacağım testin adımlarını yaz; ben onaylamadan ✅ yapma.
- Sonunda bana: ne yaptın, neyi test ettin, inceleyicinin bakması gereken 3 nokta
  (aynısı commit mesajında).
```

## 3. İnceleme şablonu

**Nerede:** Claude Code, yeni oturum · **Model:** görev tablosundaki "İnceleyen"

```text
T___ görevinin incelemesini yap. Kodu sen yazmadın.
`git log --oneline --grep "^T___"` ile görevin commit'lerini bul. Commit mesajındaki
"inceleyiciye 3 nokta" ile başla, sonra bu commit'lerin farkına bak.
CLAUDE.md'deki okuma haritasına göre gerekenleri oku.

Kontrol et:
1. "Bitti sayılır" kriteri gerçekten sağlanıyor mu?
2. Testler gerçek davranışı mı test ediyor, yoksa sadece geçmek için mi yazılmış?
3. Donanıma veya sistem ayarına yazan bir yol Güvenlik katmanını atlıyor mu?
4. GPU renk tablosuna renk boru hattı dışında yazan bir yer var mı?
5. 03-GUVENLIK.md'deki değişmez kurallardan biri ihlal ediliyor mu
   (otomatik Hz/parlaklık, firmware yazma, 60 Hz'e dokunma, karartma tabanı, panik kısayolu)?
6. Sözleşme (T002) değişmiş mi; değiştiyse 02-MIMARI.md güncellenmiş mi?

Derlemeyi ve testleri kendin de çalıştır (sessiz komutlarla).
Sonuç: ONAY veya numaralı DÜZELT listesi (her madde: dosya, sorun, beklenen düzeltme).
Düzeltmeleri kendin yapma, listele.
```

## 4. Düzeltme (inceleme DÜZELT dediyse)

**Nerede:** Claude Code, yeni oturum · **Model:** görevi yazan model

```text
T___ incelemesi aşağıdaki DÜZELT listesini verdi. Sadece bu maddeleri düzelt.
Dokümanları baştan okuma; maddelerin geçtiği dosyaları ve gerekirse CLAUDE.md'deki
haritadan ilgili bölümü aç. Commit mesajı "T___ düzeltme: ..." ile başlar.
Sonunda her madde için tek satır: ne yaptın.

[DÜZELT listesi]
```

Ardından **yeniden inceleme:** aynı inceleyen model, yeni oturum, sadece düzeltme commit'leri.

```text
T___ düzeltmesini incele. Sadece `git log --oneline --grep "^T___ düzeltme"`
commit'lerine ve aşağıdaki maddelere bak. Her madde için: düzeldi / düzelmedi.
Düzeltmenin yol açtığı yeni bir sorun gördüysen ekle. Sonuç: ONAY veya DÜZELT.

[DÜZELT listesi]
```

## 5. Faz sonu değerlendirmesi

**Nerede:** Claude app · **Model:** Opus 5.5 · **Effort:** max

```text
hp-v214a projesinde Faz __ bitti. Bu fazın görev listesi, LESSONS.md ve gerçek
monitör test sonuçlarım aşağıda. Değerlendir:
- Planla gerçek sonuç arasında fark var mı?
- Sonraki fazın görevlerinde değişmesi gereken bir şey var mı?
- LESSONS.md'ye eklenmesi gereken ders var mı?

[görev listesi + test sonuçları + LESSONS.md buraya]
```
