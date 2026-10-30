# AGENTS.md

Bu repoda çalışan her ajan bu dosyayı okur: Antigravity (Gemini, Opus, Sonnet) ve Claude Code (`CLAUDE.md` bu dosyayı içe aktarır). Kısa ve sabit tutulur: durum ve sıradaki görev burada değil, `docs/04-FAZLAR.md`'nin ilk satırında.

## Proje

HP P21b G4 monitörü (21.5", TN, 6-bit + FRC, flicker-free) için Windows uygulaması: C# / .NET 10 / WPF. Monitör RTX 3080'in HDMI'ında. Repo adı tarihsel olarak `hp-v214a`.

## Kullanıcının öncelikleri (2026-10-30)

1. **Görüntü kalitesi ve göz konforu her şeyden önce gelir.** Monitör renk ve parlaklık olarak bu panelden çıkabilecek en iyi hâlde görünsün, gözü yormasın.
2. **En yüksek hız 76 Hz** (ölçüldü). Hz modlarını kullanıcı NVIDIA özel çözünürlüğüyle ekler; uygulama modlar arasında geçer ve doğrular.
3. **Gerekli ince ayar olsun, aşırısı olmasın.** Ayar kamerasız yapılır: gözle ve desenlerle. Bu monitörde DaVinci'de renk işi yapılmaz, sadece zaman çizelgesi; renk referansı Pixel'in ekranı.
4. **Hızlı bitsin ama kalite düşmesin.** Her görev testlidir ve farklı bir modelce incelenir.
5. Arayüzün ana dili Türkçe; Azerbaycanca ve İngilizce T502'de.
6. Kullanıcı Antigravity'ye yeni başladı. Ona Türkçe, kısa ve adım adım anlat; teknik bir terimi ilk geçtiği yerde bir cümleyle açıkla.

## Değişmez kurallar (tam metin `docs/03-GUVENLIK.md`)

1. Hz ve parlaklık sadece kullanıcı değiştirdiğinde değişir. İki istisna var, ikisi de kullanıcının bilinen değerine döner: onaylanmayan Hz değişikliğinden 15 sn sonra önceki moda dönüş; dış bir programın değiştirdiği GPU renk tablosunu geri yükleme.
2. Firmware'e yazma yok. EDID override yazılmaz; Hz modlarını kullanıcı NVIDIA panelinden ekler.
3. Orijinal 60 Hz mod silinmez, değiştirilmez.
4. Her donanım ve sistem yazması geri alma noktası oluşturur ve Güvenlik katmanından geçer.
5. Panik kısayolu bütün efektleri (mavi ışık, ek karartma, canlılık) kapatır; kalibrasyon kalır.

Ayrıca: DDC 04/05/08 ve E6–FF'ye yazılmaz; GPU renk tablosunu sadece renk boru hattı yazar; ek karartma beyazı en fazla %15'e indirir; GPU'nun güç, saat, voltaj ve fan ayarlarına dokunulmaz.

## Ajanın kullanıcının bilgisayarındaki sınırları

Antigravity ajanı kullanıcının bilgisayarında, gerçek monitörün yanında çalışır.

- Uygulamayı (`MonitorTuner.App`) ve donanıma yazan kodu **çalıştırma**. Testler sahte donanımla çalışır. 🔧 testlerini sadece kullanıcı yapar; sen adımlarını yazarsın.
- Kayıt defterine, ekran ayarlarına, NVIDIA ayarlarına veya monitöre (DDC) dokunan komut çalıştırma.
- Serbest komutlar: `git`, `gh`, `dotnet restore/build/test/format`, `tools/dev/` betikleri. Başka bir komut ya da program kurulumu (`winget` vb.) gerekirse önce kullanıcıya nedenini söyle ve onay bekle.

## Katmanlar

Arayüz → Uygulama servisleri → Güvenlik → Donanım → Saf mantık. Arayüz donanımı doğrudan çağırmaz, saf mantık donanımı kullanmaz. Ayrıntı: `docs/02-MIMARI.md`.

## Görev akışı

- **Bir konuşma, bir görev.** Yazma, inceleme ve düzeltme ayrı konuşmalardır; konuşma ortasında model değişmez.
- Yazan ve inceleyen her zaman farklı model (`docs/04-FAZLAR.md` → görev tablosu).
- Başlamadan `git pull --rebase`. Bağımlı görevlerin hepsi ✅ veya 🔧 değilse başlama, kullanıcıya söyle.
- Önce test, sonra kod; görev başına yaklaşık 200 satır.
- Commit mesajı `T___: ` ile başlar. Gövdesi: ne yapıldı, neler test edildi, inceleyicinin bakması gereken 3 nokta. İnceleme buradan başlar.
- Durum: yazan bitirince 🟨 yapar. İnceleyen ONAY verirse ✅ yapar; 🔧 görevde kullanıcı testi geçene kadar 🔧 yapar. Ders varsa `LESSONS.md` → "Geliştirme dersleri".
- Push `main`'e. Push'tan önce derle, testleri çalıştır, `git pull --rebase`. Çakışma sadece `docs/04-FAZLAR.md`'deki durum sütunundaysa iki tarafı da koru; kodda çakışma varsa dur ve kullanıcıya söyle.
- Kullanım limiti yaklaşınca yeni göreve başlama; yarım görev bırakma.
- Hazır metinler (kurulum, yazma, inceleme, düzeltme, yardım): `docs/05-HANDOFF.md`.

## Okuma haritası

Dokümanların tamamını okuma, sadece gerekeni oku. Emin olamadığın yerde ilgili dokümanı aç; tahminle yazma.

| Ne | Ne zaman |
|---|---|
| `docs/04-FAZLAR.md`: "Çalışma kuralları", kendi satırın (`T___` ile ara) ve bağımlı görevlerin durumu | Her görev |
| `docs/02-MIMARI.md` | Her kod görevi ve inceleme |
| `docs/03-GUVENLIK.md` | Her yazma ve inceleme (sadece metin olan T311 ve T502 hariç) |
| `docs/01-OZELLIKLER.md` ve `docs/08-GORUNTU-YONTEMLERI.md`: sadece görev numarasının geçtiği bölümler | Görev numarası geçiyorsa |
| `docs/00-DONANIM.md` → Monitör, OSD, DDC/CI | T004, T301, T305, T306, T307, T312, T313 |
| `docs/00-DONANIM.md` → EDID, Hz tavanı | T004, T209, T311 |
| `docs/00-DONANIM.md` → Bilgisayar, Kablolama (kurtarma dahil) | T102, T104, T209, T303 |
| `docs/00-DONANIM.md` → Oturma konumu | T311, T312 |
| `donanim/hp-p21b-g4/` (ham EDID ve DDC) | T003, T004, T301 |
| `LESSONS.md` → sadece "Geliştirme dersleri" | Her görev |

`README.md`, `docs/06`, `docs/07`, `docs/09` ve `fikir/` özet, geçmiş ve arka plandır; görevlerde okunmaz.

## Derleme, test ve kullanım tasarrufu

- **Windows (Antigravity):** .NET 10 SDK kurulu olmalı (`dotnet --version` → 10.x). Değilse kullanıcıya `docs/05-HANDOFF.md` → 0'ı göster.
- **Linux (Claude Code bulut):** `dotnet` yoksa `apt-get update && apt-get install -y dotnet-sdk-10.0` (Microsoft'un indirme sunucusu engelli, Ubuntu paketi çalışıyor). `net10.0-windows` projeleri `EnableWindowsTargeting` ile derlenir; Windows'a özel testler sadece Windows'ta ve CI'da.
- Çıktıyı kısa tut: `tools/dev/` betikleri (T001 kurar) ya da `dotnet build -nologo -v q -tl:off -clp:"ErrorsOnly;Summary"` ve `dotnet test -nologo -v q -tl:off`.
- CI: `gh run list -L 3`; hata varsa `gh run view <id> --log-failed` çıktısının son 80 satırı (bash: `| tail -n 80`, PowerShell: `| Select-Object -Last 80`). Logun tamamını çekme, `gh run watch` kullanma.
- Büyük dosyayı baştan sona okuma; arayarak bölümü bul, sadece o aralığı oku. Okuduğunu tekrar okuma.
