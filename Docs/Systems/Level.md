# Seviye ve Eğri — Plan

> Durum: **uygulandı** (2026-11-18), plan kullanıcı onaylı. Godot içinde (headless) 26 yeni test, Faz 1–2 ile birlikte 134; eğri ve listeyle maliyet kontrolü 0 allocation. Fireball örneği ana sahne olarak hatasız çalışıyor.
>
> Uygulamada netleşenler:
> - Hesap `Magnitude.ScaleByLevel(sayı, seviye)` (internal); `EffectSpec.CalculateMagnitude` Constant'ta `Value`, AttributeBased'de `Coefficient` için çağırır.
> - `LevelValues` doluyken `Value`/`Coefficient` inspector'da görünür kalır (alan açıklaması "liste doluyken kullanılmaz" der): dizi düzenlenirken liste görünürlüğü değişseydi inspector her tuşta yeniden kurulurdu.
> - Godot `Curve`: test eğrisinde (domain 0–10, doğrusal tanjant) noktalar arası doğrusal, domain dışı uç değer doğrulandı.
> Karşılığı: Unreal `FScalableFloat` (+ curve table), `FGameplayEffectSpec::Level`, `FGameplayAbilitySpec::Level`.

## Amaç
- Aynı ability/effect tanımı seviyeye göre farklı sayılar versin: Fireball 1. seviyede 20 mana, 5. seviyede 30; hasar, cooldown, süre ve periyot da seviyeyle.
- Seviye çalışma anında effect spec'ine verilir; ability kendi seviyesini yaptığı spec'lere geçirir.

