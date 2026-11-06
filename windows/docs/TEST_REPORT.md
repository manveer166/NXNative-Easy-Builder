# NXNativeWindows Easy Installer v2.0.2 — Hasil Pengujian

Tanggal: **6 November 2026 (WIB)**. Easy Installer Created by **iSolution**. Original NXNativeWindows by **zenvyzenshiro**.

## Perubahan

- XIP dipilih sebagai file: **Choose Xcode .xip → pilih file → Open**. Folder pada daftar hanya untuk navigasi. Field folder output tidak tampil dan tombol Open tidak menerima folder. Nama file terpilih tampil, error sebelumnya dibersihkan, dan tombol Open tetap terlihat pada jendela 440×440.
- Ekstraksi memakai adapter Python yang dibundel. Adapter menjalankan ekstraktor dari repo GitHub terbaru tanpa mengubah source repo tersebut. Path Windows dinormalisasi dan diberi prefix extended-length sebelum I/O; target alias relatif `..` dinormalisasi sebelum disalin. Probe write/read long path dijalankan sebelum membaca XIP.
- Lokasi staging dipendekkan. SDK diaktifkan pada `sdk/iPhoneOS.sdk` setelah proses ekstraksi dan validasi berhasil. Folder parsial `sdk/extract-*` dari versi lama tidak digunakan sebagai SDK terpasang; XIP yang sudah dipilih dipakai untuk mencoba ulang secara bersih. SDK aktif lama dipertahankan sampai pengganti siap.
- Hard link canary dengan carrier yang hilang, hasil hard link yang tidak tertulis, placeholder tersisa, metadata versi di bawah 26, atau header Foundation/UIKit yang hilang menggagalkan validasi. Output gagal/cancel tidak diaktifkan dan staging dibersihkan. Log heartbeat ekstraksi muncul setiap 10 detik.
- ARM64 dan x64 memakai adapter dan perilaku UI yang sama. GUI dipublikasikan dengan binary native masing-masing. Toolchain x64 tetap memerlukan emulasi pada Windows 11 ARM64.
- Alur Python Environment identik dengan v2.0.1 berdasarkan perbandingan source. Pemeriksaan update GitHub, penggunaan ulang repo/cache, deteksi Metal, log besar, credit dan gerbang otorisasi Apple dipertahankan.

## Diagnosis log pengguna

Path header yang gagal ditulis panjangnya **273 karakter**. Upstream sudah memanggil `d.parent.mkdir(...)` sebelum `write_bytes(...)`; pola ini konsisten dengan batas path Windows ketika dukungan long path pada proses Python belum aktif. Status registry pengguna tidak diperiksa, sehingga diagnosis kebijakan Windows tersebut tetap berupa inferensi, bukan hasil pengukuran perangkat.

Dokumentasi Microsoft menjelaskan batas historis MAX_PATH 260 karakter, dukungan prefix extended-length, dan perlunya menormalkan dot segments sebelum prefix tersebut dipakai. Python 3.13 mendokumentasikan pengaruh pengaturan long paths pada `open` dan operasi path. Adapter memilih penanganan path eksplisit dan tidak mengubah registry sistem.

Referensi primer:

