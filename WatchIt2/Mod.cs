using ColossalFramework;
using ColossalFramework.UI;
using ICities;
using ModsCommon;
using ModsCommon.Settings;
using System;
using System.Collections.Generic;
using System.Globalization;
using UnityEngine;
using static Notification;
using static ModsCommon.Settings.Helper;
using static ColossalFramework.EnumExtensions;
using ModsCommon.Utilities;
using ModsCommon.UI;

namespace WatchIt2
{
    public class Mod : BasePatcherMod<Mod>
    {
        public override string NameRaw => "Watch It!";
        public override string Description => !IsBeta ? Localize.Mod_Description : CommonLocalize.Mod_DescriptionBeta;

        protected override ulong StableWorkshopId => 3725814542ul;
        protected override ulong BetaWorkshopId => 0ul;
        public override string CrowdinUrl => "https://crowdin.com/translate/intersection-marking-tool/136";

        public override List<ModVersion> Versions { get; } = new List<ModVersion>
        {
            new ModVersion(new Version("2.0"), new DateTime(2026, 5, 14)),
        };

        protected override Version RequiredGameVersion => new Version(1, 21, 1, 9);

#if BETA
        public override bool IsBeta => true;
#else
        public override bool IsBeta => false;
#endif
        protected override string IdRaw => nameof(WatchIt2);

        protected override List<BaseDependencyInfo> DependencyInfos
        {
            get
            {
                var infos = base.DependencyInfos;

                infos.Add(new ConflictDependencyInfo(DependencyState.Unsubscribe, new IdSearcher(917543381ul), "Original No Problem Notifications"));
                infos.Add(new ConflictDependencyInfo(DependencyState.Unsubscribe, new IdSearcher(2864220279ul), "No Problem Notifications fix"));
                infos.Add(new ConflictDependencyInfo(DependencyState.Unsubscribe, new IdSearcher(2866992009ul), "No Problem Notifications 2 by MacSergey"));

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
            problems = Settings.GetRenderedProblems(problems);
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
            if (!ColossalFramework.UI.UIView.HasModalInput() && !ColossalFramework.UI.UIView.HasInputFocus() && Settings.ToggleShortcut.IsPressed)
            {
                SingletonMod<Mod>.Logger.Debug($"On press shortcut");
                Settings.HidingEnabled.value = !Settings.HidingEnabled.value;
                ProblemPanel.RefreshProblemPanel();
            }
        }
    }
    public class LoadingExtension : LoadingExtensionBase
    {
        public override void OnLevelLoaded(LoadMode mode)
        {
            if (mode == LoadMode.LoadGame || mode == LoadMode.NewGame || mode == LoadMode.NewGameFromScenario)
            {
                SingletonMod<Mod>.Instance.RemoveExistingProblems(~Settings.EnabledProblems);
                InformationPanel.Create();
                ProblemPanel.Create();
            }
        }
        public override void OnLevelUnloading()
        {
            InformationPanel.Release();
            ProblemLocalizationPanel.Release();
        }
    }

    public class ProblemLocalizationPanel : CustomUIPanel
    {
        private const float PanelWidth = 580f;
        private const float PanelHeight = 580f;
        private const float HeaderHeight = 30f;
        private const float FilterButtonHeight = 26f;
        private const float ScrollbarSize = 16f;
        private const float CloseButtonSize = 24f;
        private const float PaddingSize = 8f;
        private const float RowHeight = 24f;
        private const float NameWidth = 400f;
        private const float IconSize = 18f;
        private const float IconSpacing = 3f;
        private const float RefreshInterval = 5f;
        private const string TitleText = "Problem localization - click on a building or network to focus on it";

        private static ProblemLocalizationPanel Instance { get; set; }

        private CustomUIButton CloseButton { get; set; }
        private ProblemLocalizationFilterButton BuildingsButton { get; set; }
        private ProblemLocalizationFilterButton NetworksButton { get; set; }
        private CustomUIDragHandle DragBackground { get; set; }
        private CustomUIScrollablePanel Content { get; set; }
        private UITextureAtlas NotificationAtlas { get; set; }
        private List<ProblemLocalizationItem> Items { get; } = new List<ProblemLocalizationItem>();
        private ProblemLocalizationFilter ActiveFilter { get; set; } = ProblemLocalizationFilter.Buildings;
        private bool IsPanelDragActive { get; set; }
        private bool PanelDragMoved { get; set; }
        private Vector3 LastPanelDragPosition { get; set; }
        private float LastRefreshTime { get; set; }

        public static void Toggle(UIComponent source)
        {
            if (Instance != null && Instance.isVisible)
            {
                Release();
                return;
            }

            Create(source);
        }
        public static void Create(UIComponent source)
        {
            var view = UIView.GetAView();
            if (view == null)
                return;

            if (Instance == null)
                Instance = view.AddUIComponent(typeof(ProblemLocalizationPanel)) as ProblemLocalizationPanel;

            Instance.isVisible = true;
            Instance.BringToFront();
            Instance.SetDefaultPosition();
            Instance.RefreshNow();
        }
        public static void Release()
        {
            if (Instance != null)
            {
                UnityEngine.Object.Destroy(Instance.gameObject);
                Instance = null;
            }
        }
        public static void RefreshProblemLocalizationPanel()
        {
            Instance?.RefreshNow();
        }

