using CounterStrikeSharp.API;
using CounterStrikeSharp.API.Core;
using CounterStrikeSharp.API.Core.Translations;
using CounterStrikeSharp.API.Modules.Admin;
using CounterStrikeSharp.API.Modules.Commands;
using CounterStrikeSharp.API.Modules.Cvars;
using CounterStrikeSharp.API.Modules.Entities;
using CounterStrikeSharp.API.Modules.Menu;
using System.Drawing;
using System.Text;
using System.Text.Json;

namespace ReportSystem;

public partial class ReportSystem : BasePlugin, IPluginConfig<ReportSystemConfig>
{

    public override string ModuleAuthor => "ShadowRipper866";
    public override string ModuleName => "ReportSystem";
    public override string ModuleVersion => "1.0";
    public ReportSystemConfig Config { get; set; } = null!;
    private ReportSystemConfig _settings = null!;
    private PersonTargetData[] _selectedReason = new PersonTargetData[65];
    private Dictionary<ulong, DateTime> _playerReportCooldowns = new Dictionary<ulong, DateTime>();
    public bool VisibleInMenu(CCSPlayerController player)
    {
        foreach (var flags in Config.FlagsToIgnore)
        {
            if (AdminManager.PlayerHasPermissions(player, flags)) return false;
            else return true;
        }
        return true;
    }

    public void OnConfigParsed(ReportSystemConfig config)
    {
        _settings = config;
        Config = config;
    }

    private string GetPrefix()
    {
        return Localizer["Prefix"].Value.ReplaceColorTags();
    }

    public override void Load(bool hotReload)
    {

        RegisterListener<Listeners.OnClientConnected>(slot =>
            _selectedReason[slot + 1] = new PersonTargetData { Target = -1, IsSelectedReason = false });

        RegisterListener<Listeners.OnClientDisconnectPost>(slot =>
            _selectedReason[slot + 1] = null!);

        AddCommand("css_report", "", OnReportCommand);
        AddCommandListener("say", Listener_Say);
        AddCommandListener("say_team", Listener_Say);

        Console.WriteLine($"\n[ReportSystem] v{ModuleVersion} loaded successfully");
    }

    private void OnReportCommand(CCSPlayerController? controller, CommandInfo info)
    {
        if (controller == null) return;
        OpenReportMenu(controller);
    }


    private HookResult Listener_Say(CCSPlayerController? player, CommandInfo commandInfo)
    {
        try
        {
            if (player == null || !player.IsValid || player.Index < 1 || player.Index > 64)
                return HookResult.Continue;

            var data = _selectedReason[player.Index];
            if (data == null || !data.IsSelectedReason || !data.AllowCustomReason)
                return HookResult.Continue;

            var msg = GetTextInsideQuotes(commandInfo.ArgString);
            if (!msg.StartsWith("!"))
                return HookResult.Continue;

            msg = msg[1..].Trim();
            if (string.IsNullOrWhiteSpace(msg))
            {
                player.PrintToChat(GetPrefix() + Localizer["FailedReportDueEmptyReason"]);
                data.Reset();
                return HookResult.Stop;
            }

            var target = Utilities.GetPlayerFromIndex(data.Target);
            if (target == null || !target.IsValid)
            {
                player.PrintToChat(GetPrefix() + Localizer["FailedReportDueDisconnect"].Value.ReplaceColorTags());
                data.Reset();
                return HookResult.Handled;
            }

            var serverName = ConVar.Find("hostname")?.StringValue ?? "Unknown Server";
            var serverIp = Config.ServerIP;

            ulong steamId64 = player.SteamID;
            if (_playerReportCooldowns.TryGetValue(steamId64, out DateTime lastReportTime) &&
                DateTime.UtcNow < lastReportTime.AddSeconds(_settings.CooldownSeconds))
            {
                var waitTime = (int)(lastReportTime.AddSeconds(_settings.CooldownSeconds) - DateTime.UtcNow).TotalSeconds;
                player.PrintToChat(GetPrefix() + Localizer["CooldownMessage"].Value.Replace("{0}", waitTime.ToString()));
                data.Reset();
                return HookResult.Handled;
            }

            _playerReportCooldowns[steamId64] = DateTime.UtcNow;

            player.PrintToChat(GetPrefix() + Localizer["SucessfulReport"].Value.Replace("{0}", target.PlayerName).ReplaceColorTags());
            MenuManager.CloseActiveMenu(player);
            var reportData = new ReportData
            {
                ReporterName = player.PlayerName,
                ReporterSteamId = player.SteamID.ToString(),
                ReporterStats = GetPlayerStats(player),
                TargetName = target.PlayerName,
                TargetSteamId = target.SteamID.ToString(),
                TargetIp = target.IpAddress?.Split(':')[0] ?? "N/A",
                TargetStats = GetPlayerStats(target),
                Reason = msg,
                ServerName = serverName,
                ServerIp = serverIp
            };
            Task.Run(() => SendReportToDiscord(reportData));
            data.Reset();
            return HookResult.Handled;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[ReportSystem] Error in say listener: {ex}");
            return HookResult.Continue;
        }
    }

