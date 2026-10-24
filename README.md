# Краб против ада

Маленький краб, Божья Искра и девять кругов ада. Unity 6.6 (URP), всё художественное — из
Blender-скриптов, образы — по концепт-листу (`Docs/reference/concept.png`). План — [PLAN.md](PLAN.md).

## Состояние

| Этап | Что есть |
|---|---|
| 0. Основа | проект, git + LFS, конвейер Blender → GLB → Unity (glTFast), сцены собираются кодом |
| 1. Пролог и I круг — Лимб | пляж: еда, чайка (спрятаться под плитой), волна, гроза, Божья Искра, первые мысли, втягивание материи, первая палка, разлом. Лимб: мёртвый город в тумане, тени и падальщики, создание предметов (палка, камень, лом, нож, пистолет, патроны) из органики, минерала и металла, три разлома, маяки-сохранения, Страж Лимба — бой от первого лица (луч фонаря, удар с ударной волной, призыв теней, сердце-уязвимость) |

## Управление

WASD — идти · мышь — смотреть · Shift — бежать · Пробел — прыжок · ЛКМ — удар/бросок/выстрел ·
ПКМ — прицел · E — втянуть материю / съесть / использовать (держать — для материи и разломов) ·
Tab — создать (круг чертежей) · 1–5, X/Z — оружие · колесо — дальность камеры · Esc — пауза.
Геймпад: стики, A — прыжок, X — использовать, Y — создать, RT/LT — удар/прицел, бамперы — оружие.

## Как устроено

```
Assets/Crab/
  Scripts/Core/   ядро без Unity: материя, чертежи, инвентарь, здоровье, сохранение (тесты EditMode)
  Scripts/Game/   краб (CrabRig — процедурная анимация частей, CrabController, CameraRig, HeroCombat,
                  MatterPower), враги (Shade, Crawler, Rift, Guardian), сюжет (Prologue, Limbo),
                  интерфейс (GameUI, MainMenu), звук (Sfx — синтез), эффекты (Fx)
  Editor/         SceneBuilder (Crab → Build Scenes), TextureGen, GroundMesh, Snapshot
  Shaders/        Water, Rift, Glow, Soft
  UI/             стили (Crab.uss), шрифты Russo One и Nunito (OFL)
  Models/         GLB из Tools/blender
  Tests/          EditMode (ядро) и PlayMode (пролог, бой, разлом, Страж — в настоящих сценах)
Tools/blender/    lib.py (общие помощники, запекание процедурных материалов в PBR), make_hero.py,
                  make_items.py, make_enemies.py, make_world.py, preview.py
```

- Модели: `blender -b -P Tools/blender/make_world.py -- Assets/Crab/Models/World [имя ...]` (так же hero, items,
  enemies). Каждый ассет — процедурная геометрия + процедурные материалы, запечённые в текстуры (цвет с AO,
  нормали, шероховатость). У существ части с началом координат в суставах — их двигает код.
- Сцены (`Menu`, `Prologue`, `Limbo`) строит `Crab → Build Scenes` (или `-executeMethod Crab.EditorTools.SceneBuilder.BuildAll`);
  руками сцены не правятся — правки вносятся в генератор.
- Снимки: `-executeMethod Crab.EditorTools.Snapshot.All` → `Docs/shots`.
- Тесты: `-runTests -testPlatform EditMode` и `PlayMode` (PlayMode пишет снимки с интерфейсом в `Docs/shots/play-*.png`).
- Сохранение — `save.json` в `Application.persistentDataPath`; маяки сохраняют игру.