        public override void Awake()
        {
            base.Awake();

            name = TitleText.TrimStart();
            gameObject.name = name;
            size = new Vector2(PanelWidth, PanelHeight);
            isInteractive = true;
            clipChildren = true;

            Atlas = CommonTextures.Atlas;
            BackgroundSprite = CommonTextures.PanelSmall;
            NormalBgColor = new Color32(48, 52, 54, 238);
            HoveredBgColor = NormalBgColor;
            DisabledBgColor = NormalBgColor;

            NotificationAtlas = TextureHelper.GetAtlas("Notifications");

            DragBackground = AddUIComponent<CustomUIDragHandle>();
            DragBackground.name = "Problem localization drag background";
            DragBackground.target = this;
            DragBackground.constrainToScreen = true;
            DragBackground.size = size;
            DragBackground.relativePosition = Vector3.zero;
            DragBackground.isInteractive = true;

            eventMouseDown += OnPanelMouseDown;
            eventMouseMove += OnPanelMouseMove;
            eventMouseUp += OnPanelMouseUp;

            var title = AddUIComponent<CustomUILabel>();
            title.name = "Problem localization title";
            title.AutoSize = AutoSize.None;
            title.size = new Vector2(PanelWidth - PaddingSize * 2f - CloseButtonSize - 4f, HeaderHeight);
            title.relativePosition = new Vector2(PaddingSize, 0f);
            title.text = TitleText;
            title.textScale = 0.9f;
            title.textColor = Color.white;
            title.VerticalAlignment = UIVerticalAlignment.Middle;
            title.HorizontalAlignment = UIHorizontalAlignment.Left;
            title.useDropShadow = true;
            title.dropShadowColor = new Color32(0, 0, 0, 192);
            title.dropShadowOffset = new Vector2(1f, -1f);
            title.isInteractive = false;

            CloseButton = AddUIComponent<CloseProblemLocalizationButton>();
            CloseButton.name = "Close problem localization button";
            CloseButton.relativePosition = new Vector2(PanelWidth - PaddingSize - CloseButtonSize, (HeaderHeight - CloseButtonSize) * 0.5f);
            CloseButton.eventClick += OnCloseButtonClick;

            var filterButtonWidth = (PanelWidth - PaddingSize * 2f - ScrollbarSize) * 0.5f;

            BuildingsButton = AddUIComponent<ProblemLocalizationFilterButton>();
            BuildingsButton.name = "Problem localization buildings filter";
            BuildingsButton.text = "Buildings";
            BuildingsButton.size = new Vector2(filterButtonWidth, FilterButtonHeight);
            BuildingsButton.relativePosition = new Vector2(PaddingSize, HeaderHeight);
            BuildingsButton.eventClick += OnBuildingsButtonClick;

            NetworksButton = AddUIComponent<ProblemLocalizationFilterButton>();
            NetworksButton.name = "Problem localization networks filter";
            NetworksButton.text = "Networks";
            NetworksButton.size = new Vector2(filterButtonWidth, FilterButtonHeight);
            NetworksButton.relativePosition = new Vector2(PaddingSize + filterButtonWidth, HeaderHeight);
            NetworksButton.eventClick += OnNetworksButtonClick;

            Content = AddUIComponent<CustomUIScrollablePanel>();
            Content.name = "Problem localization list";
            Content.size = new Vector2(PanelWidth - PaddingSize * 2f, PanelHeight - HeaderHeight - FilterButtonHeight - PaddingSize);
            Content.relativePosition = new Vector2(PaddingSize, HeaderHeight + FilterButtonHeight);
            Content.AutoLayout = AutoLayout.Vertical;
            Content.AutoLayoutSpace = 0;
            Content.AutoChildrenHorizontally = AutoLayoutChildren.Fill;
            Content.ScrollOrientation = UIOrientation.Vertical;
            Content.Scrollbar.DefaultStyle();
            Content.ScrollbarSize = ScrollbarSize;
            Content.ShowScroll = true;
            Content.isInteractive = true;
            Content.clipChildren = true;
            Content.eventMouseDown += OnPanelMouseDown;
            Content.eventMouseMove += OnPanelMouseMove;
            Content.eventMouseUp += OnPanelMouseUp;

            RefreshFilterButtons();
        }
        public override void Update()
        {
            base.Update();

            if (Time.realtimeSinceStartup - LastRefreshTime >= RefreshInterval)
                RefreshNow();
        }
        protected override void OnResolutionChanged(Vector2 previousResolution, Vector2 currentResolution)
        {
            base.OnResolutionChanged(previousResolution, currentResolution);
            CheckPosition();
        }

