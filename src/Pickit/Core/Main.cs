using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using AimBot.Utilities;
using ExileCore;
using ExileCore.PoEMemory;
using ExileCore.PoEMemory.Components;
using ExileCore.PoEMemory.MemoryObjects;
using ExileCore.Shared.Enums;
using ExileCore.Shared.Helpers;
using ImGuiNET;
using MoreLinq.Extensions;
using SharpDX;
using Player = ExileCore.PoEMemory.Components.Player;

namespace Aimbot.Core
{
    public class Main : BaseSettingsPlugin<Settings>
    {
        private const int PixelBorder = 3;
        private readonly Stopwatch _aimTimer = Stopwatch.StartNew();
        private readonly List<Entity> _entities = new List<Entity>();
        private bool _aiming;
        private Vector2 _clickWindowOffset;
        private bool _mouseWasHeldDown;
        private Vector2 _oldMousePos;
        private HashSet<string> _ignoredMonsters;
        private string _pluginDirectory;

        private readonly string[] _lightlessGrub =
        {
            "Metadata/Monsters/HuhuGrub/AbyssGrubMobile",
            "Metadata/Monsters/HuhuGrub/AbyssGrubMobileMinion"
        };

        private string _pluginVersion;

        private readonly string[] _raisedZombie =
        {
            "Metadata/Monsters/RaisedZombies/RaisedZombieStandard",
            "Metadata/Monsters/RaisedZombies/RaisedZombieMummy",
            "Metadata/Monsters/RaisedZombies/NecromancerRaisedZombieStandard"
        };

        private readonly string[] _summonedSkeleton =
        {
            "Metadata/Monsters/RaisedSkeletons/RaisedSkeletonStandard",
            "Metadata/Monsters/RaisedSkeletons/RaisedSkeletonStatue",
            "Metadata/Monsters/RaisedSkeletons/RaisedSkeletonMannequin",
            "Metadata/Monsters/RaisedSkeletons/RaisedSkeletonStatueMale",
            "Metadata/Monsters/RaisedSkeletons/RaisedSkeletonStatueGold",
            "Metadata/Monsters/RaisedSkeletons/RaisedSkeletonStatueGoldMale",
            "Metadata/Monsters/RaisedSkeletons/NecromancerRaisedSkeletonStandard",
            "Metadata/Monsters/RaisedSkeletons/TalismanRaisedSkeletonStandard"
        };

        //https://stackoverflow.com/questions/826777/how-to-have-an-auto-incrementing-version-number-visual-studio
        private readonly Version _version = Assembly.GetExecutingAssembly().GetName().Version;

        //public static Main Controller { get; set; }
        private DateTime _buildDate;

        public override bool Initialise()
        {
            Name = "Aim Bot";
            _pluginDirectory = DirectoryFullName;
            //Controller = this;
            _buildDate = new DateTime(2000, 1, 1).AddDays(_version.Build).AddSeconds(_version.Revision * 2);
            _pluginVersion = $"{_version}";
            _ignoredMonsters = LoadFile("Ignored Monsters");

            return true;
        }

        public override void Render()
        {
            base.Render();
            WeightDebug();
            if (Settings.ShowAimRange)
            {
                Vector3 pos = GameController.Game.IngameState.Data.LocalPlayer.GetComponent<Render>().Pos;
                DrawEllipseToWorld(pos, Settings.AimRange.Value, 25, 2, Color.LawnGreen);
            }

            try
            {
                if (Keyboard.IsKeyDown((int) Settings.AimKey.Value)
                    && !GameController.Game.IngameState.IngameUi.InventoryPanel.IsVisible
                    && !GameController.Game.IngameState.IngameUi.OpenLeftPanel.IsVisible)
                {
                    if (_aiming) return;
                    _aiming = true;
                    Aimbot();
                }
                else
                {
                    if (!_mouseWasHeldDown) return;
                    _mouseWasHeldDown = false;
                    if (Settings.RMousePos) Mouse.SetCursorPos(_oldMousePos);
                }
            }
            catch (Exception e)
            {
                LogError("Something went wrong? " + e, 5);
            }
        }

