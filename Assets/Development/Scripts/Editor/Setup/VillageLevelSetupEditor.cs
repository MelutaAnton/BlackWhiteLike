using BlackWhiteLike.Models;
using UnityEditor;

namespace BlackWhiteLike.Editor.Setup
{
    /// <summary>
    /// Шаг 7 — Кампания из 5 уровней (game-design.md, шаг 7). Собирает боевую сцену
    /// <c>Assets/Development/Scenes/Village_Base.unity</c> — тем же ядром сборки, что временная
    /// песочница <c>_TestVillage.unity</c> (<see cref="TestVillageSceneSetupEditor.BuildVillageSceneCore"/>):
    /// дома/поле/храм, рука, камера, [Systems], [Miracles], HUD, Alignment-визуал. Единственные
    /// отличия от песочницы — путь сцены и регистрация в Build Settings
    /// (<see cref="BuildSettingsEditor.EnsureCampaignScenesInBuildSettings"/>), вызываемая
    /// автоматически ядром сборки при <c>isTemporarySandbox: false</c>.
    /// fallback-<c>LevelConfig</c> ядра — LevelConfig_01.asset (см. class-doc ядра): реальная
    /// прогрессия по уровням 2-5 живёт в отдельных .asset-ассетах (LevelCatalog), сама сцена деревни
    /// едина для всей кампании (решение владельца — одна запечённая раскладка).
    /// Идемпотентен: повторный запуск дособирает недостающее, не дублирует объекты (см. ядро).
    /// </summary>
    public static class VillageLevelSetupEditor
    {
        [MenuItem("BlackWhiteLike/Levels/Build Village Level")]
        public static void BuildVillageLevel()
        {
            TestVillageSceneSetupEditor.BuildVillageSceneCore(
                BuildSettingsEditor.VillageBaseScenePath,
                GameState.LevelPlaying,
                isTemporarySandbox: false);
        }
    }
}
