using System.Numerics;
using System.Text;
using Dalamud.Bindings.ImGui;
using Dalamud.Game.ClientState.Conditions;
using Dalamud.Game.ClientState.Objects.SubKinds;
using Dalamud.Interface.Textures;
using Dalamud.Interface.Utility;
using Dalamud.Utility;
using FFXIVClientStructs.FFXIV.Client.Game.Character;
using FFXIVClientStructs.FFXIV.Client.Game.Object;
using FFXIVClientStructs.FFXIV.Client.Game.UI;
using Lumina.Excel.Sheets;
using Newtonsoft.Json;
using OrangeGuidanceTomestone.Helpers;
using OrangeGuidanceTomestone.Util;

namespace OrangeGuidanceTomestone.Ui.MainWindowTabs;

internal class Write : ITab {
    public string Name => "Write";

    private Plugin Plugin { get; }

    private int _pack;
    private int _part1 = -1;
    private (int, int) _word1 = (-1, -1);
    private int _conj = -1;
    private int _part2 = -1;
    private (int, int) _word2 = (-1, -1);
    private int _glyph;
    private int _emoteIdx = -1;

    private string _word1Search = string.Empty;
    private string _word2Search = string.Empty;
    private string _emoteSearch = string.Empty;

    private const string Placeholder = "****";
    private Pack? Pack => Pack.All.Get(this._pack);
    private Template? Template1 => this.Pack?.Templates.Get(this._part1);
    private Template? Template2 => this.Pack?.Templates.Get(this._part2);
    private string? Word1 => this.GetWord(this._word1, this.Template1);
    private string? Word2 => this.GetWord(this._word2, this.Template2);
    private string? Conjunction => this.Pack?.Conjunctions?.Get(this._conj);

    private List<Emote> Emotes { get; }

    private string? GetWord((int, int) word, Template? template) {
        if (word.Item2 == -1) {
            return Placeholder;
        }

        if (template == null) {
            return null;
        }

        if (template.Words == null) {
            if (word.Item1 == -1) {
                return Placeholder;
            }

            var pack = this.Pack;
            if (pack?.Words == null) {
                return null;
            }

            return pack.Words.Get(word.Item1)?.Words.Get(word.Item2);
        }

        return template.Words.Get(word.Item2);
    }

    internal Write(Plugin plugin) {
        this.Plugin = plugin;

        this.Emotes = [
            .. this.Plugin.DataManager.GetExcelSheet<Emote>()
                .Skip(1)
                .Where(emote => emote.TextCommand.RowId != 0)
                .OrderBy(emote => emote.Order),
        ];

        this._glyph = this.Plugin.Config.DefaultGlyph;
        Pack.UpdatePacks();
    }

    public void Dispose() {
    }

    private ISharedImmediateTexture GetGlyphImage(int i) {
        return this.Plugin.TextureProvider.GetFromManifestResource(
            typeof(Plugin).Assembly,
            $"OrangeGuidanceTomestone.img.sign_{i}.jpg"
        );
    }

    public void Draw() {
        Pack.AllMutex.Wait();

        try {
            this.DrawInner();
        } finally {
            Pack.AllMutex.Release();
        }
    }

