# GhostMacro

Windows için basit fare + klavye makro kaydedici / oynatıcı: tıkla, kaydet, oynat.

*[English below](#english)*

## Özellikler

- **Adım adım kayıt** (varsayılan): sadece tıklamalar, sürüklemeler, tekerlek ve tuşlar adım olur; fare yolu kaydedilmez.
- **Tam kayıt**: her fare hareketi ve tuş gerçek zamanlamasıyla kaydedilir.
- Adımlar arası bekleme **saniye** cinsinden, listede doğrudan düzenlenir (örn. `1,5`).
- Tuş ve kombinasyonları listeden seçerek ekleme (`Ctrl+C`, `Win+R`, `Ctrl+Shift+Esc`...).
- Metin yazdırma, bekleme, tekerlek, sürükleme adımları.
- Tekrar sayısı / sonsuz döngü, hız, rastgelelik, yumuşak fare hareketi.
- Global kısayollar, fare oynatılınca durdurma (isteğe bağlı).
- Türkçe ve İngilizce arayüz.
- Makrolar `.gmacro` (XML) dosyası olarak kaydedilir.

## Kurulum

Kurulum yok. [Releases](https://github.com/ilkayderici/ghost-macro/releases) sayfasından `GhostMacro.exe` indirip çalıştırın.
Gereksinim: Windows 10/11 (.NET Framework 4.8 Windows'ta hazır gelir).

## Kullanım

1. **Kayda başla** (F9) → ekranda istediğiniz yerlere sırayla tıklayın → yine **F9** ile bitirin.
2. Listede **Bekle (sn)** hücresine tıklayıp o adımdan *önce* beklenecek süreyi yazın.
3. Araya adım eklemek için bir satır seçip **＋ Tıklama**, **＋ Tuş** veya **＋ Bekle** düğmesine basın.
4. **Oynat** (F10) ile çalıştırın, **Durdur** (F12) ile kesin.

Diğer ayarlar (kayıt modu, hız, turlar arası bekleme...) **Gelişmiş ▾** altında.

| Kısayol | İşlev |
|---|---|
| F9 | Kaydı başlat / bitir |
| F10 | Oynat |
| F12 | Durdur |
| F8 | İmlecin olduğu yere tıklama adımı ekle |

Kısayollar **Ayarlar** menüsünden değiştirilebilir.

> **Not:** Yönetici olarak çalışan bir programı kontrol etmek için GhostMacro'yu da "Yönetici olarak çalıştır" ile açın.

## Derleme

.NET SDK gerekir.

```bash
cd src
dotnet build -c Release
```

Çıktı: `src/bin/Release/net48/GhostMacro.exe`

İç testler:

```bash
GhostMacro.exe --selftest sonuc.txt
```

---

<a id="english"></a>

## English

Simple mouse + keyboard macro recorder / player for Windows: click, record, play.

### Features

- **Step recording** (default): only clicks, drags, wheel and keys become steps; the mouse path is not recorded.
- **Full recording**: every mouse movement and key with real timing.
- Waits between steps in **seconds**, editable directly in the list (e.g. `1.5`).
- Pick keys and combinations from a list (`Ctrl+C`, `Win+R`, `Ctrl+Shift+Esc`...).
- Type-text, wait, wheel and drag steps.
- Repeat count / infinite loop, speed, randomness, smooth mouse movement.
- Global hotkeys, stop on mouse move (optional).
- Turkish and English UI.
- Macros are saved as `.gmacro` (XML) files.

### Install

No installer. Download `GhostMacro.exe` from [Releases](https://github.com/ilkayderici/ghost-macro/releases) and run it.
Requires Windows 10/11 (.NET Framework 4.8 is built in).

### Usage

1. **Start recording** (F9) → click the places you want in order → press **F9** again to finish.
2. Click a **Wait (s)** cell in the list and type the time to wait *before* that step.
3. To insert steps, select a row and press **＋ Click**, **＋ Key** or **＋ Wait**.
4. **Play** (F10) to run, **Stop** (F12) to stop.

Other settings (record mode, speed, gap between rounds...) are under **Advanced ▾**.

| Hotkey | Action |
|---|---|
| F9 | Start / stop recording |
| F10 | Play |
| F12 | Stop |
| F8 | Add a click step at the cursor position |

Hotkeys can be changed in the **Settings** menu.

> **Note:** To control a program running as administrator, run GhostMacro as administrator too.

### Build

Requires the .NET SDK.

```bash
cd src
dotnet build -c Release
```

Output: `src/bin/Release/net48/GhostMacro.exe`

Self-test:

```bash
GhostMacro.exe --selftest result.txt
```
