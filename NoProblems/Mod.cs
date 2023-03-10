using ColossalFramework;
using ICities;
using ModsCommon;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Resources;
using UnityEngine;
using static Notification;
using static ModsCommon.SettingsHelper;
using static RenderManager;
using static ColossalFramework.EnumExtensions;
using ColossalFramework.UI;
using ModsCommon.Utilities;
using ModsCommon.UI;

namespace NoProblems
{
    public class Mod : BasePatcherMod<Mod>
    {
        public override string NameRaw => "No Problem Notifications";
        public override string Description => !IsBeta ? Localize.Mod_Description : CommonLocalize.Mod_DescriptionBeta;

        protected override ulong StableWorkshopId => 2866992009ul;
        protected override ulong BetaWorkshopId => 0ul;
        public override string CrowdinUrl => "https://crowdin.com/translate/intersection-marking-tool/136";

        public override List<ModVersion> Versions { get; } = new List<ModVersion>
        {
            new ModVersion(new Version("2.0"), new DateTime(2022,9,24)),
            new ModVersion(new Version("2.1"), new DateTime(2022,9,25)),
        };

        protected override Version RequiredGameVersion => new Version(1, 16, 0, 3);

#if BETA
        public override bool IsBeta => true;
#else
        public override bool IsBeta => false;
#endif
        protected override string IdRaw => nameof(NoProblems);

        protected override List<BaseDependencyInfo> DependencyInfos
        {
            get
            {
                var infos = base.DependencyInfos;

                infos.Add(new ConflictDependencyInfo(DependencyState.Unsubscribe, new IdSearcher(917543381ul)));
                infos.Add(new ConflictDependencyInfo(DependencyState.Unsubscribe, new IdSearcher(2864220279ul)));

                return infos;
            }
        }

        protected override LocalizeManager LocalizeManager => Localize.LocaleManager;

        protected override void GetSettings(UIHelperBase helper)
        {
            var settings = new Settings();
            settings.OnSettingsUI(helper);
        }
        protected override void SetCulture(CultureInfo culture) => Localize.Culture = culture;

        protected override bool PatchProcess()
        {
            var success = true;

            success &= AddPrefix(typeof(Mod), nameof(Mod.NotificationRenderInstancePrefix), typeof(Notification), nameof(Notification.RenderInstance));
            success &= AddPrefix(typeof(Mod), nameof(Mod.NotificationAddProblemsPrefix), typeof(Notification), nameof(Notification.AddProblems));

            return success;
        }

        public void RemoveExistingProblems(ProblemStruct disabledProblems)
        {
            Logger.Debug("Start removing existing problems");

            Singleton<SimulationManager>.instance.AddAction(() =>
            {
                var buildingBuffer = Singleton<BuildingManager>.instance.m_buildings.m_buffer;
                for (ushort i = 0; i < buildingBuffer.Length; i += 1)
                {
                    if ((buildingBuffer[i].m_flags & Building.Flags.Created) != 0)
                    {
                        var oldProblems = buildingBuffer[i].m_problems;
                        if (oldProblems.IsNotNone)
                        {
                            var newProblems = Notification.RemoveProblems(oldProblems, disabledProblems);

                            if (newProblems != oldProblems)
                            {
                                Logger.Debug($"Remove problems from building #{i}");
                                buildingBuffer[i].m_problems = newProblems;
                                Singleton<BuildingManager>.instance.UpdateNotifications(i, oldProblems, newProblems);
                            }
                        }
                    }
                }

                //var nodeBuffer = Singleton<NetManager>.instance.m_nodes.m_buffer;
                //for (ushort i = 0; i < nodeBuffer.Length; i += 1)
                //{
                //    if ((nodeBuffer[i].m_flags & NetNode.Flags.Created) != 0)
                //    {
                //        var oldProblems = nodeBuffer[i].m_problems;
                //        if (oldProblems.IsNotNone)
                //        {
                //            var newProblems = Notification.RemoveProblems(oldProblems, disabledProblems);

                //            if (newProblems != oldProblems)
                //            {
                //                Logger.Debug($"Remove problems from node #{i}");
                //                nodeBuffer[i].m_problems = newProblems;
                //                Singleton<NetManager>.instance.UpdateNodeNotifications(i, oldProblems, newProblems);
                //            }
                //        }
                //    }
                //}
            });

            Logger.Debug("Finish removing existing problems");
        }

