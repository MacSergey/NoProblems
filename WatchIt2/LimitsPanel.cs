using ColossalFramework;
using ColossalFramework.UI;
using ModsCommon;
using ModsCommon.UI;
using ModsCommon.Utilities;
using System;
using System.Collections.Generic;
using System.Globalization;
using UnityEngine;

namespace WatchIt2
{
    public class LimitsPanel : CustomUIPanel
    {
        private const float PanelWidth = 580f;
        private const float PanelHeight = 580f;
        private const float HeaderHeight = 30f;
        private const float CloseButtonSize = 24f;
        private const float ColumnHeaderHeight = 24f;
        private const float PaddingSize = 8f;
        private const float RowHeight = 24f;
        private const float RowPadding = 6f;
        private const float NameWidth = 170f;
        private const float AmountWidth = 120f;
        private const float CapacityWidth = 120f;
        private const float ConsumptionWidth = 74f;
        private const float ModdedWidth = 40f;
        private const float AmountX = RowPadding + NameWidth + 8f;
        private const float CapacityX = AmountX + AmountWidth + 10f;
        private const float ConsumptionX = CapacityX + CapacityWidth + 10f;
        private const float ModdedX = ConsumptionX + ConsumptionWidth + 8f;
        private const float RefreshInterval = 5f;
        private const string ModdedTooltip = "This capacity has been modded";

        private static readonly Color32 PanelColor = new Color32(48, 52, 54, 238);
        private static readonly Color32 TransparentColor = new Color32(48, 52, 54, 0);
        private static readonly Color32 HoverRowColor = Color.white;
        private static readonly Color32 TextColor = Color.white;

        private static LimitsPanel Instance { get; set; }

        private CustomUIPanel Header { get; set; }
        private CustomUIButton CloseButton { get; set; }
        private CustomUIDragHandle DragBackground { get; set; }
        private CustomUIScrollablePanel Content { get; set; }
        private List<LimitsPanelItem> Items { get; } = new List<LimitsPanelItem>();
        private bool IsPanelDragActive { get; set; }
        private bool PanelDragMoved { get; set; }
        private Vector3 LastPanelDragPosition { get; set; }
        private float LastRefreshTime { get; set; }

        public static bool IsOpen => Instance != null && Instance.isVisible;

