using BepInEx;
using BepInEx.Configuration;
using HarmonyLib;
using ServerSync;
using System;
using System.Collections.Generic;
using UnityEngine;

namespace BossesUnderControl
{
    [BepInPlugin(PluginGUID, PluginGUID, Version)]

    public class BossesUnderControl : BaseUnityPlugin
    {
        public const string Version = "1.0.0";
        public const string PluginGUID = "Detalhes.BossesUnderControl";

        Harmony _harmony = new Harmony(PluginGUID);

        public static ConfigEntry<string> BossCooldown;


        private void Awake()
        {
            BossCooldown = config("Server config", "BossCooldown", "Eikthyr,1;gd_king,3;Bonemass,24;Dragon,24;GoblinKing,24;Bhygshan_DoD,2",
              "PrefabName,Hours;Prefab;Hours");
            _harmony.PatchAll();
        }

        ServerSync.ConfigSync configSync = new ServerSync.ConfigSync(PluginGUID) { DisplayName = PluginGUID, CurrentVersion = Version, MinimumRequiredVersion = Version };

        ConfigEntry<T> config<T>(string group, string name, T value, ConfigDescription description, bool synchronizedSetting = true)
        {
            ConfigEntry<T> configEntry = Config.Bind(group, name, value, description);

            SyncedConfigEntry<T> syncedConfigEntry = configSync.AddConfigEntry(configEntry);
            syncedConfigEntry.SynchronizedConfig = synchronizedSetting;

            return configEntry;
        }
        ConfigEntry<T> config<T>(string group, string name, T value, string description, bool synchronizedSetting = true) => config(group, name, value, new ConfigDescription(description), synchronizedSetting);

        [HarmonyPatch]
        class OfferingBowlPatch
        {
            [HarmonyPatch(typeof(OfferingBowl), nameof(OfferingBowl.SpawnBoss))]
            [HarmonyPrefix]
            private static bool UseItemPrefix(OfferingBowl __instance)
            {
                ZDO zd = GetZDO(__instance);

                if (zd is null)
                {
                    Debug.Log("No config for: " + __instance.m_bossPrefab.name);
                    return true;
                }

                int bossCooldown = GetBossCooldown(__instance.m_bossPrefab.name);
                if (bossCooldown == 0) return true;

                string BossLastSpawnedDateString = zd.GetString("BossLastSpawnedDate");
                if (BossLastSpawnedDateString != null && BossLastSpawnedDateString != "")
                {
                    DateTime bossLastSpawnedDate = Convert.ToDateTime(BossLastSpawnedDateString);
                    double totalHours = (DateTime.Now.ToUniversalTime() - bossLastSpawnedDate).TotalHours;
                    if (totalHours <= bossCooldown)
                    {
                        var timespan = TimeSpan.FromHours(bossCooldown - totalHours);
                        Player.m_localPlayer.Message(MessageHud.MessageType.Center, "This boss is in cooldown for more " + timespan.Hours + " hours and " + timespan.Minutes + " minutes");
                        return false;
                    }
                }   

                return true;
            }

            private static ZDO GetZDO(OfferingBowl __instance)
            {
                List<ZDO> zdos = new();
                Vector2i zone = ZoneSystem.instance.GetZone(__instance.transform.position);
                ZDOMan.instance.FindObjects(zone, zdos);
                return zdos.Find(x => Vector3.Distance(x.m_position, __instance.transform.position) < 2);
            }

            private static int GetBossCooldown(string bossName)
            {
                string[] array = BossCooldown.Value.Split(';');
                foreach(string str in array)
                {
                    string[] bossValues = str.Split(',');
                    if (bossValues[0] == bossName) return Convert.ToInt32(bossValues[1]);
                }

                return 0;
            }

            [HarmonyPatch(typeof(OfferingBowl), nameof(OfferingBowl.DelayedSpawnBoss))]
            [HarmonyPostfix]
            private static void DelayedSpawnBossPostfix(OfferingBowl __instance)
            {
                ZDO zd = GetZDO(__instance);
                zd.Set("BossLastSpawnedDate", DateTime.Now.ToUniversalTime().ToString());
            }
        }
    }
}