    private void DrawInner() {
        if (Pack.All.Length == 0) {
            ImGui.TextUnformatted("Please refresh the packs from the settings.");
            return;
        }

        if (this._pack < 0 || this._pack >= Pack.All.Length) {
            this._pack = 0;
        }

        var packPrev = Pack.All[this._pack].Name;
        if (ImGui.BeginCombo("Pack", packPrev)) {
            using var endCombo = new OnDispose(ImGui.EndCombo);

            for (var i = 0; i < Pack.All.Length; i++) {
                var selPack = Pack.All[i];
                if (!ImGui.Selectable(selPack.Name)) {
                    continue;
                }

                this._pack = i;
                this.ResetWriter();
            }
        }

        const string placeholder = "****";

        bool DrawPicker(string id, IReadOnlyList<string> items, ref int x) {
            var preview = x == -1 ? "" : items[x].Replace("{0}", placeholder);
            if (!ImGui.BeginCombo(id, preview)) {
                return false;
            }

            using var endCombo = new OnDispose(ImGui.EndCombo);

            var changed = false;
            if (ImGui.Selectable("<none>")) {
                x = -1;
                changed = true;
            }

            for (var i = 0; i < items.Count; i++) {
                var template = items[i].Replace("{0}", placeholder);
                if (!ImGui.Selectable(template, i == x)) {
                    continue;
                }

                x = i;
                changed = true;
            }

            return changed;
        }

        void DrawTemplatePicker(string id, IReadOnlyList<string> items, ref int x, ref (int, int) word) {
            var wasAdvanced = this.Pack?.Templates.Get(x)?.Words != null;

            var changed = DrawPicker(id, items, ref x);
            if (changed && wasAdvanced) {
                word = (-1, -1);
            }
        }

        void DrawSpecificWordPicker(string id, Template template, ref (int, int) x, ref string search) {
            if (template.Words == null) {
                return;
            }

            ImGui.SetNextWindowSizeConstraints(new Vector2(100 * ImGuiHelpers.GlobalScale, 0), new Vector2(float.MaxValue));
            var preview = x == (-1, -1) ? "" : template.Words[x.Item2];
            if (!ImGui.BeginCombo(id, preview)) {
                return;
            }

            using var endCombo = new OnDispose(ImGui.EndCombo);

            if (ImGui.IsWindowAppearing()) {
                ImGui.SetKeyboardFocusHere();
            }

            ImGui.SetNextItemWidth(-1);
            ImGui.InputTextWithHint("###word-search", "Search...", ref search, 100, ImGuiInputTextFlags.AutoSelectAll);

            for (var wordIdx = 0; wordIdx < template.Words.Length; wordIdx++) {
                var word = template.Words[wordIdx];
                if (!string.IsNullOrEmpty(search) && !word.Contains(search, StringComparison.InvariantCultureIgnoreCase)) {
                    continue;
                }

                if (ImGui.Selectable(word, x == (-1, wordIdx))) {
                    x = (-1, wordIdx);
                }
            }
        }

        void DrawWordPicker(string id, IReadOnlyList<WordList> words, ref (int, int) x, ref string search) {
            var preview = x == (-1, -1) ? "" : words[x.Item1].Words[x.Item2];
            if (!ImGui.BeginCombo(id, preview)) {
                return;
            }

            using var endCombo = new OnDispose(ImGui.EndCombo);

            for (var listIdx = 0; listIdx < words.Count; listIdx++) {
                var list = words[listIdx];
                ImGui.SetNextWindowSizeConstraints(new Vector2(100 * ImGuiHelpers.GlobalScale, 0), new Vector2(float.MaxValue));
                if (!ImGui.BeginMenu(list.Name)) {
                    continue;
                }

                using var endMenu = new OnDispose(ImGui.EndMenu);

                if (ImGui.IsWindowAppearing()) {
                    ImGui.SetKeyboardFocusHere();
                }

                ImGui.SetNextItemWidth(-1);
                ImGui.InputTextWithHint("###word-search", "Search...", ref search, 100, ImGuiInputTextFlags.AutoSelectAll);

                for (var wordIdx = 0; wordIdx < list.Words.Length; wordIdx++) {
                    var word = list.Words[wordIdx];
                    if (!string.IsNullOrEmpty(search) && !word.Contains(search, StringComparison.InvariantCultureIgnoreCase)) {
                        continue;
                    }

                    if (ImGui.MenuItem(word)) {
                        x = (listIdx, wordIdx);
                    }
                }
            }
        }

        var pack = Pack.All[this._pack];

        var lineHeight = ImGui.CalcTextSize("A").Y;
        var imageHeight = lineHeight * 4;

        var actualText = string.Empty;

        if (ImGui.BeginTable("##message-preview", 2)) {
            using var endTable = new OnDispose(ImGui.EndTable);

            ImGui.TableSetupColumn("##image", ImGuiTableColumnFlags.WidthFixed);
            ImGui.TableSetupColumn("##message", ImGuiTableColumnFlags.WidthStretch);
            ImGui.TableNextRow();

            if (ImGui.TableSetColumnIndex(0)) {
                var glyphImage = this.GetGlyphImage(this._glyph);
                var wrap = glyphImage.GetWrapOrEmpty();
                ImGui.Image(wrap.Handle, new Vector2(imageHeight));
            }

            if (ImGui.TableSetColumnIndex(1) && this._part1 != -1) {
                var preview = new StringBuilder();

                var word1 = this.Word1;
                if (this.Template1 is { } template1Preview && word1 != null) {
                    preview.Append(string.Format(template1Preview.Text, word1));
                }

                if (this.Conjunction is { } conj) {
                    var isPunc = conj.Length == 1 && char.IsPunctuation(conj[0]);
                    if (isPunc) {
                        preview.Append(conj);
                        preview.Append('\n');
                    } else {
                        preview.Append('\n');
                        preview.Append(conj);
                        preview.Append(' ');
                    }

                    var word2 = this.Word2;
                    if (this.Template2 is { } template2Preview && word2 != null) {
                        preview.Append(string.Format(template2Preview.Text, word2));
                    }
                }

                actualText = preview.ToString();
                var actualSize = ImGui.CalcTextSize(actualText);
                ImGui.Dummy(new Vector2(1, imageHeight / 2 - actualSize.Y / 2 - ImGui.GetStyle().ItemSpacing.Y));
                ImGui.TextUnformatted(actualText);
            }
        }

        ImGui.Separator();

        var templateStrings = pack.Templates
            .Select(template => template.Text)
            .ToArray();

        DrawTemplatePicker("Template##part-1", templateStrings, ref this._part1, ref this._word1);
        if (this.Template1 is { } template1 && template1.Text.Contains("{0}")) {
            if (template1.Words == null && pack.Words != null) {
                DrawWordPicker("Word##word-1", pack.Words, ref this._word1, ref this._word1Search);
            } else if (template1.Words != null) {
                DrawSpecificWordPicker("Word##word-1", template1, ref this._word1, ref this._word1Search);
            }
        }

        if (pack.Conjunctions != null) {
            DrawPicker("Conjunction##conj", pack.Conjunctions, ref this._conj);
        }

        if (this._conj != -1) {
            DrawTemplatePicker("Template##part-2", templateStrings, ref this._part2, ref this._word2);
            if (this.Template1 is { } template2 && template2.Text.Contains("{0}")) {
                if (template2.Words == null && pack.Words != null) {
                    DrawWordPicker("Word##word-2", pack.Words, ref this._word2, ref this._word2Search);
                } else if (template2.Words != null) {
                    DrawSpecificWordPicker("Word##word-2", template2, ref this._word2, ref this._word2Search);
                }
            }
        }

        if (ImGui.BeginCombo("Glyph", $"{this._glyph + 1}")) {
            using var endCombo = new OnDispose(ImGui.EndCombo);
            var tooltipShown = false;

            for (var i = 0; i < Messages.VfxPaths.Length; i++) {
                if (ImGui.Selectable($"{i + 1}", this._glyph == i)) {
                    this._glyph = i;
                }

                if (tooltipShown || !ImGui.IsItemHovered()) {
                    continue;
                }

                var glyphImage = this.GetGlyphImage(i);
                if (!glyphImage.TryGetWrap(out var wrap, out _)) {
                    continue;
                }

                ImGui.BeginTooltip();
                using var endTooltip = new OnDispose(ImGui.EndTooltip);

                ImGui.Image(wrap.Handle, new Vector2(imageHeight));
                tooltipShown = true;
            }
        }

        var emoteLabel = this._emoteIdx == -1
            ? "None"
            : this.Emotes[this._emoteIdx].Name.ToDalamudString().TextValue;
        if (ImGui.BeginCombo("Emote", emoteLabel, ImGuiComboFlags.HeightLarge)) {
            using var endCombo = new OnDispose(ImGui.EndCombo);

            if (ImGui.IsWindowAppearing()) {
                ImGui.SetKeyboardFocusHere();
            }

            ImGui.SetNextItemWidth(-1);
            ImGui.InputTextWithHint("###emote-search", "Search...", ref this._emoteSearch, 100, ImGuiInputTextFlags.AutoSelectAll);

            using var endChild = new OnDispose(ImGui.EndChild);
            if (ImGui.BeginChild("##emote-search-child", new Vector2(0, 150) * ImGuiHelpers.GlobalScale)) {
                if (ImGui.Selectable("None##no-emote", this._emoteIdx == -1)) {
                    this._emoteIdx = -1;
                    ImGui.CloseCurrentPopup();
                }

                ImGui.Separator();

                for (var i = 0; i < this.Emotes.Count; i++) {
                    var emote = this.Emotes[i];
                    var name = emote.Name.ToDalamudString().TextValue;
                    if (!string.IsNullOrEmpty(this._emoteSearch)) {
                        if (!name.Contains(this._emoteSearch, StringComparison.InvariantCultureIgnoreCase)) {
                            if (!emote.TextCommand.Value.Command.ToDalamudString().TextValue.Contains(this._emoteSearch, StringComparison.InvariantCultureIgnoreCase)) {
                                continue;
                            }
                        }
                    }

                    var unlocked = IsEmoteUnlocked(emote);
                    if (!unlocked) {
                        ImGui.BeginDisabled();
                    }

                    if (ImGui.Selectable($"{name}##emote-{emote.RowId}", this._emoteIdx == i)) {
                        this._emoteIdx = i;
                        ImGui.CloseCurrentPopup();
                    }

                    if (!unlocked) {
                        ImGui.EndDisabled();
                    }
                }
            }
        }

        this.ClearIfNecessary();

        var valid = this.ValidSetup();
        if (!valid) {
            ImGui.BeginDisabled();
        }

        var inAir = this.Plugin.Condition[ConditionFlag.Jumping]
                    || this.Plugin.Condition[ConditionFlag.Jumping61]
                    || this.Plugin.Condition[ConditionFlag.InFlight];
        if (ImGui.Button("Write") && valid && !inAir && this.Plugin.ClientState.LocalPlayer is { } player) {
            var location = HousingLocation.Current();
            var req = new MessageRequest {
                Territory = this.Plugin.ClientState.TerritoryType,
                World = player.CurrentWorld.RowId,
                Ward = location?.Ward,
                Plot = location?.CombinedPlot(),
                X = player.Position.X,
                Y = player.Position.Y,
                Z = player.Position.Z,
                Yaw = player.Rotation,
                PackId = pack.Id,
                Template1 = this._part1,
                Word1List = this._word1.Item1 == -1 ? null : this._word1.Item1,
                Word1Word = this._word1.Item2 == -1 ? null : this._word1.Item2,
                Conjunction = this._conj == -1 ? null : this._conj,
                Template2 = this._part2 == -1 ? null : this._part2,
                Word2List = this._word2.Item1 == -1 ? null : this._word2.Item1,
                Word2Word = this._word2.Item2 == -1 ? null : this._word2.Item2,
                Glyph = this._glyph,
                Emote = this._emoteIdx == -1
                    ? null
                    : this.GetEmoteData(this.Emotes[this._emoteIdx], player),
            };

            var json = JsonConvert.SerializeObject(req);
            Task.Run(async () => {
                var resp = await ServerHelper.SendRequest(
                    this.Plugin.Config.ApiKey,
                    HttpMethod.Post,
                    "/messages",
                    "application/json",
                    new StringContent(json)
                );
                var content = await resp.Content.ReadAsStringAsync();
                if (resp.IsSuccessStatusCode) {
                    var newMsg = new Message {
                        Id = Guid.Parse(content),
                        X = player.Position.X,
                        Y = player.Position.Y,
                        Z = player.Position.Z,
                        Yaw = player.Rotation,
                        Text = actualText,
                        NegativeVotes = 0,
                        PositiveVotes = 0,
                        UserVote = 0,
                        Glyph = this._glyph,
                        Emote = req.Emote,
                    };

                    this.Plugin.Messages.Add(newMsg);
                    this.ResetWriter();
                    this.Plugin.Ui.MainWindow.Visible = false;
                } else {
                    var error = JsonConvert.DeserializeObject<ErrorMessage>(content);
                    this.Plugin.Ui.ShowModal($"Error writing message.\n\nMessage from server:\n{error?.Message}");
                }
            });
        }

        if (!valid) {
            ImGui.EndDisabled();
        }
    }

