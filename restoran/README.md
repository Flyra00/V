# Setup Cepat Setelah Git Clone (Wajib)

1. Copy file contoh config lokal:

   - `appsettings.Local.example.json` -> `appsettings.Local.json`

2. Isi koneksi database milik masing-masing developer di `appsettings.Local.json`:

```json
{
  "ConnectionStrings": {
    "DefaultConnection": "Server=localhost\\SQLEXPRESS;Database=RestoranDB;Trusted_Connection=True;TrustServerCertificate=True;MultipleActiveResultSets=true"
  }
}
```

3. Jalankan migrasi database:

```bash
dotnet ef database update --project restoran.csproj --startup-project restoran.csproj
```

4. Jalankan aplikasi:

```bash
dotnet run --project restoran.csproj
```

Catatan:
- `appsettings.Local.json` sudah di-`gitignore`, jadi aman dan tidak ikut ke repository.
- Bisa juga pakai environment variable: `ConnectionStrings__DefaultConnection`.

---

Berikut struktur sistem restoran yang sudah diperbagus, dirapikan, dan disesuaikan dengan permintaan: manajemen bahan mentah dihapus, lalu ditambahkan kelola kategori, pengaturan pajak PPN, pajak service, serta beberapa improvisasi agar sistem lebih lengkap dan realistis.

Sistem Restoran

1. Role Pengguna

1. Admin

Admin memiliki akses penuh terhadap seluruh sistem.

Tugas Admin:

mengelola user

mengelola role pengguna

mengelola data kasir

mengelola data supervisor

mengelola data owner

mengelola menu

mengelola kategori menu

mengelola meja

mengelola pengaturan pajak

mengelola pengaturan service charge

mengelola metode pembayaran

mengelola master data sistem


Admin berperan sebagai pengatur utama sistem agar semua data restoran berjalan dengan benar.


---

2. Supervisor

Supervisor berfokus pada kegiatan operasional restoran.

Tugas Supervisor:

memantau aktivitas restoran

mengelola fasilitas restoran

mencatat kerusakan fasilitas

mencatat barang pecah atau hilang

memantau status meja

memantau pesanan yang sedang berjalan

membantu mengecek kelancaran pelayanan


Catatan:

manajemen bahan mentah tidak dimasukkan lagi

supervisor lebih diarahkan pada operasional, fasilitas, dan kontrol pelayanan



---

3. Kasir

Kasir berfokus pada transaksi dan pembayaran.

Tugas Kasir:

menerima pesanan offline

melihat pesanan dari customer

memproses pembayaran tunai

memproses pembayaran QRIS / transfer / e-wallet

mencetak struk pembayaran

melihat transaksi hari ini

mengubah status pembayaran

membatalkan transaksi jika diperlukan, sesuai izin sistem


Catatan:

kasir hanya menangani bagian transaksi

kasir tidak memiliki akses ke pengaturan sistem utama



---

4. Bagian Masak / Dapur

Bagian masak berfokus pada pesanan yang masuk ke dapur.

Tugas Bagian Masak:

menerima daftar pesanan makanan dan minuman

melihat detail pesanan per meja

melihat catatan khusus dari customer

mengubah status pesanan menjadi diproses

mengubah status pesanan menjadi selesai

mencetak struk dapur


Contoh status pesanan dapur:

menunggu

diproses

selesai

dibatalkan


Catatan tambahan:

struk kasir dan struk dapur dapat dibuat berbeda

struk dapur hanya menampilkan menu yang perlu dibuat

struk kasir menampilkan total pembayaran, pajak, service, dan metode bayar



---

5. Owner

Owner berfokus pada pemantauan bisnis.

Tugas Owner:

melihat dashboard pendapatan

melihat laporan penjualan harian

melihat laporan penjualan bulanan

melihat menu paling laku

melihat metode pembayaran yang digunakan

melihat laporan pajak dan service charge

melihat data kerusakan barang

melihat data kehilangan barang

melihat performa restoran secara keseluruhan


Catatan:

owner tidak perlu mengubah data transaksi

owner lebih cocok diberi akses lihat laporan saja

owner bisa melihat data sebagai bahan evaluasi bisnis



---

6. Customer

Customer adalah pengguna yang melakukan pemesanan.

Jenis Customer:

tamu biasa

member

member plus


Fitur Customer:

scan QR meja

nomor meja otomatis terbaca

melihat daftar menu

melihat kategori menu

memilih makanan atau minuman

menambahkan catatan pesanan

melihat subtotal pesanan

melihat pajak dan service charge

memilih pembayaran online atau lewat kasir

melihat status pesanan

mengakses akun member


Contoh catatan pesanan:

tidak pedas

es sedikit

tanpa bawang

gula sedikit

saus dipisah



---

2. Hak Akses Ringkas

Role	Hak Akses