        private void WeightDebug()
        {
            if (!Settings.DebugMonsterWeight) return;
            foreach (var entity in GameController.Entities)
            {
                if (Convert.ToInt32(entity.DistancePlayer) < Settings.AimRange.Value &&
                    entity.HasComponent<Monster>() && entity.IsAlive)
                {
                    //LogMessage($"Entity DistancePlayer: {entity.DistancePlayer}", 1);
                    //LogMessage($"DistancePlayer: {Settings.AimRange.Value}", 1);
                    var camera = GameController.Game.IngameState.Camera;
                    var chestScreenCoords = camera.WorldToScreen(entity.Pos.Translate(0, 0, -170));
                    if (chestScreenCoords == new Vector2()) continue;
                    var iconRect = new Vector2(chestScreenCoords.X, chestScreenCoords.Y);
                    float maxWidth = 0;
                    float maxheight = 0;

                    // draw weight
                    Graphics.DrawText(AimWeightEb(entity).ToString(CultureInfo.InvariantCulture), iconRect, Color.White,
                        15, FontAlign.Center);
                    chestScreenCoords.Y += 15;
                    maxheight += 15;
                    maxWidth = Math.Max(maxWidth, 15);
                    var background = new RectangleF(chestScreenCoords.X - maxWidth / 2 - 3,
                        chestScreenCoords.Y - maxheight, maxWidth + 6,
                        maxheight);
                    Graphics.DrawBox(background, Color.Black);
                }
            }
        }

        private void DrawEllipseToWorld(Vector3 vector3Pos, int radius, int points, int lineWidth, Color color)
        {
            var camera = GameController.Game.IngameState.Camera;
            var plottedCirclePoints = new List<Vector3>();
            var slice = 2 * Math.PI / points;
            for (var i = 0; i < points; i++)
            {
                var angle = slice * i;
                var x = (decimal) vector3Pos.X + decimal.Multiply(radius, (decimal) Math.Cos(angle));
                var y = (decimal) vector3Pos.Y + decimal.Multiply(radius, (decimal) Math.Sin(angle));
                plottedCirclePoints.Add(new Vector3((float) x, (float) y, vector3Pos.Z));
            }

            //Entity rndEntity =
            //    GameController.Entities.FirstOrDefault(x =>
            //        x.HasComponent<Render>() && GameController.Player.Address != x.Address);
            for (var i = 0; i < plottedCirclePoints.Count; i++)
            {
                if (i >= plottedCirclePoints.Count - 1)
                {
                    Vector2 pointEnd1 = camera.WorldToScreen(plottedCirclePoints.Last() /*, rndEntity*/);
                    Vector2 pointEnd2 = camera.WorldToScreen(plottedCirclePoints[0] /*, rndEntity*/);
                    Graphics.DrawLine(pointEnd1, pointEnd2, lineWidth, color);
                    return;
                }

                Vector2 point1 = camera.WorldToScreen(plottedCirclePoints[i] /*, rndEntity*/);
                Vector2 point2 = camera.WorldToScreen(plottedCirclePoints[i + 1] /*, rndEntity*/);
                Graphics.DrawLine(point1, point2, lineWidth, color);
            }
        }

        public override void EntityAdded(Entity entity)
        {
            _entities.Add(entity);
        }

        public override void EntityRemoved(Entity entity)
        {
            _entities.Remove(entity);
        }

        private HashSet<string> LoadFile(string fileName)
        {
            string file = $@"{_pluginDirectory}\{fileName}.txt";
            //string file = $@"{fileName}.txt";
            if (!File.Exists(file))
            {
                LogError($@"Failed to find {file}", 10);
                return null;
            }

            HashSet<string> hashSet = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            string[] lines = File.ReadAllLines(file);
            lines.Where(x => !string.IsNullOrWhiteSpace(x) && !x.StartsWith("#")).ForEach(x => hashSet.Add(x.Trim()));
            return hashSet;
        }

        private bool IsIgnoredMonster(string path)
        {
            return _ignoredMonsters.Any(ignoreString => path.ToLower().Contains(ignoreString.ToLower()));
        }

        private void Aimbot()
        {
            if (_aimTimer.ElapsedMilliseconds < Settings.AimLoopDelay)
            {
                _aiming = false;
                return;
            }

            if (Settings.AimPlayers)
                PlayerAim();
            else
                MonsterAim();
            _aimTimer.Restart();
            _aiming = false;
        }


        // TODO: useless
        //public int TryGetStat(string playerStat)
        //{
        //    return !GameController.EntityListWrapper.PlayerStats.TryGetValue(
        //        GameController.Files.Stats.records[playerStat].ID, out var statValue)
        //        ? 0
        //        : statValue;
        //}

        private int TryGetStat(string playerStat, Entity entity)
        {
            return !entity.GetComponent<Stats>().StatDictionary
                .TryGetValue((GameStat) GameController.Files.Stats.records[playerStat].ID,
                    out var statValue) // TODO: test typecast (GameStat)
                ? 0
                : statValue;
        }

