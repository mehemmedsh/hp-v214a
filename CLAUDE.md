# CLAUDE.md

Her Claude Code oturumu bu dosyayı otomatik okur. Kısa ve sabit tutulur: durum ve sıradaki görev burada değil, `docs/04-FAZLAR.md`'nin ilk satırında.

## Proje

HP P21b G4 monitörü için Windows uygulaması (C# / .NET 10 / WPF) ve v2'de Pixel yardımcı uygulaması (Kotlin). Monitör RTX 3080'in HDMI'ında, RTX 3090 ağır işler için boş. Repo adı tarihsel olarak `hp-v214a`.

## Değişmez kurallar (özet; tam metin `docs/03-GUVENLIK.md`)

1. Hz ve parlaklık sadece kullanıcı değiştirdiğinde değişir. Üç istisna var, üçü de kullanıcının bilinen değerine döner: onaylanmayan değişiklikte 15 sn'de geri dönüş, sürücü güncellemesinin sildiği onaylı ayarı geri yükleme, dış programın değiştirdiği GPU tablosunu geri yükleme.
2. Firmware'e yazma yok.
3. Orijinal 60 Hz mod silinmez, değiştirilmez.
4. Her donanım ve sistem yazması geri alma noktası oluşturur ve Güvenlik katmanından geçer.
5. Overclock testleri ve deneyler telefondan başlatılamaz.
6. Panik kısayolu bütün efektleri ve katmanları kapatır; kalibrasyon kalır.

Ayrıca: DDC 04/05/08 ve E6–FF'ye yazılmaz; GPU renk tablosunu sadece renk boru hattı yazar; ek karartma beyazı en fazla %15'e indirir; güç limiti sadece düşürülür.

## Katmanlar

Arayüz → Uygulama servisleri → Güvenlik → Donanım → Saf mantık. Arayüz donanımı doğrudan çağırmaz, saf mantık donanımı kullanmaz. Ayrıntı: `docs/02-MIMARI.md`.

## Görev akışı

- Bir oturum, bir görev. Yazma, inceleme ve düzeltme ayrı oturumlardır; model oturum ortasında değişmez.
- Önce test, sonra kod; görev başına yaklaşık 200 satır.
- Commit mesajı `T___: ` ile başlar. Gövdesi: ne yapıldı, neler test edildi, inceleyicinin bakması gereken 3 nokta. İnceleme buradan başlar.
- Bitince: `docs/04-FAZLAR.md`'de durum ✅ ve "Sıradaki görev"; ders varsa `LESSONS.md` → "Geliştirme dersleri".
- 🔧 görev, kullanıcı gerçek monitörde test etmeden ✅ olmaz.
- Hazır metinler: `docs/05-HANDOFF.md`.

## Okuma haritası

Dokümanların tamamını okuma, sadece gerekeni oku. Emin olamadığın yerde ilgili dokümanı aç; tahminle yazma.

| Ne | Ne zaman |
|---|---|
| `docs/04-FAZLAR.md`: "Çalışma kuralları", kendi satırın (`grep -n "| T___ |"`) ve bağımlı görevlerin durumu | Her görev |
| `docs/02-MIMARI.md` | Her kod görevi ve inceleme |
| `docs/03-GUVENLIK.md` | Her yazma ve inceleme (sadece metin olan T311 ve T502 hariç) |
| `docs/01-OZELLIKLER.md` ve `docs/08-GORUNTU-YONTEMLERI.md`: sadece görev numarasının geçtiği bölümler (`grep -n "T___"`) | Görev numarası geçiyorsa |
| `docs/00-DONANIM.md` → Monitör, OSD, DDC/CI | T004, T301, T305, T306, T307, T702, T703, T907, T908 |
| `docs/00-DONANIM.md` → EDID, Hz tavanı | T004, T101, T102, T2xx, T304, T805, T901 |
| `docs/00-DONANIM.md` → Bilgisayar, Kablolama (kurtarma dahil) | T102, T104, T303, T304, T403–T406, T806, T807, T902, T903 |
| `docs/00-DONANIM.md` → Oturma konumu | T401, T402, T803 |
| `donanim/hp-p21b-g4/` (ham EDID ve DDC) | T003, T004, T101, T301 |
| `LESSONS.md` → sadece "Geliştirme dersleri" | Her görev (donanım bulguları zaten `00-DONANIM.md`'de) |

`README.md`, `docs/06`, `docs/07`, `docs/09` ve `fikir/` özet, geçmiş ve arka plandır; görevlerde okunmaz.

## Derleme, test ve token tasarrufu

- Bulut oturumunda `dotnet` yoksa: `apt-get update && apt-get install -y dotnet-sdk-10.0` (Microsoft'un indirme sunucusu engelli, Ubuntu paketi çalışıyor).
- `net10.0-windows` projeleri `EnableWindowsTargeting` ile Linux'ta da derlenir; platformdan bağımsız projelerin testleri Linux'ta çalışır. Windows'a özel testler sadece CI'da.
- Push'tan önce derle ve testleri çalıştır. Çıktıyı kısa tut: `tools/dev/` betikleri (T001 kurar) ya da `dotnet build -nologo -v q -tl:off -clp:"ErrorsOnly;Summary"` ve `dotnet test -nologo -v q -tl:off`.
- CI: `gh run list -L 3`; hata varsa `gh run view <id> --log-failed | tail -n 80`. Logun tamamını çekme, `gh run watch` kullanma.
- Büyük dosyayı baştan sona okuma; `grep -n` ile bölümü bul, sadece o aralığı oku. Okuduğunu tekrar okuma.
- Kullanım limiti yaklaşınca yeni göreve başlama; yarım görev bırakma.