Admin	Akses penuh seluruh sistem
Supervisor	Operasional, fasilitas, kerusakan barang, monitoring pesanan
Kasir	Transaksi, pembayaran, cetak struk
Bagian Masak	Pesanan dapur, status masakan, cetak pesanan dapur
Owner	Laporan, pendapatan, pajak, service, performa restoran
Customer	Scan meja, pesan menu, bayar, akses member



---

3. Fitur Utama Sistem

1. Manajemen User

Fitur ini digunakan untuk mengatur pengguna sistem.

Isi fitur:

login

logout

tambah user

edit user

hapus user

atur role pengguna

ubah password

status user aktif / tidak aktif



---

2. Manajemen Role

Fitur ini digunakan untuk membedakan hak akses setiap pengguna.

Role utama:

admin

supervisor

kasir

bagian masak

owner

customer


Dengan role management, setiap pengguna hanya bisa mengakses fitur sesuai tugasnya.


---

3. Manajemen Meja

Fitur ini digunakan untuk mengatur meja restoran.

Isi fitur:

nomor meja

status meja

QR meja

meja aktif

meja kosong

sesi meja


Status meja sebaiknya tidak hanya menggunakan teks biasa, tetapi menggunakan TableSession.

Contoh logika:

jika ada session aktif, meja dianggap terisi

jika tidak ada session aktif, meja dianggap kosong

setelah pembayaran selesai, session meja ditutup



---

4. Manajemen Kategori Menu

Fitur ini ditambahkan agar menu restoran lebih rapi.

Contoh kategori:

makanan utama

minuman

dessert

snack

paket hemat

menu promo

menu rekomendasi


Fungsi kategori:

memudahkan customer mencari menu

memudahkan admin mengelola daftar menu

memudahkan laporan penjualan berdasarkan jenis menu



---

5. Manajemen Menu

Fitur ini digunakan untuk mengatur daftar makanan dan minuman.

Isi data menu:

nama menu

kategori

harga

deskripsi

foto menu

status tersedia / tidak tersedia

menu favorit

menu promo


Catatan:

karena manajemen bahan dihapus, stok bahan mentah tidak perlu dimasukkan

menu cukup diberi status tersedia atau tidak tersedia

status menu bisa diubah oleh admin atau supervisor



---

6. Pemesanan

Fitur ini digunakan untuk mencatat pesanan customer.

Alur pemesanan:

customer scan QR meja → pilih menu → tambah catatan → kirim pesanan → pesanan masuk ke kasir dan dapur

Isi data pesanan:

nomor meja

nama customer jika member

daftar menu

jumlah pesanan

catatan pesanan

subtotal

status pesanan


Status pesanan:

menunggu

diterima

diproses

selesai

dibatalkan



---

7. Pembayaran

Fitur ini digunakan untuk memproses pembayaran.

Metode pembayaran:

tunai

QRIS

transfer bank

e-wallet

debit/kartu jika dibutuhkan


Data pembayaran:

subtotal

PPN

service charge

diskon jika ada

total akhir

metode pembayaran

status pembayaran


Status pembayaran:

belum dibayar

menunggu konfirmasi

lunas

gagal

dibatalkan



---

8. Pengaturan Pajak

Fitur ini ditambahkan untuk mengatur PPN dan pajak/service restoran.

PPN

Admin dapat mengatur:

nama pajak

persentase PPN

status aktif / tidak aktif


Contoh:

PPN 11%

PPN aktif

PPN dihitung dari subtotal pesanan


Service Charge

Admin dapat mengatur:

nama biaya service

persentase service

status aktif / tidak aktif


Contoh:

service charge 5%

service charge aktif

dihitung dari subtotal atau setelah PPN, sesuai aturan restoran


Contoh perhitungan:

Subtotal pesanan: Rp100.000
PPN 11%: Rp11.000
Service 5%: Rp5.000
Total bayar: Rp116.000


---

9. Cetak Struk

Sistem dapat mencetak dua jenis struk.

Struk Kasir

Berisi:

nama restoran

nomor meja

tanggal transaksi

daftar menu

subtotal

PPN

service charge

diskon

total bayar

metode pembayaran


Struk Dapur

Berisi:

nomor meja

daftar menu

jumlah menu

catatan pesanan

waktu pesanan

status pesanan


Struk dapur tidak perlu menampilkan harga.


---

10. Manajemen Fasilitas dan Inventaris

Karena manajemen bahan mentah dihapus, bagian stok diganti menjadi inventaris restoran saja.

Contoh inventaris:

piring

gelas

mangkok

sendok

garpu

kursi

meja

blender

mesin kasir

printer struk


Fitur inventaris:

tambah barang

edit barang

jumlah barang

kondisi barang

status barang

laporan rusak

laporan hilang



---

11. Laporan Kerusakan Barang

Fitur ini digunakan untuk mencatat barang yang rusak, pecah, atau hilang.

Isi laporan:

nama barang

jumlah barang

jenis kejadian

keterangan

waktu kejadian

siapa yang melaporkan

status laporan


