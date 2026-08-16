using HarmonyLib;
using RoomIconsMod;
using SecretHistories.Infrastructure.Modding;
using UnityEngine;

/// Entry point. The class name must equal the synopsis "name" with non-alphanumerics
/// stripped ("Room Icons" -> "RoomIcons"), and must sit in the global namespace, or the
/// loader silently skips the mod.
public class RoomIcons
{
    public const string HarmonyId = "robyn.bookofhours.roomicons";

    internal static string ModRoot;

    public static void Initialise(Mod mod)
    {
        Debug.Log("[RoomIcons] Initialise: applying Harmony patches.");
        ModRoot = mod != null ? mod.ModRootFolder : string.Empty;

        new Harmony(HarmonyId).PatchAll(typeof(RoomIcons).Assembly);

        var pump = new GameObject("RoomIcons_Pump");
        Object.DontDestroyOnLoad(pump);
        pump.AddComponent<RoomIconsPump>();

        Debug.Log("[RoomIcons] Initialise: done.");
    }
}
