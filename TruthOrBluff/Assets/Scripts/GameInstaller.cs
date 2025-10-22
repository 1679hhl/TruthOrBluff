using Zenject;
using LiarsBar;

public class GameInstaller : MonoInstaller
{
    public override void InstallBindings()
    {
        // 绑定 IGameConfig 到 GameConfig
        Container.Bind<IGameConfig>().To<GameConfig>().AsSingle();

        // 如果需要，可以绑定其他依赖项
        // Container.Bind<OtherDependency>().AsSingle();
    }
}
