using HarmonyLib;
using PhantomBrigade;
using PhantomBrigade.Data;
using PhantomBrigade.Mods;
using PhantomBrigade.Overworld;
using System.Reflection;
using UnityEngine;

// All mods must use a unique namespace in case they contain colliding type names, like one below
namespace NewGamePlus
{
    // All mods using libraries must include one class inheriting from ModLink
    public class NewGamePlusLink: ModLink
    {
        public override void OnLoad(Harmony harmonyInstance)
        {
            // Note that you have access to metadata, which includes directory name and full path to this loaded mod.
            // You can also access ModManager.loadedModsLookup to find other loaded mods and interact with them (e.g. if you're relying on another mod)
            Assembly executingAssembly = Assembly.GetExecutingAssembly();
            Debug.Log("Mod " + metadata.id + " is executing OnLoad | Using HarmonyInstance.PatchAll on assembly (" + executingAssembly.FullName + ") | Directory: " + metadata.directory + " | Full path: " + metadata.path);
            harmonyInstance.PatchAll(executingAssembly);
        }
    }

    // Container class for patches - useful if you prefer to keep patches organized under one umbrella type
    public class Patches
    {
        [HarmonyPatch(typeof(PhantomBrigade.Data.ScenarioFunctions))]
        [HarmonyPatch("OnEndCapital")]
        public class Patch
        {
            public static void Postfix()
            {
                Debug.Log("Mod executes this as suffix to ScenarioFunctions.OnEndCapital()");

                int levelBoost = 20;
                StartNewGamePlus(levelBoost);
            }

            public static void StartNewGamePlus(int levelBoost)
            {
                //PersistentEntity linkedPersistentEntity = IDUtility.GetLinkedPersistentEntity(target);
                PersistentEntity playerBasePersistent = IDUtility.playerBasePersistent;
                //if (playerBasePersistent == null || linkedPersistentEntity == null)
                //{
                //    Debug.LogError("StartReset | Event function failed due to missing base (" + UtilityString.ToStringNullCheck<PersistentEntity>(playerBasePersistent) + ") or target (" + UtilityString.ToStringNullCheck<PersistentEntity>(linkedPersistentEntity) + ")");
                //    return;
                //}

                //GameContext game = Contexts.sharedInstance.game;
                //if (!game.hasGameControllerStateCurrent || game.gameControllerStateCurrent.s != "overworld")
                //{
                //    Debug.LogWarning("Failed to restart: the current state is not overworld");
                //    return;
                //}

                OverworldEntity baseOverworld = IDUtility.playerBaseOverworld;
                string startingProvinceName = DataShortcuts.overworld.startingProvinceName;
                OverworldEntity overworldEntity = IDUtility.GetOverworldEntity(startingProvinceName);
                DataContainerOverworldProvinceBlueprint dataContainerOverworldProvinceBlueprint = overworldEntity?.dataLinkOverworldProvince.data;
                if (baseOverworld == null || playerBasePersistent == null || overworldEntity == null || dataContainerOverworldProvinceBlueprint == null)
                {
                    Debug.LogWarning("Failed to restart: one of dependencies is null | Name: " + startingProvinceName);
                    return;
                }

                if (dataContainerOverworldProvinceBlueprint.spawns == null || dataContainerOverworldProvinceBlueprint.spawns.Count == 0)
                {
                    Debug.LogWarning("Failed to restart: starting province " + startingProvinceName + " has no points");
                    return;
                }

                if (!dataContainerOverworldProvinceBlueprint.spawns.ContainsKey("base_return"))
                {
                    Debug.LogWarning("Failed to restart: starting province " + startingProvinceName + " has no point group base_return");
                }

                DataBlockOverworldProvincePoint dataBlockOverworldProvincePoint = dataContainerOverworldProvinceBlueprint.spawns["base_return"][0];
                Vector3 baseReturnPos = dataBlockOverworldProvincePoint.position;
                
                Debug.LogWarning("Successfully initiated reset to province " + startingProvinceName);
                
                OverworldUtility.StopMovement(baseOverworld);
                baseOverworld.isDeployed = false;
                PostprocessingHelper.SetBlackoutTarget(1f, instant: false);
                AudioUtility.CreateAudioSyncUpdate("gameover", 1f);

                // Tell the game the world needs to be fully rebuilt
                PersistentContext persistent = Contexts.sharedInstance.persistent;
                persistent.isWorldGenerationRequired = true;

                // Delay subsequent code a couple of frames to ensure all reactive systems run correctly, world is fully regenerated etc.
                Co.Delay(3, delegate
                {
                    ResetBasePosition(baseOverworld, baseReturnPos);
                    IncrementCombatUnitLevel(levelBoost);
                });
            }

            public static void ResetBasePosition(OverworldEntity baseOverworld, Vector3 baseSpawnPointPos)
            {
                PostprocessingHelper.SetBlackoutTarget(0f, instant: false);

                // Ensure the base has all its movement cancelled
                OverworldUtility.StopMovement(baseOverworld);

                // Move the base to one of spawn points in starting province
                baseOverworld.ReplacePosition(baseSpawnPointPos);

                // Make the base check its vertical position in case center of the province is under terrain
                baseOverworld.isPositionUnchecked = true;

                OverworldContext overworld = Contexts.sharedInstance.overworld;
                // overworld.ReplaceSimulationLockCountdown(value);
                // overworld.ReplaceSimulationTimeScale(50f);
                overworld.ReplaceCameraFocusRequest(IDUtility.playerBaseOverworld.position.v);
                // overworld.ReplaceSelectedEntity(IDUtility.playerBaseOverworld.id.id);
                Debug.LogWarning("Initiated reset base position");
            }

            public static void IncrementCombatUnitLevel(int levelBoost)
            {
                // Get all entities in overworld context
                OverworldContext overworld = Contexts.sharedInstance.overworld;
                var entities = overworld.GetEntities();

                foreach (var entityOverworld in entities)
                {
                    // Skip all entities that aren't actually sites on the map
                    if (!entityOverworld.hasDataKeyOverworldEntityBlueprint)
                        continue;

                    // Skip entities that don't have a persistent context counterpart or don't have a garrison
                    var entityPersistent = IDUtility.GetLinkedPersistentEntity(entityOverworld);
                    if (entityPersistent == null || !entityPersistent.hasCombatUnits || !entityPersistent.hasCombatUnitLevel)
                        continue;

                    var levelCurrent = entityPersistent.combatUnitLevel.i;
                    entityPersistent.ReplaceCombatUnitLevel(levelCurrent + levelBoost);
                }
            }
        }
    }
}