        private void SetDefaultPosition()
        {
            var view = UIView.GetAView();
            var resolution = view?.GetScreenResolution() ?? new Vector2(1920f, 1080f);
            relativePosition = new Vector3(Mathf.Max(0f, (resolution.x - width) * 0.5f), Mathf.Max(0f, (resolution.y - height) * 0.5f));
            CheckPosition();
        }
        private void CheckPosition()
        {
            var view = UIView.GetAView();
            var resolution = view?.GetScreenResolution() ?? new Vector2(1920f, 1080f);

            var position = relativePosition;
            position.x = Mathf.Clamp(position.x, 0f, Mathf.Max(0f, resolution.x - width));
            position.y = Mathf.Clamp(position.y, 0f, Mathf.Max(0f, resolution.y - height));
            relativePosition = position;
        }
        private void BeginPanelDrag(UIMouseEventParameter eventParam)
        {
            if ((eventParam.buttons & UIMouseButton.Left) == 0)
                return;

            var plane = new Plane(transform.TransformDirection(Vector3.back), transform.position);
            if (!plane.Raycast(eventParam.ray, out var enter))
                return;

            BringToFront();
            IsPanelDragActive = true;
            PanelDragMoved = false;
            LastPanelDragPosition = eventParam.ray.origin + eventParam.ray.direction * enter;
        }
        private void MovePanelDrag(UIMouseEventParameter eventParam)
        {
            if (!IsPanelDragActive || (eventParam.buttons & UIMouseButton.Left) == 0)
            {
                IsPanelDragActive = false;
                return;
            }

            var view = GetUIView();
            if (view == null)
                return;

            var plane = new Plane(view.uiCamera.transform.TransformDirection(Vector3.back), LastPanelDragPosition);
            if (!plane.Raycast(eventParam.ray, out var enter))
                return;

            var currentPosition = eventParam.ray.origin + eventParam.ray.direction * enter;
            var delta = currentPosition - LastPanelDragPosition;
            if (delta.sqrMagnitude <= 0.000001f)
                return;

            PanelDragMoved = true;
            transform.position += delta;
            CheckPosition();
            LastPanelDragPosition = currentPosition;
            eventParam.Use();
        }
        private void EndPanelDrag(UIMouseEventParameter eventParam)
        {
            IsPanelDragActive = false;
            MakePixelPerfect();
            CheckPosition();
        }
        private bool ConsumePanelDragClick()
        {
            if (!PanelDragMoved)
                return false;

            PanelDragMoved = false;
            return true;
        }
        private void OnPanelMouseDown(UIComponent component, UIMouseEventParameter eventParam)
        {
            BeginPanelDrag(eventParam);
        }
        private void OnPanelMouseMove(UIComponent component, UIMouseEventParameter eventParam)
        {
            MovePanelDrag(eventParam);
        }
        private void OnPanelMouseUp(UIComponent component, UIMouseEventParameter eventParam)
        {
            EndPanelDrag(eventParam);
        }
        private void OnCloseButtonClick(UIComponent component, UIMouseEventParameter eventParam)
        {
            if (eventParam.used)
                return;

            if (ConsumePanelDragClick())
            {
                eventParam.Use();
                return;
            }

            Release();
            eventParam.Use();
        }
        private void OnBuildingsButtonClick(UIComponent component, UIMouseEventParameter eventParam)
        {
            SetFilter(ProblemLocalizationFilter.Buildings, eventParam);
        }
        private void OnNetworksButtonClick(UIComponent component, UIMouseEventParameter eventParam)
        {
            SetFilter(ProblemLocalizationFilter.Networks, eventParam);
        }
        private void SetFilter(ProblemLocalizationFilter filter, UIMouseEventParameter eventParam)
        {
            if (eventParam.used)
                return;

            ActiveFilter = filter;
            RefreshFilterButtons();
            RefreshNow();
            eventParam.Use();
        }
        private void RefreshFilterButtons()
        {
            if (BuildingsButton != null)
                BuildingsButton.IsSelected = ActiveFilter == ProblemLocalizationFilter.Buildings;

            if (NetworksButton != null)
                NetworksButton.IsSelected = ActiveFilter == ProblemLocalizationFilter.Networks;
        }
        private void RefreshNow()
        {
            LastRefreshTime = Time.realtimeSinceStartup;

            try
            {
                var locations = ReadProblemLocations();
                var itemIndex = 0;

                foreach (var location in locations)
                {
                    if (!IsLocationVisible(location))
                        continue;

                    var item = GetItem(itemIndex);
                    item.Set(location, NotificationAtlas);
                    item.isVisible = true;
                    itemIndex += 1;
                }

                for (var i = itemIndex; i < Items.Count; i += 1)
                    Items[i].isVisible = false;

                Content.Reset();
            }
            catch (Exception e)
            {
                SingletonMod<Mod>.Logger.Error(e);
            }
        }
        private bool IsLocationVisible(ProblemLocation location)
        {
            switch (ActiveFilter)
            {
                case ProblemLocalizationFilter.Networks:
                    return location.Type == ProblemLocationType.Network;
                case ProblemLocalizationFilter.Buildings:
                default:
                    return location.Type != ProblemLocationType.Network;
            }
        }
        private ProblemLocalizationItem GetItem(int index)
        {
            while (Items.Count <= index)
            {
                var item = Content.AddUIComponent<ProblemLocalizationItem>();
                item.name = "Problem localization item";
                item.Owner = this;
                item.Height = RowHeight;
                Items.Add(item);
            }

            return Items[index];
        }
        private static List<ProblemLocation> ReadProblemLocations()
        {
            var locations = new Dictionary<string, ProblemLocation>();

            var buildingManager = Singleton<BuildingManager>.instance;
            var buildingBuffer = buildingManager.m_buildings.m_buffer;
            for (ushort i = 0; i < buildingBuffer.Length; i += 1)
            {
                if ((buildingBuffer[i].m_flags & Building.Flags.Created) == 0)
                    continue;

                var problems = Settings.GetBuildingPanelProblems(buildingBuffer[i]);
                if (problems.IsNone)
                    continue;

                var name = buildingManager.GetBuildingName(i, InstanceID.Empty);
                if (string.IsNullOrEmpty(name))
                    name = $"Building #{i}";

                AddLocation(locations, $"building:{i}", name, problems, new InstanceID { Building = i }, buildingBuffer[i].m_position, ProblemLocationType.Building);
            }

            var netManager = Singleton<NetManager>.instance;
            var nodeBuffer = netManager.m_nodes.m_buffer;
            var segmentBuffer = netManager.m_segments.m_buffer;
            for (ushort i = 0; i < nodeBuffer.Length; i += 1)
            {
                if ((nodeBuffer[i].m_flags & NetNode.Flags.Created) == 0)
                    continue;

                var problems = Settings.GetPanelProblems(nodeBuffer[i].m_problems);
                if (problems.IsNone)
                    continue;

                var segment = GetNamedSegment(nodeBuffer[i]);
                var key = segment != 0 ? $"network:{segment}" : $"node:{i}";
                var name = GetNetworkName(netManager, nodeBuffer, i, segment);

                var position = segment != 0 ? netManager.m_segments.m_buffer[segment].m_middlePosition : nodeBuffer[i].m_position;
                AddLocation(locations, key, name, problems, segment != 0 ? new InstanceID { NetSegment = segment } : new InstanceID { NetNode = i }, position, ProblemLocationType.Network);
            }

            for (ushort i = 0; i < segmentBuffer.Length; i += 1)
            {
                if ((segmentBuffer[i].m_flags & NetSegment.Flags.Created) == 0)
                    continue;

                var problems = Settings.GetNetworkSegmentPanelProblems(segmentBuffer[i]);
                if (problems.IsNone)
                    continue;

                var name = GetNetworkName(netManager, nodeBuffer, segmentBuffer[i].m_startNode, i);
                AddLocation(locations, $"network:{i}", name, problems, new InstanceID { NetSegment = i }, segmentBuffer[i].m_middlePosition, ProblemLocationType.Network);
            }

            var result = new List<ProblemLocation>(locations.Values);
            result.Sort((a, b) => string.Compare(a.Name, b.Name, StringComparison.CurrentCultureIgnoreCase));
            return result;
        }
        private static void AddLocation(Dictionary<string, ProblemLocation> locations, string key, string name, ProblemStruct problems, InstanceID instance, Vector3 position, ProblemLocationType type)
        {
            if (locations.TryGetValue(key, out var location))
            {
                location.Problems |= problems;
                return;
            }

            locations[key] = new ProblemLocation(name, problems, instance, position, type);
        }
        private static string GetNetworkName(NetManager netManager, NetNode[] nodeBuffer, ushort node, ushort segment)
        {
            if (segment != 0)
            {
                var name = netManager.GetSegmentName(segment);
                if (!string.IsNullOrEmpty(name))
                    return name;

                name = GetNetInfoName(netManager.m_segments.m_buffer[segment].m_infoIndex);
                if (!string.IsNullOrEmpty(name))
                    return name;
            }

            var nodeName = GetNetInfoName(nodeBuffer[node].m_infoIndex);
            if (!string.IsNullOrEmpty(nodeName))
                return nodeName;

            return segment != 0 ? $"Network #{segment}" : $"Network #{node}";
        }
        private static string GetNetInfoName(ushort infoIndex)
        {
            if (infoIndex == 0)
                return null;

            var info = PrefabCollection<NetInfo>.GetPrefab(infoIndex);
            if (info == null)
                return null;

            var name = info.GetLocalizedTitle();
            if (!string.IsNullOrEmpty(name))
                return name;

            return info.name;
        }
        private static ushort GetNamedSegment(NetNode node)
        {
            if (node.m_segment0 != 0) return node.m_segment0;
            if (node.m_segment1 != 0) return node.m_segment1;
            if (node.m_segment2 != 0) return node.m_segment2;
            if (node.m_segment3 != 0) return node.m_segment3;
            if (node.m_segment4 != 0) return node.m_segment4;
            if (node.m_segment5 != 0) return node.m_segment5;
            if (node.m_segment6 != 0) return node.m_segment6;
            if (node.m_segment7 != 0) return node.m_segment7;

            return 0;
        }

