using BlackWhiteLike.Models;
using BlackWhiteLike.Services.Flow;
using UnityEngine.SceneManagement;
using VContainer.Unity;

namespace BlackWhiteLike.Installers
{
    /// <summary>
    /// Точка входа root-scope (game-design §7, шаг 7): если игра стартовала со сцены Bootstrap —
    /// сразу переключается на MainMenu. Позволяет держать Bootstrap пустой служебной сценой
    /// (только <c>[RootScope]</c>), не собирая туда UI. Не трогает FSM (<c>IGameFlowService</c>) —
    /// это делает сценовый <c>GameBootstrap</c> после загрузки MainMenu.
    /// </summary>
    public sealed class BootstrapFlowEntryPoint : IStartable
    {
        private readonly ISceneLoaderService _sceneLoader;

        public BootstrapFlowEntryPoint(ISceneLoaderService sceneLoader)
        {
            _sceneLoader = sceneLoader;
        }

        public void Start()
        {
            if (SceneManager.GetActiveScene().name == SceneNames.Bootstrap)
            {
                _sceneLoader.Load(SceneNames.MainMenu);
            }
        }
    }
}
