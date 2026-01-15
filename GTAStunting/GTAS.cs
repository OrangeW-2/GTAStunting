using System;
using System.Collections.Generic;
using System.Windows.Forms;
using GTA;
using GTA.Math;
using GTA.Native;
using System.IO;
using GTA.UI;

namespace GTAStunting
{
    /// <summary>
    /// Main script for GTA Stunting mod. Handles input, settings, and coordinates all subsystems.
    /// </summary>
    public class GTAS : Script
    {
        #region Fields

        // State
        private static int notification_id;
        private static bool should_notify;
        private static bool time_frozen;
        private static bool ghost_town;
        private static bool invulnerable;
        private static bool unnoticable;
        private static bool wasRecording = false;
        private static bool showActionReplayWarning = false;
        private static HashSet<int> boostVehicleHashes;

        // Keyboard Controls
        private Keys key_save;
        private Keys key_load;
        private Keys key_save_momentum;
        private Keys key_load_momentum;
        private Keys key_spawn;
        private Keys key_jetpack;
        private Keys key_jetpack_forward;
        private Keys key_jetpack_backward;
        private Keys key_jetpack_up;
        private Keys key_jetpack_down;
        private Keys key_noticable;
        private Keys key_vulnerable;
        private Keys key_time;
        private Keys key_ghosttown;
        private Keys key_weather;
        private Keys key_save_second_vehicle;
        private Keys key_save_second_position;
        private Keys key_export_data;
        private Keys key_clear_attempts;
        private Keys key_show_stats;
        private Keys key_toggle_graph;
        private Keys key_save_ghost;
        private Keys key_import_ghosts;
        private Keys key_save_vehicle_config;
        private Keys key_load_vehicle_config;
        private Keys key_linear_steering;
        private Keys key_ghost_record;

        // Controller Bindings
        private GTA.Control controllerSave;
        private GTA.Control controllerLoad;
        private GTA.Control controllerSaveModifier;
        private GTA.Control controllerLoadModifier;
        private const GTA.Control ControlNone = (GTA.Control)(-1);

        #endregion

        #region Constructor

        public GTAS()
        {
            boostVehicleHashes = new HashSet<int>
            {
                Function.Call<int>(Hash.GET_HASH_KEY, "taxi"),
                Function.Call<int>(Hash.GET_HASH_KEY, "dynasty"),
                Function.Call<int>(Hash.GET_HASH_KEY, "eudora"),
                Function.Call<int>(Hash.GET_HASH_KEY, "broadway")
            };

            Tick += OnTick;
            Aborted += OnAborted;
            KeyDown += OnKeyDown;
            KeyUp += OnKeyUp;

            EnsureSettings();
            string iniPath = Path.ChangeExtension(Filename, ".ini");
            LoadSettings(ScriptSettings.Load(iniPath));

            Function.Call(Hash.DISABLE_STUNT_JUMP_SET, 0);
        }

        #endregion

        #region Event Handlers

