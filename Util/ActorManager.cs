using Dalamud.Plugin.Services;
using FFXIVClientStructs.FFXIV.Client.Game.Character;
using FFXIVClientStructs.FFXIV.Client.Game.Object;
using FFXIVClientStructs.Interop;
using Lumina.Excel.Sheets;

namespace NorthStar.Util;

internal class ActorManager : IDisposable
{
    private Plugin Plugin { get; }
    private readonly Stack<uint> _idx = [];
    private readonly Queue<BaseActorAction> _tasks = [];

    internal ActorManager(Plugin plugin)
    {
        Plugin = plugin;
        Plugin.Framework.Update += OnFramework;
        Plugin.ClientState.TerritoryChanged += OnTerritoryChange;
        Plugin.Ui.Viewer.View += OnView;
    }

    public void Dispose()
    {
        Plugin.Ui.Viewer.View -= OnView;
        Plugin.ClientState.TerritoryChanged -= OnTerritoryChange;
        Plugin.Framework.Update -= OnFramework;

        if (_idx.Count > 0)
        {
            unsafe
            {
                var objMan = ClientObjectManager.Instance();
                new DeleteAction().Run(this, objMan);
            }
        }
    }

    private unsafe void OnFramework(IFramework framework)
    {
        if (!_tasks.TryPeek(out var actorAction))
        {
            return;
        }

        var objMan = ClientObjectManager.Instance();
        var success = false;

        if (actorAction.Tries < 10)
        {
            try
            {
                actorAction.Tries += 1;
                success = actorAction.Run(this, objMan);
            }
            catch (Exception ex)
            {
                Plugin.Log.Error(ex, "Error in actor action queue");
            }
        }
        else
        {
            Plugin.Log.Warning("too many retries, skipping");
            success = true;
        }

        if (success)
        {
            _tasks.Dequeue();
        }
    }

    private void OnTerritoryChange(ushort obj)
    {
        _idx.Clear();
    }

    private void OnView(Message? message)
    {
        var msg = message == null ? "null" : "not null";
        Plugin.Log.Debug($"OnView message is {msg}");
        Despawn();

        if (Plugin.Config.ShowEmotes && message?.Emote != null)
        {
            Spawn(message);
        }
    }

    internal void Spawn(Message message)
    {
        _tasks.Enqueue(new SpawnAction(message));
    }

    internal void Despawn()
    {
        _tasks.Enqueue(new DeleteAction());
    }

    private abstract unsafe class BaseActorAction
    {
        /// <summary>
        /// Run this action.
        /// </summary>
        /// <returns>true if the action is finished, false if it should be run again</returns>
        public abstract bool Run(ActorManager manager, ClientObjectManager* objMan);

        public int Tries { get; set; }

        protected IEnumerable<Pointer<BattleChara>> GetBattleCharas(
            ActorManager manager,
            Pointer<ClientObjectManager> objMan
        )
        {
            foreach (var idx in manager._idx)
            {
                var ptr = GetChara(objMan, idx);
                if (ptr == null)
                {
                    continue;
                }

                yield return ptr.Value;
            }
        }

        private static Pointer<BattleChara>? GetChara(Pointer<ClientObjectManager> objMan, uint idx)
        {
            var obj = (BattleChara*)objMan.Value->GetObjectByIndex((ushort)idx);
            return obj == null ? null : obj;
        }
    }