        private static void NotificationRenderInstancePrefix(ref ProblemStruct problems)
        {
            if (Settings.HidingEnabled)
            {
                switch (Settings.HideType)
                {
                    case 0:
                    case 2:
                        problems &= Settings.EnabledProblems;
                        break;
                    case 1 when (problems & ProblemStruct.MajorOrFatal).IsNone:
                        problems &= Settings.EnabledProblems;
                        break;
                }
            }
        }
        private static void NotificationAddProblemsPrefix(ref ProblemStruct problems2)
        {
            if (Settings.HideType == 2)
                problems2 &= Settings.EnabledProblems;
        }
    }

    public class ThreadingExtension : ThreadingExtensionBase
    {
        public override void OnUpdate(float realTimeDelta, float simulationTimeDelta)
        {
            if (!UIView.HasModalInput() && !UIView.HasInputFocus() && Settings.ToggleShortcut.IsPressed)
            {
                SingletonMod<Mod>.Logger.Debug($"On press shortcut");
                Settings.HidingEnabled.value = !Settings.HidingEnabled.value;
            }
        }
    }
    public class LoadingExtension : LoadingExtensionBase
    {
        public override void OnLevelLoaded(LoadMode mode)
        {
            if (mode == LoadMode.LoadGame || mode == LoadMode.NewGame || mode == LoadMode.NewGameFromScenario)
                SingletonMod<Mod>.Instance.RemoveExistingProblems(~Settings.EnabledProblems);
        }
    }

    public class NoProblemShortcut : ModShortcut<Mod>
    {
        public NoProblemShortcut(string name, string labelKey, InputKey key, Action action = null) : base(name, labelKey, key, action) { }
    }


    public class Settings : BaseSettings<Mod>
    {
        public static NoProblemShortcut ToggleShortcut { get; } = new NoProblemShortcut(nameof(ToggleShortcut), nameof(Localize.Setting_ToggleShortcut), SavedInputKey.Encode(KeyCode.N, true, true, false));

        public static ProblemStruct EnabledProblems { get; private set; }
        public static Dictionary<ProblemStruct, SavedBool> Data { get; } = new Dictionary<ProblemStruct, SavedBool>();
        public static SavedBool HidingEnabled { get; } = new SavedBool(nameof(HidingEnabled), SettingsFile, true, true);
        public static SavedInt HideType { get; } = new SavedInt(nameof(HideType), SettingsFile, 0, true);

        static Settings()
        {
            EnabledProblems = ProblemStruct.All;

            foreach (var problem in ProblemStruct.All)
            {
                var saved = new SavedBool(problem.ToString(), SettingsFile, true, true);
                Data[problem] = saved;

                Set(problem, saved);
            }
        }

        private static void Set(ProblemStruct problem, bool disable)
        {
            if (!disable)
                EnabledProblems |= problem;
            else
                EnabledProblems &= ~problem;
        }
        private static string GetTitle(ProblemStruct problem)
        {
            if ((problem & (new ProblemStruct(Problem1.StructureVisited | Problem1.StructureVisitedService))).IsNotNone)
                return ColossalFramework.Globalization.Locale.Get("NOTIFICATION_VISITED");
            else
            {
                var key = problem.m_Problems1 != Problem1.None ? problem.m_Problems1.Name("Text") : problem.m_Problems2.Name("Text");
                var title = ColossalFramework.Globalization.Locale.Get("NOTIFICATION_TITLE", key).Trim();
                return title;
            }
        }
        private static string GetIcon(ProblemStruct problem)
        {
            return problem.m_Problems1 != Problem1.None ? problem.m_Problems1.Name("Normal") : problem.m_Problems2.Name("Normal");
        }
        private static Group GetGroup(ProblemStruct problem)
        {
            if ((problem & GeneralProblems).IsNotNone)
                return Group.General;
            else if ((problem & СonsumptionProblems).IsNotNone)
                return Group.Сonsumption;
            else if ((problem & NotConnectedProblems).IsNotNone)
                return Group.NotConnected;
            else if ((problem & DisastersProblems).IsNotNone)
                return Group.Disasters;
            else if ((problem & ParksProblems).IsNotNone)
                return Group.Parks;
            else if ((problem & IndustryProblems).IsNotNone)
                return Group.Industry;
            else if ((problem & CampusProblems).IsNotNone)
                return Group.Campus;
            else if ((problem & FishingProblems).IsNotNone)
                return Group.Fishing;
            else if ((problem & AirportProblems).IsNotNone)
                return Group.Airport;
            else if ((problem & PedestrianProblems).IsNotNone)
                return Group.Pedestrian;
            else
                return Group.Other;
        }
        private static ProblemStruct GetProblems(Group group)
        {
            switch (group)
            {
                case Group.General: return GeneralProblems;
                case Group.Other: return OtherProblems;
                case Group.Сonsumption: return СonsumptionProblems;
                case Group.NotConnected: return NotConnectedProblems;
                case Group.Disasters: return DisastersProblems;
                case Group.Parks: return ParksProblems;
                case Group.Industry: return IndustryProblems;
                case Group.Campus: return CampusProblems;
                case Group.Fishing: return FishingProblems;
                case Group.Airport: return AirportProblems;
                case Group.Pedestrian: return PedestrianProblems;
                default: return ProblemStruct.None;
            }
        }

