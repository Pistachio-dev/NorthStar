using System.Globalization;
using System.Numerics;
using Dalamud.Bindings.ImGui;
using Dalamud.Utility;
using FFXIVClientStructs.Interop;
using Lumina.Excel.Sheets;
using OrangeGuidanceTomestone.Helpers;
using OrangeGuidanceTomestone.Util;

namespace OrangeGuidanceTomestone.Ui.MainWindowTabs;

internal class Settings : ITab {
    public string Name => "Settings";

    private Plugin Plugin { get; }
    private int _tab;
    private string _extraCode = string.Empty;
    private IReadOnlyList<(uint, string)> Territories { get; }
    private List<(uint, bool, string)> FilteredTerritories { get; set; }

    private delegate void DrawSettingsDelegate(ref bool anyChanged, ref bool vfx);

    private IReadOnlyList<(string, DrawSettingsDelegate)> Tabs { get; }

    private string _filter = string.Empty;
    private string _debugFilter = string.Empty;

    internal Settings(Plugin plugin) {
        this.Plugin = plugin;

        this.Territories = this.Plugin.DataManager.GetExcelSheet<TerritoryType>()
            .Where(row => row.RowId != 0)
            .Select(row => (row.RowId, row.PlaceName.Value.Name.ToDalamudString().TextValue))
            .Where(entry => !string.IsNullOrWhiteSpace(entry.TextValue))
            .ToList();
        this.FilterTerritories(null);

        this.Tabs = [
            ("General", this.DrawGeneral),
            ("Writer", this.DrawWriter),
            ("Viewer", this.DrawViewer),
            ("Signs", this.DrawSigns),
            ("Unlocks", this.DrawUnlocks),
            ("Account", this.DrawAccount),
            ("Debug", this.DrawDebug),
            ("Data/privacy", this.DrawDataPrivacy),
        ];
    }

    public void Dispose() {
    }

    private void FilterTerritories(string? text) {
        var filter = !string.IsNullOrWhiteSpace(text);

        var territories = this.Territories
            .Where(terr => !this.Plugin.Config.BannedTerritories.Contains(terr.Item1))
            .Select(terr => (terr.Item1, false, terr.Item2));

        var tt = this.Plugin.DataManager.GetExcelSheet<TerritoryType>();
        this.FilteredTerritories = this.Plugin.Config.BannedTerritories
            .OrderBy(terr => terr)
            .Select(terr => (terr, true, tt.GetRowOrDefault(terr)?.PlaceName.Value.Name.ToDalamudString().TextValue ?? $"{terr}"))
            .Concat(territories)
            .Where(terr => !filter || CultureInfo.InvariantCulture.CompareInfo.IndexOf(terr.Item3, text!, CompareOptions.OrdinalIgnoreCase) != -1)
            .ToList();
    }

    public void Draw() {
        ImGui.PushTextWrapPos();

        var anyChanged = false;
        var vfx = false;

        var widestTabName = this.Tabs
            .Select(entry => ImGui.CalcTextSize(entry.Item1).X)
            .Max();

        var leftOver = ImGui.GetContentRegionAvail().X - widestTabName - ImGui.GetStyle().ItemSpacing.X - ImGui.GetStyle().FrameBorderSize;
        var childHeight = ImGui.GetContentRegionAvail().Y - ImGui.GetStyle().ItemSpacing.Y * 2;
        if (ImGui.BeginTable("##settings-tabs", 2)) {
            ImGui.TableSetupColumn("##names", ImGuiTableColumnFlags.None, widestTabName + ImGui.GetStyle().ItemSpacing.X);
            ImGui.TableSetupColumn("##content", ImGuiTableColumnFlags.None, leftOver);

            ImGui.TableNextRow();

            if (ImGui.TableSetColumnIndex(0)) {
                for (var i = 0; i < this.Tabs.Count; i++) {
                    var (name, _) = this.Tabs[i];
                    if (ImGui.Selectable($"{name}##tab-{i}", i == this._tab)) {
                        this._tab = i;
                    }
                }
            }

            if (ImGui.TableSetColumnIndex(1)) {
                if (ImGui.BeginChild("##tab-content-child", new Vector2(-1, childHeight))) {
                    var (_, draw) = this.Tabs[this._tab];
                    draw(ref anyChanged, ref vfx);
                }

                ImGui.EndChild();
            }

            ImGui.EndTable();
        }

        if (anyChanged) {
            this.Plugin.SaveConfig();
        }

        if (vfx) {
            this.Plugin.Messages.RemoveVfx();
            this.Plugin.Messages.Clear();
            this.Plugin.Messages.SpawnVfx();
        }

        ImGui.PopTextWrapPos();
    }