    private unsafe EmoteData GetEmoteData(Emote emote, IPlayerCharacter player) {
        var chara = (Character*) GameObjectManager.Instance()->Objects.GetObjectByGameObjectId(player.GameObjectId);

        return new EmoteData {
            Id = emote.RowId,
            Customise = player.Customize.ToList(),
            Equipment = chara->DrawData.EquipmentModelIds
                .ToArray()
                .Select(equip => new EquipmentData {
                    Id = equip.Id,
                    Variant = equip.Variant,
                    Stain0 = equip.Stain0,
                    Stain1 = equip.Stain1,
                })
                .ToArray(),
            Weapon = chara->DrawData.WeaponData
                .ToArray()
                .Select(weapon => new WeaponData {
                    ModelId = new WeaponModelId {
                        Id = weapon.ModelId.Id,
                        Kind = weapon.ModelId.Type,
                        Variant = weapon.ModelId.Variant,
                        Stain0 = weapon.ModelId.Stain0,
                        Stain1 = weapon.ModelId.Stain1,
                    },
                    Flags1 = weapon.Flags1,
                    Flags2 = weapon.Flags2,
                    State = weapon.State,
                })
                .ToArray(),
            Glasses = chara->DrawData.GlassesIds[0],
            HatHidden = chara->DrawData.IsHatHidden,
            VisorToggled = chara->DrawData.IsVisorToggled,
            WeaponHidden = chara->DrawData.IsWeaponHidden,
        };
    }

