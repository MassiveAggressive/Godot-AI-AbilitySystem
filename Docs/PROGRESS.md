# İlerleme Takibi

> Her AI/geliştirici: işe başlamadan önce oku, başlarken "Yapılıyor"a ekle, bitirince "Yapıldı"ya taşı.
> Format: `- [x] Açıklama — [Yapan, YYYY-MM-DD]`

## Yapıldı
- [x] Proje iskeleti: `Docs/` klasörü ve sınıf klasörleri için yapı — [Claude, 2026-11-16]
- [x] Kural/mimari/ilerleme dokümanları (`RULES.md`, `ARCHITECTURE.md`, `PROGRESS.md`, `AGENTS.md`, `CLAUDE.md`) — [Claude, 2026-11-16]
- [x] Godot 4.7.2 C# test projesi: `project.godot`, `AbilitySystem.csproj` (proje kökünde, repoya girmez) — [Claude, 2026-11-16]
- [x] Tag sistemi uygulandı; ayrıntılı geçmiş Tags reposunun `Docs/PROGRESS.md` dosyasında — [Claude, 2026-11-16]
- [x] Proje addon'a çevrildi: bu repo `addons/AbilitySystem/` oldu (git geçmişi korundu), tag sistemi `addons/Tags/` (Godot-AI-Tags) olarak ayrıldı; `plugin.cfg` + `AbilitySystemPlugin` eklendi — [Claude, 2026-11-17]

- [x] Attribute sistemi planı (`Docs/Systems/Attribute.md`): set'ler C# sınıfı, iki çarpan işlemi, değer sınıfı `AttributeValue`, aktöre özel başlangıç değerleri init effect ile — [Claude, 2026-11-17]
- [x] Klasör yapısı sadeleştirildi: `Core/` ve `Assets/` kalktı, sınıf klasörleri doğrudan addon kökünde (`RULES.md` §4, kullanıcı kararı) — [Claude, 2026-11-17]

## Yapılıyor
- (boş)

## Yapılacak
- [ ] Attribute sistemi uygulaması (`Docs/Systems/Attribute.md`): `ModifierOp`, `AttributeValue`, `AttributeSet`, `AttributeId`, `AttributeRegistry`, `AttributeContainer`
- [ ] `Modifier`
- [ ] `EffectData` (Resource) / `Effect` / `EffectSpec`
- [ ] `AbilityData` (Resource) / `Ability` / `AbilitySpec`
- [ ] `Cooldown`
- [ ] `AbilityTask`
- [ ] `AbilitySystemComponent` (en son, alt sistemleri birleştirir)
- [ ] Örnek sahne ve örnek ability (Fireball vb.)

## Sorunlar / Notlar
- Henüz ana sahne (`run/main_scene`) yok; örnek sahne eklenince proje kökündeki `project.godot`'a girilmeli.
- `.sln` oluşturulmadı; gerekirse Godot editöründen (Project → Tools → C# → Create C# solution) üretilebilir.
- `AbilitySystemPlugin` şimdilik boş; editör özellikleri eklendikçe doldurulur.
- `Docs/RULES.md` Tags reposunda da aynı; değişirse ikisini birden güncelle.
- Attribute set'lerini Resource tabanlı yapma fikri ertelendi; artıları/eksileri `Docs/Systems/Attribute.md` → "İleride Değerlendirilecek".
