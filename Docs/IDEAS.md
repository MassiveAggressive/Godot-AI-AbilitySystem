# Fikirler — AbilitySystem

> Henüz kararlaştırılmamış, ileride eklenebilecek özellikler. Unreal GAS'ta var, bizde yok. Önceliği kullanıcı belirler; seçilen madde ilgili `Systems/<Sistem>.md` planına taşınır ve buradan silinir.
> Kesin kararlar ve planlı işler burada değil: `Systems/` ve `PROGRESS.md`.

## Effect
- **Taşma ayarları:** yığın sınırına ulaşınca başka effect uygula (`OverflowEffects`), taşan uygulamayı reddet (`bDenyOverflowApplication`), taşınca yığını temizle (`bClearStackOnOverflow`).
- **Koşullu effect'ler:** bir effect uygulanınca (veya başarılı olunca) başka bir effect'i tetikleme.
- **Uygulanma şansı:** `%X ihtimalle uygulan`.
- **Modifier başına tag şartı:** modifier yalnızca kaynakta/hedefte belirli tag'ler varken geçerli.
- **Etkinleşince periyot politikası:** etkisiz effect tekrar etkinleşince periyodu sıfırlama veya hemen çalıştırıp sıfırlama (Unreal `ResetPeriod`/`ExecuteAndResetPeriod`); Faz 3'te yalnızca `NeverReset`.
- **Kaynak tag'ine göre bağışıklık:** uygulayanın tag'lerine bakan bağışıklık (ör. düşman takımından gelenlere); Faz 3'te yalnızca gelen effect'in `AssetTags`'ı.
- **Custom magnitude takibi:** hesaplama sınıfı baktığı attribute'ları bildirir, süreli effect'te AttributeBased gibi takip edilir; Faz 3'te süreli effect'te bir kez hesaplanır.
- **Dinamik tag'ler:** spec üzerinde çalışma anında effect'e asset/granted tag ekleme.
- **Zengin effect context:** vuruş sonucu, kaynak nesne gibi ek bilgi taşıyan paket (şu an `Instigator`, `Causer`, ASC).
- **Effect ile ability verme.**
- **Özel uygulama şartı:** koddan yazılan uygulama kontrolü (Unreal `ApplicationRequirement`).
- **Periyodik effect'te yığın eklenince çalıştırma:** yığın eklendiği anda bir kez çalıştırma seçeneği (şu an çalıştırılmıyor; Unreal'da da böyle olduğu doğrulanmadı).

## Ability / ASC (`Systems/Ability.md` kapsamı dışında kalanlar)
- Örneksiz ve çalıştırma başına örnekleme (şu an yalnızca aktör başına kopya).
- Aktifken yeniden tetikleme (`bRetriggerInstancedAbility`).
- `await` ile task; animasyon task'ı (`PlayAnimationAndWait`); animasyondan olay gönderme.
- Hedefleme (target actor / hedef verisi).
- Kaynak/hedef tag şartları; başarısızlık tag'leri.
- Aynı ability'yi birden fazla kaynağın vermesi (sayaç).

## Cue (`Systems/Cue.md` kapsamı dışında kalanlar)
- **Ability cue'ları:** ability'den `ExecuteCue(tag)`, `AddCue(tag)` (ability bitince kalkar; ör. cast parlaması).
- **Koddan cue ve ASC cue olayı:** `asc.Cues.Execute/Add/Remove`; aktör tüm cue'lara kendi kodunda tepki verir (Unreal `IGameplayCueInterface`).
- **Fireball örneğine cue:** cast ve vuruş görselleri.
- **Sahne havuzu:** sık açılan cue sahnelerini yeniden kullanma (Unreal cue actor recycling).
- **Cue büyüklüğü:** parametrede effect'ten bir sayı (Unreal `RawMagnitude`/`NormalizedMagnitude`, `MagnitudeAttribute`).
- **Yığın eklenince cue:** her yeni yığında Executed seçeneği (Unreal `Suppress Stacking Cues` karşılığı; şu an hiçbir şey olmaz).
- **Cue konumu:** vuruş noktası ve normali parametrede (Unreal `Location`/`Normal`); şu an Executed sahnesi aktörün konumunda açılır.

## Tags (Godot-AI-Tags reposu, burada tutulur çünkü AbilitySystem ihtiyaç duyar)
- `TagQuery` / `TagRequirements`: karmaşık sorgular (herhangi biri / hepsi / hiçbiri).
- `TagCountContainer` için tag'e özel dinleyici.
- **Tag seçme ağacı:** `TagContainer` için tag seçimi, Unreal'daki gibi hiyerarşik ağaç penceresiyle (ör. `State` → `Debuff` → `Stun`, `Action` → `Attack` → `Melee`/`Range`; düğümler açılıp kapanır, işaretlenerek eklenir). Şu an her tag, dropdown menüden tek tek seçiliyor (`TagContainer.BuildTagOptions`); tag sayısı artınca düz liste zorlaşıyor. Editör eklentisi (`#if TOOLS`) ve `TagContainer`'ın inspector alanı değişir; çalışma anı ve `Tags.tres` biçimi aynı kalır. Ayrıca aynı seçici tekil tag alanları için de kullanılabilir (`Magnitude.CallerTagName`, ability tetikleyicileri).

## Timers (Godot-AI-Timers reposu)
- Zaman genişletme: aktöre/gruba özel zaman ölçeği (bullet-time, donma). Şu an tek global saat.

## Seviye
- **Ortak eğri tablosu:** birden çok satırlı tablo (Unreal `UCurveTable`, CSV içe aktarma); şu an her eğri ayrı bir Godot `Curve` `.tres`'i (`Systems/Level.md`).

## Araçlar
- Debug görünümü (aktör üzerindeki effect/attribute/tag'leri gösteren panel veya komut).
- Resource tabanlı attribute set (`Systems/Attribute.md` → İleride Değerlendirilecek).
