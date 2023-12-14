using BepInEx;
using BepInEx.Configuration;
using HarmonyLib;
using Photon.Pun;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using TMPro;
using UnboundLib;
using UnboundLib.GameModes;
using UnboundLib.Networking;
using UnboundLib.Utils.UI;
using UnityEngine;

namespace PickReborn
{
    [BepInDependency("com.willis.rounds.unbound", BepInDependency.DependencyFlags.HardDependency)]
    [BepInPlugin(ModId, ModName, "0.1.0")]
    [BepInProcess("Rounds.exe")]
    public class PickReborn : BaseUnityPlugin
    {
        private const string ModId = "pykess.rounds.plugins.pickncardsplus";
        private const string ModName = "Pick N Cards Plus";
        private const string CompatibilityModName = "PickNCardsPlus";

        private const int maxPicks = 5;

        internal static PickReborn instance;

        public static ConfigEntry<int> StartPicksConfig;
        public static ConfigEntry<int> PicksConfig;
        public static ConfigEntry<float> DelayConfig;
        internal static bool startPick;

        internal static int startPicks;
        internal static int picks;
        internal static float delay;

        internal static bool lockPickQueue = false;
        internal static List<int> playerIDsToPick = new List<int>() { };
        internal static bool extraPicksInPorgress = false;

        private void Awake()
        {
            instance = this;

            // bind configs with BepInEx
            StartPicksConfig = Config.Bind(CompatibilityModName, "Start Picks", 1, "Total number of card picks per player at the start phase");

            PicksConfig = Config.Bind(CompatibilityModName, "Picks", 1, "Total number of card picks per player per pick phase");

            DrawReborn.DrawReborn.StartNumDrawsConfig = Config.Bind(CompatibilityModName, "Start Draws", 5, "Number of cards drawn from the deck to choose from at the start of the game");

            DrawReborn.DrawReborn.NumDrawsConfig = Config.Bind(CompatibilityModName, "Draws", 5, "Number of cards drawn from the deck to choose from");

            DelayConfig = Config.Bind(CompatibilityModName, "DelayBetweenDraws", 0.1f, "Delay (in seconds) between each card being drawn.");

            // apply patches
            new Harmony(ModId).PatchAll();
        }
        private void Start()
        {
            // call settings as to not orphan them
            startPicks = StartPicksConfig.Value;
            picks = PicksConfig.Value;
            delay = DelayConfig.Value;

            // add credits
            Unbound.RegisterCredits("Pick N Cards", new string[] { "Pykess (Code)", "Willis (Original picktwocards concept, icon)" }, new string[] { "github", "Support Pykess" }, new string[] { "https://github.com/pdcook/PickNCards", "https://ko-fi.com/pykess" });

            // add GUI to modoptions menu
            Unbound.RegisterMenu(ModName, () => { }, this.NewGUI, null, false);

            // handshake to sync settings
            Unbound.RegisterHandshake(ModId, this.OnHandShakeCompleted);

            // hooks for picking N cards
            GameModeManager.AddHook(GameModeHooks.HookGameStart, GameStart, GameModeHooks.Priority.First);
            GameModeManager.AddHook(GameModeHooks.HookPickStart, (gm) => ResetPickQueue(), GameModeHooks.Priority.First);
            GameModeManager.AddHook(GameModeHooks.HookPickEnd, ExtraPicks, GameModeHooks.Priority.First);

            // read settings to not orphan them
            DrawReborn.DrawReborn.numDraws = DrawReborn.DrawReborn.NumDrawsConfig.Value;
            DrawReborn.DrawReborn.startNumDraws = DrawReborn.DrawReborn.StartNumDrawsConfig.Value;
        }

        internal static IEnumerator GameStart(IGameModeHandler gm)
        {
            if(startPicks > 0)
            {
                startPick = true;
            }
            yield break;
        }