- [Microsoft: Maximum Path Length Limitation](https://learn.microsoft.com/en-us/windows/win32/fileio/maximum-file-path-limitation)
- [Python 3.13: Removing the MAX_PATH Limitation](https://docs.python.org/3.13/using/windows.html#removing-the-max-path-limitation)
- [Ekstraktor upstream pada commit yang diperiksa](https://github.com/zenvyzenshiro/NXNativeWindows/blob/269d61ff974b2e8e073d7357466ae130c3f6812f/win/sdk/extract_xip.py)

## Pemeriksaan yang benar-benar dijalankan

| Pemeriksaan | Hasil | Cakupan / batas |
|---|---|---|
| Tes inti + GitHub nyata | **93/93 lulus** | Host Linux; termasuk reuse repo, timeout/cancel, otorisasi, SDK staging/aktivasi/rollback dan proses Python nyata dengan XIP sintetis |
| Tes inti memakai Python 3.13.17 | **92/92 lulus** | Pengulangan suite yang sama dengan child SDK memakai 3.13.17; tanpa mengulang unduhan live GitHub |
| Tes adapter SDK Python 3.13.17 | **10 lulus, 1 skipped, 0 gagal** | XAR/PBZX/CPIO sintetis, hard link carrier di luar SDK, placeholder, alias framework, path >273 karakter, penolakan traversal, normalisasi path Windows/UNC |
| Python 3.12.14 | **10 lulus, 1 skipped, 0 gagal** | Tes adapter yang sama pada runtime awal build |
| Proses child SDK nyata | **Lulus** | C# memasang helper embedded, menjalankan Python, mengekstrak fixture, memvalidasi, mengaktifkan SDK dan membersihkan staging |
| SDK header panjang | **Lulus fixture I/O Linux** | Nama/path menyerupai header yang gagal; isi hasil dibandingkan byte demi byte dengan carrier sintetis |
| I/O Win32 extended-length | **Belum dijalankan** | Satu tes ini dilewati di Linux. Prefix/normalisasi Windows diuji sebagai string; hasil itu tidak menggantikan uji Win32 |
| UI produksi Avalonia/Skia | **22/22 skenario, 0 masalah akhir** | Render headless Linux memakai kontrol produksi, bukan mockup |
| File picker XIP | Lulus | Pilihan file dan Open melalui handler asli, folder ditolak, tidak ada field output, error pulih setelah pilihan valid |
| Resize XIP | Lulus | 760×600, 520×480, 440×440; teks, bagian layout dan tombol Open diperiksa |
| Main, Apple, folder Browse, log | Lulus regresi | 6 ukuran main, 3 ukuran Apple, 3 ukuran Browse, 3 density render, drag splitter dan interaksi Browse |
| GitHub terbaru | Lulus HTTP nyata | `main` / `269d61ff974b2e8e073d7357466ae130c3f6812f`, unduh/extract lalu reuse tanpa unduhan kedua |
| Build/publish | Lulus ARM64 dan x64 | .NET SDK 10.0.403, publish exit code 0; resource EXE versi 2.0.2 |
| Arsitektur binary | Lulus inspeksi PE | Host EXE, ANGLE, Skia dan HarfBuzz sesuai x64 / ARM64; bukan eksekusi native Windows |

Fixture XIP berisi data buatan, termasuk teks header `SYNTHETIC TEST HEADER`; tidak ada SDK atau binary Apple yang dibundel. Baseline ekstraktor upstream disertakan dengan lisensi MIT dan hash di `tests/Fixtures/README.md` agar tes dapat diulang tanpa internet. Produksi tetap menggunakan ekstraktor dari repo yang diperiksa saat install.

Satu tes heartbeat lama semula bergantung pada delay 120 ms dan gagal saat mesin build sibuk. Tes tersebut diperbaiki menjadi sinkronisasi berdasarkan kedatangan heartbeat nyata sebelum server fixture mengirim header. Hasil akhir tabel di atas diambil setelah perbaikan. Pengujian visual juga memastikan pesan error tidak tertinggal setelah pemilihan XIP valid.

## Yang belum diuji end-to-end

EXE v2.0.2 belum dijalankan langsung di Windows x64 maupun ARM64. Tidak ada perangkat Windows aktif yang tersambung pada sesi pembuatan. Host Linux tidak bisa membuktikan penanganan Win32 saat LongPathsEnabled nonaktif, dialog/UAC Windows, atau perilaku driver/tool native di perangkat pengguna. ARM64 tetap eksperimental untuk toolchain x64 yang berjalan melalui emulasi.

Belum diuji dengan Xcode XIP asli berizin, paket/sertifikat Apple asli, kompilasi SDK/Metal asli, build/signing/sideload IPA dan playback iPhone. CI Windows disiapkan untuk tes Win32, SDK sintetis, Browse dan smoke startup x64, tetapi belum dijalankan pada penyerahan ini. Render density bukan uji perpindahan monitor Windows fisik.

Autentikasi, lisensi dan otorisasi SDK Apple tetap berlaku. Attestation izin yang mencakup penggunaan SDK pada Windows tetap diperiksa sebelum ekstraksi. Adapter tidak memperoleh kredensial Apple, mengunduh SDK dari sumber alternatif, atau menghapus gate tersebut. Pemeriksaan signature XIP Windows sendiri belum diimplementasikan; gunakan file yang diperoleh resmi.

## Memakai pembaruan

1. Stop/tutup v2.0.1, lalu jalankan **EXE v2.0.2** yang sesuai: `win-arm64` untuk Windows 11 ARM64, `win-x64` untuk Intel/AMD.
2. Pilih **folder instalasi lama** dan klik **Install Everything**. Tool yang masih lolos probe dan repo utuh dipakai kembali. Tidak perlu menghapus instalasi lama.
3. XIP yang sudah tersimpan pada pengaturan dapat dipakai ulang. Jika diminta, pilih file `.xip` resmi lalu **Open**. Lokasi output diatur otomatis.
4. Hasil SDK parsial lama diabaikan; SDK baru baru diaktifkan setelah validasi selesai.

## Artefak final

| EXE | Byte | SHA-256 |
|---|---:|---|
| `NXNativeWindowsEasyInstaller-v2.0.2-win-arm64.exe` | 47,016,235 | `bbc039474be89af85f4a1ea6021e7c42ac57f109018238c45de955a74407ef70` |
| `NXNativeWindowsEasyInstaller-v2.0.2-win-x64.exe` | 48,329,311 | `c823c1c8faa1a00f4dd803d87de6fcde2759cdf4a804580b67260e017b5909a6` |

EXE self-contained; belum ditandatangani Authenticode. Source lengkap mencakup adapter embedded, UI, engine, build/CI scripts, fixture generator, baseline MIT, panduan, hasil JSON/log dan PNG render di `docs/evidence/`. Checksum deliverable ada di `SHA256SUMS-v2.0.2.txt`.
