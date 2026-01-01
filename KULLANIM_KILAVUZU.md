# 📰 HaberBenim - Kullanım Kılavuzu

Otomatik haber toplama, düzenleme ve sosyal medyaya yayınlama platformu.

---

## 🚀 Başlangıç

### Sistemi Başlatma

Terminal'de proje klasöründe şu komutu çalıştırın:

```bash
./start-all.sh
```

Bu komut tüm servisleri başlatır:

- **Admin Panel:** http://localhost:4200
- **Public Web:** http://localhost:4201
- **API:** http://localhost:5078

---

## 🔐 Giriş Yapma

1. http://localhost:4200 adresine gidin
2. Giriş bilgileri:
   - **E-posta:** `admin@local`
   - **Şifre:** `Admin123!`

---

## 📋 Ana Özellikler

### 1. 📡 Kaynak Yönetimi (Sources)

Haber kaynaklarınızı ekleyin ve yönetin.

**Menü:** Sidebar → Sources

| Kaynak Türü     | Açıklama                        |
| --------------- | ------------------------------- |
| **RSS**         | RSS feed URL'si ile haber çekme |
| **X (Twitter)** | X hesaplarından tweet çekme     |

**Yeni Kaynak Ekleme:**

1. "+ Add Source" butonuna tıklayın
2. Kaynak türünü seçin (RSS veya X)
3. RSS için URL, X için kullanıcı adı girin
4. Kategori ve güven seviyesini belirleyin
5. Kaydet

---

### 2. 📥 Feed (Gelen Haberler)

Kaynaklardan otomatik çekilen haberler burada listelenir.

**Menü:** Sidebar → Feed

**Durumlar:**

- 🟡 **Pending:** Beklemede
- 🟢 **AutoReady:** Otomatik yayına hazır
- 🔵 **Approved:** Onaylandı
- 🔴 **Rejected:** Reddedildi
- ✅ **Published:** Yayınlandı

**İşlemler:**

- Habere tıklayarak detay sayfasına gidin
- Düzenleyin, onaylayın veya reddedin

---

### 3. ✏️ Editör (Editor)

Haber içeriğini düzenleyin ve yayına hazırlayın.

**Özellikler:**

- Başlık ve özet düzenleme
- Web içeriği düzenleme
- X (Twitter) metni düzenleme
- Instagram açıklaması düzenleme
- Görsel yönetimi (AI görsel üretimi dahil)

**Yayın Kanalları:**

- ☐ Web (Public website)
- ☐ X (Twitter)
- ☐ Instagram
- ☐ Mobile Push

---

### 4. 🖼️ Görsel Yönetimi

Her haber için görsel ekleyebilirsiniz.

**Seçenekler:**

1. **Kaynak Görseli:** Orijinal kaynaktan otomatik çekme
2. **AI Görsel:** Yapay zeka ile görsel üretme
3. **Manuel:** Kendi görselinizi yükleyin

**AI Görsel Üretme:**

1. Editör sayfasında "Media" bölümüne gidin
2. "AI Görsel Üret" butonuna tıklayın
3. İsteğe bağlı prompt yazın
4. Üretilen görsel otomatik eklenir

---

### 5. 📤 Yayınlama

Haberleri farklı kanallara yayınlayın.

**Menü:** Sidebar → Publishing

**Yayın Süreci:**

1. Feed veya Editor'den haberi seçin
2. Yayın kanallarını işaretleyin (Web, X, Instagram)
3. "Publish" butonuna tıklayın

**Yayın Durumları:**

- ⏳ **Pending:** Sırada bekliyor
- ✅ **Succeeded:** Başarılı
- ❌ **Failed:** Başarısız
- 🔄 **Retry:** Tekrar denenecek

---

### 6. 📷 Instagram Entegrasyonu

Instagram'a otomatik paylaşım yapın.

**Menü:** Sidebar → Instagram

**İlk Kurulum:**

1. Instagram sayfasına gidin
2. Graph API Explorer'dan Page Access Token alın
3. Token'ı forma yapıştırın
4. Instagram User ID'nizi girin
5. "Bağlantıyı Kaydet" tıklayın