        private void PlayerAim()
        {
            var player = GameController.Player;

            var alivePlayers = _entities
                .Where(x => x.HasComponent<Player>()
                            && x.IsAlive
                            && x.Address != AimBot.Utilities.Player.entity_.Address
                            && TryGetStat("ignored_by_enemy_target_selection", x) == 0
                            && TryGetStat("cannot_die", x) == 0
                            && TryGetStat("cannot_be_damaged", x) == 0)
                .Select(x => new Tuple<float, Entity>(Misc.EntityDistance(x, player), x))
                .OrderBy(x => x.Item1)
                .ToList();
            var closestMonster = alivePlayers.FirstOrDefault(x => x.Item1 < Settings.AimRange);
            if (closestMonster == null) return;
            if (!_mouseWasHeldDown)
            {
                _oldMousePos = Mouse.GetCursorPositionVector();
                _mouseWasHeldDown = true;
            }

            if (closestMonster.Item1 >= Settings.AimRange)
            {
                _aiming = false;
                return;
            }

            var camera = GameController.Game.IngameState.Camera;
            var entityPosToScreen =
                camera.WorldToScreen(closestMonster.Item2.Pos.Translate(0, 0, 0) /*, closestMonster.Item2*/);
            var vectWindow = GameController.Window.GetWindowRectangle();
            if (entityPosToScreen.Y + PixelBorder > vectWindow.Bottom ||
                entityPosToScreen.Y - PixelBorder < vectWindow.Top)
            {
                _aiming = false;
                return;
            }

            if (entityPosToScreen.X + PixelBorder > vectWindow.Right ||
                entityPosToScreen.X - PixelBorder < vectWindow.Left)
            {
                _aiming = false;
                return;
            }

            _clickWindowOffset = GameController.Window.GetWindowRectangle().TopLeft;
            Mouse.SetCursorPos(entityPosToScreen + _clickWindowOffset);
        }

        private static bool HasAnyBuff(Entity entity, string[] buffList, bool contains = false)
        {
            if (!entity.HasComponent<Life>()) return false;
            foreach (Buff buff in entity.GetComponent<Life>().Buffs)
            {
                if (buffList.Any(
                    searchedBuff => contains ? buff.Name.Contains(searchedBuff) : searchedBuff == buff.Name))
                    return true;
            }

            return false;
        }

        private static bool HasAnyBuff(List<Buff> entityBuffs, string[] buffList, bool contains = false)
        {
            if (entityBuffs.Count <= 0) return false;
            foreach (Buff buff in entityBuffs)
            {
                if (buffList.Any(
                    searchedBuff => contains ? buff.Name.Contains(searchedBuff) : searchedBuff == buff.Name))
                    return true;
            }

            return false;
        }

        private static bool HasAnyMagicAttribute(List<string> entitiesMagicMods, string[] magicList,
            bool contains = false)
        {
            if (entitiesMagicMods.Count <= 0) return false;
            foreach (var buff in entitiesMagicMods)
            {
                foreach (var magicSearch in magicList)
                {
                    if (contains ? !buff.Contains(magicSearch) : magicSearch != buff) continue;
                    //LogMessage($"{buff} Contains {magicSearch}", 1);
                    return true;
                }
            }

            return false;
        }