        protected override void FillSettings()
        {
            base.FillSettings();
            AddLanguage(GeneralTab);

            var toggles = new Dictionary<ProblemStruct, ToggleSettingsItem>();
            var generalGroup = GeneralTab.AddOptionsGroup(CommonLocalize.Settings_General);

            AddKeyMappingButton(generalGroup, ToggleShortcut);

            AddTogglePanel(generalGroup, Localize.Setting_HideType, HideType, new string[] { Localize.Setting_HideAny, Localize.Setting_HideNormal, Localize.Setting_Remove }, OnDisabledChanged);

            var color = new Color32(255, 215, 81, 255);
            var description = string.Format(Localize.Setting_HideDescription, Localize.Setting_HideAny.AddColor(color), Localize.Setting_HideNormal.AddColor(color), Localize.Setting_Remove.AddColor(color));
            var descrItem = AddLabel(generalGroup, description, 0.8f);
            descrItem.Borders = SettingsContentItem.Border.None;
            descrItem.LabelItem.processMarkup = true;

            AddSpace(generalGroup, 15f);

            var buttonPanel = AddButtonPanel(generalGroup, new RectOffset(0, 0, 5, 5), 10);
            AddButton(buttonPanel, Localize.Settings_DisableAll, () => Switch(toggles, ProblemStruct.All, true), 250, 1f);
            AddButton(buttonPanel, Localize.Settings_EnableAll, () => Switch(toggles, ProblemStruct.All, false), 250, 1f);


            var groups = new Dictionary<Group, UIHelper>()
            {
                { Group.General, AddOptionsGroup(Group.General) },
                { Group.Other, AddOptionsGroup(Group.Other) },
                { Group.Сonsumption, AddOptionsGroup(Group.Сonsumption) },
                { Group.NotConnected, AddOptionsGroup(Group.NotConnected) },
                { Group.Disasters, AddOptionsGroup(Group.Disasters) },
                { Group.Parks, AddOptionsGroup(Group.Parks) },
                { Group.Industry, AddOptionsGroup(Group.Industry) },
                { Group.Campus, AddOptionsGroup(Group.Campus) },
                { Group.Fishing, AddOptionsGroup(Group.Fishing) },
                { Group.Airport, AddOptionsGroup(Group.Airport) },
                { Group.Pedestrian, AddOptionsGroup(Group.Pedestrian) },
            };

            foreach (var groupKV in groups)
            {
                var group = groupKV.Key;
                var helper = groupKV.Value;

                var buttonGroup = AddButtonPanel(helper, new RectOffset(0, 0, 5, 15), 10);
                AddButton(buttonGroup, Localize.Settings_DisableEntireGrope, () => Switch(toggles, GetProblems(group), true), 250, 1f);
                AddButton(buttonGroup, Localize.Settings_EnableEntireGrope, () => Switch(toggles, GetProblems(group), false), 250, 1f);
            }

            var notificationAtlas = TextureHelper.GetAtlas("Notifications");

            foreach (var problem in ProblemStruct.All & ~Ignore)
            {
                var title = GetTitle(problem);
                var icon = GetIcon(problem);
                var text = notificationAtlas.name != "Notifications" ? title : $"<sprite {icon}> {title}";
                var saved = Data[problem];

                var group = groups[GetGroup(problem)];
                var toggle = AddToggle(group, string.Format(Localize.Setting_DisableProblem, text), saved);
                toggle.LabelItem.atlas = notificationAtlas;
                toggle.LabelItem.processMarkup = true;
                toggle.Control.OnStateChanged += (value) =>
                {
                    Set(problem, saved);
                    OnDisabledChanged();
                };

                toggles[problem] = toggle;
            }
        }
        private UIHelper AddOptionsGroup(Group group) => GeneralTab.AddOptionsGroup(SingletonMod<Mod>.Instance.GetLocalizedString($"Settings_{group}Group"));