        private void OnTick(object sender, EventArgs e)
        {
            Player player = Game.Player;
            Ped ped = player?.Character;
            if (ped == null || !ped.Exists()) return;

            // Update subsystems
            SpeedTracker.RecordTick();
            SpeedGraph.Update();
            SpeedGraph.Draw();
            SteeringManager.Update();
            GhostRecorder.Update();

            // Action Replay Check
            // Native source: https://github.com/scripthookvdotnet/scripthookvdotnet/blob/main/source/scripting_v3/GTA.Native/NativeHashes.cs
            bool isRecording = Function.Call<bool>((Hash)0x1897CA71995A90B4); // IS_REPLAY_RECORDING
            
            if (isRecording)
            {
                showActionReplayWarning = false;
            }
            else if (wasRecording && !isRecording)
            {
                showActionReplayWarning = true;
            }
            
            wasRecording = isRecording;

            if (showActionReplayWarning)
            {
                DrawText("~r~Action Replay stopped! Press F2 to restart.", 0.5f, 0.05f, 0.5f, 255, 0, 0);
            }

            Teleporter.OnTick();

            // Controller inputs for save/load (configurable via INI)
            // Only process if using a controller to avoid keyboard conflicts
            if (Game.LastInputMethod == InputMethod.GamePad)
            {
                // Save: Modifier + SaveButton (or just SaveButton if modifier is None)
                bool saveModPressed = (controllerSaveModifier == ControlNone) || Game.IsControlPressed(controllerSaveModifier);
                if (saveModPressed && Game.IsControlJustPressed(controllerSave))
                {
                    Entity controlledEntity = GetControlledEntity();
                    if (controlledEntity != null)
                        Teleporter.Save(controlledEntity);
                }

                // Load: Modifier + LoadButton
                bool loadModPressed = (controllerLoadModifier == ControlNone) || Game.IsControlPressed(controllerLoadModifier);
                if (loadModPressed && Game.IsControlJustPressed(controllerLoad))
                {
                    Teleporter.Load(ped);
                }
            }

            // Keep weather synced when time is frozen
            if (time_frozen && World.NextWeather != World.Weather)
            {
                World.NextWeather = World.Weather;
            }

            // Ghost town - remove traffic and peds
            if (ghost_town)
            {
                Function.Call(Hash.SET_PARKED_VEHICLE_DENSITY_MULTIPLIER_THIS_FRAME, 0);
                Function.Call(Hash.SET_PED_DENSITY_MULTIPLIER_THIS_FRAME, 0);
                Function.Call(Hash.SET_VEHICLE_DENSITY_MULTIPLIER_THIS_FRAME, 0);
            }

            // Jetpack movement
            if (Jetpack.is_active)
            {
                Vector3 direction = Vector3.Zero;
                Entity controlledEntity = GetControlledEntity();
                if (controlledEntity != null)
                {
                    if (Game.IsKeyPressed(key_jetpack_forward)) direction += controlledEntity.ForwardVector;
                    if (Game.IsKeyPressed(key_jetpack_backward)) direction -= controlledEntity.ForwardVector;
                    if (Game.IsKeyPressed(key_jetpack_up)) direction += Vector3.WorldUp;
                    if (Game.IsKeyPressed(key_jetpack_down)) direction += Vector3.WorldDown;
                    Jetpack.Move(direction);
                }
            }

            // Taxi boost handling
            Vehicle currentVehicle = ped.CurrentVehicle;
            if (currentVehicle != null && currentVehicle.Exists() && boostVehicleHashes.Contains(currentVehicle.Model.Hash))
            {
                if (TaxiBoost.boosting)
                    TaxiBoost.HandleWallStick(currentVehicle);

                if (Game.IsControlEnabled(GTA.Control.VehicleHeadlight))
                    Game.DisableControlThisFrame(GTA.Control.VehicleHeadlight);

                if (!TaxiBoost.boosting && Game.IsControlJustPressed(GTA.Control.VehicleHeadlight))
                {
                    TaxiBoost.Boost();
                    return;
                }

                if (TaxiBoost.boosting && Game.IsControlJustReleased(GTA.Control.VehicleHeadlight))
                    TaxiBoost.StopBoost();
            }
        }

        private void OnKeyDown(object sender, KeyEventArgs e)
        {
            // Currently unused, but required for event subscription
        }