        private void MonsterAim()
        {
            var aliveAndHostile = _entities?.Where(x => x.HasComponent<Monster>()
                                                        && x.IsAlive
                                                        && x.IsHostile
                                                        && !IsIgnoredMonster(x.Path)
                                                        && TryGetStat(
                                                            "ignored_by_enemy_target_selection",
                                                            x) == 0
                                                        && TryGetStat("cannot_die", x) ==
                                                        0
                                                        && TryGetStat("cannot_be_damaged",
                                                            x) == 0
                                                        && !HasAnyBuff(x, new[]
                                                        {
                                                            "capture_monster_captured",
                                                            "capture_monster_disappearing"
                                                        }))
                .Select(x => new Tuple<float, Entity>(AimWeightEb(x), x))
                .OrderByDescending(x => x.Item1)
                .ToList();
            if (aliveAndHostile?.FirstOrDefault(x => x.Item1 < Settings.AimRange) != null)
            {
                var heightestWeightedTarget =
                    aliveAndHostile.FirstOrDefault(x => x.Item1 < Settings.AimRange);
                if (!_mouseWasHeldDown)
                {
                    _oldMousePos = Mouse.GetCursorPositionVector();
                    _mouseWasHeldDown = true;
                }

                if (heightestWeightedTarget != null && heightestWeightedTarget.Item1 >= Settings.AimRange)
                {
                    _aiming = false;
                    return;
                }

                var camera = GameController.Game.IngameState.Camera;
                if (heightestWeightedTarget == null) return;
                var entityPosToScreen =
                    camera.WorldToScreen(
                        heightestWeightedTarget.Item2.Pos.Translate(0, 0, 0));
                var vectWindow = GameController.Window.GetWindowRectangle();
                if (entityPosToScreen.Y + PixelBorder > vectWindow.Bottom ||
                    entityPosToScreen.Y - PixelBorder < vectWindow.Top)
                {
                    _aiming = false;
                    return;
                }

                if (entityPosToScreen.X + PixelBorder > vectWindow.Right ||
                    entityPosToScreen.X - PixelBorder < vectWindow.Left)
                {
                    _aiming = false;
                    return;
                }

                _clickWindowOffset = GameController.Window.GetWindowRectangle().TopLeft;
                Mouse.SetCursorPos(entityPosToScreen + _clickWindowOffset);
            }
        }

        private float AimWeightEb(Entity entity)
        {
            var player = GameController.Player;
            //weight -= Misc.EntityDistance(entity, player) / 10;
            var weight = Misc.EntityDistance(entity, player) / 10;
            //LogMessage($"Mob weight: {weight}", 1);
            var rarity = entity.GetComponent<ObjectMagicProperties>().Rarity;
            var monsterMagicProperties = new List<string>();
            if (entity.HasComponent<ObjectMagicProperties>())
                monsterMagicProperties = entity.GetComponent<ObjectMagicProperties>().Mods;
            var monsterBuffs = new List<Buff>();
            if (entity.HasComponent<Life>()) monsterBuffs = entity.GetComponent<Life>().Buffs;
            if (HasAnyMagicAttribute(monsterMagicProperties, new[]
            {
                "AuraCannotDie"
            }, true))
                weight += Settings.CannotDieAura;
            if (entity.GetComponent<Life>().HasBuff("capture_monster_trapped"))
                weight += Settings.CaptureMonsterTrapped;
            if (entity.GetComponent<Life>().HasBuff("harbinger_minion_new")) weight += Settings.HarbingerMinionWeight;
            if (entity.GetComponent<Life>().HasBuff("capture_monster_enraged"))
                weight += Settings.CaptureMonsterEnraged;
            if (entity.Path.Contains("/BeastHeart")) weight += Settings.BeastHearts;
            if (entity.Path == "Metadata/Monsters/Tukohama/TukohamaShieldTotem") weight += Settings.TukohamaShieldTotem;
            if (HasAnyMagicAttribute(monsterMagicProperties, new[]
            {
                "MonsterRaisesUndeadText"
            }))
            {
                weight += Settings.RaisesUndead;
            }

            // Experimental, seems like a buff only strongbox monsters get
            if (HasAnyBuff(monsterBuffs, new[]
            {
                "summoned_monster_epk_buff"
            }))
            {
                weight += Settings.StrongBoxMonster;
            }

            switch (rarity)
            {
                case MonsterRarity.Unique:
                    weight += Settings.UniqueRarityWeight;
                    break;
                case MonsterRarity.Rare:
                    weight += Settings.RareRarityWeight;
                    break;
                case MonsterRarity.Magic:
                    weight += Settings.MagicRarityWeight;
                    break;
                case MonsterRarity.White:
                    weight += Settings.NormalRarityWeight;
                    break;
                case MonsterRarity.Error:
                    break;
                default:
                    throw new ArgumentOutOfRangeException();
            }

            if (entity.HasComponent<SoundParameterBreach>()) weight += Settings.BreachMonsterWeight;
            if (entity.HasComponent<DiesAfterTime>()) weight += Settings.DiesAfterTime;
            if (_summonedSkeleton.Any(path => entity.Path == path)) weight += Settings.SummonedSkeoton;
            if (_raisedZombie.Any(path => entity.Path == path)) weight += Settings.RaisedZombie;
            if (_lightlessGrub.Any(path => entity.Path == path)) weight += Settings.LightlessGrub;
            if (entity.Path.Contains("TaniwhaTail")) weight += Settings.TaniwhaTail;

            return weight;
        }
    }

    public class SoundParameterBreach : Component
    {
    }
}