        public static void Toggle(UIComponent source)
        {
            if (IsOpen)
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
                Instance = view.AddUIComponent(typeof(LimitsPanel)) as LimitsPanel;

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
        public override void Awake()
        {
            base.Awake();

            name = "Limits overview";
            gameObject.name = name;
            size = new Vector2(PanelWidth, PanelHeight);
            isInteractive = true;
            clipChildren = true;

            Atlas = CommonTextures.Atlas;
            BackgroundSprite = CommonTextures.PanelSmall;

            DragBackground = AddUIComponent<CustomUIDragHandle>();
            DragBackground.name = "Limits panel drag background";
            DragBackground.target = this;
            DragBackground.constrainToScreen = true;
            DragBackground.size = size;
            DragBackground.relativePosition = Vector3.zero;
            DragBackground.isInteractive = true;

            eventMouseDown += OnPanelMouseDown;
            eventMouseMove += OnPanelMouseMove;
            eventMouseUp += OnPanelMouseUp;

            AddLabel(this, "Limits title", "Limits overview", new Vector2(PanelWidth - PaddingSize * 2f - CloseButtonSize - 4f, HeaderHeight), new Vector2(PaddingSize, 0f), UIHorizontalAlignment.Left, 0.9f, Color.white, true);

            CloseButton = AddUIComponent<CloseLimitsButton>();
            CloseButton.name = "Close limits button";
            CloseButton.relativePosition = new Vector2(PanelWidth - PaddingSize - CloseButtonSize, (HeaderHeight - CloseButtonSize) * 0.5f);
            CloseButton.eventClick += OnCloseButtonClick;

            Header = AddUIComponent<CustomUIPanel>();
            Header.name = "Limits column header";
            Header.size = new Vector2(PanelWidth - PaddingSize * 2f, ColumnHeaderHeight);
            Header.relativePosition = new Vector2(PaddingSize, HeaderHeight);
            Header.Atlas = CommonTextures.Atlas;
            Header.BackgroundSprite = CommonTextures.PanelSmall;
            Header.isInteractive = true;
            Header.eventMouseDown += OnPanelMouseDown;
            Header.eventMouseMove += OnPanelMouseMove;
            Header.eventMouseUp += OnPanelMouseUp;

            AddLabel(Header, "Limit name header", "Limit", new Vector2(NameWidth, ColumnHeaderHeight), new Vector2(RowPadding, 0f), UIHorizontalAlignment.Left, 0.75f, Color.white, true);
            AddLabel(Header, "Limit amount header", "Used", new Vector2(AmountWidth, ColumnHeaderHeight), new Vector2(AmountX, 0f), UIHorizontalAlignment.Right, 0.75f, Color.white, true);
            AddLabel(Header, "Limit capacity header", "Capacity", new Vector2(CapacityWidth, ColumnHeaderHeight), new Vector2(CapacityX, 0f), UIHorizontalAlignment.Right, 0.75f, Color.white, true);
            AddLabel(Header, "Limit consumption header", "Usage", new Vector2(ConsumptionWidth, ColumnHeaderHeight), new Vector2(ConsumptionX, 0f), UIHorizontalAlignment.Right, 0.75f, Color.white, true);

            Content = AddUIComponent<CustomUIScrollablePanel>();
            Content.name = "Limits list";
            Content.size = new Vector2(PanelWidth - PaddingSize * 2f, PanelHeight - HeaderHeight - ColumnHeaderHeight - PaddingSize);
            Content.relativePosition = new Vector2(PaddingSize, HeaderHeight + ColumnHeaderHeight);
            Content.AutoLayout = AutoLayout.Vertical;
            Content.AutoLayoutSpace = 0;
            Content.AutoChildrenHorizontally = AutoLayoutChildren.Fill;
            Content.ScrollOrientation = UIOrientation.Vertical;
            Content.Scrollbar.DefaultStyle();
            Content.ScrollbarSize = 12f;
            Content.ShowScroll = true;
            Content.isInteractive = true;
            Content.clipChildren = true;
            Content.eventMouseDown += OnPanelMouseDown;
            Content.eventMouseMove += OnPanelMouseMove;
            Content.eventMouseUp += OnPanelMouseUp;

            ApplyBackgroundOpacity();
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
        private void RefreshNow()
        {
            LastRefreshTime = Time.realtimeSinceStartup;

            try
            {
                for (var i = 0; i < Limits.Length; i += 1)
                {
                    var item = GetItem(i);
                    item.Set(Limits[i], ReadLimit(Limits[i]));
                    item.isVisible = true;
                }

                for (var i = Limits.Length; i < Items.Count; i += 1)
                    Items[i].isVisible = false;

                Content.Reset();
            }
            catch (Exception e)
            {
                SingletonMod<Mod>.Logger.Error(e);
            }
        }
        private void ApplyBackgroundOpacity()
        {
            NormalBgColor = PanelColor;
            HoveredBgColor = PanelColor;
            DisabledBgColor = PanelColor;

            if (Header != null)
            {
                Header.NormalBgColor = TransparentColor;
                Header.HoveredBgColor = TransparentColor;
                Header.DisabledBgColor = TransparentColor;
            }

            for (var i = 0; i < Items.Count; i += 1)
                Items[i].ApplyBackgroundOpacity();
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
        private LimitsPanelItem GetItem(int index)
        {
            while (Items.Count <= index)
            {
                var item = Content.AddUIComponent<LimitsPanelItem>();
                item.name = "Limits item";
                item.Owner = this;
                item.Init();
                item.Height = RowHeight;
                Items.Add(item);
            }

            return Items[index];
        }
        private static CustomUILabel AddLabel(UIComponent parent, string name, string text, Vector2 size, Vector2 position, UIHorizontalAlignment alignment, float textScale, Color32 textColor, bool bold)
        {
            var label = parent.AddUIComponent<CustomUILabel>();
            label.name = name;
            label.AutoSize = AutoSize.None;
            label.size = size;
            label.relativePosition = position;
            label.text = text;
            label.textScale = textScale;
            label.textColor = textColor;
            label.VerticalAlignment = UIVerticalAlignment.Middle;
            label.HorizontalAlignment = alignment;
            label.Bold = bold;
            label.useDropShadow = true;
            label.dropShadowColor = new Color32(0, 0, 0, 192);
            label.dropShadowOffset = new Vector2(1f, -1f);
            label.isInteractive = false;
            return label;
        }
        private static LimitValue ReadLimit(LimitInfo info)
        {
            try
            {
                return info.Read();
            }
            catch (Exception e)
            {
                SingletonMod<Mod>.Logger.Error(e);
                return new LimitValue(0, 0, false);
            }
        }
        private static string FormatNumber(int value)
        {
            return string.Format(CultureInfo.CurrentCulture, "{0:0,0}", value);
        }
        private static int GetConsumption(int amount, int capacity)
        {
            if (capacity <= 0)
                return 0;

            return (int)(amount / (float)capacity * 100f);
        }
        private static readonly LimitInfo[] Limits =
        {
            new LimitInfo("Areas", ReadAreas),
            new LimitInfo("Buildings", ReadBuildings),
            new LimitInfo("Citizens", ReadCitizens),
            new LimitInfo("Citizen Units", ReadCitizenUnits),
            new LimitInfo("Citizen Instances", ReadCitizenInstances),
            new LimitInfo("Disasters", ReadDisasters),
            new LimitInfo("Districts", ReadDistricts),
            new LimitInfo("Events", ReadEvents),
            new LimitInfo("Loans", ReadLoans),
            new LimitInfo("Net Segments", ReadNetSegments),
            new LimitInfo("Net Nodes", ReadNetNodes),
            new LimitInfo("Net Lanes", ReadNetLanes),
            new LimitInfo("Path Units", ReadPathUnits),
            new LimitInfo("Props", ReadProps),
            new LimitInfo("Radio Channels", ReadRadioChannels),
            new LimitInfo("Radio Contents", ReadRadioContents),
            new LimitInfo("Transport Lines", ReadTransportLines),
            new LimitInfo("Trees", ReadTrees),
            new LimitInfo("Vehicles", ReadVehicles),
            new LimitInfo("Vehicles Parked", ReadParkedVehicles),
            new LimitInfo("Zoned Blocks", ReadZonedBlocks),
        };

        private static LimitValue ReadAreas()
        {
            var manager = Singleton<GameAreaManager>.instance;
            return new LimitValue(manager.m_areaCount, manager.m_maxAreaCount, manager.m_maxAreaCount > 9);
        }
        private static LimitValue ReadBuildings()
        {
            var manager = Singleton<BuildingManager>.instance;
            return new LimitValue(manager.m_buildingCount, (int)manager.m_buildings.m_size, manager.m_buildings.m_size > BuildingManager.MAX_BUILDING_COUNT);
        }
        private static LimitValue ReadCitizens()
        {
            var manager = Singleton<CitizenManager>.instance;
            return new LimitValue(manager.m_citizenCount, (int)manager.m_citizens.m_size, manager.m_citizens.m_size > CitizenManager.MAX_CITIZEN_COUNT);
        }
        private static LimitValue ReadCitizenUnits()
        {
            var manager = Singleton<CitizenManager>.instance;
            return new LimitValue(manager.m_unitCount, (int)manager.m_units.m_size, manager.m_units.m_size > CitizenManager.MAX_UNIT_COUNT);
        }
        private static LimitValue ReadCitizenInstances()
        {
            var manager = Singleton<CitizenManager>.instance;
            return new LimitValue(manager.m_instanceCount, (int)manager.m_instances.m_size, manager.m_instances.m_size > CitizenManager.MAX_INSTANCE_COUNT);
        }
        private static LimitValue ReadDisasters()
        {
            var manager = Singleton<DisasterManager>.instance;
            return new LimitValue(manager.m_disasterCount, manager.m_disasters.m_size, manager.m_disasters.m_size > DisasterManager.MAX_DISASTER_COUNT);
        }
        private static LimitValue ReadDistricts()
        {
            var manager = Singleton<DistrictManager>.instance;
            return new LimitValue(manager.m_districtCount, (int)manager.m_districts.m_size, manager.m_districts.m_size > DistrictManager.MAX_DISTRICT_COUNT);
        }
        private static LimitValue ReadEvents()
        {
            var manager = Singleton<EventManager>.instance;
            return new LimitValue(manager.m_eventCount, manager.m_events.m_size, manager.m_events.m_size > EventManager.MAX_EVENT_COUNT);
        }
        private static LimitValue ReadLoans()
        {
            var manager = Singleton<EconomyManager>.instance;
            return new LimitValue(manager.CountLoans(), EconomyManager.MAX_LOANS, false);
        }
        private static LimitValue ReadNetSegments()
        {
            var manager = Singleton<NetManager>.instance;
            return new LimitValue(manager.m_segmentCount, (int)manager.m_segments.m_size, manager.m_segments.m_size > NetManager.MAX_SEGMENT_COUNT);
        }
        private static LimitValue ReadNetNodes()
        {
            var manager = Singleton<NetManager>.instance;
            return new LimitValue(manager.m_nodeCount, (int)manager.m_nodes.m_size, manager.m_nodes.m_size > NetManager.MAX_NODE_COUNT);
        }
        private static LimitValue ReadNetLanes()
        {
            var manager = Singleton<NetManager>.instance;
            return new LimitValue(manager.m_laneCount, (int)manager.m_lanes.m_size, manager.m_lanes.m_size > NetManager.MAX_LANE_COUNT);
        }
        private static LimitValue ReadPathUnits()
        {
            var manager = Singleton<PathManager>.instance;
            return new LimitValue(manager.m_pathUnitCount, (int)manager.m_pathUnits.m_size, manager.m_pathUnits.m_size > PathManager.MAX_PATHUNIT_COUNT);
        }
        private static LimitValue ReadProps()
        {
            var manager = Singleton<PropManager>.instance;
            var amount = manager.m_propCount;

            if (manager.m_props != null)
                return new LimitValue(amount, (int)manager.m_props.m_size, manager.m_props.m_size > PropManager.MAX_PROP_COUNT);

            return new LimitValue(amount, Math.Max(amount, PropManager.MAX_PROP_COUNT), true);
        }
        private static LimitValue ReadRadioChannels()
        {
            var manager = Singleton<AudioManager>.instance;
            return new LimitValue(manager.m_radioChannelCount, manager.m_radioChannels.m_size, manager.m_radioChannels.m_size > AudioManager.MAX_RADIO_CHANNEL_COUNT);
        }
        private static LimitValue ReadRadioContents()
        {
            var manager = Singleton<AudioManager>.instance;
            return new LimitValue(manager.m_radioContentCount, manager.m_radioContents.m_size, manager.m_radioContents.m_size > AudioManager.MAX_RADIO_CONTENT_COUNT);
        }
        private static LimitValue ReadTransportLines()
        {
            var manager = Singleton<TransportManager>.instance;
            return new LimitValue(manager.m_lineCount, (int)manager.m_lines.m_size, manager.m_lines.m_size > TransportManager.MAX_LINE_COUNT);
        }
        private static LimitValue ReadTrees()
        {
            var manager = Singleton<TreeManager>.instance;
            return new LimitValue(manager.m_treeCount, (int)manager.m_trees.m_size, manager.m_trees.m_size > TreeManager.MAX_TREE_COUNT);
        }
        private static LimitValue ReadVehicles()
        {
            var manager = Singleton<VehicleManager>.instance;
            return new LimitValue(manager.m_vehicleCount, (int)manager.m_vehicles.m_size, manager.m_vehicles.m_size > VehicleManager.MAX_VEHICLE_COUNT);
        }
        private static LimitValue ReadParkedVehicles()
        {
            var manager = Singleton<VehicleManager>.instance;
            return new LimitValue(manager.m_parkedCount, (int)manager.m_parkedVehicles.m_size, manager.m_parkedVehicles.m_size > VehicleManager.MAX_PARKED_COUNT);
        }
        private static LimitValue ReadZonedBlocks()
        {
            var manager = Singleton<ZoneManager>.instance;
            return new LimitValue(manager.m_blockCount, (int)manager.m_blocks.m_size, manager.m_blocks.m_size > ZoneManager.MAX_BLOCK_COUNT);
        }

        private delegate LimitValue LimitReader();

        private class LimitInfo
        {
            public string Name { get; }
            public LimitReader Read { get; }

            public LimitInfo(string name, LimitReader read)
            {
                Name = name;
                Read = read;
            }
        }

        private struct LimitValue
        {
            public int Amount { get; }
            public int Capacity { get; }
            public bool Modded { get; }

            public LimitValue(int amount, int capacity, bool modded)
            {
                Amount = amount;
                Capacity = capacity;
                Modded = modded;
            }
        }

        private class CloseLimitsButton : CustomUIButton
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

        private class LimitsPanelItem : CustomUIPanel
        {
            public LimitsPanel Owner { private get; set; }

            private CustomUILabel NameLabel { get; set; }
            private CustomUILabel AmountLabel { get; set; }
            private CustomUILabel CapacityLabel { get; set; }
            private CustomUILabel ConsumptionLabel { get; set; }
            private CustomUILabel ModdedLabel { get; set; }

            public float Height
            {
                set => size = new Vector2(PanelWidth - PaddingSize * 2f, value);
            }

            public override void Awake()
            {
                base.Awake();

                isInteractive = true;
                Atlas = CommonTextures.Atlas;
                BackgroundSprite = CommonTextures.PanelSmall;
                eventMouseDown += OnItemMouseDown;
                eventMouseMove += OnItemMouseMove;
                eventMouseUp += OnItemMouseUp;

                NameLabel = AddLabel(this, "Limit name", string.Empty, new Vector2(NameWidth, RowHeight), new Vector2(RowPadding, 0f), UIHorizontalAlignment.Left, 0.75f, TextColor, false);
                AmountLabel = AddLabel(this, "Limit amount", string.Empty, new Vector2(AmountWidth, RowHeight), new Vector2(AmountX, 0f), UIHorizontalAlignment.Right, 0.75f, TextColor, false);
                CapacityLabel = AddLabel(this, "Limit capacity", string.Empty, new Vector2(CapacityWidth, RowHeight), new Vector2(CapacityX, 0f), UIHorizontalAlignment.Right, 0.75f, TextColor, false);
                ConsumptionLabel = AddLabel(this, "Limit usage", string.Empty, new Vector2(ConsumptionWidth, RowHeight), new Vector2(ConsumptionX, 0f), UIHorizontalAlignment.Right, 0.75f, TextColor, false);
                ModdedLabel = AddLabel(this, "Limit modded", string.Empty, new Vector2(ModdedWidth, RowHeight), new Vector2(ModdedX, 0f), UIHorizontalAlignment.Center, 0.75f, TextColor, true);
            }
            public void Init()
            {
                ApplyBackgroundOpacity();
            }
            public void ApplyBackgroundOpacity()
            {
                NormalBgColor = TransparentColor;
                HoveredBgColor = HoverRowColor;
                DisabledBgColor = TransparentColor;
            }
            public void Set(LimitInfo info, LimitValue value)
            {
                var consumption = GetConsumption(value.Amount, value.Capacity);
                var amount = FormatNumber(value.Amount);
                var capacity = FormatNumber(value.Capacity);
                var consumptionText = consumption.ToString(CultureInfo.CurrentCulture) + "%";

                NameLabel.text = info.Name;
                AmountLabel.text = amount;
                CapacityLabel.text = capacity;
                ConsumptionLabel.text = consumptionText;
                ModdedLabel.text = value.Modded ? "*" : string.Empty;
                ModdedLabel.tooltip = value.Modded ? ModdedTooltip : string.Empty;

                tooltip = string.Format(CultureInfo.CurrentCulture, "{0}: {1} / {2} ({3})", info.Name, amount, capacity, consumptionText);
                NameLabel.tooltip = tooltip;
                AmountLabel.tooltip = tooltip;
                CapacityLabel.tooltip = tooltip;
                ConsumptionLabel.tooltip = tooltip;
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
        }
    }
}
