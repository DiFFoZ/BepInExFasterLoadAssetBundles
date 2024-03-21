﻿using System.Collections.Generic;
using BepInEx.Logging;
using HarmonyLib;
using Mono.Cecil;

namespace BepInExFasterLoadAssetBundles;

public class BepInExFasterLoadAssetBundlesPatcher
{
    internal static Harmony Harmony { get; } = new(nameof(BepInExFasterLoadAssetBundlesPatcher));
    internal static ManualLogSource Logger { get; } = new(nameof(BepInExFasterLoadAssetBundlesPatcher));

    // Cannot be renamed, method name is important
    public static void Finish()
    {
        Harmony.PatchAll(typeof(BepInExFasterLoadAssetBundlesPatcher).Assembly);
    }

    // cannot be removed, BepInEx checks it
    public static IEnumerable<string> TargetDLLs { get; } = [];

    // cannot be removed, BepInEx checks it
    // https://github.com/BepInEx/BepInEx/blob/v5-lts/BepInEx.Preloader/Patching/AssemblyPatcher.cs#L67
    public static void Patch(AssemblyDefinition _)
    {
    }
}
