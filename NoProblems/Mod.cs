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

namespace NoProblems
{
    public class Mod : BasePatcherMod<Mod>
    {
        public override string NameRaw => "No Problems Notifications";
        public override string Description => !IsBeta ? Localize.Mod_Description : CommonLocalize.Mod_DescriptionBeta;

        protected override ulong StableWorkshopId => 0ul;
        protected override ulong BetaWorkshopId => 0ul;
        public override string CrowdinUrl => "https://crowdin.com/translate/macsergey-other-mods/74";

        public override List<ModVersion> Versions { get; } = new List<ModVersion>
        {
            new ModVersion(new Version("1.0"), new DateTime()),
        };

        protected override Version RequiredGameVersion => new Version(1, 15, 0, 7);

#if BETA
        public override bool IsBeta => true;
#else
        public override bool IsBeta => false;
#endif
        protected override string IdRaw => nameof(NoProblems);

        protected override ResourceManager LocalizeManager => Localize.ResourceManager;

        protected override void GetSettings(UIHelperBase helper)
        {
            var settings = new Settings();
            settings.OnSettingsUI(helper);
        }
        protected override void SetCulture(CultureInfo culture) => Localize.Culture = culture;

        protected override bool PatchProcess()
        {
            var success = true;

            return success;
        }

        private void RemoveExistingProblems(ProblemStruct problem)
        {
            Singleton<SimulationManager>.instance.AddAction(() =>
            {
                var buildingBuffer = Singleton<BuildingManager>.instance.m_buildings.m_buffer;
                for (ushort i = 0; i < buildingBuffer.Length; i += 1)
                {
                    if (buildingBuffer[i].m_flags != 0)
                    {
                        var oldProblems = buildingBuffer[i].m_problems;
                        if (oldProblems.IsNone)
                        {
                            var newProblems = Notification.RemoveProblems(oldProblems, problem);

                            if (newProblems != oldProblems)
                            {
                                buildingBuffer[i].m_problems = newProblems;
                                Singleton<BuildingManager>.instance.UpdateNotifications(i, oldProblems, newProblems);
                            }
                        }
                    }
                }

                var nodeBuffer = Singleton<NetManager>.instance.m_nodes.m_buffer;
                for (ushort i = 0; i < nodeBuffer.Length; i += 1)
                {
                    if (nodeBuffer[i].m_flags != 0)
                    {
                        var oldProblems = nodeBuffer[i].m_problems;
                        if (oldProblems.IsNone)
                        {
                            var newProblems = Notification.RemoveProblems(oldProblems, problem);

                            if (nodeBuffer[i].m_problems != oldProblems)
                            {
                                nodeBuffer[i].m_problems = newProblems;
                                Singleton<NetManager>.instance.UpdateNodeNotifications(i, oldProblems, newProblems);
                            }
                        }
                    }
                }
            });
        }
    }

    public class Settings : BaseSettings<Mod>
    {
        public static ProblemStruct DisabledProblems { get; private set; }
        public static Dictionary<ProblemStruct, SavedBool> Data { get; } = new Dictionary<ProblemStruct, SavedBool>();
        static Settings()
        {
            DisabledProblems = ProblemStruct.None;

            foreach (var problem in ProblemStruct.All)
            {
                var saved = new SavedBool(problem.ToString(), SettingsFile, true, true);
                Data[problem] = saved;

                Set(problem, saved);
            }
        }

        private static void Set(ProblemStruct problem, bool disable)
        {
            if (disable)
                DisabledProblems |= problem;
            else
                DisabledProblems &= ~problem;
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

        protected override void FillSettings()
        {
            base.FillSettings();

            var groups = new Dictionary<Group, UIHelper>()
            {
                { Group.General, GeneralTab.AddGroup(CommonLocalize.Settings_General) },
                { Group.Other,GeneralTab.AddGroup(nameof(Group.Other)) },
                { Group.Сonsumption,GeneralTab.AddGroup(nameof(Group.Сonsumption)) },
                { Group.NotConnected,GeneralTab.AddGroup(nameof(Group.NotConnected)) },
                { Group.Disasters,GeneralTab.AddGroup(nameof(Group.Disasters)) },
                { Group.Parks,GeneralTab.AddGroup(nameof(Group.Parks)) },
                { Group.Industry,GeneralTab.AddGroup(nameof(Group.Industry)) },
                { Group.Campus,GeneralTab.AddGroup(nameof(Group.Campus)) },
                { Group.Fishing,GeneralTab.AddGroup(nameof(Group.Fishing)) },
                { Group.Airport,GeneralTab.AddGroup(nameof(Group.Airport)) },
                { Group.Pedestrian,GeneralTab.AddGroup(nameof(Group.Pedestrian)) },
            };

            var notificationAtlas = TextureHelper.GetAtlas("Notifications");

            foreach (var problem in ProblemStruct.All)
            {
                var title = GetTitle(problem);
                var icon = GetIcon(problem);
                var text = notificationAtlas.name != "Notifications" ? title : $"<sprite {icon}> {title}";
                var saved = Data[problem];

                var group = groups[GetGroup(problem)];
                var checkBox = AddCheckBox(group, string.Format(Localize.Setting_DisableProblem, text), saved, () => Set(problem, saved));
                var label = checkBox.Find<UILabel>("Label");
                label.atlas = notificationAtlas;
                label.processMarkup = true;
                label.autoSize = false;
                label.width += 30f;
            }
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
            Problem1.TooLong |
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
    }
}
