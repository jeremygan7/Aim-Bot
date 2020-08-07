using ExileCore;
using ExileCore.PoEMemory.Components;
using ExileCore.PoEMemory.MemoryObjects;

namespace AimBot.Utilities
{
    public class Player
    {
        public static Entity entity_;
        public static long          Experience => entity_.GetComponent<ExileCore.PoEMemory.Components.Player>().XP;
        public static string        Name       => entity_.GetComponent<ExileCore.PoEMemory.Components.Player>().PlayerName;
        public static float         X          => entity_.GetComponent<Render>().X;
        public static float         Y          => entity_.GetComponent<Render>().Y;
        public static int           Level      => entity_.GetComponent<ExileCore.PoEMemory.Components.Player>().Level;
        public static Life          Health     => entity_.GetComponent<Life>();
        public static AreaInstance  Area       => BasePlugin.API.GameController.Area.CurrentArea;
        public static uint           AreaHash   => BasePlugin.API.GameController.Game.IngameState.Data.CurrentAreaHash;

        public static bool HasBuff(string buffName) => entity_.GetComponent<Life>().HasBuff(buffName);
    }
}