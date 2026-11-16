# İlerleme Takibi

> Her AI/geliştirici: işe başlamadan önce oku, başlarken "Yapılıyor"a ekle, bitirince "Yapıldı"ya taşı.
> Format: `- [x] Açıklama — [Yapan, YYYY-MM-DD]`

## Yapıldı
- [x] Proje iskeleti: `Assets/`, `Core/`, `Resource/`, `Docs/` klasörleri — [Claude, 2026-11-16]
- [x] Kural/mimari/ilerleme dokümanları (`RULES.md`, `ARCHITECTURE.md`, `PROGRESS.md`, `AGENTS.md`, `CLAUDE.md`) — [Claude, 2026-11-16]
- [x] Godot 4.7.2 C# test projesi: `project.godot`, `AbilitySystem.csproj` (proje kökünde, repoya girmez) — [Claude, 2026-11-16]
- [x] Tag sistemi uygulandı; ayrıntılı geçmiş Tags reposunun `Docs/PROGRESS.md` dosyasında — [Claude, 2026-11-16]
- [x] Proje addon'a çevrildi: bu repo `addons/AbilitySystem/` oldu (git geçmişi korundu), tag sistemi `addons/Tags/` (Godot-AI-Tags) olarak ayrıldı; `plugin.cfg` + `AbilitySystemPlugin` eklendi — [Claude, 2026-11-17]

## Yapılıyor
- (boş)

## Yapılacak
- [ ] `Attribute` / `AttributeSet`
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