        private class ProblemLocation
        {
            public string Name { get; }
            public ProblemStruct Problems { get; set; }
            public InstanceID Instance { get; }
            public Vector3 Position { get; }
            public ProblemLocationType Type { get; }

            public ProblemLocation(string name, ProblemStruct problems, InstanceID instance, Vector3 position, ProblemLocationType type)
            {
                Name = name;
                Problems = problems;
                Instance = instance;
                Position = position;
                Type = type;
            }
        }

        private enum ProblemLocalizationFilter
        {
            Buildings,
            Networks,
        }

        private enum ProblemLocationType
        {
            Building,
            Network,
        }

        private class ProblemLocalizationFilterButton : CustomUIButton
        {
            public override void Awake()
            {
                base.Awake();

                isInteractive = true;
                clipChildren = true;

                BgAtlas = CommonTextures.Atlas;
                BgSprites = CommonTextures.PanelSmall;
                SelBgSprites = CommonTextures.PanelSmall;
                BgColors = new ColorSet(
                    new Color32(48, 52, 54, 224),
                    new Color32(73, 78, 87, 255),
                    new Color32(82, 88, 98, 255),
                    new Color32(73, 78, 87, 255),
                    new Color32(48, 52, 54, 224));
                SelBgColors = new ColorSet(
                    new Color32(73, 78, 87, 255),
                    new Color32(88, 96, 108, 255),
                    new Color32(96, 105, 119, 255),
                    new Color32(88, 96, 108, 255),
                    new Color32(73, 78, 87, 255));

                AllTextColors = Color.white;
                TextHorizontalAlignment = UIHorizontalAlignment.Center;
                TextVerticalAlignment = UIVerticalAlignment.Middle;
                textScale = 0.78f;
                useDropShadow = true;
                dropShadowColor = new Color32(0, 0, 0, 192);
                dropShadowOffset = new Vector2(1f, -1f);
            }
        }

