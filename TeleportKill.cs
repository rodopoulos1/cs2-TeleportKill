using CounterStrikeSharp.API;
using CounterStrikeSharp.API.Core;
using CounterStrikeSharp.API.Core.Attributes.Registration;
using CounterStrikeSharp.API.Modules.Entities;
using CounterStrikeSharp.API.Core.Attributes;
using CounterStrikeSharp.API.Modules.Admin;

namespace TeleportKillPlugin
{
    using System.Text.Json.Serialization;
    using CounterStrikeSharp.API.Core.Attributes.Registration;

    public class TeleportKillConfig : BasePluginConfig
    {
        [JsonPropertyName("TeleportHeight")]
        public float TeleportHeight { get; set; } = 10.0f;

        [JsonPropertyName("EnableChatMessage")]
        public bool EnableChatMessage { get; set; } = true;

        [JsonPropertyName("TeleportChance")]
        public int TeleportChance { get; set; } = 100; // 0 to 100

        [JsonPropertyName("TeleportFlags")]
        public List<string> TeleportFlags { get; set; } = new();

        [JsonPropertyName("TeleportCooldownSeconds")]
        public int TeleportCooldownSeconds { get; set; } = 0; // 0 = no cooldown

        [JsonPropertyName("TeleportsPerRound")]
        public int TeleportsPerRound { get; set; } = 0; // 0 = unlimited
    }

    public class TeleportKill : BasePlugin, IPluginConfig<TeleportKillConfig>
    {
        public override string ModuleName => "Teleport Kill";
        public override string ModuleVersion => "1.0.0";
        public override string ModuleAuthor => "Rodopoulos";

        public TeleportKillConfig Config { get; set; } = new();

        public void OnConfigParsed(TeleportKillConfig config)
        {
            Config = config;
        }

        // Cooldown and teleports per round control
        private readonly Dictionary<ulong, DateTime> _lastTeleport = new();
        private readonly Dictionary<ulong, int> _teleportsThisRound = new();

        [GameEventHandler]
        public HookResult OnPlayerDeath(EventPlayerDeath @event, GameEventInfo info)
        {
            var victim = @event.Userid;
            var killer = @event.Attacker;

            if (killer == null || victim == null)
                return HookResult.Continue;

            if (killer == victim)
                return HookResult.Continue; // ignore suicide

            ulong killerId = killer.SteamID;

            // Cooldown
            if (Config.TeleportCooldownSeconds > 0)
            {
                if (_lastTeleport.TryGetValue(killerId, out var lastTime))
                {
                    var diff = DateTime.UtcNow - lastTime;
                    if (diff.TotalSeconds < Config.TeleportCooldownSeconds)
                        return HookResult.Continue;
                }
            }

            // Per round limit
            if (Config.TeleportsPerRound > 0)
            {
                if (_teleportsThisRound.TryGetValue(killerId, out var count) && count >= Config.TeleportsPerRound)
                    return HookResult.Continue;
            }

            // Permission flags check
            if (Config.TeleportFlags != null && Config.TeleportFlags.Count > 0)
            {
                if (!AdminManager.PlayerHasPermissions(killer, Config.TeleportFlags.ToArray()))
                    return HookResult.Continue;
            }

            // Chance system
            var rand = new Random();
            int sorteio = rand.Next(1, 101); // 1 to 100
            if (sorteio > Config.TeleportChance)
                return HookResult.Continue;

            // Get victim position
            var victimPos = victim.PlayerPawn.Value?.AbsOrigin;
            if (victimPos == null)
                return HookResult.Continue;

            // Teleport killer
            var newPos = victimPos;
            newPos.Z += Config.TeleportHeight;
            killer.PlayerPawn.Value?.Teleport(newPos, killer.PlayerPawn.Value.AbsRotation, killer.PlayerPawn.Value.AbsVelocity);

            // Update cooldown and round count
            if (Config.TeleportCooldownSeconds > 0)
                _lastTeleport[killerId] = DateTime.UtcNow;
            if (Config.TeleportsPerRound > 0)
                _teleportsThisRound[killerId] = _teleportsThisRound.TryGetValue(killerId, out var c) ? c + 1 : 1;

            // Chat message if enabled
            if (Config.EnableChatMessage)
            {
                string mensagem = Localizer["Prefix"] + Localizer["TeleportSuccess", killer.PlayerName, victim.PlayerName];
                Server.PrintToChatAll(mensagem);
            }

            return HookResult.Continue;
        }

        // Reset teleport count per round
        [GameEventHandler]
        public HookResult OnRoundStart(EventRoundStart @event, GameEventInfo info)
        {
            _teleportsThisRound.Clear();
            return HookResult.Continue;
        }
    }
}
