// Crowd Placer: spawns a list of minis from a JSON file, relative to an anchor mini (written Oct 2026).
// GM mode, in a board: select the anchor mini (stood on the slab's marker), press the hotkey (default Ctrl+Shift+K).
// File format (see examples/; any script can write one):
//   { "marker": {"x":..,"y":..,"z":..},                       slab coords the anchor mini stands on
//     "blueprints": {"key": "talespire://creature-blueprint/..."},
//     "creatures": [ {"bp":"key","x":..,"y":..,"z":..,"rot":deg,"name":"...","hidden":false}, ... ] }
// rot: 0 faces +z, but creatures turn the OPPOSITE way to slab props (verified in game 3 Nov 2026), so generators
// should write the negated slab-rule angle.
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using BepInEx;
using BepInEx.Configuration;
using Bounce.Mathematics;
using Bounce.Unmanaged;
using ModP.B64;
using Newtonsoft.Json;
using Unity.Collections;
using Unity.Mathematics;
using UnityEngine;

[BepInPlugin("zak.talespire.crowdplacer", "Crowd Placer", "0.3.0")]
public class CrowdPlacer : BaseUnityPlugin
{
    class Vec { public float x, y, z; }
    class Entry { public string bp; public float x, y, z, rot; public string name; public bool hidden; }
    class CrowdFile { public Vec marker; public Dictionary<string, string> blueprints; public List<Entry> creatures; }

    ConfigEntry<string> _file;
    ConfigEntry<KeyboardShortcut> _key;
    bool _busy;

    void Awake()
    {
        _file = Config.Bind("General", "CrowdFile", Path.Combine(Paths.ConfigPath, "crowd.json"), "JSON crowd list to spawn (default: BepInEx/config/crowd.json)");
        _key = Config.Bind("General", "Hotkey", new KeyboardShortcut(KeyCode.K, KeyCode.LeftControl, KeyCode.LeftShift), "Spawn the crowd around the selected mini");
        Logger.LogInfo("Crowd Placer loaded; crowd file " + _file.Value);
    }

    void Update()
    {
        if (_busy || !_key.Value.IsDown()) return;
        if (!BoardSessionManager.InBoard || !LocalClient.IsInGmMode) { Say("Crowd Placer: be in a board, in GM mode"); return; }
        if (!CreatureManager.TryGetCreatureData(LocalClient.SelectedCreatureId, out var anchor)) { Say("Crowd Placer: select the anchor mini first"); return; }
        CrowdFile crowd;
        try { crowd = JsonConvert.DeserializeObject<CrowdFile>(File.ReadAllText(_file.Value)); }
        catch (Exception e) { Say("Crowd Placer: can't read " + Path.GetFileName(_file.Value)); Logger.LogError(e); return; }
        StartCoroutine(Spawn(crowd, anchor.Position));
    }

    IEnumerator Spawn(CrowdFile crowd, float3 anchor)
    {
        _busy = true;
        var data = new Dictionary<string, CreatureDataV4>();
        foreach (var kv in crowd.blueprints)
        {
            if (TryDecode(kv.Value, out var d)) data[kv.Key] = d;
            else Logger.LogError("Bad blueprint " + kv.Key);
        }
        var m = crowd.marker ?? new Vec();
        int ok = 0, failed = 0;
        foreach (var c in crowd.creatures)
        {
            if (!data.TryGetValue(c.bp, out var d)) { failed++; continue; }
            if (!string.IsNullOrEmpty(c.name)) d.Alias = c.name;
            d.CreatureId = default;                                   // fresh id per copy
            // A copied blueprint keeps the source mini's own Rotation, and the game uses it over the rot argument
            // (7 Nov 2026: every Cogskill spectator and all 12 test minis faced west), so overwrite it here.
            d.Rotation = bam3.FromEulerDegrees(new float3(0f, c.rot, 0f));
            var pos = anchor + new float3(c.x - m.x, c.y - m.y, c.z - m.z);
            var rot = quaternion.EulerXYZ(0f, math.radians(c.rot), 0f);
            var r = CreatureManager.CreateAndAddNewCreatureResult.MorphDataNotReady;
            for (int tries = 0; tries < 300; tries++)                // the model may still be loading the first time
            {
                r = CreatureManager.TryCreateAndAddNewCreature(d, pos, rot, false, c.hidden, !string.IsNullOrEmpty(c.name));
                if (r != CreatureManager.CreateAndAddNewCreatureResult.MorphDataNotReady) break;
                yield return null;
            }
            if (r == CreatureManager.CreateAndAddNewCreatureResult.Success) ok++;
            else { failed++; Logger.LogWarning($"{c.name ?? c.bp} at {pos}: {r}"); }
            if (ok % 5 == 0) yield return null;                       // spread the work over frames
        }
        Say($"Crowd Placer: placed {ok}" + (failed > 0 ? $", {failed} failed (see BepInEx log)" : ""));
        _busy = false;
    }

    // Mirrors CreatureManager.TryProcessCreatureBlueprintString (blueprint versions 2 and 3), minus the paste tool.
    static bool TryDecode(string url, out CreatureDataV4 data)
    {
        data = default;
        const string prefix = "talespire://creature-blueprint/";   // data may itself contain '/', so never cut at the last one
        var b64 = (url.StartsWith(prefix) ? url.Substring(prefix.Length) : url).Replace('_', '/');
        using var bytes = new NativeList<byte>(1024, Allocator.Temp);
        if (!Base64.TryDecodeInto(b64, bytes)) return false;
        var reader = new BrSerialize.Reader();
        reader.Reset(bytes, 0);
        reader.Read<ushort>(out var version);
        if (version == 2) data = CreatureBlueprintDataV1.Deserialize(reader).ToCreatureDataV4InterningPackUris();
        else if (version == 3) data = CreatureBlueprintDataV2.Deserialize(reader).ToCreatureDataV4InterningPackUris();
        else return false;
        return true;
    }

    static void Say(string text) => SystemMessage.DisplayInfoText(text, 4f);
}