        private class CloseProblemLocalizationButton : CustomUIButton
        {
            public override void Awake()
            {
                base.Awake();

                size = new Vector2(CloseButtonSize, CloseButtonSize);
                isInteractive = true;
                clipChildren = true;

                Atlas = CommonTextures.Atlas;
                BgSprites = new SpriteSet(CommonTextures.CloseButtonNormal, CommonTextures.CloseButtonHovered, CommonTextures.CloseButtonPressed, CommonTextures.CloseButtonNormal, string.Empty);
                text = string.Empty;
                tooltip = string.Empty;
            }
        }

        private class ProblemLocalizationItem : CustomUIPanel
        {
            public ProblemLocalizationPanel Owner { private get; set; }

            private CustomUILabel NameLabel { get; set; }
            private List<CustomUISprite> Icons { get; } = new List<CustomUISprite>();
            private int VisibleIconCount { get; set; }
            private ProblemLocation Location { get; set; }

            public float Height
            {
                set => size = new Vector2(PanelWidth - PaddingSize * 2f, value);
            }

            public override void Awake()
            {
                base.Awake();

                isInteractive = true;
                eventClick += OnItemClick;
                eventMouseDown += OnItemMouseDown;
                eventMouseMove += OnItemMouseMove;
                eventMouseUp += OnItemMouseUp;

                Atlas = CommonTextures.Atlas;
                BackgroundSprite = CommonTextures.PanelSmall;
                NormalBgColor = new Color32(48, 52, 54, 0);
                HoveredBgColor = Color.white;
                DisabledBgColor = NormalBgColor;

                NameLabel = AddUIComponent<CustomUILabel>();
                NameLabel.AutoSize = AutoSize.None;
                NameLabel.size = new Vector2(NameWidth, RowHeight);
                NameLabel.relativePosition = new Vector2(0f, 0f);
                NameLabel.textScale = 0.75f;
                NameLabel.textColor = Color.white;
                NameLabel.VerticalAlignment = UIVerticalAlignment.Middle;
                NameLabel.HorizontalAlignment = UIHorizontalAlignment.Left;
                NameLabel.useDropShadow = true;
                NameLabel.dropShadowColor = new Color32(0, 0, 0, 192);
                NameLabel.dropShadowOffset = new Vector2(1f, -1f);
                NameLabel.isInteractive = false;
            }
            public void Set(ProblemLocation location, UITextureAtlas atlas)
            {
                Location = location;
                tooltip = location.Name;
                NameLabel.text = location.Name;
                NameLabel.tooltip = location.Name;

                var iconIndex = 0;
                foreach (var problem in Settings.PanelProblems)
                {
                    if ((location.Problems & problem).IsNone)
                        continue;

                    var icon = GetIcon(iconIndex);
                    var title = Settings.GetTitle(problem);
                    InformationPanel.ResolveProblemIcon(problem, atlas, out var iconAtlas, out var iconSprite);
                    icon.atlas = iconAtlas;
                    icon.spriteName = iconSprite;
                    icon.tooltip = title;
                    icon.isVisible = true;
                    iconIndex += 1;
                }

                for (var i = iconIndex; i < Icons.Count; i += 1)
                    Icons[i].isVisible = false;

                VisibleIconCount = iconIndex;
                LayoutIcons();
            }
            protected override void OnSizeChanged()
            {
                base.OnSizeChanged();
                LayoutIcons();
            }
            private CustomUISprite GetIcon(int index)
            {
                while (Icons.Count <= index)
                {
                    var icon = AddUIComponent<CustomUISprite>();
                    icon.size = new Vector2(IconSize, IconSize);
                    icon.isInteractive = false;
                    Icons.Add(icon);
                }

                LayoutIcons();
                return Icons[index];
            }
            private void LayoutIcons()
            {
                if (Icons == null)
                    return;

                var iconAreaWidth = VisibleIconCount * IconSize + Mathf.Max(0, VisibleIconCount - 1) * IconSpacing;
                var x = Mathf.Max(0f, width - iconAreaWidth - 16f);
                for (var i = 0; i < Icons.Count; i += 1)
                    Icons[i].relativePosition = new Vector2(x + (IconSize + IconSpacing) * i, (RowHeight - IconSize) * 0.5f);

                if (NameLabel != null)
                    NameLabel.size = new Vector2(Mathf.Max(0f, x - 8f), RowHeight);
            }
            private void OnItemClick(UIComponent component, UIMouseEventParameter eventParam)
            {
                if (eventParam.used || Location == null)
                    return;

                if (Owner != null && Owner.ConsumePanelDragClick())
                {
                    eventParam.Use();
                    return;
                }

                Focus(Location);
                eventParam.Use();
            }
            private void OnItemMouseDown(UIComponent component, UIMouseEventParameter eventParam)
            {
                Owner?.BeginPanelDrag(eventParam);
            }
            private void OnItemMouseMove(UIComponent component, UIMouseEventParameter eventParam)
            {
                Owner?.MovePanelDrag(eventParam);
            }
            private void OnItemMouseUp(UIComponent component, UIMouseEventParameter eventParam)
            {
                Owner?.EndPanelDrag(eventParam);
            }
            private static void Focus(ProblemLocation location)
            {
                try
                {
                    var instance = location.Instance;
                    if (instance.IsEmpty)
                        return;

                    var instanceManager = Singleton<InstanceManager>.instance;
                    var cameraController = ToolsModifierControl.cameraController;

                    if (instanceManager != null)
                    {
                        instanceManager.SelectInstance(instance);
                        instanceManager.FollowInstance(instance);
                    }

                    var position = location.Position;
                    Quaternion rotation;
                    Vector3 size;
                    InstanceManager.GetPosition(instance, out position, out rotation, out size);

                    if (cameraController != null)
                        cameraController.SetTarget(instance, position, true);
                }
                catch (Exception e)
                {
                    SingletonMod<Mod>.Logger.Error(e);
                }
            }
        }
    }