    private void DrawGeneral(ref bool anyChanged, ref bool vfx) {
        anyChanged |= vfx |= ImGui.Checkbox("Disable in trials", ref this.Plugin.Config.DisableTrials);
        anyChanged |= vfx |= ImGui.Checkbox("Disable in Deep Dungeons", ref this.Plugin.Config.DisableDeepDungeon);
        anyChanged |= vfx |= ImGui.Checkbox("Disable in cutscenes", ref this.Plugin.Config.DisableInCutscene);
        anyChanged |= vfx |= ImGui.Checkbox("Disable in /gpose", ref this.Plugin.Config.DisableInGpose);

        ImGui.Spacing();
        ImGui.TextUnformatted("Ban list (click to ban or unban)");

        ImGui.SetNextItemWidth(-1);
        if (ImGui.InputTextWithHint("##filter", "Search...", ref this._filter, 128)) {
            this.FilterTerritories(this._filter);
        }

        if (ImGui.BeginChild("##ban-list", new Vector2(-1, -1), true)) {
            var toAdd = -1L;
            var toRemove = -1L;

            var clipper = ImGuiHelper.Clipper(this.FilteredTerritories.Count);
            while (clipper.Step()) {
                for (var i = clipper.DisplayStart; i < clipper.DisplayEnd; i++) {
                    var (terrId, isBanned, name) = this.FilteredTerritories[i];
                    if (isBanned) {
                        this.DrawBannedTerritory(terrId, name, ref toRemove);
                    } else {
                        this.DrawTerritory(terrId, name, ref toAdd);
                    }
                }
            }

            ImGui.Separator();

            if (toRemove > -1) {
                this.Plugin.Config.BannedTerritories.Remove((uint) toRemove);
                if (this.Plugin.ClientState.TerritoryType == toRemove) {
                    this.Plugin.Messages.SpawnVfx();
                }
            }

            if (toAdd > -1) {
                this.Plugin.Config.BannedTerritories.Add((uint) toAdd);
                if (this.Plugin.ClientState.TerritoryType == toAdd) {
                    this.Plugin.Framework.RunOnFrameworkThread(() => {
                        this.Plugin.Messages.RemoveVfx();
                        this.Plugin.Messages.Clear();
                    });
                }
            }

            if (toRemove > -1 || toAdd > -1) {
                this.Plugin.SaveConfig();
                this.FilterTerritories(this._filter);
            }
        }

        ImGui.EndChild();
    }

    private void DrawTerritory(uint rowId, string name, ref long toAdd) {
        if (this.Plugin.Config.BannedTerritories.Contains(rowId)) {
            return;
        }

        if (ImGui.Selectable($"{name}##{rowId}")) {
            toAdd = rowId;
        }
    }

    private void DrawBannedTerritory(uint terrId, string name, ref long toRemove) {
        if (ImGui.Selectable($"{name}##{terrId}", true)) {
            toRemove = terrId;
        }
    }

    private void DrawWriter(ref bool anyChanged, ref bool vfx) {
        if (ImGui.Button("Refresh packs")) {
            Pack.UpdatePacks();
        }

        var glyph = this.Plugin.Config.DefaultGlyph + 1;
        if (ImGui.InputInt("Default glyph", ref glyph)) {
            this.Plugin.Config.DefaultGlyph = Math.Min(Messages.VfxPaths.Length - 1, Math.Max(0, glyph - 1));
            anyChanged = true;
        }
    }

    private void DrawViewer(ref bool anyChanged, ref bool vfx) {
        anyChanged |= ImGui.SliderFloat("Viewer opacity", ref this.Plugin.Config.ViewerOpacity, 0f, 100.0f, $"{this.Plugin.Config.ViewerOpacity:N3}%%");
        anyChanged |= ImGui.Checkbox("Open the viewer automatically when near a sign", ref this.Plugin.Config.AutoViewer);
        anyChanged |= ImGui.Checkbox("Close the viewer automatically when no signs are nearby", ref this.Plugin.Config.AutoViewerClose);

        if (this.Plugin.Config.AutoViewerClose) {
            ImGui.TreePush("auto-viewer-close-sub");
            anyChanged |= ImGui.Checkbox("Hide viewer titlebar", ref this.Plugin.Config.HideTitlebar);
            ImGui.TreePop();
        }

        anyChanged |= ImGui.Checkbox("Lock viewer in place", ref this.Plugin.Config.LockViewer);
        anyChanged |= ImGui.Checkbox("Click through viewer", ref this.Plugin.Config.ClickThroughViewer);

        anyChanged |= ImGui.Checkbox("Show player emotes", ref this.Plugin.Config.ShowEmotes);
        anyChanged |= ImGui.SliderFloat("Player emote opacity", ref this.Plugin.Config.EmoteAlpha, 0f, 100f, "%.2f%%");
    }