## Kararlar
| Konu | Karar |
|---|---|
| Değer tanımı | `Magnitude`'da iki yol. `LevelValues` (seviye 1, 2, 3… için değerler) doluysa o kullanılır. Boşsa sayı × `LevelCurve(seviye)` (Godot `Curve`, boşsa çarpan 1). Eğri `.tres` olarak kaydedilip birçok tanımda paylaşılabilir (Unreal curve table satırı gibi). **Kullanıcı** |
| Seviye tipi | `float` (Unreal gibi), varsayılan 1. `Ability.Level` int'ten float'a geçer. **Kullanıcı** |
| Listede ara seviye | Unreal curve table gibi: iki değer arasında doğrusal (`[20, 30, 40]`, seviye 2.5 → 35). 1'in altında ilk değer, son seviyenin üstünde son değer (sabit uzatma). **Kullanıcı ("Unreal gibi")** |
| Yığın | Yığın kendi seviyesini ve magnitude'larını korur; farklı seviyeli uygulama yalnızca sayıyı artırır (Faz 2 kuralı sürer). **Kullanıcı** |
| Maliyette SetByCaller | Yok; seviyeli maliyet ve cooldown eğri/listeyle yapılır. **Kullanıcı** |
| Seviye değişimi | `AbilityContainer.SetLevel`: yeni seviye bundan sonraki spec'lerde ve maliyet/cooldown'da geçerli. Aktif effect'ler uygulandıkları seviyede kalır (Unreal gibi). **Kullanıcı** |
| ASC'de seviye | Yok. Unreal'ın ASC'sinde seviye yoktur; karakter seviyesi oyunun işidir, verirken/uygularken koddan geçilir. Başlangıç effect'leri ve ability'leri seviye 1. Seviyeli aktör için kodla: `Abilities.Give(ability, 5)`, `Effects.Apply(new EffectSpec(init) { Level = 5 })`. **Kullanıcı ("Unreal gibi")** |
| Ölçeklenen türler | Constant'ta `Value`, AttributeBased'de `Coefficient` (Unreal'da ikisi de ScalableFloat). SetByCaller ölçeklenmez (değeri kod verir). Custom hesap seviyeyi `context.Level`'dan okur. Süre ve periyot da `Magnitude` olduğu için kendiliğinden ölçeklenir. |

## Değişiklikler

### Magnitude
- Yeni alanlar, yalnızca Constant ve AttributeBased'de görünür: `float[] LevelValues`, `Curve LevelCurve`.
- Ölçekli sayı (`number` = Constant'ta `Value`, AttributeBased'de `Coefficient`):
  - `LevelValues` doluysa: `i = seviye − 1`; `i ≤ 0` → ilk değer, `i ≥ son` → son değer, arada `Lerp(values[⌊i⌋], values[⌊i⌋+1], i − ⌊i⌋)`. `Value`/`Coefficient` kullanılmaz.
  - Değilse: `LevelCurve` boşsa `number`, doluysa `number × LevelCurve.Sample(seviye)`.
- AttributeBased sonucu: ölçekli katsayı × attribute'un current değeri (takip ve snapshot kuralları aynı).
- Godot `Curve` notları (alan açıklamasına ve dokümana): eğrinin **Max Domain**'ini en yüksek seviyeye, **Max Value**'yu en büyük çarpana ayarla (Godot varsayılanı 0–1). Unreal gibi düz çizgiler için noktaların tanjantlarını doğrusal yap. Domain dışında uç değer kullanılır.

### EffectSpec, ActiveEffect, CalculationContext
- `EffectSpec.Level { get; set; } = 1f`: uygulamadan önce verilir (`SourceAttributes` gibi). `CalculateMagnitude` bunu kullanır.
- `ActiveEffect.Level` (= `Spec.Level`, salt okunur), `CalculationContext.Level` (spec yoksa 1).
- Yığın: değişiklik yok; yığın ilk spec'iyle kalır.

### Ability
- `Level` float; `AbilityContainer.Give(definition, float level = 1f)`. Zaten verilmiş tanımı tekrar vermek seviyeyi değiştirmez (K5); bunun için `SetLevel`.
- `MakeEffectSpec` spec'e `Level = Level` yazar; önbellekteki maliyet ve cooldown spec'leri de.
- `bool AbilityContainer.SetLevel(AbilityHandle handle, float level)`: seviyeyi yazar, maliyet/cooldown spec'lerini yeniden kurar (ability aktifken de). Aktif effect'lere dokunmaz.
- Kullanıcı kodunda `int` olarak okunan `Level` derleme hatası verir; şu an addon'da ve `Resource/`/`Test/` örneklerinde böyle kullanım yok.

## Performans
- Liste: dizi okuma + doğrusal interpolasyon. Eğri: Godot `Curve.Sample` (engine çağrısı, allocation yok). UI'nin her kare sorduğu kontroller (`CanActivate`, `CanAffordCost`, `CooldownRemaining`) 0 allocation kalır.
- `SetLevel` iki spec kurar (seviye değişiminde, kare başına değil).

## Test (headless)
- Liste: tam seviye, ara seviye (doğrusal), 1'in altı ve son seviyenin üstü, tek elemanlı liste, boş liste → eğri.
- Eğri: çarpan, eğri yok → sayı aynen, aynı eğrinin iki magnitude'da paylaşılması.
- AttributeBased katsayısının ölçeklenmesi; SetByCaller'ın ölçeklenmemesi; Custom'da `context.Level`.
- Süre ve periyodun seviyeyle değişmesi; `EffectSpec.Level` varsayılan 1; `ActiveEffect.Level`.
- Yığın: farklı seviyeli uygulama sayıyı artırır, magnitude değişmez.
- Ability: `Give(…, level)`, `MakeEffectSpec` seviyesi, `SetLevel` sonrası maliyet/cooldown ve `CanAffordCost` yeni seviyede, aktif effect eski seviyede; kontrol sorgularında 0 allocation.
- Faz 1–2 testleri yeniden (Ability `Level` tipi değişiyor).

## Uygulama Sırası
1. `Magnitude` alanları, ölçekli sayı hesabı, inspector görünürlüğü.
2. `EffectSpec.Level`, `ActiveEffect.Level`, `CalculationContext.Level`; `CalculateMagnitude`.
3. `Ability.Level` float, `MakeEffectSpec`, önbellekteki spec'ler, `AbilityContainer.Give`/`SetLevel`.
4. Testler (yeni + Faz 1–2).
5. Doküman: `Effect.md`, `Ability.md` (netleşenler), `ARCHITECTURE.md`, bu dosyanın durumu.

## Bağımlılıklar
Yeni sınıf yok. `Magnitude` Godot'un `Curve` tipini kullanır (export, Resource; zorunlu Godot tipi). Katmanlar arası yön değişmez.

## Bu Planın Dışında
- ASC'de seviye alanı (kullanıcı kararı: Unreal gibi yok).
- Maliyet/cooldown'da SetByCaller (kullanıcı kararı: yok).
- Seviye değişince aktif effect'lerin güncellenmesi (kullanıcı kararı: kalır).
- Birden çok satırlı ortak tablo (Unreal `UCurveTable` / CSV içe aktarma): şimdilik tek tek `Curve` `.tres`'leri; gerekirse IDEAS.
