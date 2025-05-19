using Dalamud.Configuration;
using Dalamud.Game.Command;
using Dalamud.Game.Text;
using Dalamud.Game.Text.SeStringHandling;
using Dalamud.Game.Text.SeStringHandling.Payloads;
using Dalamud.Plugin;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text.Json.Serialization;
using System.Threading;
using ImGuiNET;
using Dalamud.Plugin.Services;

namespace FPSLimiter
{

    public class Configuration : IPluginConfiguration
    {
        public int Version { get; set; }
        public int FpsCap = 60;
        public int FpsCapUnfocused = 20;
        public int FpsCapPrev = 20;
        public bool FpsCapEnabled = true;
        public bool FpsCapUnfocusedEnabled = true;
        public bool DisableOnLogin = false;
        public bool DisableOnZoning = true;

        [JsonIgnore] private IDalamudPluginInterface pluginInterface;

        public void Initialize(IDalamudPluginInterface PluginInterface)
        {
            pluginInterface = PluginInterface;
        }

        public void Save()
        {
            pluginInterface.SavePluginConfig(this);
        }
    }

    public class Plugin : IDalamudPlugin
    {
        public string Name => "fps limiter";
        public string Cmd => "/fps";

        private Stopwatch stopwatch;
        private Configuration settings;
        private IDalamudPluginInterface pluginInterface;
        public bool Alternate = true;
        public bool ShowConfig = false;

        public Plugin(IDalamudPluginInterface PluginInterface)
        {
            pluginInterface = PluginInterface;
            pluginInterface.Create<Svc>();
            settings = (Configuration)pluginInterface.GetPluginConfig() ?? new Configuration();
            stopwatch = new Stopwatch();

            Svc.Commands.AddHandler(Cmd, new CommandInfo(OnCmd)
            {
                HelpMessage = "프레임 제한 설정 - /fps # [bg|all]",
                ShowInHelp = true
            });
            
            Svc.Framework.Update += OnUpdate;
            
            pluginInterface.UiBuilder.Draw += Draw;
            pluginInterface.UiBuilder.OpenConfigUi += ToggleConfig;
        }

        public void ToggleConfig()
        {
            ShowConfig = !ShowConfig;
        }

        public void Draw()
        {
            if (!ShowConfig)
                return;
            ImGui.Begin("fps limiter##configWindow", ref ShowConfig, ImGuiWindowFlags.AlwaysAutoResize | ImGuiWindowFlags.NoResize);
            if (ImGui.Checkbox("##fpslimiterEnabled", ref settings.FpsCapEnabled))
            {
                pluginInterface.SavePluginConfig(settings);
            }
            if (ImGui.IsItemHovered())
            {
                ImGui.BeginTooltip();
                ImGui.Text("최대 프레임");
                ImGui.EndTooltip();
            }
            ImGui.SameLine();
            ImGui.SetNextItemWidth(115);
            if (ImGui.InputInt("##fpscap", ref settings.FpsCap, 1, 5))
            {
                if (settings.FpsCap < 5) settings.FpsCap = 5; // silly idea protection
                pluginInterface.SavePluginConfig(settings);
            }

            if (ImGui.Checkbox("##fpslimiterbgEnabled", ref settings.FpsCapUnfocusedEnabled))
            {
                pluginInterface.SavePluginConfig(settings);
            }
            if (ImGui.IsItemHovered())
            {
                ImGui.BeginTooltip();
                ImGui.Text("최소 프레임");
                ImGui.EndTooltip();
            }
            ImGui.SameLine();
            ImGui.SetNextItemWidth(115);
            if (ImGui.InputInt("##fpscapunfocused", ref settings.FpsCapUnfocused, 1, 1))
            {
                if (settings.FpsCapUnfocused < 1) settings.FpsCapUnfocused = 1;
                pluginInterface.SavePluginConfig(settings);
            }
            if (ImGui.CollapsingHeader(""))
            {
                ImGui.Text("설정 값 무시");
                ImGui.Checkbox("로그인##disableOnLogin", ref settings.DisableOnLogin);
                if (ImGui.IsItemHovered())
                {
                    ImGui.BeginTooltip();
                    ImGui.Text("타이틀, 캐릭터 선택, 캐릭터 커스터마이징 화면 등\n로그인 하기 전 모든 곳에서 프레임 제한 설정 값을 무시합니다.\n캐릭터 선택 창에서 일정 값 이상의 프레임레이트를 요구하는 플러그인의 원활한 작동에 필요할 수 있습니다.");
                    ImGui.EndTooltip();
                }
                ImGui.Checkbox("지역 이동##disableOnZoning", ref settings.DisableOnZoning);
                if (ImGui.IsItemHovered())
                {
                    ImGui.BeginTooltip();
                    ImGui.Text("지역 이동 시 프레임 제한 설정 값을 무시합니다.\n로딩 속도가 조금이나마 빨라질 수 있습니다.");
                    ImGui.EndTooltip();
                }
            }
        }

