# AGENTS.md — AbilitySystem

Godot 4 C# addon, Unreal GAS benzeri. Namespace `AbilitySystem`. Kullandığı addon'lar: `Tags` (`using Tags;`), `Timers` (`using Timers;`). Onlar bu addon'u bilmez. Birden fazla yapay zeka çalışıyor.

## Kurallar (özet; kod/sahne yazmadan önce `Docs/RULES.md`)
- `Gameplay` ön eki yok. Tek sorumluluk; aşırı soyutlama, aşırı bağımlılık ve döngüsel bağımlılık yok.
- Her sınıf `addons/AbilitySystem/<Sınıf>/` klasöründe. Projeye özel veri proje kökündeki `Resource/`'da.
- Sahne: düğüm doğrudan özel sınıftan yaratılır (`script` + `metadata/_custom_type_script`), UID `.cs.uid`'den.
- Hot-path'te allocation/LINQ/boxing yok; mümkünse saf C#.
- Test yazma/çalıştırma yok (kullanıcı ayrı yapar); yalnızca derlemeyi kontrol et.
- Git bu klasörde çalışır. `Test/` kullanıcıya ait, dokunma. Kapsam dışı değişiklik yok, kuralları kendi başına değiştirme.

## Dokümanlar (yalnızca gerekince oku)
- `Docs/PROGRESS.md`: işe başlarken "Yapılıyor"a ekle, bitince güncelle; başkasının işine dokunma.
- `Docs/ARCHITECTURE.md`: sınıf haritası, bağımlılık yönü.
- `Docs/IDEAS.md`: karara bağlanmamış ileride eklenebilecek özellikler; yeni fikir çıkınca oraya yaz.
- `Docs/Systems/<Sistem>.md`: yalnızca üzerinde çalışılan sistemin planı.