    private unsafe class SpawnAction(Message message) : BaseActorAction
    {
        public override bool Run(ActorManager manager, ClientObjectManager* objMan)
        {
            if (message.Emote == null)
            {
                Plugin.Log.Warning("refusing to spawn an actor for a message without an emote");
                return true;
            }

            var idx = objMan->CreateBattleCharacter();
            if (idx == 0xFFFFFFFF)
            {
                Plugin.Log.Debug("actor could not be spawned");
                return true;
            }

            manager._idx.Push(idx);
            var emote = message.Emote;
            var emoteRow = manager.GetValidEmote(emote.Id);

            var chara = (BattleChara*)objMan->GetObjectByIndex((ushort)idx);

            chara->ObjectKind = ObjectKind.BattleNpc;
            chara->TargetableStatus = 0;
            chara->Position = message.Position;
            chara->Rotation = message.Yaw;
            var drawData = &chara->DrawData;

            var maxLen = Math.Min(sizeof(CustomizeData), emote.Customise.Count);
            var rawCustomise = (byte*)&drawData->CustomizeData;
            for (var i = 0; i < maxLen; i++)
            {
                rawCustomise[i] = emote.Customise[i];
            }

            // check if data is valid to prevent crashes
            if (!(&drawData->CustomizeData)->NormalizeCustomizeData(&drawData->CustomizeData))
            {
                drawData->CustomizeData = new CustomizeData();
            }

            // weapon and equipment values don't cause crashes, just transparent body parts
            for (var i = 0; i < Math.Min(drawData->EquipmentModelIds.Length, emote.Equipment.Length); i++)
            {
                var equip = emote.Equipment[i];
                drawData->Equipment((DrawDataContainer.EquipmentSlot)i) = new EquipmentModelId
                {
                    Id = equip.Id,
                    Variant = equip.Variant,
                    Stain0 = equip.Stain0,
                    Stain1 = equip.Stain1,
                };
            }

            if (emoteRow is { DrawsWeapon: true })
            {
                for (var i = 0; i < Math.Min(drawData->WeaponData.Length, emote.Weapon.Length); i++)
                {
                    var weapon = emote.Weapon[i];
                    drawData->Weapon((DrawDataContainer.WeaponSlot)i).ModelId = new FFXIVClientStructs.FFXIV.Client.Game.Character.WeaponModelId
                    {
                        Id = weapon.ModelId.Id,
                        Type = weapon.ModelId.Kind,
                        Variant = weapon.ModelId.Variant,
                        Stain0 = weapon.ModelId.Stain0,
                        Stain1 = weapon.ModelId.Stain1,
                    };
                    drawData->Weapon((DrawDataContainer.WeaponSlot)i).Flags1 = weapon.Flags1;
                    drawData->Weapon((DrawDataContainer.WeaponSlot)i).Flags2 = weapon.Flags2;
                    drawData->Weapon((DrawDataContainer.WeaponSlot)i).State = weapon.State;
                }
            }

            drawData->IsHatHidden = emote.HatHidden;
            drawData->IsVisorToggled = emote.VisorToggled;
            drawData->IsWeaponHidden = emote.WeaponHidden;

            drawData->SetGlasses(0, (ushort)emote.Glasses);

            chara->Alpha = Math.Clamp(manager.Plugin.Config.EmoteAlpha / 100, 0, 1);

            manager._tasks.Enqueue(new EnableAction(emoteRow?.ActionTimeline[0].Value));
            return true;
        }
    }

    private Emote? GetValidEmote(uint rowId)
    {
        var emote = Plugin.DataManager.GetExcelSheet<Emote>().GetRowOrDefault(rowId);
        if (emote == null)
        {
            return null;
        }

        return emote.Value.TextCommand.RowId == 0 ? null : emote;
    }

    private unsafe class EnableAction(ActionTimeline? action) : BaseActorAction
    {
        public override bool Run(ActorManager manager, ClientObjectManager* objMan)
        {
            var allReady = true;
            foreach (var chara in GetBattleCharas(manager, objMan))
            {
                if (!chara.Value->IsReadyToDraw())
                {
                    allReady = false;
                    continue;
                }

                chara.Value->EnableDraw();

                if (action == null)
                {
                    continue;
                }

                chara.Value->SetMode(CharacterModes.AnimLock, 0);
                if (action.Value.Slot == 0)
                {
                    chara.Value->Timeline.TimelineSequencer.PlayTimeline((ushort)action.Value.RowId);
                }
                else
                {
                    chara.Value->Timeline.BaseOverride = (ushort)action.Value.RowId;
                }
            }

            return allReady;
        }
    }

    private unsafe class DeleteAction : BaseActorAction
    {
        public override bool Run(ActorManager manager, ClientObjectManager* objMan)
        {
            foreach (var wrapper in GetBattleCharas(manager, objMan))
            {
                wrapper.Value->DisableDraw();
                var idx = objMan->GetIndexByObject((GameObject*)wrapper.Value);
                objMan->DeleteObjectByIndex((ushort)idx, 0);
            }

            manager._idx.Clear();
            return true;
        }
    }
}