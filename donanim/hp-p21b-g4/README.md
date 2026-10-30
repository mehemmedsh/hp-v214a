# HP P21b G4: ham donanım verileri

İlk gün kontrolünde (2026-10-30) kullanıcının ünitesinden alınan dosyalar. Yorumları `docs/00-DONANIM.md` ve `LESSONS.md` içinde.

| Dosya | Ne | Nasıl alındı | Kullanan görev |
|---|---|---|---|
| `edid-original.bin` | Orijinal EDID (256 bayt, EDID 1.3 + CTA-861 uzantısı) | CRU → Export, hiçbir değişiklik uygulanmadan | T101 test seti, T004 profil |
| `edid-original.txt` | Aynı EDID, onaltılık metin | CRU → Export | — |
| `ddc-vcp-60hz.txt` | Tüm DDC/CI VCP kodları ve değerleri (60 Hz'de) | ControlMyMonitor → Save All Items | T004, T301 |

Bu dosyalar **orijinal** hali gösterir; uygulama hiçbir zaman bunların üstüne yazmaz.