    public class WatchIt2Shortcut : ModShortcut<Mod>
    {
        public WatchIt2Shortcut(string name, string labelKey, InputKey key, Action action = null) : base(name, labelKey, key, action) { }
    }

    public class Settings : BaseSettings<Mod>
    {
        protected override string GeneralTabLabel => "Watch It! 2";
        protected override bool ShowInfoSection => false;
        protected override bool ShowSupportTab => false;
        protected override bool AlwaysShowTabStrip => true;

        public static WatchIt2Shortcut ToggleShortcut { get; } = new WatchIt2Shortcut(nameof(ToggleShortcut), nameof(Localize.Setting_ToggleShortcut), SavedInputKey.Encode(KeyCode.N, true, true, false));

        public static ProblemStruct EnabledProblems { get; private set; }
        public static Dictionary<ProblemStruct, SavedBool> Data { get; } = new Dictionary<ProblemStruct, SavedBool>();
        public static SavedBool HidingEnabled { get; } = new SavedBool(nameof(HidingEnabled), SettingsFile, true, true);
        public static SavedBool InformationPanelVerticalLayout { get; } = new SavedBool(nameof(InformationPanelVerticalLayout), SettingsFile, false, true);
        public static SavedInt InformationPanelDragBackgroundOpacity { get; } = new SavedInt(nameof(InformationPanelDragBackgroundOpacity), SettingsFile, 0, true);
        public static SavedInt HideType { get; } = new SavedInt(nameof(HideType), SettingsFile, 0, true);
        public static SavedBool AbandonedBuildingsDisabled { get; } = new SavedBool(nameof(AbandonedBuildingsDisabled), SettingsFile, true, true);
        public static SavedBool BurnedDownBuildingsDisabled { get; } = new SavedBool(nameof(BurnedDownBuildingsDisabled), SettingsFile, true, true);

        internal static ProblemStruct AbandonedBuildingProblem { get; } = new ProblemStruct((Problem2)(1UL << 63));
        internal static ProblemStruct BurnedDownBuildingProblem { get; } = new ProblemStruct((Problem2)(1UL << 62));
        private static ProblemStruct FireProblem { get; } = new ProblemStruct(Problem1.Fire);
        private static ProblemStruct FloodProblem { get; } = new ProblemStruct(Problem1.Flood);

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
        internal static ProblemStruct CountedProblems => ProblemStruct.All & ~Ignore;
        internal static IEnumerable<ProblemStruct> PanelProblems
        {
            get
            {
                foreach (var problem in CountedProblems)
                    yield return problem;

                yield return AbandonedBuildingProblem;
                yield return BurnedDownBuildingProblem;
            }
        }
        internal static bool AllProblemsDisabled
        {
            get
            {
                foreach (var problem in CountedProblems)
                {
                    if (!Data.TryGetValue(problem, out var saved) || !saved.value)
                        return false;
                }

                return AbandonedBuildingsDisabled.value && BurnedDownBuildingsDisabled.value;
            }
        }
        internal static string AllProblemsToggleLabel => AllProblemsDisabled ? Localize.Settings_EnableAll : Localize.Settings_DisableAll;

        internal static ProblemStruct GetPanelProblems(ProblemStruct problems)
        {
            return Settings.GetRenderedProblems(problems) & CountedProblems & EnabledProblems;
        }
        internal static ProblemStruct GetNetworkSegmentPanelProblems(NetSegment segment)
        {
            if ((segment.m_flags & NetSegment.Flags.Flooded) != 0 || (segment.m_problems & FloodProblem).IsNotNone)
                return GetPanelProblems(FloodProblem);

            return ProblemStruct.None;
        }
        internal static ProblemStruct GetBuildingPanelProblems(Building building)
        {
            var problems = GetPanelProblems(building.m_problems);
            if (IsAbandonedBuilding(building))
            {
                problems = ProblemStruct.None;

                if (!AbandonedBuildingsDisabled.value)
                    problems |= AbandonedBuildingProblem;
            }
            else if (IsBurnedDownBuilding(building))
            {
                problems &= ~FireProblem;

                if (!BurnedDownBuildingsDisabled.value)
                    problems |= BurnedDownBuildingProblem;
            }

            return problems;
        }
        private static bool IsAbandonedBuilding(Building building)
        {
            return (building.m_flags & Building.Flags.Abandoned) != 0;
        }
        private static bool IsBurnedDownBuilding(Building building)
        {
            return (building.m_flags & Building.Flags.BurnedDown) != 0 && (building.m_problems & FireProblem).IsNotNone;
        }
        internal static bool IsAbandonedBuildingProblem(ProblemStruct problem)
        {
            return problem == AbandonedBuildingProblem;
        }
        internal static bool IsBurnedDownBuildingProblem(ProblemStruct problem)
        {
            return problem == BurnedDownBuildingProblem;
        }

