# Новые возможности COI Editor

## Сохранение состояния опций

Добавлена возможность сохранять и автоматически загружать включенные опции между сессиями игры.

### Возможности

- **Автоматическая загрузка**: При загрузке сохранения игры автоматически применяются сохраненные опции
- **Ручное управление**: Кнопки в UI для сохранения, загрузки и очистки состояния
- **Полное покрытие**: Сохраняются все переключаемые опции из всех категорий

### Расположение файла

Состояние сохраняется в:
```
%APPDATA%\Captain of Industry\Mods\ResourceQuantityEditor\options_state.txt
```

### Использование

1. Откройте редактор (F8)
2. Перейдите на вкладку **Sandbox**
3. Настройте нужные опции
4. Нажмите **"Save current"** для сохранения текущего состояния
5. При следующей загрузке игры опции применятся автоматически

### Управление через UI

В секции **"Options state"** доступны:
- **Save current** - сохранить текущее состояние всех опций
- **Load saved** - загрузить сохраненное состояние вручную
- **Clear saved** - удалить сохраненный файл
- Индикатор наличия сохраненного состояния

### Сохраняемые категории опций

#### Economy Cheats
- Instant build & construction
- Source/sink buildings
- No maintenance
- No construction costs
- Free research
- Infinite focus

#### Settlement Needs
- No food need
- No workers need
- No power need
- No computing need
- No unity need
- Unlimited unity
- No clean water need
- No wastewater production
- No disease effects

#### Environment Cheats
- Disable food consumption
- Instant tree growth
- No air pollution
- No water pollution
- No ship pollution
- No train pollution
- No vehicle pollution
- No bio waste
- No landfill waste
- No toxic slurry waste
- No depleted uranium waste

#### Terrain Controls
- Process mining designations
- Process surface designations
- Disable terrain physics
- Unlimited mining area
- Unlimited tower area

#### Logistics
- No fuel consumption
- Unlimited vehicle fuel
- Instant cargo ships

### Технические детали

#### Новые файлы
- `Services/OptionsStateService.cs` - сервис для сохранения/загрузки состояния

#### Измененные файлы
- `ResourceQuantityEditor.cs` - регистрация сервиса и автозагрузка
- `ResourceQuantityEditorUi.cs` - UI для управления состоянием

#### Формат файла

Простой текстовый формат:
```
OptionName=True
AnotherOption=False
...
```

### Примечания

- Файл создается автоматически при первом сохранении
- Опции применяются только если файл существует
- При ошибках загрузки выводятся предупреждения в лог
- Можно редактировать файл вручную (не рекомендуется)