    private void OpenReportMenu(CCSPlayerController controller)
    {
        string playersMenuTitle = Localizer["ReportMenuTitle"];
        BaseMenu reportMenu = _settings.UseCenterHtmlMenu ? new CenterHtmlMenu(playersMenuTitle, this) : new ChatMenu(playersMenuTitle);

        var players = Utilities.GetPlayers().Where(p => p.IsValid && !p.IsBot && (_settings.Debug || p != controller)).ToList();

        if (players.Count == 0)
        {
            reportMenu.AddMenuOption(Localizer["NoPlayers"], (ctl, _) => { });
        }
        else
        {
            foreach (var player in players)
            {
                var capturedPlayer = player;
                if (VisibleInMenu(player) && !Config.Debug)
                {
                    reportMenu.AddMenuOption($"{capturedPlayer.PlayerName}", (ctl, _) =>
                    {
                        OpenReasonsMenu(ctl, capturedPlayer);
                    });
                }
                else reportMenu.AddMenuOption($"{capturedPlayer.PlayerName}", (ctl, _) =>
                {
                    OpenReasonsMenu(ctl, capturedPlayer);
                });
            }
        }
        reportMenu.Open(controller);
    }

    private void OpenReasonsMenu(CCSPlayerController controller, CCSPlayerController targetPlayer)
    {
        if (controller == null)
        {
            Console.WriteLine("[ReportSystem] controller is null in OpenReasonsMenu.");
            return;
        }
        if (targetPlayer == null)
        {
            Console.WriteLine("[ReportSystem] targetPlayer is null in OpenReasonsMenu.");
            return;
        }
        if (_settings == null)
        {
            Console.WriteLine("[ReportSystem] _settings is null in OpenReasonsMenu.");
            return;
        }
        if (_settings.DefaultReasons == null || _settings.DefaultReasons.Length == 0)
        {
            Console.WriteLine("[ReportSystem] DefaultReasons is null or empty in OpenReasonsMenu.");
            return;
        }

        string reasonsMenuTitle = Localizer["SelectReportReason"] ?? "Выберите причину репорта";
        BaseMenu reasonsMenu = _settings.UseCenterHtmlMenu ? new CenterHtmlMenu(reasonsMenuTitle, this) { PostSelectAction = PostSelectAction.Close } : new ChatMenu(reasonsMenuTitle) { PostSelectAction = PostSelectAction.Close };

        foreach (var reason in _settings.DefaultReasons)
        {
            reasonsMenu.AddMenuOption(reason, (player, _) =>
            {
                if (targetPlayer == null)
                {
                    player.PrintToChat(GetPrefix() + Localizer["TargetUnavailable"]);
                    return;
                }

                var serverName = ConVar.Find("hostname")?.StringValue ?? "Unknown Server";
                var serverIp = Config.ServerIP ?? "N/A";

                ulong steamId64 = player.SteamID;
                if (_playerReportCooldowns.TryGetValue(steamId64, out DateTime lastReportTime) && DateTime.UtcNow < lastReportTime.AddSeconds(_settings.CooldownSeconds))
                {
                    var waitTime = (int)(lastReportTime.AddSeconds(_settings.CooldownSeconds) - DateTime.UtcNow).TotalSeconds;
                    player.PrintToChat(GetPrefix() + Localizer["CooldownMessage"]);
                    return;
                }

                _playerReportCooldowns[steamId64] = DateTime.UtcNow;

                player.PrintToChat(GetPrefix() + Localizer["SuccessfulReport"]);
                string targetIp = "N/A";
                try
                {
                    targetIp = targetPlayer.IpAddress != null ? targetPlayer.IpAddress.Split(':')[0] : "N/A";
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"[ReportSystem] targetPlayer.IpAddress error: {ex.Message}");
                    targetIp = "N/A";
                }

                string targetStats = "N/A";
                try
                {
                    targetStats = GetPlayerStats(targetPlayer);
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"[ReportSystem] GetPlayerStats(targetPlayer) error: {ex.Message}");
                    targetStats = "N/A";
                }

                var reportData = new ReportData
                {
                    ReporterName = player.PlayerName,
                    ReporterSteamId = player.SteamID.ToString(),
                    ReporterStats = GetPlayerStats(player),
                    TargetName = targetPlayer.PlayerName,
                    TargetSteamId = targetPlayer.SteamID.ToString(),
                    TargetIp = targetIp,
                    TargetStats = targetStats,
                    Reason = reason,
                    ServerName = serverName,
                    ServerIp = serverIp
                };
                Task.Run(() => SendReportToDiscord(reportData));
            });
        }

        reasonsMenu.AddMenuOption(Localizer["CustomReason"], (player, _) =>
        {
            if (player == null)
            {
                Console.WriteLine("[ReportSystem] player is null in Custom Reason option.");
                return;
            }
            if (targetPlayer == null)
            {
                player.PrintToChat(GetPrefix() + Localizer["TargetUnavailable"]);
                return;
            }
            _selectedReason[player.Index] = new PersonTargetData
            {
                Target = (int)targetPlayer.Index,
                IsSelectedReason = true,
                AllowCustomReason = true
            };
            player.PrintToChat(GetPrefix() + Localizer["CustomReasonTutorial"]);
        });

        if (reasonsMenu is CenterHtmlMenu chm)
            chm.Open(controller);
        else
            MenuManager.OpenChatMenu(controller, (ChatMenu)reasonsMenu);
    }

    private string GetPlayerStats(CCSPlayerController player)
    {
        if (player == null || !player.IsValid) return "N/A";

        var stats = player.ActionTrackingServices?.MatchStats;
        if (stats == null) return "N/A";
        double kdaValue = (stats.Deaths == 0) ? (stats.Kills == 0 ? 0.0 : (double)stats.Kills) : (double)stats.Kills / stats.Deaths;
        string kda = kdaValue.ToString("F2");
        return $"K: {stats.Kills} | D: {stats.Deaths}\nK/D: {kda}";
    }


    private async Task SendReportToDiscord(ReportData data)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(_settings.WebhookUrl)) Console.WriteLine("[ReportSystem] Webhook URL is not configured.");

            using var httpClient = new HttpClient();
            var timezone = TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, TimeZoneInfo.FindSystemTimeZoneById("FLE Standard Time"));

            int discordColor = ParseColor(_settings.SelectedColor);
            var payload = new
            {

                content = _settings.Description,
                embeds = new[]
                {
                    new
                    {
                        title  = Localizer["ReportServer"] + data.ServerName,
                        description = "# 🔔 " + Localizer["ReportTitle"],
                        color  = discordColor,
                        fields = new[]
                        {
                            new {
                                name   = "👤 " + Localizer["ReportVictim"],
                                value  = $"[**{data.ReporterName}**](https://steamcommunity.com/profiles/{data.ReporterSteamId}/)",
                                inline = true
                            },
                            new {
                                name   = "📊 " + Localizer["ReportStatistic"],
                                value  = $"```{data.ReporterStats}```",
                                inline = false
                            },
                            new {
                                name   = "🎯 " + Localizer["ReportSuspect"],
                                value  = $"[**{data.TargetName}**](https://steamcommunity.com/profiles/{data.TargetSteamId}/)",
                                inline = false
                            },
                            new {
                                name   = "📊 " + Localizer["ReportStatistic"],
                                value  = $"```{data.TargetStats}```",
                                inline = true
                            },
                            new {
                                name   = "📝 " + Localizer["ReportReason"],
                                value  = $"**{data.Reason}**",
                                inline = false
                            },

                            new {
                                name   = "🆔 SteamID",
                                value  = $"```{data.TargetSteamId}```",
                                inline = true
                            },
                            new {
                                name   = "🌐 " + Localizer["ReportSuspectIP"],
                                value  = $"```{data.TargetIp}```",
                                inline = true
                            },
                            new{
                                name   = "🌐 " + Localizer["ReportConnectField"],
                                value  = $"```connect {Config.ServerIP}```",
                                inline = false
                            },
                        },
                        footer = new {
                            text = $"ReportSystem v{ModuleVersion}"
                        }
                    }
                }
            };

            var content = new StringContent(
                JsonSerializer.Serialize(payload),
                Encoding.UTF8,
                "application/json"
            );

            var response = await httpClient.PostAsync(_settings.WebhookUrl, content);
            if (!response.IsSuccessStatusCode)
            {
                Console.WriteLine($"[ReportSystem] Discord error: {response.StatusCode}");
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[ReportSystem] Discord send error: {ex.Message}");
        }
    }

    private int ParseColor(string input)
    {
        if (string.IsNullOrWhiteSpace(input))
            return 0xFFFFFF;
        string color = input.Trim().TrimStart('#');
        if (color.StartsWith("0X", StringComparison.OrdinalIgnoreCase))
            color = color.Substring(2);

        color = color.ToUpperInvariant();
        if (color.Length == 3)
            color = $"{color[0]}{color[0]}{color[1]}{color[1]}{color[2]}{color[2]}";
        if (color.Length == 6 || color.Length == 8)
        {
            try
            {
                string hex = color.Length == 6 ? "FF" + color : color;
                uint argb = Convert.ToUInt32(hex, 16);
                return (int)(argb & 0x00FFFFFF);
            }
            catch
            {

            }
        }
        try
        {
            string htmlHex = color.Length > 6 ? color.Substring(color.Length - 6, 6) : color;
            var sysColor = ColorTranslator.FromHtml("#" + htmlHex);
            return (sysColor.R << 16) | (sysColor.G << 8) | sysColor.B;
        }
        catch { }
        try
        {
            var c = Color.FromName(input.Trim());
            if (c.IsKnownColor || c.IsNamedColor)
                return (c.R << 16) | (c.G << 8) | c.B;
        }
        catch { }

        Console.WriteLine($"[ReportSystem] Unrecognized color format: {input}");
        return 0xFFFFFF;
    }
    private string GetTextInsideQuotes(string input)
    {
        int start = input.IndexOf('"');
        int end = input.LastIndexOf('"');
        return (start >= 0 && end > start) ? input.Substring(start + 1, end - start - 1) : string.Empty;
    }
}