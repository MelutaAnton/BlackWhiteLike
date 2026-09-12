using BlackWhiteLike.Models;
using BlackWhiteLike.Services.Village;

namespace BlackWhiteLike.Tests
{
    /// <summary>
    /// Фейк <see cref="IVillagerDiseaseService"/> для тестов, которым не нужен реальный подбор
    /// кандидата (например, <c>PeriodicNeedServiceTests</c>) — <see cref="HasEligibleCandidate"/>
    /// settable напрямую, остальные члены — нейтральные заглушки ("никто не болен").
    /// </summary>
    internal sealed class FakeVillagerDiseaseService : IVillagerDiseaseService
    {
        public bool HasEligibleCandidate { get; set; } = true;

        public bool IsSick(VillagerId id) => false;

        public bool TryGetActiveNeed(VillagerId id, out NeedType type, out int siteIndex)
        {
            type = default;
            siteIndex = default;
            return false;
        }

        public bool TryGetSickVillager(int siteIndex, out VillagerId id)
        {
            id = default;
            return false;
        }
    }
}
