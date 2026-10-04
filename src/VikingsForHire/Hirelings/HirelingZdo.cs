namespace VikingsForHire.Hirelings
{
    /// <summary>Hireling ZDO keys. Persisted in worlds and snapshots: never rename.</summary>
    internal static class HirelingZdo
    {
        public const string PrefabName = "VFH_Hireling";
        public static readonly int PrefabHash = PrefabName.GetStableHashCode();

        public const string Hid = "vfh_hid";
        public const string BoardId = "vfh_board_id";
        public const string Job = "vfh_job";
        public const string Level = "vfh_level";
        public const string Stance = "vfh_stance";
        public const string Mode = "vfh_mode";
        public const string FollowMode = "vfh_follow_mode";
        public const string Owner = "vfh_owner";
        public const string OwnerName = "vfh_owner_name";
        public const string StayPos = "vfh_stay_pos";
        public const string Radius = "vfh_radius";
        public const string LeavingSince = "vfh_leaving_since";
        public const string Order = "vfh_order";
        public const string DeliverPending = "vfh_deliver_pending";
        public const string Name = "vfh_name";
        public const string Model = "vfh_model";
        public const string Hair = "vfh_hair";
        public const string Beard = "vfh_beard";
        public const string Skin = "vfh_skin";
        public const string HairColor = "vfh_hair_color";
        public const string Home = "vfh_home";
        public const string Status = "vfh_status";
        /// <summary>What the hireling is doing right now (chopping, delivering…), shown under the status on hover.</summary>
        public const string Activity = "vfh_activity";

        /// <summary>Set once the first-spawn setup (full health) has run, so later loads keep the saved health.</summary>
        public const string Initialized = "vfh_initialized";
    }
}
