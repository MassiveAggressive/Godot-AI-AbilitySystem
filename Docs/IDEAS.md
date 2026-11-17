# Fikirler — AbilitySystem

> Henüz kararlaştırılmamış, ileride eklenebilecek özellikler. Unreal GAS'ta var, bizde yok. Önceliği kullanıcı belirler; seçilen madde ilgili `Systems/<Sistem>.md` planına taşınır ve buradan silinir.
> Kesin kararlar ve planlı işler burada değil: `Systems/` ve `PROGRESS.md`.

## Effect
- **Taşma ayarları:** yığın sınırına ulaşınca başka effect uygula (`OverflowEffects`), taşan uygulamayı reddet (`bDenyOverflowApplication`), taşınca yığını temizle (`bClearStackOnOverflow`).
- **Koşullu effect'ler:** bir effect uygulanınca (veya başarılı olunca) başka bir effect'i tetikleme.
- **Uygulanma şansı:** `%X ihtimalle uygulan`.
- **Seviye ve eğri:** effect seviyesi; magnitude'un seviyeye göre eğriden okunması (Unreal `ScalableFloat`). Ability seviyesiyle birlikte düşünülmeli.
- **Modifier başına tag şartı:** modifier yalnızca kaynakta/hedefte belirli tag'ler varken geçerli.
- **Etkinleşince periyot politikası:** etkisiz effect tekrar etkinleşince periyodu sıfırlama veya hemen çalıştırıp sıfırlama (Unreal `ResetPeriod`/`ExecuteAndResetPeriod`); Faz 3'te yalnızca `NeverReset`.
- **Kaynak tag'ine göre bağışıklık:** uygulayanın tag'lerine bakan bağışıklık (ör. düşman takımından gelenlere); Faz 3'te yalnızca gelen effect'in `AssetTags`'ı.
- **Custom magnitude takibi:** hesaplama sınıfı baktığı attribute'ları bildirir, süreli effect'te AttributeBased gibi takip edilir; Faz 3'te süreli effect'te bir kez hesaplanır.
- **Dinamik tag'ler:** spec üzerinde çalışma anında effect'e asset/granted tag ekleme.
- **Zengin effect context:** vuruş sonucu, kaynak nesne gibi ek bilgi taşıyan paket (şu an `Instigator`, `Causer`, ASC).
- **Effect ile ability verme.**
- **Özel uygulama şartı:** koddan yazılan uygulama kontrolü (Unreal `ApplicationRequirement`).
- **Gameplay Cue'lar:** effect'e bağlı görsel/ses geri bildirimi (başlama, bitme, çalıştırma). Godot'ta sahne/efekt bağlama tasarımı gerekir.
- **Periyodik effect'te yığın eklenince çalıştırma:** yığın eklendiği anda bir kez çalıştırma seçeneği (şu an çalıştırılmıyor; Unreal'da da böyle olduğu doğrulanmadı).

## Ability / ASC (planlanan işlerin eksik ayrıntıları; Ability planı yazılırken ele alınacak)
- Ability tag kuralları: iptal eder, engeller, aktivasyon için gerekli/engelleyici tag'ler.
- Instancing politikaları (örneksiz, aktör başına, çalıştırma başına).
- Gameplay event'leri ile ability tetikleme.
- Hedefleme (target actor / hedef verisi).
- Pasif ability'ler, ability seviyesi.
- Giriş (input) bağlama.

## Tags (Godot-AI-Tags reposu, burada tutulur çünkü AbilitySystem ihtiyaç duyar)
- `TagQuery` / `TagRequirements`: karmaşık sorgular (herhangi biri / hepsi / hiçbiri).
- `TagCountContainer` için tag'e özel dinleyici.

## Timers (Godot-AI-Timers reposu)
- Zaman genişletme: aktöre/gruba özel zaman ölçeği (bullet-time, donma). Şu an tek global saat.

## Araçlar
- Debug görünümü (aktör üzerindeki effect/attribute/tag'leri gösteren panel veya komut).
- Resource tabanlı attribute set (`Systems/Attribute.md` → İleride Değerlendirilecek).
