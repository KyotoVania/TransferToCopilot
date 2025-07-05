namespace ScriptableObjects
{
    using UnityEngine;

    [CreateAssetMenu(fileName = "NewZapBannerTargetEffect", menuName = "ScriptableObjects/SpellEffects/ZapBannerTarget", order = 1)]
    public class ZapBannerTargetEffect_SO : BaseSpellEffect_SO
    {
        [Header("Effect Configuration")]
        [Tooltip("The visual effect to spawn at the target")]
        public GameObject zapVFX;
        
        [Tooltip("Vertical offset for the VFX spawn position relative to the building")]
        public float vfxYOffset = 1f;
        
        [Tooltip("Amount of damage to deal to the target building")]
        public int damageAmount = 25;
        
        [Tooltip("Sound to play when the lightning strikes")]
        public AK.Wwise.Event zapSound;
        
        [Tooltip("Sound to play if there is no valid target")]
        public AK.Wwise.Event failSound;

        public override void ExecuteEffect(GameObject caster, int perfectCount)
        {
            // Check if we have a banner controller and it has a target
            if (!BannerController.Exists || !BannerController.Instance.HasActiveBanner || BannerController.Instance.CurrentBuilding == null)
            {
                Debug.LogWarning("[ZapBannerTargetEffect] No valid banner target found!");
                
                // Play fail sound if available
                if (failSound != null && failSound.IsValid() && caster != null)
                {
                    failSound.Post(caster);
                }
                
                return; // Early exit - no target
            }

            // Get the target building
            Building targetBuilding = BannerController.Instance.CurrentBuilding;
            
            // Calculate bonus damage from perfect timing
            int totalDamage = damageAmount;
            if (perfectCount > 0)
            {
                // 10% bonus damage per perfect hit in the sequence
                totalDamage += Mathf.FloorToInt(damageAmount * 0.1f * perfectCount);
                Debug.Log($"[ZapBannerTargetEffect] Perfect sequence bonus! Damage increased from {damageAmount} to {totalDamage}");
            }
            
            // Spawn VFX at target position
            if (zapVFX != null)
            {
                // Spawn effect at the building's position with configurable offset
                Vector3 spawnPosition = targetBuilding.transform.position + new Vector3(0, vfxYOffset, 0);
                GameObject vfxInstance = Object.Instantiate(zapVFX, spawnPosition, Quaternion.identity);
                
                // Destroy VFX after 3 seconds
                Object.Destroy(vfxInstance, 3f);
            }
            
            // Play zap sound at target location
            if (zapSound != null && zapSound.IsValid())
            {
                if (caster != null)
                {
                    zapSound.Post(caster);
                }
            }
            
            // Deal damage to the target
            targetBuilding.TakeDamage(totalDamage);
            
            Debug.Log($"[ZapBannerTargetEffect] Lightning strike dealt {totalDamage} damage to {targetBuilding.name}");
        }
        
        public override string GetEffectDescription()
        {
            return $"Lance un éclair sur le bâtiment actuellement ciblé par la bannière, infligeant {damageAmount} points de dégâts. Les frappes parfaites augmentent les dégâts.";
        }
    }
}
