namespace VikingsForHire.Board
{
    /// <summary>Hiring board ZDO keys. Names are persisted in worlds: never rename.</summary>
    internal static class BoardZdo
    {
        public const string PrefabName = "VFH_HiringBoard";
        public static readonly int PrefabHash = PrefabName.GetStableHashCode();

        public const string Id = "vfh_board_id";
        public const string Level = "vfh_board_level";
        public const string Roster = "vfh_roster";
        /// <summary>The board's production orders (Core.Orders.OrderList, serialized).</summary>
        public const string Orders = "vfh_orders";
        public const string LastUpkeepDay = "vfh_last_upkeep_day";

        /// <summary>Set on objects spawned by test fixtures so clear_area can remove exactly those.</summary>
        public const string Fixture = "vfh_fixture";

        public static string GetId(ZDO zdo) => zdo.GetString(Id);
        public static int GetLevel(ZDO zdo) => System.Math.Max(1, zdo.GetInt(Level, 1));
    }
}
