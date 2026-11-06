# NXNativeWindows Easy Installer v2.0.2

Kredit: **Easy Installer Created by iSolution** · **Original NXNativeWindows by zenvyzenshiro**.

1. Buka EXE v2.0.2 sesuai arsitektur Windows. Gunakan ARM64 untuk Windows 11 ARM64, x64 untuk Intel/AMD.
2. Jika versi lama sedang terbuka, tekan Stop lalu tutup dan jalankan EXE v2.0.2. Pilih folder instalasi yang sama agar tool yang sudah terpasang digunakan kembali, lalu klik **Install Everything**.
3. Log besar selalu terlihat. Tarik pembatas horizontal ke atas untuk memperbesar log. Wrap membungkus baris panjang.
4. Pada **Apple Setup**, klik **Choose Xcode .xip**, pilih file Xcode `.xip` resmi, lalu **Open**. Folder pada daftar hanya untuk navigasi; tidak perlu memilih folder output. Hasil ekstraksi dan path SDK diatur otomatis ke `sdk\iPhoneOS.sdk`. Long path Windows ditangani oleh adapter ekstraksi. Hasil parsial dari kegagalan lama diabaikan dan SDK diekstrak kembali ke staging bersih. Folder repo seperti `C:\NXNativeWindows` tidak dianggap sebagai SDK.
5. Metal yang sudah terpasang dideteksi otomatis. Jika belum ada, unduh paket Metal Developer Tools for Windows dari Apple ke Downloads atau di samping XIP. Aplikasi mencari paket itu dan menjalankan installer/ekstraksi yang didukung; tidak ada pemilih folder Metal.
6. Login, UAC, dan layar lisensi Apple dapat tetap memerlukan tindakan pengguna. Metal Windows terpisah dari Xcode XIP. Penggunaan iOS SDK pada Windows tetap memerlukan otorisasi yang memang mencakupnya; persetujuan lisensi Xcode standar saja tidak cukup.
7. Setelah semua pemeriksaan wajib lulus, gunakan **Launch NXNativeWindows IPA Builder**.

**Browse** membuka pemilih folder di dalam aplikasi. Buka folder dengan klik dua kali atau tempel path; subfolder baru bisa diketik pada Installation path. Cancel mempertahankan path sebelumnya. Jika memilih drive, gunakan subfolder seperti `D:\NXNativeWindows`, bukan akar drive.

**Check for Updates** memeriksa commit GitHub terbaru. **Install / Repair** memperbaiki pemasangan yang sama. Repo dengan commit yang sama diperiksa checksum-nya lalu digunakan kembali; source yang hilang/rusak dipulihkan dari cache sebelum mengunduh ulang. Log menunjukkan koneksi, byte unduhan, retry, dan jumlah file ekstraksi. Alur Python Environment tetap dipertahankan.

Tampilan produksi sudah dirender dan diperiksa dalam 22 skenario, termasuk resize, density dan drag pembatas log. Tes inti lulus 93/93. Instalasi penuh pada Windows, paket Apple asli dan build IPA tetap belum diuji end-to-end di lingkungan pembuatan ini. ARM64 masih eksperimental. Detail ada di `docs/TEST_REPORT.md`.

Tes SDK tambahan pada Python 3.13.17: 10 lulus, 1 tes I/O Win32 dilewati karena host pengujian Linux. Adapter memakai path extended Windows dan tidak mengubah registry sistem. I/O Win32 sebenarnya tetap belum diuji langsung pada perangkat Windows.