        private void OnHandShakeCompleted()
        {
            if(PhotonNetwork.IsMasterClient)
            {
                NetworkingManager.RPC_Others(typeof(PickReborn), nameof(SyncSettings), new object[] { startPicks, picks, DrawReborn.DrawReborn.startNumDraws, DrawReborn.DrawReborn.numDraws });
            }
        }
        [UnboundRPC]
        private static void SyncSettings(int host_startPicks, int host_picks, int host_startDraws, int host_draws)
        {
            startPicks = host_startPicks;
            picks = host_picks;
            DrawReborn.DrawReborn.startNumDraws = host_startDraws;
            DrawReborn.DrawReborn.numDraws = host_draws;
        }
        private void NewGUI(GameObject menu)
        {

            MenuHandler.CreateText(ModName + " Options", menu, out TextMeshProUGUI _, 60);
            MenuHandler.CreateText(" ", menu, out TextMeshProUGUI _, 30);

            void StartPicksChanged(float val)
            {
                StartPicksConfig.Value = Mathf.RoundToInt(Mathf.Clamp(val, 0f, maxPicks));
                startPicks = StartPicksConfig.Value;
                OnHandShakeCompleted();
            }
            MenuHandler.CreateSlider("Number of starter cards to pick", menu, 30, 0f, maxPicks, StartPicksConfig.Value, StartPicksChanged, out UnityEngine.UI.Slider _, true);
            MenuHandler.CreateText(" ", menu, out TextMeshProUGUI _, 30);

            void PicksChanged(float val)
            {
                PicksConfig.Value = Mathf.RoundToInt(Mathf.Clamp(val, 0f, maxPicks));
                picks = PicksConfig.Value;
                OnHandShakeCompleted();
            }
            MenuHandler.CreateSlider("Number of cards to pick", menu, 30, 0f, maxPicks, PicksConfig.Value, PicksChanged, out UnityEngine.UI.Slider _, true);
            MenuHandler.CreateText(" ", menu, out TextMeshProUGUI _, 30);

            MenuHandler.CreateText("Draw N Cards Options", menu, out TextMeshProUGUI _, 60);
            MenuHandler.CreateText(" ", menu, out TextMeshProUGUI _, 30);

            void StartDrawsChanged(float val)
            {
                DrawReborn.DrawReborn.StartNumDrawsConfig.Value = Mathf.RoundToInt(Mathf.Clamp(val, 1f, DrawReborn.DrawReborn.maxDraws));
                DrawReborn.DrawReborn.startNumDraws = DrawReborn.DrawReborn.StartNumDrawsConfig.Value;
                OnHandShakeCompleted();
            }

            MenuHandler.CreateSlider("Number of cards to draw at start", menu, 30, 1f, DrawReborn.DrawReborn.maxDraws, DrawReborn.DrawReborn.StartNumDrawsConfig.Value, StartDrawsChanged, out UnityEngine.UI.Slider _, true);
            MenuHandler.CreateText(" ", menu, out TextMeshProUGUI _, 30);

            void DrawsChanged(float val)
            {
                DrawReborn.DrawReborn.NumDrawsConfig.Value = Mathf.RoundToInt(Mathf.Clamp(val, 1f, DrawReborn.DrawReborn.maxDraws));
                DrawReborn.DrawReborn.numDraws = DrawReborn.DrawReborn.NumDrawsConfig.Value;
                OnHandShakeCompleted();
            }

            MenuHandler.CreateSlider("Number of cards to draw", menu, 30, 1f, DrawReborn.DrawReborn.maxDraws, DrawReborn.DrawReborn.NumDrawsConfig.Value, DrawsChanged, out UnityEngine.UI.Slider _, true);
            MenuHandler.CreateText(" ", menu, out TextMeshProUGUI _, 30);

            void DelayChanged(float val)
            {
                DelayConfig.Value = Mathf.Clamp(val, 0f, 0.5f);
                delay = DelayConfig.Value;
            }

            MenuHandler.CreateSlider("Time between each card draw", menu, 30, 0f, 0.5f, DelayConfig.Value, DelayChanged, out UnityEngine.UI.Slider _, false);
            MenuHandler.CreateText(" ", menu, out TextMeshProUGUI _, 30);
        }
        [UnboundRPC]
        public static void RPC_RequestSync(int requestingPlayer)
        {
            NetworkingManager.RPC(typeof(PickReborn), nameof(PickReborn.RPC_SyncResponse), requestingPlayer, PhotonNetwork.LocalPlayer.ActorNumber);
        }

        [UnboundRPC]
        public static void RPC_SyncResponse(int requestingPlayer, int readyPlayer)
        {
            if(PhotonNetwork.LocalPlayer.ActorNumber == requestingPlayer)
            {
                instance.RemovePendingRequest(readyPlayer, nameof(PickReborn.RPC_RequestSync));
            }
        }

