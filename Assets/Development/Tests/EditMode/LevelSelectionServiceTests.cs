using NUnit.Framework;
using UnityEngine;
using BlackWhiteLike.Data.Configs.Levels;
using BlackWhiteLike.Services.Level;

namespace BlackWhiteLike.Tests
{
    /// <summary>Тесты <see cref="LevelSelectionService"/> (game-design §7, шаг 7).</summary>
    public class LevelSelectionServiceTests
    {
        [Test]
        public void TryGetSelected_NothingSelected_ReturnsFalse()
        {
            var service = new LevelSelectionService();

            var found = service.TryGetSelected(out var level);

            Assert.IsFalse(found);
            Assert.IsNull(level);
        }

        [Test]
        public void Select_ThenTryGetSelected_ReturnsTrue_AndSameConfig()
        {
            var service = new LevelSelectionService();
            var level = ScriptableObject.CreateInstance<LevelConfig>();

            service.Select(level);

            Assert.IsTrue(service.TryGetSelected(out var selected));
            Assert.AreSame(level, selected);
        }

        [Test]
        public void ClearSelection_AfterSelect_TryGetSelectedReturnsFalse()
        {
            var service = new LevelSelectionService();
            var level = ScriptableObject.CreateInstance<LevelConfig>();
            service.Select(level);

            service.ClearSelection();

            Assert.IsFalse(service.TryGetSelected(out var selected));
            Assert.IsNull(selected);
        }

        [Test]
        public void ConsumeReturnToLevelSelect_WithoutRequest_ReturnsFalse()
        {
            var service = new LevelSelectionService();

            Assert.IsFalse(service.ConsumeReturnToLevelSelect());
        }

        [Test]
        public void RequestReturnToLevelSelect_ThenConsume_ReturnsTrue_ExactlyOnce()
        {
            var service = new LevelSelectionService();

            service.RequestReturnToLevelSelect();

            Assert.IsTrue(service.ConsumeReturnToLevelSelect());
            Assert.IsFalse(service.ConsumeReturnToLevelSelect()); // одноразовость
        }
    }
}