    private void DrawSigns(ref bool anyChanged, ref bool vfx) {
        anyChanged |= vfx |= ImGui.Checkbox("Remove glow effect from signs", ref this.Plugin.Config.RemoveGlow);
        if (ImGui.SliderFloat("Sign opacity", ref this.Plugin.Config.SignAlpha, 0, 100, "%.2f%%")) {
            anyChanged = true;

            this.WithEachVfx(vfx => {
                unsafe {
                    vfx.Value->Alpha = Math.Clamp(this.Plugin.Config.SignAlpha / 100.0f, 0, 1);
                }
            });
        }

        var intensity = (this.Plugin.Config.SignRed + this.Plugin.Config.SignGreen + this.Plugin.Config.SignBlue) / 3;
        if (ImGui.SliderFloat("Sign intensity", ref intensity, 0, 100, "%.2f%%")) {
            anyChanged = true;
            this.Plugin.Config.SignRed = intensity;
            this.Plugin.Config.SignGreen = intensity;
            this.Plugin.Config.SignBlue = intensity;

            var scaled = Math.Clamp(intensity / 100.0f, 0, 1);
            this.WithEachVfx(vfx => {
                unsafe {
                    vfx.Value->Red = scaled;
                    vfx.Value->Green = scaled;
                    vfx.Value->Blue = scaled;
                }
            });
        }

        if (ImGui.TreeNodeEx("Individual colour intensities")) {
            using var treePop = new OnDispose(ImGui.TreePop);

            if (ImGui.SliderFloat("Red intensity", ref this.Plugin.Config.SignRed, 0, 100, "%.2f%%")) {
                anyChanged = true;
                this.WithEachVfx(vfx => {
                    unsafe {
                        vfx.Value->Red = Math.Clamp(this.Plugin.Config.SignRed / 100, 0, 1);
                    }
                });
            }

            if (ImGui.SliderFloat("Green intensity", ref this.Plugin.Config.SignGreen, 0, 100, "%.2f%%")) {
                anyChanged = true;
                this.WithEachVfx(vfx => {
                    unsafe {
                        vfx.Value->Green = Math.Clamp(this.Plugin.Config.SignGreen / 100, 0, 1);
                    }
                });
            }

            if (ImGui.SliderFloat("Blue intensity", ref this.Plugin.Config.SignBlue, 0, 100, "%.2f%%")) {
                anyChanged = true;
                this.WithEachVfx(vfx => {
                    unsafe {
                        vfx.Value->Blue = Math.Clamp(this.Plugin.Config.SignBlue / 100, 0, 1);
                    }
                });
            }
        }
    }

    private void WithEachVfx(Action<Pointer<Vfx.VfxStruct>> action) {
        if (!this.Plugin.Vfx.Mutex.With(TimeSpan.Zero, out var releaser)) {
            return;
        }

        using (releaser) {
            foreach (var (_, ptr) in this.Plugin.Vfx.Spawned) {
                unsafe {
                    action((Vfx.VfxStruct*) ptr);
                }
            }
        }
    }

    private void DrawUnlocks(ref bool anyChanged, ref bool vfx) {
        this.ExtraCodeInput();
    }

    private void DrawAccount(ref bool anyChanged, ref bool vfx) {
        this.DeleteAccountButton();
    }