    private static unsafe bool IsEmoteUnlocked(Emote emote) {
        return UIState.Instance()->IsEmoteUnlocked((ushort) emote.RowId);
    }

    private void ResetWriter() {
        this._part1 = this._part2 = this._conj = -1;
        this._word1 = (-1, -1);
        this._word2 = (-1, -1);
        this._glyph = this.Plugin.Config.DefaultGlyph;
        this._emoteIdx = -1;
        this._emoteSearch = string.Empty;
        this._word1Search = string.Empty;
        this._word2Search = string.Empty;
    }

    private void ClearIfNecessary() {
        if (this._pack == -1) {
            this._part1 = -1;
        }

        var pack = Pack.All[this._pack];

        if (this._part1 == -1 || !pack.Templates[this._part1].Text.Contains("{0}")) {
            this._word1 = (-1, -1);
        }

        if (this._conj == -1) {
            this._part2 = -1;
        }

        if (this._part2 == -1 || !pack.Templates[this._part2].Text.Contains("{0}")) {
            this._word2 = (-1, -1);
        }
    }

    private bool ValidSetup() {
        if (this._pack == -1 || this._part1 == -1) {
            return false;
        }

        var pack = Pack.All[this._pack];
        var template1 = pack.Templates[this._part1];
        var temp1Variable = template1.Text.Contains("{0}");

        switch (temp1Variable) {
            case true when this._word1 == (-1, -1):
            case false when this._word1 != (-1, -1):
                return false;
        }

        if (this._conj == -1 && (this._part2 != -1 || this._word2 != (-1, -1))) {
            return false;
        }

        if (this._conj != -1) {
            if (this._part2 == -1) {
                return false;
            }

            var template2 = pack.Templates[this._part2];
            var temp2Variable = template2.Text.Contains("{0}");

            switch (temp2Variable) {
                case true when this._word2 == (-1, -1):
                case false when this._word2 != (-1, -1):
                    return false;
            }
        }

        return true;
    }
}
