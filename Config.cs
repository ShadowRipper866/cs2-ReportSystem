using CounterStrikeSharp.API.Modules.Config;
using CounterStrikeSharp.API.Core;
using System.Text.Json.Serialization;
using System.Collections.Generic;

namespace ReportSystem
{
    public class ReportSystemConfig : BasePluginConfig
    {
        [JsonPropertyName("WebhookUrl")]
        public string WebhookUrl { get; set; } = "";

        [JsonPropertyName("FlagsToIgnore")]
        public List<string> FlagsToIgnore { get; set; } = new List<string>();

        [JsonPropertyName("UseCenterHtmlMenu")]
        public bool UseCenterHtmlMenu { get; set; } = true;

        [JsonPropertyName("SelectedColor")]
        public string SelectedColor { get; set; } = "#FFFFFF";

        [JsonPropertyName("Description")]
        public string Description { get; set; } = "May be role pings??";

        [JsonPropertyName("DefaultReasons")]
        public string[] DefaultReasons { get; set; } = new string[] { "Cheating", "Griefing", "AFK" };

        [JsonPropertyName("Debug")]
        public bool Debug { get; set; } = false;

        [JsonPropertyName("ServerIP")]
        public string ServerIP { get; set; } = "0.0.0.0:20000";

        public int CooldownSeconds { get; set; } = 30;
    }

    public class PersonTargetData
    {
        public int Target { get; set; }
        public string? IpAddress { get; }
        public bool IsSelectedReason { get; set; }
        public bool AllowCustomReason { get; set; } = false;

        public void Reset()
        {
            Target = -1;
            IsSelectedReason = false;
            AllowCustomReason = false;
        }
    }
    public class ReportData
    {
        public string ReporterName { get; set; } = "";
        public string ReporterSteamId { get; set; } = "";
        public string ReporterStats { get; set; } = "";
        public string TargetName { get; set; } = "";
        public string TargetSteamId { get; set; } = "";
        public string TargetIp { get; set; } = "";
        public string TargetStats { get; set; } = "";
        public string Reason { get; set; } = "";
        public string ServerName { get; set; } = "";
        public string ServerIp { get; set; } = "";
        public int ServerPort { get; set; }
    }
}