    private void DrawDebug(ref bool anyChanged, ref bool vfx) {
        if (!ImGui.BeginTabBar("debug-tabs")) {
            return;
        }

        using var endTabBar = new OnDispose(ImGui.EndTabBar);

        if (ImGui.BeginTabItem("VFX")) {
            using var endTabItem = new OnDispose(ImGui.EndTabItem);

            ImGui.Checkbox("Show debug information", ref this.Plugin.Ui.Debug);

            ImGui.InputText("Filter", ref this._debugFilter, 64);

            if (ImGui.BeginTable("###debug-info", 2)) {
                using var endTable = new OnDispose(ImGui.EndTable);

                ImGui.TableSetupColumn("ID", ImGuiTableColumnFlags.WidthStretch);
                ImGui.TableSetupColumn("VFX pointer");
                ImGui.TableHeadersRow();

                using var guard = this.Plugin.Vfx.Mutex.With();
                foreach (var (id, ptr) in this.Plugin.Vfx.Spawned) {
                    var idLabel = id.ToString("N");
                    var ptrLabel = ptr.ToString("X");

                    if (!string.IsNullOrWhiteSpace(this._debugFilter)) {
                        if (
                            !idLabel.Contains(this._debugFilter, StringComparison.CurrentCultureIgnoreCase)
                            && !ptrLabel.Contains(this._debugFilter, StringComparison.CurrentCultureIgnoreCase)
                        ) {
                            continue;
                        }
                    }

                    ImGui.TableNextRow();

                    if (ImGui.TableSetColumnIndex(0)) {
                        ImGui.TextUnformatted(id.ToString("N"));
                        if (ImGui.IsItemClicked()) {
                            ImGui.SetClipboardText(id.ToString("N"));
                        }
                    }

                    if (ImGui.TableSetColumnIndex(1)) {
                        ImGui.TextUnformatted(ptr.ToString("X"));
                        if (ImGui.IsItemClicked()) {
                            ImGui.SetClipboardText(ptr.ToString("X"));
                        }
                    }
                }
            }
        }

        if (ImGui.BeginTabItem("Housing")) {
            using var endTabItem = new OnDispose(ImGui.EndTabItem);

            var loc = HousingLocation.Current();
            if (loc != null) {
                ImGui.TextUnformatted($"Apartment: {loc.Apartment:X}h/{loc.Apartment}");
                ImGui.TextUnformatted($"ApartmentWing: {loc.ApartmentWing:X}h/{loc.ApartmentWing}");
                ImGui.TextUnformatted($"Ward: {loc.Ward:X}h/{loc.Ward}");
                ImGui.TextUnformatted($"Plot: {loc.Plot:X}h/{loc.Plot}");
                ImGui.TextUnformatted($"Yard: {loc.Yard:X}h/{loc.Yard}");

            } else {
                ImGui.TextUnformatted("loc was null");
            }
        }
    }

    private void DrawDataPrivacy(ref bool anyChanged, ref bool vfx) {
        ImGui.PushTextWrapPos();
        using var popTextWrap = new OnDispose(ImGui.PopTextWrapPos);

        ImGui.TextUnformatted("The only data that Orange Guidance Tomestone collects from you is what you submit via the plugin interface.");
        ImGui.TextUnformatted("When you install the plugin, an anonymous account is automatically created for you. This account does not store any information about you, and it can be deleted at any time.");
        ImGui.TextUnformatted("In order for the emote function to work, the plugin sends information about your equipment as well as your complete character customisation data. This is used to display your character performing the emote when your message is viewed.");
        ImGui.TextUnformatted("Orange Guidance Tomestone stores all data you send via the plugin until you delete it or delete your account.");
        ImGui.TextUnformatted("While you are online, Orange Guidance Tomestone sends a periodic notification to the server to mark your account as active, which allows your messages to be viewed by other players.");
        ImGui.TextUnformatted("Orange Guidance Tomestone does not sell or share your data, and it does not collect any personally-identifiable information. No tracking is used in this plugin.");
    }

    private void ExtraCodeInput() {
        ImGui.InputText("Extra code", ref this._extraCode, 128);
        if (!ImGui.Button("Claim")) {
            return;
        }

        var code = this._extraCode;
        Task.Run(async () => {
            var resp = await ServerHelper.SendRequest(
                this.Plugin.Config.ApiKey,
                HttpMethod.Post,
                "/claim",
                null,
                new StringContent(code)
            );

            if (resp.IsSuccessStatusCode) {
                this._extraCode = string.Empty;
                var text = await resp.Content.ReadAsStringAsync();
                if (uint.TryParse(text, out var extra)) {
                    this.Plugin.Ui.MainWindow.ExtraMessages = extra;
                    this.Plugin.Ui.ShowModal($"Code claimed.\n\nYou can now post up to {Messages.MaxAmount + extra:N0} messages.");
                } else {
                    this.Plugin.Ui.ShowModal("Code claimed but the server gave an unexpected response.");
                }
            } else {
                this.Plugin.Ui.ShowModal("Invalid code.");
            }
        });
    }

    private void DeleteAccountButton() {
        var ctrl = ImGui.GetIO().KeyCtrl;
        if (!ctrl) {
            ImGui.BeginDisabled();
        }

        if (ImGui.Button("Delete account")) {
            Task.Run(async () => {
                var resp = await ServerHelper.SendRequest(
                    this.Plugin.Config.ApiKey,
                    HttpMethod.Delete,
                    "/account"
                );

                if (resp.IsSuccessStatusCode) {
                    this.Plugin.Config.ApiKey = string.Empty;
                    this.Plugin.SaveConfig();
                }
            });
        }

        if (!ctrl) {
            ImGui.EndDisabled();
        }

        ImGui.SameLine();
        ImGuiHelper.HelpIcon("Hold Ctrl to enable delete button.");

        ImGui.TextUnformatted("This will delete all your messages and votes.");
    }
}