        internal static ProblemStruct GetRenderedProblems(ProblemStruct problems)
        {
            if (HidingEnabled)
            {
                switch (HideType)
                {
                    case 0:
                    case 2:
                        problems &= EnabledProblems;
                        break;
                    case 1 when (problems & ProblemStruct.MajorOrFatal).IsNone:
                        problems &= EnabledProblems;
                        break;
                }
            }

            return problems;
        }

        internal static string GetTitle(ProblemStruct problem)
        {
            if (IsAbandonedBuildingProblem(problem))
                return "Abandoned";
            else if (IsBurnedDownBuildingProblem(problem))
                return "Burned down";

            if ((problem & (new ProblemStruct(Problem1.StructureVisited | Problem1.StructureVisitedService))).IsNotNone)
            {
                if (ColossalFramework.Globalization.Locale.Exists("NOTIFICATION_VISITED"))
                    return ColossalFramework.Globalization.Locale.Get("NOTIFICATION_VISITED");
                else
                    return Problem1.StructureVisited.ToString();
            }
            else
            {
                var key = problem.m_Problems1 != Problem1.None ? problem.m_Problems1.Name("Text") : problem.m_Problems2.Name("Text");
                if (ColossalFramework.Globalization.Locale.Exists("NOTIFICATION_TITLE", key))
                    return ColossalFramework.Globalization.Locale.Get("NOTIFICATION_TITLE", key).Trim();
                else
                    return key;
            }
        }
        internal static string GetIcon(ProblemStruct problem)
        {
            if (IsAbandonedBuildingProblem(problem))
                return "BuildingNotificationAbandoned";
            else if (IsBurnedDownBuildingProblem(problem))
                return "BuildingNotificationBurnedDown";

            return problem.m_Problems1 != Problem1.None ? problem.m_Problems1.Name("Normal") : problem.m_Problems2.Name("Normal");
        }
        private static Group GetGroup(ProblemStruct problem)
        {
            if (IsAbandonedBuildingProblem(problem) || IsBurnedDownBuildingProblem(problem) || (problem & GeneralProblems).IsNotNone)
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
            var generalSection = GeneralTab.AddOptionsSection(CommonLocalize.Settings_General);
            CheckPanelSettingsItem hideTypeCheckBoxes = null;

            var opacityItem = generalSection.AddUIComponent<OpacitySlider>();
            opacityItem.Label = "Panel transparency";
            opacityItem.Value = InformationPanelDragBackgroundOpacity.value;
            opacityItem.OnValueChanged += OnInformationPanelBackgroundOpacityChanged;
            var verticalLayoutItem = generalSection.AddUIComponent<VerticalLayoutToggle>();
            verticalLayoutItem.Label = "Vertical information panel";
            verticalLayoutItem.Value = InformationPanelVerticalLayout;
            verticalLayoutItem.OnValueChanged += OnInformationPanelLayoutChanged;

            var restoreButtonPanel = generalSection.AddButtonPanel(new RectOffset(0, 0, 5, 5), 0);
            restoreButtonPanel.AddButton("Restore default values", () => RestoreDefaultValues(opacityItem, verticalLayoutItem, hideTypeCheckBoxes), 250f, 1f);

            var hideTypeGroup = generalSection.AddItemsGroup();
            hideTypeCheckBoxes = hideTypeGroup.AddTogglePanel(Localize.Setting_HideType, HideType, new string[] { Localize.Setting_HideAny, Localize.Setting_HideNormal, Localize.Setting_Remove }, OnDisabledChanged).checkBoxes;

            var color = new Color32(255, 215, 81, 255);
            var description = string.Format(Localize.Setting_HideDescription, Localize.Setting_HideAny.AddColor(color), Localize.Setting_HideNormal.AddColor(color), Localize.Setting_Remove.AddColor(color));
            var descrItem = hideTypeGroup.AddLabel(description, 0.8f);
            descrItem.Borders = SettingsItemBorder.None;
            descrItem.LabelItem.processMarkup = true;

            generalSection.AddKeyMappingButton(ToggleShortcut);
            generalSection.AddSpace(15f);

            var buttonPanel = generalSection.AddButtonPanel(new RectOffset(0, 0, 5, 5), 10);
            buttonPanel.AddButton(Localize.Settings_DisableAll, () => Switch(toggles, ProblemStruct.All, true, true), 250, 1f);
            buttonPanel.AddButton(Localize.Settings_EnableAll, () => Switch(toggles, ProblemStruct.All, false, true), 250, 1f);


            var groups = new Dictionary<Group, CustomUIPanel>()
            {
                { Group.General, AddOptionsSection(Group.General) },
                { Group.Other, AddOptionsSection(Group.Other) },
                { Group.Сonsumption, AddOptionsSection(Group.Сonsumption) },
                { Group.NotConnected, AddOptionsSection(Group.NotConnected) },
                { Group.Disasters, AddOptionsSection(Group.Disasters) },
                { Group.Parks, AddOptionsSection(Group.Parks) },
                { Group.Industry, AddOptionsSection(Group.Industry) },
                { Group.Campus, AddOptionsSection(Group.Campus) },
                { Group.Fishing, AddOptionsSection(Group.Fishing) },
                { Group.Airport, AddOptionsSection(Group.Airport) },
                { Group.Pedestrian, AddOptionsSection(Group.Pedestrian) },
            };

            foreach (var groupKV in groups)
            {
                var group = groupKV.Key;
                var helper = groupKV.Value;

                var buttonGroup = helper.AddButtonPanel(new RectOffset(0, 0, 5, 15), 10);
                buttonGroup.AddButton(Localize.Settings_DisableEntireGrope, () => Switch(toggles, GetProblems(group), true, group == Group.General), 250, 1f);
                buttonGroup.AddButton(Localize.Settings_EnableEntireGrope, () => Switch(toggles, GetProblems(group), false, group == Group.General), 250, 1f);
            }

            var notificationAtlas = TextureHelper.GetAtlas("Notifications");

            foreach (var problem in ProblemStruct.All & ~Ignore)
                AddProblemToggle(toggles, groups[GetGroup(problem)], problem, Data[problem], notificationAtlas);

            AddProblemToggle(toggles, groups[Group.General], AbandonedBuildingProblem, AbandonedBuildingsDisabled, notificationAtlas);
            AddProblemToggle(toggles, groups[Group.General], BurnedDownBuildingProblem, BurnedDownBuildingsDisabled, notificationAtlas);
        }
        private void AddProblemToggle(Dictionary<ProblemStruct, ToggleSettingsItem> toggles, CustomUIPanel group, ProblemStruct problem, SavedBool saved, UITextureAtlas notificationAtlas)
        {
            var title = GetTitle(problem);
            var text = InformationPanel.TryGetProblemIcon(problem, notificationAtlas, out var iconAtlas, out var icon) ?
                $" <sprite {icon}>   {title}" :
                title;

            var toggle = group.AddToggle(string.Format(Localize.Setting_DisableProblem, text), saved);
            toggle.LabelItem.Atlas = iconAtlas;
            toggle.LabelItem.processMarkup = true;
            toggle.Control.OnValueChanged += (value) =>
            {
                SetProblemDisabled(problem, saved.value);
                OnDisabledChanged();
            };

            toggles[problem] = toggle;
        }
        private CustomUIPanel AddOptionsSection(Group group) => GeneralTab.AddOptionsSection(SingletonMod<Mod>.Instance.GetLocalizedString($"Settings_{group}Group"));

