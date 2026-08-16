using HarmonyLib;
using RoomIconsMod;
using SecretHistories.Abstract;
using SecretHistories.Manifestations;

[HarmonyPatch(typeof(RoomManifestation), "UpdateVisuals")]
public static class RoomManifestation_UpdateVisuals_Patch
{
    // Real signature is UpdateVisuals(IManifestable, Sphere); Harmony matches injected
    // parameters by name, so the unused Sphere can be omitted.
    private static void Postfix(RoomManifestation __instance, IManifestable manifestable)
    {
        RoomOverlayBuilder.BuildOrRefresh(__instance, manifestable);
    }
}