        private void OnKeyUp(object sender, KeyEventArgs e)
        {
            Player player = Game.Player;
            Ped ped = player?.Character;
            if (ped == null || !ped.Exists()) return;

            // Teleporter controls
            if (e.KeyCode == key_save)
            {
                Entity entity = GetControlledEntity();
                if (entity != null) Teleporter.Save(entity);
                return;
            }
            if (e.KeyCode == key_load) { Teleporter.Load(ped); return; }
            if (e.KeyCode == key_save_momentum)
            {
                Entity entity = GetControlledEntity();
                if (entity != null) Teleporter.Save(entity);
                return;
            }
            if (e.KeyCode == key_load_momentum) { Teleporter.LoadWithSpeed(ped); return; }
            if (e.KeyCode == key_save_second_vehicle) { Teleporter.SaveSecondVehicle(); return; }
            if (e.KeyCode == key_save_second_position) { Teleporter.SaveSecondPosition(); return; }

            // Spawner and utilities
            if (e.KeyCode == key_spawn) { VehicleSpawner.Spawner(""); return; }
            if (e.KeyCode == key_jetpack) { Jetpack.Toggle(); return; }
            if (e.KeyCode == key_noticable) { ToggleNoticable(); return; }
            if (e.KeyCode == key_vulnerable) { ToggleVulnerable(); return; }
            if (e.KeyCode == key_time) { ToggleTimeFreeze(); return; }
            if (e.KeyCode == key_ghosttown) { ToggleGhostTown(); return; }
            if (e.KeyCode == key_weather) { CycleWeather(); return; }

            // Speed tracking controls
            if (e.KeyCode == key_export_data) { SpeedTracker.ExportToCSV(); return; }
            if (e.KeyCode == key_clear_attempts)
            {
                GhostRecorder.Clear();
                Notify("Ghost Recording Cleared");
                return;
            }
            if (e.KeyCode == key_show_stats) { SpeedTracker.ShowStats(); return; }
            if (e.KeyCode == key_toggle_graph) { SpeedGraph.Toggle(); return; }
            if (e.KeyCode == key_save_ghost) { SpeedTracker.SaveLastAttempt(); return; }
            if (e.KeyCode == key_import_ghosts)
            {
                string input = Game.GetUserInput("");
                if (!string.IsNullOrEmpty(input)) SpeedTracker.ImportFromCSV(input);
                return;
            }

            // Vehicle config controls
            if (e.KeyCode == key_save_vehicle_config)
            {
                Ped character = Game.Player.Character;
                if (character.IsInVehicle())
                {
                    string input = Game.GetUserInput("");
                    if (!string.IsNullOrEmpty(input))
                        VehiclePersistence.SaveVehicle(character.CurrentVehicle, input);
                }
                else
                {
                    Notify("Must be in a vehicle!");
                }
                return;
            }

            if (e.KeyCode == key_load_vehicle_config)
            {
                Ped character = Game.Player.Character;
                if (character.IsInVehicle())
                {
                    string input = Game.GetUserInput("");
                    if (!string.IsNullOrEmpty(input))
                        VehiclePersistence.LoadVehicle(character.CurrentVehicle, input);
                }
                else
                {
                    Notify("Must be in a vehicle!");
                }
                return;
            }

            // Other toggles
            if (e.KeyCode == key_linear_steering) { SteeringManager.Toggle(); return; }
            if (e.KeyCode == key_ghost_record) { GhostRecorder.ToggleRecording(); return; }
        }

        private void OnAborted(object sender, EventArgs e)
        {
            should_notify = false;

            // Clean up state on script abort
            if (time_frozen) ToggleTimeFreeze();
            if (ghost_town) ToggleGhostTown();
            if (unnoticable) ToggleNoticable();
            if (invulnerable) ToggleVulnerable();
            if (Jetpack.is_active) Jetpack.Toggle();

            // Delete spawned vehicles
            foreach (Vehicle v in VehicleSpawner.vehicles)
            {
                if (v != null) v.Delete();
            }
        }

        #endregion

        #region Settings

