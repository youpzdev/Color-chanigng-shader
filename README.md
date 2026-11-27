# Проверка шейдера

Сцены StencilBuffer находятся в `Assets/Scenes/StencilBuffer/`.
Сцены и инструкции запуска описаны в `Assets/Documentation/StencilBuffer/README.md`.

Открой `Assets/Scenes/ShaderTest.unity` и нажми Play.

- WASD или стрелки - движение относительно камеры.
- Space - прыжок с земли.
- При падении за пределы площадки персонаж возвращается в точку старта.

Куб и сфера на подиумах используют материал
`Assets/Materials/ShaderTest/SineColorScale.mat`. В материале меняются
два цвета, скорости анимации, минимальный и максимальный масштаб.
Параметры движения находятся в `Assets/Settings/ShaderTest/PlayerSettings.asset`.

Трава использует `Assets/Materials/Grass/GardenGrass.mat`. В материале
настраиваются цвета корней и кончиков, сила и направление ветра.
Поля X и Y направления соответствуют мировым осям X и Z.
`GrassBounds` обновляет границы отрисовки при изменении силы ветра и масштаба.
Трава не имеет коллайдеров и не мешает движению персонажа.

Команда `Tools/Shader Test/Build Garden Grass` пересоздает газон в этой сцене
с тем же расположением травинок и сохраняет сцену. Используй ее вне Play Mode.

Проверка травы в открытом редакторе, после входа в Play Mode:

```text
unity command script run --file Tests/GrassVerification.cs --entry GrassVerification.Run --project-path <папка проекта> --format json --non-interactive --caller plugin --skill unity-pipeline
```

Проверка включает компиляцию, геометрию, границы отрисовки и расчет ветра
на GPU. Запускается на текущей сцене `ShaderTest`.

Условия задания и выбранный пайплайн записаны в
`../../../docs/01-color-changing-shader/task.md`.

## Папки

- `Assets/Scenes/` - сцены, StencilBuffer в отдельной подпапке
- `Assets/Scripts/` - код по заданиям, редакторские инструменты в Editor
- `Assets/Shaders/`, `Materials/`, `Models/`, `Textures/`, `Prefabs/` - ассеты по типам
- `Assets/Settings/` - настройки движения, света и рендеринга
- `Assets/Fonts/` - шрифты с лицензиями
- `Assets/Plugins/` и `Assets/youpzdev/` - сторонние и общие библиотеки

В учебных сценах экранного текста нет