Contoh jenis kejadian:

pecah

rusak

hilang

perlu diganti



---

12. Member

Fitur member digunakan untuk customer yang memiliki akun.

Jenis member:

member biasa

member plus


Fitur member:

melihat riwayat pesanan

mendapatkan poin

mendapatkan promo tertentu

menyimpan data nomor telepon

mendapatkan level member


Contoh level:

regular

silver

gold

platinum



---

13. Promo dan Diskon

Improvisasi tambahan yang bagus untuk sistem restoran adalah fitur promo.

Jenis promo:

diskon persen

diskon nominal

promo menu tertentu

promo khusus member

promo minimal pembelian


Contoh:

diskon 10% untuk member

potongan Rp10.000 minimal belanja Rp100.000

buy 1 get 1 untuk menu tertentu



---

14. Laporan

Laporan digunakan oleh owner dan admin untuk melihat kondisi bisnis.

Jenis laporan:

laporan pendapatan harian

laporan pendapatan bulanan

laporan penjualan menu

laporan menu terlaris

laporan metode pembayaran

laporan pajak PPN

laporan service charge

laporan diskon

laporan kerusakan barang

laporan transaksi dibatalkan



---

4. Entitas Data yang Dipakai

User

Id

FullName

Username

Password

RoleId

Status


Role

Id

RoleName

Description


DiningTable

Id

TableNumber

QRCode

Status


TableSession

Id

TableId

GuestType

MemberId

StartTime

EndTime

Status


Category

Id

CategoryName

Description

Status


Menu

Id

CategoryId

Name

Description

Price

Image

Status


Order

Id

TableSessionId

OrderDate

OrderStatus

Subtotal

TaxAmount

ServiceAmount

DiscountAmount

TotalAmount


OrderItem

Id

OrderId

MenuId

Qty

Price

Note

ItemStatus


Payment

Id

OrderId

Method

Amount

PaymentDate

PaymentStatus


TaxSetting

Id

TaxName

TaxPercentage

IsActive


ServiceSetting

Id

ServiceName

ServicePercentage

IsActive


InventoryItem

Id

ItemName

Category

Qty

Condition

Status


DamageReport

Id

ItemId

Qty

Description

ReportedBy

ReportDate

Status


Member

Id

Name

Phone

Level

Point

Status


Promo

Id

PromoName

PromoType

DiscountValue

MinimumPurchase

StartDate

EndDate

Status



---

5. Alur Sistem

Alur Customer

Customer scan QR meja → nomor meja terbaca otomatis → customer memilih menu → customer menambahkan catatan jika ada → customer mengirim pesanan → pesanan masuk ke kasir dan dapur → customer melakukan pembayaran → pesanan diproses


---

Alur Kasir

Kasir menerima pesanan → mengecek detail transaksi → memproses pembayaran → sistem menghitung subtotal, PPN, service, dan total akhir → kasir mencetak struk → status pembayaran menjadi lunas


---

Alur Dapur

Dapur menerima pesanan → melihat detail menu dan catatan customer → mengubah status menjadi diproses → makanan/minuman dibuat → status diubah menjadi selesai


---

Alur Supervisor

Supervisor memantau operasional → mengecek status meja dan pesanan → mencatat fasilitas rusak atau barang pecah → membuat laporan operasional


---

Alur Owner

Owner membuka dashboard → melihat pendapatan → melihat laporan penjualan → melihat pajak dan service charge → melihat laporan kerusakan barang → mengevaluasi performa restoran


---

6. Struktur Modul Sistem

Modul	Fungsi

User Management	Mengelola akun pengguna
Role Management	Mengatur hak akses
Meja	Mengatur nomor meja dan QR meja
Table Session	Mengatur sesi customer per meja
Kategori Menu	Mengelola kategori makanan/minuman
Menu	Mengelola daftar menu
Order	Mengelola pemesanan
Kitchen Order	Mengelola pesanan dapur
Payment	Mengelola pembayaran
Tax Setting	Mengatur PPN
Service Setting	Mengatur biaya service
Promo	Mengatur diskon/promo
Member	Mengelola customer member
Inventaris	Mengelola barang restoran non-bahan
Damage Report	Mencatat barang rusak/hilang
Report	Menampilkan laporan bisnis



---

7. Kesimpulan Struktur Baru

Sistem restoran ini memiliki 6 role utama:

admin

supervisor

kasir

bagian masak

owner

customer


Modul utama sistem:

manajemen user

manajemen role

manajemen meja

table session

manajemen kategori menu

manajemen menu

pemesanan

pembayaran

pengaturan PPN

pengaturan service charge

cetak struk

member

promo

inventaris non-bahan

laporan kerusakan barang

laporan penjualan


Bagian manajemen bahan mentah harus dihapus, sehingga sistem menjadi lebih sederhana dan fokus pada transaksi restoran, pengelolaan menu, pembayaran, pajak, service charge, inventaris restoran, serta laporan bisnis.