        private bool SwitchInProgress { get; set; } = false;
        private void OnDisabledChanged()
        {
            if (!SwitchInProgress && HideType == 2)
                SingletonMod<Mod>.Instance.RemoveExistingProblems(~EnabledProblems);
        }
        private void Switch(Dictionary<ProblemStruct, ToggleSettingsItem> toggles, ProblemStruct problems, bool state)
        {
            SwitchInProgress = true;
            foreach (var problem in problems)
            {
                if (toggles.TryGetValue(problem, out var toggle))
                    toggle.State = state;
            }
            SwitchInProgress = false;

            if (state)
                OnDisabledChanged();
        }

        private enum Group
        {
            General,
            Other,
            Сonsumption,
            NotConnected,
            Disasters,
            Parks,
            Industry,
            Campus,
            Fishing,
            Airport,
            Pedestrian,
        }

        private static ProblemStruct GeneralProblems = new ProblemStruct(
            Problem1.Garbage |
            Problem1.Electricity |
            Problem1.Water |
            Problem1.Fire |
            Problem1.DirtyWater |
            Problem1.Crime |
            Problem1.Pollution |
            Problem1.Sewage |
            Problem1.Death |
            Problem1.Noise |
            Problem1.Flood |
            Problem1.Snow |
            Problem1.Heating,
            Problem2.CannotBeReached
            );

        private static ProblemStruct OtherProblems = new ProblemStruct(
            Problem1.TurnedOff |
            Problem1.TooFewServices |
            Problem1.LandValueLow |
            Problem1.LandfillFull |
            Problem1.Emptying |
            Problem1.TaxesTooHigh |
            Problem1.EmptyingFinished |
            Problem1.WasteTransferFacilityFull
            );

        private static ProblemStruct СonsumptionProblems = new ProblemStruct(
            Problem1.NoFuel |
            Problem1.NoCustomers |
            Problem1.NoResources |
            Problem1.NoGoods |
            Problem1.NoPlaceforGoods |
            Problem1.NoWorkers |
            Problem1.NoEducatedWorkers
            );

        private static ProblemStruct NotConnectedProblems = new ProblemStruct(
            Problem1.RoadNotConnected |
            Problem1.ElectricityNotConnected |
            Problem1.WaterNotConnected |
            Problem1.LineNotConnected |
            Problem1.DepotNotConnected |
            Problem1.HeatingNotConnected |
            Problem1.TrackNotConnected |
            Problem1.PathNotConnected |
            Problem1.NoTrolleybusWires
            );

        private static ProblemStruct DisastersProblems = new ProblemStruct(
            Problem1.StructureDamaged |
            Problem1.StructureVisited |
            Problem1.StructureVisitedService |
            Problem1.NoFood |
            Problem1.Evacuating
            );

        private static ProblemStruct ParksProblems = new ProblemStruct(
            Problem1.NoPark |
            Problem1.NoMainGate
            );

        private static ProblemStruct IndustryProblems = new ProblemStruct(
            Problem1.NotInIndustryArea |
            Problem1.WrongAreaType |
            Problem1.ResourceNotSelected |
            Problem1.NoNaturalResources |
            Problem1.NoInputProducts
            );

        private static ProblemStruct CampusProblems = new ProblemStruct(
            Problem1.WrongCampusAreaType |
            Problem1.NotInCampusArea |
            Problem1.PathNotConnectedCampus
            );

        private static ProblemStruct FishingProblems = new ProblemStruct(
            Problem1.FishingRouteIncomplete |
            Problem1.FishFarmWaterDirty |
            Problem1.NoFishingGoods |
            Problem1.FishingRouteWaterDirty |
            Problem1.FishingRouteInefficient |
            Problem1.NoPlaceForFishingGoods
            );

        private static ProblemStruct AirportProblems = new ProblemStruct(
            Problem1.NotInAirportArea |
            Problem1.PathNotConnectedAirport |
            Problem1.NoTerminal
            );

        private static ProblemStruct PedestrianProblems = new ProblemStruct(
            Problem2.NotInPedestrianZone |
            Problem2.PedestrianZoneHighCargoTraffic |
            Problem2.PedestrianZoneHighGarbageTraffic |
            Problem2.NoCargoServicePoint |
            Problem2.NoGarbageServicePoint
            );

        private static ProblemStruct Ignore = new ProblemStruct(Problem1.TooLong);
    }
}