        public void OnCmd(string command, string arg)
        {
            var args = arg.Split(' ');
            if (args.Length < 1) return;

            if (!int.TryParse(args[0], out int fpsCapNew))
            {
                ShowConfig = !ShowConfig;
                return;
            }
            if (args.Length == 2)
            {
                if (args[1].ToLower() == "bg")
                {
                    settings.FpsCapUnfocused = fpsCapNew;
                    Svc.Chat.Print(new XivChatEntry()
                    {
                        Message = new SeString(new List<Payload>()
                        {
                            new TextPayload("Your background FPS is now capped to "),
                            new UIGlowPayload((ushort)551),
                            new TextPayload(settings.FpsCapUnfocused.ToString()),
                            new UIGlowPayload(0),
                            new TextPayload(".")
                        })
                    });
                }
                else if (args[1].ToLower() == "all")
                {
                    settings.FpsCapUnfocused = fpsCapNew;
                    settings.FpsCap = fpsCapNew;
                    Svc.Chat.Print(new XivChatEntry()
                    {
                        Message = new SeString(new List<Payload>()
                        {
                            new TextPayload("Your background FPS and FPS are now capped to "),
                            new UIGlowPayload((ushort)541),
                            new TextPayload(settings.FpsCapUnfocused.ToString()),
                            new UIGlowPayload(0),
                            new TextPayload(".")
                        })
                    });
                }
                pluginInterface.SavePluginConfig(settings);
                return;
            }

            if (fpsCapNew == settings.FpsCap)
            {
                fpsCapNew = settings.FpsCapPrev;
                settings.FpsCapPrev = settings.FpsCap;
            }
            settings.FpsCap = fpsCapNew;

            Svc.Chat.Print(new XivChatEntry()
            {
                Message = new SeString(new List<Payload>()
                {
                    new TextPayload("Your FPS is now capped to "),
                    new UIGlowPayload(Alternate ? (ushort)566 : (ushort)540),
                    new TextPayload(settings.FpsCap.ToString()),
                    new UIGlowPayload(0),
                    new TextPayload(".")
                })
            });

            Alternate = !Alternate;
            pluginInterface.SavePluginConfig(settings);
        }

        [DllImport("user32.dll", CharSet = CharSet.Auto, ExactSpelling = true)] private static extern IntPtr GetForegroundWindow();
        [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)] private static extern int GetWindowThreadProcessId(IntPtr handle, out int processId);
        public static bool IsGameFocused
        {
            get
            {
                var activatedHandle = GetForegroundWindow();
                if (activatedHandle == IntPtr.Zero)
                    return false;
                var procId = Environment.ProcessId;
                _ = GetWindowThreadProcessId(activatedHandle, out var activeProcId);
                return activeProcId == procId;
            }
        }

        public void OnUpdate(IFramework framework)
        {
            if ((!settings.FpsCapEnabled && IsGameFocused) || (!settings.FpsCapUnfocusedEnabled && !IsGameFocused)) return;
            if (settings.DisableOnLogin && !Svc.ClientState.IsLoggedIn) return;
            if (settings.DisableOnZoning && Svc.Condition[Dalamud.Game.ClientState.Conditions.ConditionFlag.BetweenAreas]) return;
            //var wantedMs = 1.0f / fpsCap * 1000;

            stopwatch.Stop();
            //var elapsedMS = stopwatch.ElapsedTicks / 10000f;
            //var sleepTime = Math.Max((1.0f / fpsCap * 1000) - (stopwatch.ElapsedTicks / 10000f), 0);

            Thread.Sleep((int)Math.Max((1.0f / (settings.FpsCapUnfocusedEnabled && !IsGameFocused ? settings.FpsCapUnfocused : settings.FpsCap) * 1000) - (stopwatch.ElapsedTicks / 10000f), 0));
            stopwatch.Restart();
        }

        public void Dispose()
        {
            if (settings.FpsCap < 5) settings.FpsCap = 60;
            pluginInterface.SavePluginConfig(settings);
            Svc.Commands.RemoveHandler(Cmd);
            Svc.Framework.Update -= OnUpdate;
            pluginInterface.UiBuilder.Draw -= Draw;
            pluginInterface.UiBuilder.OpenConfigUi -= ToggleConfig;
        }
    }
}