**Gereksinimler:**

- Professional Instagram hesabı (Business/Creator)
- Instagram hesabı Facebook sayfasına bağlı olmalı
- Görseller herkese açık URL'den erişilebilir olmalı

---

### 7. 🐦 X (Twitter) Entegrasyonu

X'e otomatik tweet atın.

**Menü:** Sidebar → X Integration

**Kurulum:**

1. X Developer Portal'dan API anahtarlarını alın
2. Ayarlar sayfasında anahtarları girin:
   - API Key
   - API Secret Key
   - Access Token
   - Access Token Secret
3. "Test Connection" ile bağlantıyı doğrulayın

---

### 8. 📊 Analytics

Yayın istatistiklerini görüntüleyin.

**Menü:** Sidebar → Analytics

**Göstergeler:**

- Toplam yayınlanan haber sayısı
- Kanal bazlı yayın sayıları
- Başarı/başarısızlık oranları
- Kaynak bazlı performans

---

### 9. ⚠️ Alerts (Uyarılar)

Sistem uyarılarını takip edin.

**Menü:** Sidebar → Alerts

**Uyarı Türleri:**

- 🔴 **Critical:** Acil müdahale gerekli
- 🟠 **Warning:** Dikkat edilmeli
- 🔵 **Info:** Bilgilendirme

---

### 10. 📜 Audit Log

Tüm sistem işlemlerinin kaydı.

**Menü:** Sidebar → Audit

Kimin, ne zaman, ne yaptığını görebilirsiniz.

---

## 🌐 Public Website

Yayınlanan haberler burada görüntülenir:

**URL:** http://localhost:4201

Ziyaretçiler:

- Ana sayfada son haberleri görür
- Habere tıklayarak detay sayfasına gider
- Kategorilere göre filtreleme yapabilir

---

## ⚙️ Ayarlar

### System Settings

**Menü:** Sidebar → Settings

Burada sistem geneli ayarları yapabilirsiniz:

- API anahtarları
- OAuth yapılandırması
- Varsayılan değerler

---

## 🔄 Otomatik İşlemler

Sistem arka planda şunları otomatik yapar:

| İşlem                          | Sıklık       |
| ------------------------------ | ------------ |
| RSS kaynaklarından haber çekme | Her 5 dakika |
| X kaynaklarından tweet çekme   | Her 1 dakika |
| Yayın kuyruğunu işleme         | Sürekli      |

---

## ❓ Sık Sorulan Sorular

### Haberler neden gelmiyor?

1. Kaynak aktif mi kontrol edin (Sources → Active toggle)
2. Kaynak URL/identifier doğru mu?
3. Alerts sayfasında hata var mı?

### Instagram paylaşımı çalışmıyor?

1. Token süresi dolmuş olabilir (60 gün geçerli)
2. PUBLIC_ASSET_BASE_URL ayarlı mı?
3. Görsel mevcut mu? (Instagram görsel zorunlu)

### X paylaşımı çalışmıyor?

1. API anahtarları doğru mu?
2. "Test Connection" başarılı mı?
3. X Developer Portal'da "Read and Write" izni var mı?

### Görsel üretilemiyor?

1. AI Image ayarları aktif mi?
2. Internet bağlantısı var mı? (Pollinations API kullanılıyor)

---

## 📞 Destek

Sorun yaşarsanız:

1. Alerts sayfasını kontrol edin
2. API loglarını inceleyin: `/tmp/api-output.log`
3. Angular loglarını inceleyin: `/tmp/angular-output.log`

---

## 🎉 İpuçları

1. **Hızlı Yayın:** Feed'den direkt "Approve & Publish" yapabilirsiniz
2. **Toplu İşlem:** Birden fazla haber seçip toplu onay/red yapabilirsiniz
3. **Önizleme:** Yayınlamadan önce "Preview" ile kontrol edin
4. **Zamanlama:** İleri tarihli yayın için "Schedule" kullanın

---

**İyi yayınlar! 📰✨**
