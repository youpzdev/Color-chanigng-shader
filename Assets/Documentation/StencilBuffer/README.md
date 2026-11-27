# StencilBuffer

## Запуск

Открой одну из сцен в `Assets/Scenes/StencilBuffer/` и нажми Play:

- `01_XRay.unity` - персонаж и три препятствия разной высоты. Начальная позиция показывает частичное перекрытие.
- `02_Portal.unity` - две комнаты. Первая видна обычным способом, вторая только через проем портала.

WASD или стрелки - движение, Shift - бег, Space - прыжок, мышь - обзор.
Esc освобождает курсор, щелчок мышью снова включает управление. R возвращает
персонажа на старт. В X-ray используется камера от третьего лица, в портале -
от первого. Портал смотровой, без телепортации.

Настройки движения и чувствительности мыши находятся в
`Assets/Settings/StencilBuffer/DemoControllerSettings.asset`. Старый контроллер первой работы
сохраняется отдельно. Новый `StencilPlayerController` используется только
в stencil-сценах.

Экранного текста и HUD нет. Управление описано выше. Оформление можно
повторно применить командой `Tools/Stencil Buffer/Polish Presentation`.

## Материалы и рендерер

`Assets/Shaders/StencilBuffer/XRayOutline.shader` и `XRayOutlineFeature` используют stencil bit 1.
Маска всего силуэта исключает внутреннюю заливку. Расширенная оболочка
проверяет глубину и рисует контур только за препятствием. Финальный проход
очищает свой бит. Персонаж выбирается по слою `StencilActor`.

`Assets/Shaders/StencilBuffer/PortalMask.shader` записывает stencil bit 2. `PortalRoom.shader`
рисует геометрию второй комнаты только при совпадении этого бита. Маска
рисуется раньше обычной геометрии, содержимое комнаты - после нее, с записью
и проверкой глубины. Геометрия второй комнаты не отбрасывает внешние тени.

Параметры контура находятся в `Assets/Materials/StencilBuffer/XRayOutline.mat`.
Обе новые камеры используют `Assets/Settings/StencilBuffer/StencilForwardRenderer.asset`,
добавленный в PC и Mobile как дополнительный рендерер. Старый индекс 0
сохраняется. В новом рендерере выключены Depth Priming и SSAO.

## Проверки

В терминале, с открытым редактором и явным путем к корню Unity-проекта:

```text
unity command script run --file Tests/StencilVerification.cs --entry StencilVerification.Run --project-path <папка проекта> --format json --non-interactive --caller plugin --skill unity-pipeline
```

Проверка анализирует отрисованные пиксели: полное и частичное перекрытие,
отсутствие контура на видимой части, границы портала, отключение маски,
передний предмет, смещение камеры и обратную сторону.

Для проверки управления запусти нужную сцену в Play Mode, выбери Game View
и выполни:

```text
unity command script run --file Tests/StencilMovementVerification.cs --entry StencilMovementVerification.Run --project-path <папка проекта> --format json --non-interactive --caller plugin --skill unity-pipeline
```

Проверка посылает события клавиатуры через Input System, наблюдает движение,
прыжок и приземление, затем возвращает персонажа в начальную точку.

Дополнительная проверка контроллера в Play Mode:

```text
unity command script run --file Tests/StencilControllerVerification.cs --entry StencilControllerVerification.Run --project-path <папка проекта> --format json --non-interactive --caller plugin --skill unity-pipeline
```

Она проверяет разгон, торможение, одинаковую скорость по диагонали, бег,
ограничения прыжка, буфер приземления, короткое окно прыжка после схода с
края, оба режима камеры и освобождение курсора при потере фокуса.

Меню `Tools/Stencil Buffer/Create Assignment Scenes` предназначено только
для первоначальной сборки сцен. Если сцена задания уже существует,
генератор отказывается ее перезаписывать.
