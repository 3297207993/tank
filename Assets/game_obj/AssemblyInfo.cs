using System.Runtime.CompilerServices;

// 测试程序集需要访问 internal 的 AiNavigation，以及 MapGenerator 里测试用的地图注入方法。
[assembly: InternalsVisibleTo("TankGame.Tests.EditMode")]
[assembly: InternalsVisibleTo("TankGame.Tests.PlayMode")]
