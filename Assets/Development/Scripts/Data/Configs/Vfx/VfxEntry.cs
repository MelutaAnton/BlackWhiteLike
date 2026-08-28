using System;
using BlackWhiteLike.Models;
using UnityEngine;

namespace BlackWhiteLike.Data.Configs.Vfx
{
    /// <summary>
    /// Одна строка соответствия VfxEffectType → префаб/пул (game-design §14.2) — элемент массива
    /// <see cref="VfxConfig"/>. Простая сериализуемая структура-данные (не сервис/SO) — по образцу
    /// <c>NeedEventEntry</c> (см. <c>Data/Configs/Levels/NeedEventEntry.cs</c>), но здесь как класс
    /// с геттерами (а не публичные поля), чтобы Inspector-массив в VfxConfig оставался читаемым SO,
    /// а VfxFactory работал через свойства, как и с остальными конфигами проекта.
    /// </summary>
    [Serializable]
    public sealed class VfxEntry
    {
        [SerializeField] private VfxEffectType _type;
        [SerializeField] private GameObject _prefab;
        [Tooltip("Сколько инстансов заранее создать в пуле (VfxFactory.Prewarm) при старте.")]
        [SerializeField] private int _poolSize = 4;

        public VfxEffectType Type => _type;
        public GameObject Prefab => _prefab;
        public int PoolSize => _poolSize;
    }
}