        private void LoadSettings(ScriptSettings settings)
        {
            // Position save/load
            Enum.TryParse(settings.GetValue("Controls", "Save Position", "Y"), out key_save);
            Enum.TryParse(settings.GetValue("Controls", "Load Position", "E"), out key_load);
            Enum.TryParse(settings.GetValue("Controls", "Save with Speed", "OemCloseBrackets"), out key_save_momentum);
            Enum.TryParse(settings.GetValue("Controls", "Load with Speed", "OemOpenBrackets"), out key_load_momentum);
            Enum.TryParse(settings.GetValue("Controls", "Save Second Vehicle", "N"), out key_save_second_vehicle);
            Enum.TryParse(settings.GetValue("Controls", "Save Second Position", "M"), out key_save_second_position);

            // Utilities
            Enum.TryParse(settings.GetValue("Controls", "Spawn Vehicle", "J"), out key_spawn);
            Enum.TryParse(settings.GetValue("Controls", "Toggle Jetpack", "B"), out key_jetpack);
            Enum.TryParse(settings.GetValue("Controls", "Toggle Noticable", "K"), out key_noticable);
            Enum.TryParse(settings.GetValue("Controls", "Toggle Vulnerable", "L"), out key_vulnerable);
            Enum.TryParse(settings.GetValue("Controls", "Toggle Time Freeze", "I"), out key_time);
            Enum.TryParse(settings.GetValue("Controls", "Toggle Ghost Town", "O"), out key_ghosttown);
            Enum.TryParse(settings.GetValue("Controls", "Cycle Weather", "U"), out key_weather);

            // Jetpack
            Enum.TryParse(settings.GetValue("Controls", "Jetpack Forward", "W"), out key_jetpack_forward);
            Enum.TryParse(settings.GetValue("Controls", "Jetpack Backward", "S"), out key_jetpack_backward);
            Enum.TryParse(settings.GetValue("Controls", "Jetpack Up", "Space"), out key_jetpack_up);
            Enum.TryParse(settings.GetValue("Controls", "Jetpack Down", "C"), out key_jetpack_down);

            // Speed tracking
            Enum.TryParse(settings.GetValue("Controls", "Export Data", "F5"), out key_export_data);
            Enum.TryParse(settings.GetValue("Controls", "Clear Attempts", "F6"), out key_clear_attempts);
            Enum.TryParse(settings.GetValue("Controls", "Show Stats", "F7"), out key_show_stats);
            Enum.TryParse(settings.GetValue("Controls", "Toggle Graph", "G"), out key_toggle_graph);
            Enum.TryParse(settings.GetValue("Controls", "Save Ghost", "F8"), out key_save_ghost);
            Enum.TryParse(settings.GetValue("Controls", "Import Ghosts", "F9"), out key_import_ghosts);

            // Vehicle config
            Enum.TryParse(settings.GetValue("Controls", "Save Vehicle Config", "F10"), out key_save_vehicle_config);
            Enum.TryParse(settings.GetValue("Controls", "Load Vehicle Config", "F11"), out key_load_vehicle_config);

            // Other
            Enum.TryParse(settings.GetValue("Controls", "Toggle Linear Steering", "F12"), out key_linear_steering);
            Enum.TryParse(settings.GetValue("Controls", "Ghost Record", "T"), out key_ghost_record);

            // Controller bindings
            // Helper for nullable parsing
            GTA.Control ParseControl(string section, string key, string defaultName)
            {
                 string val = settings.GetValue(section, key, defaultName);
                 if (val.Equals("None", StringComparison.OrdinalIgnoreCase)) return ControlNone;
                 if (Enum.TryParse(val, out GTA.Control result)) return result;
                 return ControlNone;
            }

            controllerSave = ParseControl("Controller", "Save", "ScriptPadDown");
            controllerLoad = ParseControl("Controller", "Load", "VehicleHorn");
            controllerSaveModifier = ParseControl("Controller", "SaveModifier", "None");
            controllerLoadModifier = ParseControl("Controller", "LoadModifier", "None");

            // Default states
            if (settings.GetValue("Defaults", "Frozen Time", false)) ToggleTimeFreeze();
            if (settings.GetValue("Defaults", "Ghost Town", false)) ToggleGhostTown();
            if (settings.GetValue("Defaults", "Invulnerable", false)) ToggleVulnerable();
            if (settings.GetValue("Defaults", "Unnoticable", false)) ToggleNoticable();

            Weather weather;
            if (Enum.TryParse(settings.GetValue("Defaults", "Weather", ""), out weather))
                World.Weather = weather;

            Function.Call(Hash.SET_CLOCK_TIME, settings.GetValue("Defaults", "Time of day", 12), 0, 0);

            Jetpack.LoadSettings(settings);
            should_notify = settings.GetValue("UI", "Notify", true);
        }

        #endregion

        #region Utility Methods

        /// <summary>
        /// Shows a notification to the player (if notifications are enabled).
        /// </summary>
        public static void Notify(string message)
        {
            if (should_notify)
            {
#pragma warning disable CS0618 // Type or member is obsolete
                Notification.Hide(notification_id);
                notification_id = Notification.Show(message, false);
#pragma warning restore CS0618 // Type or member is obsolete
            }
        }

        /// <summary>
        /// Returns the entity the player is currently controlling (vehicle or ped).
        /// </summary>
        public static Entity GetControlledEntity()
        {
            Player player = Game.Player;
            Ped ped = player?.Character;
            if (ped == null || !ped.Exists()) return null;

            if (ped.IsInVehicle())
            {
                Vehicle vehicle = ped.CurrentVehicle;
                if (vehicle != null && vehicle.Exists())
                    return vehicle;
            }
            return ped;
        }