        private IEnumerator WaitForSyncUp()
        {
            if(PhotonNetwork.OfflineMode)
            {
                yield break;
            }
            yield return this.SyncMethod(nameof(PickReborn.RPC_RequestSync), null, PhotonNetwork.LocalPlayer.ActorNumber);
        }
        internal static IEnumerator ResetPickQueue()
        {
            if(!extraPicksInPorgress)
            {
                playerIDsToPick = new List<int>() { };
                lockPickQueue = false;
            }
            yield break;
        }
        internal static IEnumerator ExtraPicks(IGameModeHandler gm)
        {

            if(!extraPicksInPorgress)
            {
                if((picks <= 1 && startPick == false) || (startPicks <= 1 && startPick == true) || playerIDsToPick.Count() < 1)
                {
                    yield break;
                }

                lockPickQueue = true;
                extraPicksInPorgress = true;
                yield return instance.WaitForSyncUp();

                for(int _ = 0; _ < (startPick ? startPicks : picks) - 1; _++)
                {
                    yield return instance.WaitForSyncUp();
                    //yield return GameModeManager.TriggerHook(GameModeHooks.HookPickStart);
                    for(int i = 0; i < playerIDsToPick.Count(); i++)
                    {
                        yield return instance.WaitForSyncUp();
                        int playerID = playerIDsToPick[i];
                        yield return GameModeManager.TriggerHook(GameModeHooks.HookPlayerPickStart);
                        CardChoiceVisuals.instance.Show(playerID, true);
                        yield return CardChoice.instance.DoPick(1, playerID, PickerType.Player);
                        yield return new WaitForSecondsRealtime(0.1f);
                        yield return GameModeManager.TriggerHook(GameModeHooks.HookPlayerPickEnd);
                        yield return new WaitForSecondsRealtime(0.1f);
                    }
                    //yield return GameModeManager.TriggerHook(GameModeHooks.HookPickEnd);
                }

                CardChoiceVisuals.instance.Hide();
                extraPicksInPorgress = false;
                startPick = false;
            }
            yield break;
        }
        // patch to skip pick phase if requested
        [Serializable]
        [HarmonyPatch(typeof(CardChoiceVisuals), "Show")]
        [HarmonyPriority(Priority.First)]
        class CardChoiceVisualsPatchShow
        {
            private static bool Prefix(CardChoice __instance)
            {
                if(picks == 0) { return false; }
                else { return true; }
            }
        }

        // patch to determine which players have picked this phase
        [Serializable]
        [HarmonyPatch(typeof(CardChoice), "DoPick")]
        [HarmonyPriority(Priority.First)]
        class CardChoicePatchDoPick
        {
            private static bool Prefix(CardChoice __instance)
            {
                if(picks == 0) { return false; }
                else { return true; }
            }
            private static void Postfix(CardChoice __instance, int picketIDToSet)
            {
                if(!lockPickQueue && /*checked if player is alreadly in the queue*/!playerIDsToPick.Contains(picketIDToSet)) { playerIDsToPick.Add(picketIDToSet); }
            }
        }

        // patch to change draw rate
        [HarmonyPatch]
        class CardChoicePatchReplaceCards
        {
            static Type GetNestedReplaceCardsType()
            {
                Type[] nestedTypes = typeof(CardChoice).GetNestedTypes(BindingFlags.Instance | BindingFlags.NonPublic);
                Type nestedType = null;

                foreach(Type type in nestedTypes)
                {
                    if(type.Name.Contains("ReplaceCards"))
                    {
                        nestedType = type;
                        break;
                    }
                }

                return nestedType;
            }

            static MethodBase TargetMethod()
            {
                return AccessTools.Method(GetNestedReplaceCardsType(), "MoveNext");
            }

            static float GetNewDelay()
            {
                return delay;
            }

            static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
            {
                List<CodeInstruction> codes = instructions.ToList();

                FieldInfo f_theInt = ExtensionMethods.GetFieldInfo(typeof(PublicInt), "theInt");
                MethodInfo m_GetNewDelay = ExtensionMethods.GetMethodInfo(typeof(CardChoicePatchReplaceCards), nameof(GetNewDelay));

                int index = -1;
                for(int i = 0; i < codes.Count; i++)
                {
                    if(codes[i].StoresField(f_theInt) && codes[i + 1].opcode == OpCodes.Ldarg_0 && codes[i + 2].opcode == OpCodes.Ldc_R4 && (float)(codes[i + 2].operand) == 0.1f && codes[i + 3].opcode == OpCodes.Newobj)
                    {
                        index = i;
                        break;
                    }
                }
                if(index == -1)
                {
                    throw new Exception("[REPLACECARDS PATCH] INSTRUCTION NOT FOUND");
                }
                else
                {
                    codes[index + 2] = new CodeInstruction(OpCodes.Call, m_GetNewDelay);
                }

                return codes.AsEnumerable();
            }
        }
    }
}

