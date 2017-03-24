using Rust;
namespace Oxide.Plugins
{
    [Info("NoFallDamage", "redBDGR", "1.0.1")]
    [Description("Removes fall damage")]
    class NoFallDamage : RustPlugin
    {
        public const string permissionName = "nofalldamage.use";
        void Init()
        {
            permission.RegisterPermission(permissionName, this);
        }
        void OnEntityTakeDamage(BaseCombatEntity entity, HitInfo info)
        {
            if (!(info.damageTypes.GetMajorityDamageType() == DamageType.Fall)) return;
            if (permission.UserHasPermission(entity.ToPlayer()?.UserIDString, permissionName))
                info.damageTypes = new DamageTypeList();
        }
    }
}