        private void OnInformationPanelLayoutChanged(bool value)
        {
            InformationPanel.RefreshLayout();
        }
        private void OnInformationPanelBackgroundOpacityChanged(int value)
        {
            InformationPanelDragBackgroundOpacity.value = value;
            InformationPanel.RefreshBackgroundOpacity();
        }
        private void RestoreDefaultValues(OpacitySlider opacityItem, VerticalLayoutToggle verticalLayoutItem, CheckPanelSettingsItem hideTypeCheckBoxes)
        {
            InformationPanelDragBackgroundOpacity.value = 50;
            InformationPanelVerticalLayout.value = false;
            HideType.value = 1;

            opacityItem.Value = InformationPanelDragBackgroundOpacity.value;
            verticalLayoutItem.Value = InformationPanelVerticalLayout.value;
            if (hideTypeCheckBoxes != null)
                hideTypeCheckBoxes.Value = HideType.value;
            else
                OnDisabledChanged(HideType.value);

            InformationPanel.RefreshBackgroundOpacity();
            InformationPanel.RestoreDefaultPosition();
        }


        private bool SwitchInProgress { get; set; } = false;
        private void OnDisabledChanged(int index = 0)
        {
            if (!SwitchInProgress)
                ApplyDisabledProblemsChanged();
        }
        internal static void ToggleAllProblemsDisabled()
        {
            SetAllProblemsDisabled(!AllProblemsDisabled);
        }
        private static void SetAllProblemsDisabled(bool disable)
        {
            SetProblemsDisabled(CountedProblems, disable);
            SetProblemDisabled(AbandonedBuildingProblem, disable);
            SetProblemDisabled(BurnedDownBuildingProblem, disable);
            ApplyDisabledProblemsChanged();
        }
        private static void SetProblemDisabled(ProblemStruct problem, bool disable)
        {
            if (IsAbandonedBuildingProblem(problem))
                AbandonedBuildingsDisabled.value = disable;
            else if (IsBurnedDownBuildingProblem(problem))
                BurnedDownBuildingsDisabled.value = disable;
            else
                Set(problem, disable);
        }
        private static void SetProblemsDisabled(ProblemStruct problems, bool disable)
        {
            foreach (var problem in problems & CountedProblems)
            {
                if (Data.TryGetValue(problem, out var saved))
                    saved.value = disable;

                SetProblemDisabled(problem, disable);
            }
        }
        private static void ApplyDisabledProblemsChanged()
        {
            if (HideType == 2)
                SingletonMod<Mod>.Instance.RemoveExistingProblems(~EnabledProblems);

            ProblemPanel.RefreshProblemPanel();
        }
        private void Switch(Dictionary<ProblemStruct, ToggleSettingsItem> toggles, ProblemStruct problems, bool state, bool includeBuildingStateProblems = false)
        {
            SwitchInProgress = true;
            SetProblemsDisabled(problems, state);
            foreach (var problem in problems & CountedProblems)
            {
                if (toggles.TryGetValue(problem, out var toggle))
                    toggle.State = state;
            }

            if (includeBuildingStateProblems)
            {
                SetProblemDisabled(AbandonedBuildingProblem, state);
                if (toggles.TryGetValue(AbandonedBuildingProblem, out var toggle))
                    toggle.State = state;

                SetProblemDisabled(BurnedDownBuildingProblem, state);
                if (toggles.TryGetValue(BurnedDownBuildingProblem, out var burnedDownToggle))
                    burnedDownToggle.State = state;
            }

            SwitchInProgress = false;

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
