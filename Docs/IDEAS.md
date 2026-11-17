# Fikirler — AbilitySystem

> Henüz kararlaştırılmamış, ileride eklenebilecek özellikler. Unreal GAS'ta var, bizde yok. Önceliği kullanıcı belirler; seçilen madde ilgili `Systems/<Sistem>.md` planına taşınır ve buradan silinir.
> Kesin kararlar ve planlı işler burada değil: `Systems/` ve `PROGRESS.md`.

## Effect
- **Taşma ayarları:** yığın sınırına ulaşınca başka effect uygula (`OverflowEffects`), taşan uygulamayı reddet (`bDenyOverflowApplication`), taşınca yığını temizle (`bClearStackOnOverflow`).
- **Koşullu effect'ler:** bir effect uygulanınca (veya başarılı olunca) başka bir effect'i tetikleme.
- **Uygulanma şansı:** `%X ihtimalle uygulan`.
- **Seviye ve eğri:** effect seviyesi; magnitude'un seviyeye göre eğriden okunması (Unreal `ScalableFloat`). Ability seviyesiyle birlikte düşünülmeli.
- **Modifier başına tag şartı:** modifier yalnızca kaynakta/hedefte belirli tag'ler varken geçerli.
- **Ayrı tag şartları:** kaldırma şartı (tag gelince effect silinir) ve süregelen şart (Faz 3 inhibition) ayrı alanlar; şu an yalnızca uygulama şartları var.
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