        private void EnsureSettings()
        {
            string path = Path.ChangeExtension(Filename, ".ini");
            if (File.Exists(path)) return;

            string defaults = @"[Controls]
Save Position=Y
Load Position=E
Save with Speed=OemCloseBrackets
Load with Speed=OemOpenBrackets
Save Second Vehicle=N
Save Second Position=M
Spawn Vehicle=J
Toggle Jetpack=B
Toggle Noticable=K
Toggle Vulnerable=L
Toggle Time Freeze=I
Toggle Ghost Town=O
Cycle Weather=U
Jetpack Forward=W
Jetpack Backward=S
Jetpack Up=Space
Jetpack Down=C
Export Data=F5
Clear Attempts=F6
Show Stats=F7
Toggle Graph=G
Save Ghost=F8
Import Ghosts=F9
Save Vehicle Config=F10
Load Vehicle Config=F11
Toggle Linear Steering=F12
Ghost Record=T

[Controller]
;Teleport save and loading!
;Use 'None' for NO modifier
Save=ScriptPadDown
SaveModifier=None
Load=VehicleHorn
LoadModifier=None

[Defaults]
Frozen Time=True
Ghost Town=True
Invulnerable=True
Unnoticable=True
Weather=ExtraSunny
Time of day=12

[Jetpack]
Speed=10
Speed Multiplier=100
Speed Min=0
Speed Max=5000

[UI]
Notify=true";

            try
            {
                File.WriteAllText(path, defaults);
                Notify("Generated default settings file.");
            }
            catch (Exception ex)
            {
                Notify("Failed to generate settings: " + ex.Message);
            }
        }

        #endregion

        #region Toggle Methods

        private static void DrawText(string text, float x, float y, float scale, int r, int g, int b)
        {
            Function.Call(Hash.SET_TEXT_FONT, 4);
            Function.Call(Hash.SET_TEXT_SCALE, scale, scale);
            Function.Call(Hash.SET_TEXT_COLOUR, r, g, b, 255);
            Function.Call(Hash.SET_TEXT_CENTRE, true);
            Function.Call(Hash.SET_TEXT_OUTLINE);
            Function.Call(Hash.BEGIN_TEXT_COMMAND_DISPLAY_TEXT, "STRING");
            Function.Call(Hash.ADD_TEXT_COMPONENT_SUBSTRING_PLAYER_NAME, text);
            Function.Call(Hash.END_TEXT_COMMAND_DISPLAY_TEXT, x, y);
        }

        private static void ToggleTimeFreeze()
        {
            time_frozen = !time_frozen;
            Function.Call(Hash.PAUSE_CLOCK, time_frozen);
            Notify(time_frozen ? "Time & Weather Frozen" : "Time & Weather Unfrozen");
        }

        private static void ToggleGhostTown()
        {
            ghost_town = !ghost_town;
            Function.Call(Hash.SET_DISTANT_CARS_ENABLED, !ghost_town);
            Function.Call(Hash.DISABLE_VEHICLE_DISTANTLIGHTS, ghost_town);
            Notify(ghost_town ? "Ghost Town on" : "Ghost Town off");
        }

        private static void CycleWeather()
        {
            World.Weather = World.Weather != Weather.Halloween ? World.Weather + 1 : Weather.ExtraSunny;
            Notify("Weather set to " + World.Weather);
        }

        private static void ToggleVulnerable()
        {
            invulnerable = !invulnerable;
            Function.Call(Hash.SET_PLAYER_INVINCIBLE_BUT_HAS_REACTIONS, Game.Player, invulnerable);
            Notify(invulnerable ? "Invulnerable" : "Vulnerable");
        }

        private static void ToggleNoticable()
        {
            unnoticable = !unnoticable;
            Function.Call(Hash.SET_EVERYONE_IGNORE_PLAYER, Game.Player, unnoticable);
            Function.Call(Hash.SET_POLICE_IGNORE_PLAYER, Game.Player, unnoticable);
            Function.Call(Hash.SET_DISPATCH_COPS_FOR_PLAYER, Game.Player, !unnoticable);
            Game.MaxWantedLevel = unnoticable ? 0 : 5;
            Notify(unnoticable ? "Unnoticable" : "Noticable");
        }

        #endregion
    }
}
