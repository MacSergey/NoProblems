using ColossalFramework;
using ColossalFramework.UI;
using ModsCommon;
using ModsCommon.UI;
using ModsCommon.Utilities;
using System;
using System.Collections.Generic;
using UnityEngine;
using static Notification;

namespace WatchIt2
{
    public class InformationPanel : CustomUIPanel
    {
        private const float TileSize = 40f;
        private const float IconSize = 32f;
        private const float IndicatorSize = 40f;
        private const int VerticalRows = 13;
        private const float DragBackgroundOffset = 6f;
        private const float EdgeOffset = 16f;
        private const float RefreshInterval = 5f;
        private const string SettingsIconSprite = "SettingsIcon";
        private const string CollapsedIconSprite = "Collapsed";
        private const string AbandonedIconSprite = "Abandoned";
        private const string BurnedDownIconSprite = "BurnedDown";
        private const string NotificationIconHappiness20 = "NotificationIconHappiness20";
        private const string NotificationIconHappiness40 = "NotificationIconHappiness40";
        private const string NotificationIconHappiness60 = "NotificationIconHappiness60";
        private const string NotificationIconHappiness80 = "NotificationIconHappiness80";
        private const string NotificationIconHappiness100 = "NotificationIconHappiness100";
        private const float SettingsMenuWidth = 260f;
        private const float SettingsMenuPadding = 8f;
        private const float SettingsMenuRowHeight = 30f;
        private const float SettingsMenuSliderRowHeight = 44f;
        private const float SettingsMenuSpace = 6f;

        private static InformationPanel Instance { get; set; }
        private static UITextureAtlas IndicatorAtlas { get; set; }
        private static SettingsMenu PopupMenu { get; set; }
        private static ProblemStruct CollapsedBuildingProblem { get; } = new ProblemStruct(Problem1.StructureDamaged);

        private List<InformationPanelItem> Items { get; } = new List<InformationPanelItem>();
        private SettingsButton SettingsToggle { get; set; }
        private ProblemPanel ProblemsPanel { get; set; }
        private CustomUIPanel DragBackgroundVisual { get; set; }
        private CustomUIDragHandle DragBackground { get; set; }
        private bool IsPanelDragActive { get; set; }
        private bool PanelDragMoved { get; set; }
        private Vector3 LastPanelDragPosition { get; set; }
        private float LastRefreshTime { get; set; }

        public static void Create()
        {
            var view = UIView.GetAView();
            if (view == null)
                return;

            if (Instance == null)
                Instance = view.AddUIComponent(typeof(InformationPanel)) as InformationPanel;

            Instance.isVisible = true;
            Instance.BringToFront();
            Instance.RefreshNow();
        }
        public static void Release()
        {
            CloseSettingsMenu();
            LimitsPanel.Release();
            ProblemPanel.Release();

            if (Instance != null)
            {
                UnityEngine.Object.Destroy(Instance.gameObject);
                Instance = null;
            }
        }
        public static void RefreshLayout()
        {
            if (Instance == null)
                return;

            Instance.ProblemsPanel?.RefreshNow();
            Instance.ApplyLayout();
            Instance.CheckPosition();
            PopupMenu?.SetPosition(Instance.SettingsToggle);
        }
        public static void RefreshBackgroundOpacity()
        {
            Instance?.ApplyDragBackgroundOpacity();
        }
        public static void RestoreDefaultPosition()
        {
            if (Instance == null)
                return;

            Instance.ProblemsPanel?.RefreshNow();
            Instance.ApplyLayout();
            Instance.SetDefaultPosition();
            Instance.CheckPosition();
            PopupMenu?.SetPosition(Instance.SettingsToggle);
        }
        internal static ProblemPanel CurrentProblemPanel
        {
            get
            {
                if (Instance == null)
                    Create();

                if (Instance != null && Instance.ProblemsPanel == null)
                {
                    Instance.ProblemsPanel = Instance.AddUIComponent<ProblemPanel>();
                    Instance.ProblemsPanel.Init(Instance);
                    Instance.ApplyLayout();
                }

                return Instance?.ProblemsPanel;
            }
        }
        private static void ToggleSettingsMenu(UIComponent source)
        {
            if (PopupMenu != null && PopupMenu.isVisible)
            {
                CloseSettingsMenu();
                return;
            }

            var view = UIView.GetAView();
            if (view == null)
                return;

            PopupMenu = view.AddUIComponent(typeof(SettingsMenu)) as SettingsMenu;

            PopupMenu.Init(source);
        }
        private static void CloseSettingsMenu()
        {
            if (PopupMenu != null)
            {
                UnityEngine.Object.Destroy(PopupMenu.gameObject);
                PopupMenu = null;
            }

            Instance?.SettingsToggle?.SetSelected(false);
        }

        public override void Awake()
        {
            base.Awake();

            name = "Information panel";
            gameObject.name = name;
            isInteractive = true;
            clipChildren = true;

            Atlas = CommonTextures.Atlas;
            BackgroundSprite = CommonTextures.EmptyWithoutBorder;
            NormalBgColor = new Color32(255, 255, 255, 0);
            HoveredBgColor = new Color32(255, 255, 255, 0);

            Padding = new RectOffset();
            AutoLayout = AutoLayout.Disabled;

            DragBackgroundVisual = AddUIComponent<CustomUIPanel>();
            DragBackgroundVisual.name = "Information panel drag background visual";
            DragBackgroundVisual.Atlas = CommonTextures.Atlas;
            DragBackgroundVisual.BackgroundSprite = CommonTextures.PanelSmall;
            DragBackgroundVisual.isInteractive = false;

            DragBackground = AddUIComponent<CustomUIDragHandle>();
            DragBackground.name = "Information panel drag background";
            DragBackground.target = this;
            DragBackground.constrainToScreen = true;
            DragBackground.isInteractive = true;

            foreach (var info in Infos)
            {
                var item = AddUIComponent<InformationPanelItem>();
                item.Init(info);
                Items.Add(item);
            }

            SettingsToggle = AddUIComponent<SettingsButton>();
            SettingsToggle.name = "Settings button";
            SettingsToggle.Init();

            ProblemsPanel = AddUIComponent<ProblemPanel>();
            ProblemsPanel.Init(this);

            ApplyLayout();
            ApplyDragBackgroundOpacity();
        }
        public override void Start()
        {
            base.Start();
            SetDefaultPosition();
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
            PopupMenu?.SetPosition(SettingsToggle);
        }

        private void SetDefaultPosition()
        {
            var view = UIView.GetAView();
            var resolution = view?.GetScreenResolution() ?? new Vector2(1920f, 1080f);

            var x = Mathf.Max(0f, (resolution.x - width) * 0.5f);
            var y = Mathf.Min(EdgeOffset, Mathf.Max(0f, resolution.y - height));
            relativePosition = new Vector3(x, y);
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
        private void ApplyLayout()
        {
            if (Settings.InformationPanelVerticalLayout)
                ApplyVerticalLayout();
            else
                ApplyHorizontalLayout();
        }
        private void ApplyHorizontalLayout()
        {
            var panelWidth = TileSize * (Infos.Length + 1) + DragBackgroundOffset * 2f;
            var problemHeight = ProblemsPanel?.GetLayoutHeight(false, panelWidth) ?? 0f;
            size = new Vector2(panelWidth, TileSize + problemHeight + DragBackgroundOffset * 2f);
            SetDragBackgroundSize();

            if (SettingsToggle != null)
                SettingsToggle.relativePosition = new Vector3(DragBackgroundOffset, DragBackgroundOffset);

            for (var i = 0; i < Items.Count; i += 1)
                Items[i].relativePosition = new Vector3(DragBackgroundOffset + TileSize * (i + 1), DragBackgroundOffset);

            ProblemsPanel?.ApplyLayout(false, panelWidth, DragBackgroundOffset + TileSize);
        }
        private void ApplyVerticalLayout()
        {
            var panelWidth = TileSize * 2f + DragBackgroundOffset * 2f;
            var problemHeight = ProblemsPanel?.GetLayoutHeight(true, panelWidth) ?? 0f;
            size = new Vector2(panelWidth, TileSize * (VerticalRows + 1) + problemHeight + DragBackgroundOffset * 2f);
            SetDragBackgroundSize();

            if (SettingsToggle != null)
                SettingsToggle.relativePosition = new Vector3((width - TileSize) * 0.5f, DragBackgroundOffset);

            for (var i = 0; i < Items.Count; i += 1)
            {
                var column = i / VerticalRows;
                var row = i % VerticalRows;
                Items[i].relativePosition = new Vector3(DragBackgroundOffset + TileSize * column, DragBackgroundOffset + TileSize * (row + 1));
            }

            ProblemsPanel?.ApplyLayout(true, panelWidth, DragBackgroundOffset + TileSize * (VerticalRows + 1));
        }
        private void SetDragBackgroundSize()
        {
            if (DragBackgroundVisual != null)
            {
                DragBackgroundVisual.size = size;
                DragBackgroundVisual.relativePosition = Vector3.zero;
            }

            if (DragBackground != null)
            {
                DragBackground.size = size;
                DragBackground.relativePosition = Vector3.zero;
            }
        }
        private void ApplyDragBackgroundOpacity()
        {
            if (DragBackgroundVisual == null)
                return;

            var transparency = Mathf.Clamp(Settings.InformationPanelDragBackgroundOpacity.value, 0, 100);
            var alpha = (byte)Mathf.RoundToInt((100f - transparency) * 255f / 100f);
            var color = new Color32(48, 52, 54, alpha);

            DragBackgroundVisual.BgColors = new ColorSet(color);
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
        internal void BeginChildPanelDrag(UIMouseEventParameter eventParam)
        {
            BeginPanelDrag(eventParam);
        }
        internal void MoveChildPanelDrag(UIMouseEventParameter eventParam)
        {
            MovePanelDrag(eventParam);
        }
        internal void EndChildPanelDrag(UIMouseEventParameter eventParam)
        {
            EndPanelDrag(eventParam);
        }
        internal bool ConsumeChildPanelDragClick()
        {
            return ConsumePanelDragClick();
        }
        internal void RefreshProblemPanelLayout()
        {
            ApplyLayout();
            CheckPosition();
            PopupMenu?.SetPosition(SettingsToggle);
        }
        private void RefreshNow()
        {
            LastRefreshTime = Time.realtimeSinceStartup;

            try
            {
                var infoManager = Singleton<InfoManager>.instance;

                foreach (var item in Items)
                {
                    item.RefreshValue();
                    item.SetSelected(infoManager != null && item.Info.Matches(infoManager.CurrentMode, infoManager.CurrentSubMode));
                }

                SettingsToggle?.SetSelected(PopupMenu != null && PopupMenu.isVisible);

                if (ProblemsPanel?.RefreshNow() == true)
                    RefreshProblemPanelLayout();
            }
            catch (Exception e)
            {
                SingletonMod<Mod>.Logger.Error(e);
            }
        }

        private static int GetPercentage(string name)
        {
            try
            {
                if (UsesAvailabilityPercentage(name))
                {
                    int capacity;
                    int need;
                    GetCapacityAndNeed(name, out capacity, out need);
                    return GetAvailabilityPercentage(capacity, need);
                }

                if (UsesUsagePercentage(name))
                {
                    int capacity;
                    int need;
                    GetCapacityAndNeed(name, out capacity, out need);
                    return GetUsagePercentage(capacity, need);
                }

                switch (name)
                {
                    case "FireDepartment":
                        return GetResourcePercentage(ImmaterialResourceManager.Resource.FireDepartment);
                    case "PoliceDepartment":
                        return GetResourcePercentage(ImmaterialResourceManager.Resource.PoliceDepartment);
                    case "Traffic":
                        return ClampPercentage((int)Singleton<VehicleManager>.instance.m_lastTrafficFlow);
                    case "GroundPollution":
                        return ClampPercentage(GetDistrict().GetGroundPollution());
                    case "DrinkingWaterPollution":
                        return ClampPercentage(GetDistrict().GetWaterPollution());
                    case "NoisePollution":
                        return GetResourcePercentage(ImmaterialResourceManager.Resource.NoisePollution);
                    case "Fire":
                        return GetResourcePercentage(ImmaterialResourceManager.Resource.FireHazard);
                    case "Crime":
                        return GetResourcePercentage(ImmaterialResourceManager.Resource.CrimeRate);
                    case "Unemployment":
                        return ClampPercentage(GetDistrict().GetUnemployment());
                    case "Health":
                        return ClampPercentage(GetDistrict().m_residentialData.m_finalHealth);
                    case "CityAttractiveness":
                        return GetCityAttractiveness();
                    case "Happiness":
                        return ClampPercentage(GetDistrict().m_finalHappiness);
                    default:
                        return 0;
                }
            }
            catch (Exception e)
            {
                SingletonMod<Mod>.Logger.Error(e);
                return 0;
            }
        }
        private static bool UsesAvailabilityPercentage(string name)
        {
            switch (name)
            {
                case "Electricity":
                case "Water":
                case "Sewage":
                case "Garbage":
                case "ElementarySchool":
                case "HighSchool":
                case "University":
                case "Healthcare":
                case "Crematorium":
                case "Jail":
                case "Heating":
                    return true;
                default:
                    return false;
            }
        }
        private static bool UsesUsagePercentage(string name)
        {
            switch (name)
            {
                case "Landfill":
                case "Library":
                case "Cemetery":
                    return true;
                default:
                    return false;
            }
        }
        private static void GetCapacityAndNeed(string name, out int capacity, out int need)
        {
            capacity = 0;
            need = 100;

            var district = GetDistrict();
            switch (name)
            {
                case "Electricity":
                    capacity = district.GetElectricityCapacity();
                    need = district.GetElectricityConsumption();
                    break;
                case "Water":
                    capacity = district.GetWaterCapacity();
                    need = district.GetWaterConsumption();
                    break;
                case "Sewage":
                    capacity = district.GetSewageCapacity();
                    need = district.GetSewageAccumulation();
                    break;
                case "Garbage":
                    capacity = district.GetIncinerationCapacity();
                    need = district.GetGarbageAccumulation();
                    break;
                case "ElementarySchool":
                    capacity = district.GetEducation1Capacity();
                    need = district.GetEducation1Need();
                    break;
                case "HighSchool":
                    capacity = district.GetEducation2Capacity();
                    need = district.GetEducation2Need();
                    break;
                case "University":
                    capacity = district.GetEducation3Capacity();
                    need = district.GetEducation3Need();
                    break;
                case "Healthcare":
                    capacity = district.GetHealCapacity();
                    need = district.GetSickCount();
                    break;
                case "Crematorium":
                    capacity = district.GetCremateCapacity();
                    need = district.GetDeadCount();
                    break;
                case "Jail":
                    capacity = district.GetCriminalCapacity();
                    need = district.GetCriminalAmount() + district.GetExtraCriminals();
                    break;
                case "Heating":
                    capacity = district.GetHeatingCapacity();
                    need = district.GetHeatingConsumption();
                    break;
                case "Landfill":
                    capacity = district.GetGarbageCapacity();
                    need = district.GetGarbageAmount();
                    break;
                case "Library":
                    capacity = district.GetLibraryCapacity();
                    need = district.GetLibraryVisitorCount();
                    break;
                case "Cemetery":
                    capacity = district.GetDeadCapacity();
                    need = district.GetDeadAmount();
                    break;
            }
        }
        private static int GetAvailabilityPercentage(int capacity, int need)
        {
            if (need > 0)
                return ClampPercentage((int)(capacity / (float)need * 50f));

            return capacity > 0 ? 100 : 0;
        }
        private static int GetUsagePercentage(int capacity, int need)
        {
            if (capacity > 0)
                return ClampPercentage((int)(need / (float)capacity * 100f));

            return need > 0 ? 100 : 0;
        }
        private static int GetResourcePercentage(ImmaterialResourceManager.Resource resource)
        {
            var manager = Singleton<ImmaterialResourceManager>.instance;
            if (manager == null)
                return 0;

            int value;
            manager.CheckTotalResource(resource, out value);
            return ClampPercentage(value);
        }
        private static int GetCityAttractiveness()
        {
            var manager = Singleton<ImmaterialResourceManager>.instance;
            if (manager == null)
                return 0;

            int attractiveness;
            int landValue;
            manager.CheckTotalResource(ImmaterialResourceManager.Resource.Attractiveness, out attractiveness);
            manager.CheckTotalResource(ImmaterialResourceManager.Resource.LandValue, out landValue);

            attractiveness += landValue;
            return ClampPercentage(100 * attractiveness / Mathf.Max(attractiveness + 200, 200));
        }
        private static District GetDistrict()
        {
            return Singleton<DistrictManager>.instance.m_districts.m_buffer[0];
        }
        private static int ClampPercentage(int value)
        {
            return (int)Mathf.Clamp(value, 0f, 100f);
        }
        private static Color32 GetGaugeColor(InformationInfo info, int percentage)
        {
            var value = info.HigherIsBetter ? percentage : 100 - percentage;

            if (value >= 75)
                return new Color32(95, 194, 61, 255);
            else if (value >= 50)
                return new Color32(234, 188, 57, 255);
            else
                return new Color32(229, 82, 71, 255);
        }
        private static UITextureAtlas GetIndicatorAtlas()
        {
            if (IndicatorAtlas != null)
                return IndicatorAtlas;

            try
            {
                var sprites = new Dictionary<string, RectOffset>();
                foreach (var spriteName in IndicatorSpriteNames)
                    sprites[spriteName] = new RectOffset();

                IndicatorAtlas = TextureHelper.CreateAtlas("InformationPanelIndicators", sprites);
            }
            catch (Exception e)
            {
                SingletonMod<Mod>.Logger.Error(e);
            }

            return IndicatorAtlas;
        }
        private static UITextureAtlas GetIndicatorAtlas(string name)
        {
            return GetIndicatorAtlas();
        }
        internal static bool IsCollapsedBuildingProblem(ProblemStruct problem)
        {
            return (problem & CollapsedBuildingProblem).IsNotNone;
        }
        internal static void ResolveProblemIcon(ProblemStruct problem, UITextureAtlas fallbackAtlas, out UITextureAtlas atlas, out string spriteName)
        {
            if (IsCollapsedBuildingProblem(problem) || Settings.IsAbandonedBuildingProblem(problem) || Settings.IsBurnedDownBuildingProblem(problem))
            {
                atlas = GetIndicatorAtlas();
                spriteName = IsCollapsedBuildingProblem(problem) ?
                    CollapsedIconSprite :
                    Settings.IsAbandonedBuildingProblem(problem) ? AbandonedIconSprite : BurnedDownIconSprite;

                if (atlas != null && atlas[spriteName] != null)
                {
                    return;
                }
            }

            atlas = fallbackAtlas;
            spriteName = Settings.GetIcon(problem);
        }
        internal static bool TryGetProblemIcon(ProblemStruct problem, UITextureAtlas fallbackAtlas, out UITextureAtlas atlas, out string spriteName)
        {
            ResolveProblemIcon(problem, fallbackAtlas, out atlas, out spriteName);
            return atlas != null && !string.IsNullOrEmpty(spriteName) && atlas[spriteName] != null;
        }
        private static string GetIndicatorSpriteName(string name, int percentage)
        {
            if (UseIndicatorsList2(name))
            {
                if (percentage > 87.5)
                    return "GreenIndicator";
                else if (percentage > 75)
                    return "GreenYellowIndicator2";
                else if (percentage > 62.5)
                    return "GreenYellowIndicator";
                else if (percentage > 50)
                    return "YellowIndicator2";
                else if (percentage > 37.5)
                    return "YellowIndicator";
                else if (percentage > 25)
                    return "YellowRedIndicator";
                else if (percentage > 12.5)
                    return "RedIndicator2";
                else
                    return "RedIndicator";
            }

            if (UseNewIndicators(name))
            {
                if (percentage > 87.5)
                    return "RedIndicator";
                else if (percentage > 75)
                    return "RedIndicator2";
                else if (percentage > 62.5)
                    return "YellowRedIndicator";
                else if (percentage > 50)
                    return "YellowIndicator";
                else if (percentage > 37.5)
                    return "YellowIndicator2";
                else if (percentage > 25)
                    return "GreenYellowIndicator";
                else if (percentage > 12.5)
                    return "GreenYellowIndicator2";
                else
                    return "GreenIndicator";
            }
            
            if (percentage > 87.5)
                return "GreenIndicator";
            else if (percentage > 75)
                return "GreenYellowIndicator2";
            else if (percentage > 62.5)
                return "GreenYellowIndicator";
            else if (percentage > 55)
                return "YellowIndicator2";
            else if (percentage > 45)
                return "YellowIndicator";
            else if (percentage > 25)
                return "RedIndicator3";
            else if (percentage > 12.5)
                return "RedIndicator2";
            else
                return "RedIndicator";
        }

        private static string GetHappinessSpriteName(string name, int percentage)
        {
            if (!UseHappiness(name))
                return string.Empty;

            if (percentage > 80)
                return NotificationIconHappiness100;
            else if (percentage > 60)
                return NotificationIconHappiness80;
            else if (percentage > 40)
                return NotificationIconHappiness60;
            else if (percentage > 20)
                return NotificationIconHappiness40;
            else
                return NotificationIconHappiness20;

        }

        private static bool UseNewIndicators(string name)
        {
            switch (name)
            {
                case "Landfill":
                case "Library":
                case "Cemetery":
                case "GroundPollution":
                case "DrinkingWaterPollution":
                case "NoisePollution":
                case "Fire":
                case "Crime":
                case "Unemployment":
                    return true;
                default:
                    return false;
            }
        }
        private static bool UseIndicatorsList2(string name)
        {
            switch (name)
            {
                case "Traffic":
                case "Health":
                case "CityAttractiveness":
                    return true;
                default:
                    return false;
            }
        }
        private static bool UseHappiness(string name)
        {
            switch (name)
            {
                case "Happiness":
                    return true;
                default:
                    return false;
            }
        }
        private static void OpenInfoTab(InformationInfo info)
        {
            var infoManager = Singleton<InfoManager>.instance;
            if (infoManager == null)
                return;

            if (info.Matches(infoManager.CurrentMode, infoManager.CurrentSubMode))
                infoManager.SetCurrentMode(InfoManager.InfoMode.None, InfoManager.SubInfoMode.None);
            else
                infoManager.SetCurrentMode(info.InfoMode, info.SubInfoMode);
        }

        private static readonly InformationInfo[] Infos =
        {
            new InformationInfo("Electricity", "Electricity", true, InfoManager.InfoMode.Electricity, InfoManager.SubInfoMode.Default, "E", "ToolbarIconElectricity", "InfoIconElectricity", "ElectricityIcon", "ElectricityNormal"),
            new InformationInfo("Water", "Water", true, InfoManager.InfoMode.Water, InfoManager.SubInfoMode.Default, "W", "ToolbarIconWaterAndSewage", "InfoIconWater", "InfoIconWater2", "WaterIcon", "WaterNormal"),
            new InformationInfo("Sewage", "Sewage", true, InfoManager.InfoMode.Water, InfoManager.SubInfoMode.Default, "S", "SubBarWaterServices", "InfoIconWater2", "SewageIcon", "SewageNormal"),
            new InformationInfo("Garbage", "Garbage", true, InfoManager.InfoMode.Garbage, InfoManager.SubInfoMode.Default, "G", "ToolbarIconGarbage", "InfoIconGarbage", "GarbageIcon", "GarbageNormal"),
            new InformationInfo("ElementarySchool", "Elementary school", true, InfoManager.InfoMode.Education, InfoManager.SubInfoMode.ElementarySchool, "ES", "SubBarEducationElementarySchool", "InfoIconEducation", "ToolbarIconEducation", "EducationIcon"),
            new InformationInfo("HighSchool", "High school", true, InfoManager.InfoMode.Education, InfoManager.SubInfoMode.HighSchool, "HS", "SubBarEducationHighSchool", "InfoIconEducation", "ToolbarIconEducation", "EducationIcon"),
            new InformationInfo("University", "University", true, InfoManager.InfoMode.Education, InfoManager.SubInfoMode.University, "U", "SubBarEducationUniversity", "InfoIconEducation", "ToolbarIconEducation", "EducationIcon"),
            new InformationInfo("Healthcare", "Healthcare", true, InfoManager.InfoMode.Health, InfoManager.SubInfoMode.HealthCare, "HC", "ToolbarIconHealthcare", "InfoIconHealth", "HealthIcon", "HealthcareIcon"),
            new InformationInfo("Crematorium", "Crematorium", true, InfoManager.InfoMode.Health, InfoManager.SubInfoMode.DeathCare, "Cr", "SubBarHealthcareDeathcare", "DeathCareIcon", "CrematoriumIcon", "ToolbarIconHealthcare"),
            new InformationInfo("FireDepartment", "Fire department", true, InfoManager.InfoMode.FireSafety, InfoManager.SubInfoMode.Default, "FD", "ToolbarIconFireDepartment", "InfoIconFireSafety", "FireSafetyIcon", "FireDepartmentIcon", "FireNormal"),
            new InformationInfo("PoliceDepartment", "Police department", true, InfoManager.InfoMode.CrimeRate, InfoManager.SubInfoMode.Default, "P", "ToolbarIconPolice", "InfoIconCrimeRate", "PoliceIcon", "CrimeNormal"),
            new InformationInfo("Jail", "Jail", true, InfoManager.InfoMode.CrimeRate, InfoManager.SubInfoMode.Prisons, "J", "SubBarPolicePrison", "PrisonIcon", "JailIcon", "ToolbarIconPolice"),
            new InformationInfo("Heating", "Heating", true, InfoManager.InfoMode.Heating, InfoManager.SubInfoMode.Default, "H", "SubBarWaterHeatingGroup", "InfoIconHeating", "HeatingIcon", "ToolbarIconWaterAndSewage", "HeatingNormal"),
            new InformationInfo("Landfill", "Landfill", false, InfoManager.InfoMode.Garbage, InfoManager.SubInfoMode.Default, "Lf", "LandfillIcon", "ToolbarIconGarbage", "GarbageIcon", "GarbageNormal"),
            new InformationInfo("Library", "Library", false, InfoManager.InfoMode.Education, InfoManager.SubInfoMode.LibraryEducation, "Li", "LibraryIcon", "InfoIconEducation", "ToolbarIconEducation", "EducationIcon"),
            new InformationInfo("Cemetery", "Cemetery", false, InfoManager.InfoMode.Health, InfoManager.SubInfoMode.DeathCare, "Ce", "CemeteryIcon", "DeathCareIcon", "ToolbarIconHealthcare"),
            new InformationInfo("Traffic", "Traffic", true, InfoManager.InfoMode.Traffic, InfoManager.SubInfoMode.Default, "T", "InfoIconTrafficCongestion", "InfoIconTraffic", "TrafficIcon", "ToolbarIconRoads"),
            new InformationInfo("GroundPollution", "Ground pollution", false, InfoManager.InfoMode.Pollution, InfoManager.SubInfoMode.Default, "GP", "InfoIconPollution", "PollutionIcon", "PollutionNormal"),
            new InformationInfo("DrinkingWaterPollution", "Drinking water pollution", false, InfoManager.InfoMode.Pollution, InfoManager.SubInfoMode.Default, "WP", "InfoIconWater", "InfoIconWater2", "WaterPollutionIcon", "DirtyWaterNormal"),
            new InformationInfo("NoisePollution", "Noise pollution", false, InfoManager.InfoMode.NoisePollution, InfoManager.SubInfoMode.Default, "N", "InfoIconNoisePollution", "NoisePollutionIcon", "NoiseNormal"),
            new InformationInfo("Fire", "Fire", false, InfoManager.InfoMode.FireSafety, InfoManager.SubInfoMode.Default, "F", "InfoIconFireSafety", "FireSafetyIcon", "FireIcon", "FireNormal"),
            new InformationInfo("Crime", "Crime", false, InfoManager.InfoMode.CrimeRate, InfoManager.SubInfoMode.Default, "C", "InfoIconCrimeRate", "CrimeIcon", "ToolbarIconPolice", "CrimeNormal"),
            new InformationInfo("Unemployment", "Unemployment", false, InfoManager.InfoMode.Density, InfoManager.SubInfoMode.Default, "Un", "InfoIconEmployment", "EmploymentIcon", "InfoIconDensity", "ToolbarIconZoning"),
            new InformationInfo("Health", "Health", true, InfoManager.InfoMode.Health, InfoManager.SubInfoMode.HealthCare, "He", "InfoIconHealth", "HealthIcon", "ToolbarIconHealthcare"),
            new InformationInfo("CityAttractiveness", "City attractiveness", true, InfoManager.InfoMode.Tourism, InfoManager.SubInfoMode.Attractiveness, "A", "InfoIconTourism", "TourismIcon", "AttractivenessIcon", "ToolbarIconBeautification"),
            new InformationInfo("Happiness", "Happiness", true, InfoManager.InfoMode.Happiness, InfoManager.SubInfoMode.Default, "Ha", NotificationIconHappiness60, NotificationIconHappiness80, NotificationIconHappiness100, NotificationIconHappiness40, NotificationIconHappiness20, "InfoPanelIconHappiness", "InfoIconHappiness", "HappinessIcon", "ToolbarIconBeautification"),
        };
        private static readonly string[] IndicatorSpriteNames =
        {
            CollapsedIconSprite,
            AbandonedIconSprite,
            BurnedDownIconSprite,
            "GreenIndicator",
            "GreenYellowIndicator",
            "GreenYellowIndicator2",
            "RedIndicator",
            "RedIndicator2",
            "RedIndicator3",
            NotificationIconHappiness20,
            NotificationIconHappiness40,
            NotificationIconHappiness60,
            NotificationIconHappiness80,
            NotificationIconHappiness100,
            SettingsIconSprite,
            "YellowIndicator",
            "YellowIndicator2",
            "YellowRedIndicator",
        };

        private class InformationInfo
        {
            public string Name { get; }
            public string Title { get; }
            public bool HigherIsBetter { get; }
            public InfoManager.InfoMode InfoMode { get; }
            public InfoManager.SubInfoMode SubInfoMode { get; }
            public string FallbackText { get; }
            public string[] IconCandidates { get; }

            public InformationInfo(string name, string title, bool higherIsBetter, InfoManager.InfoMode infoMode, InfoManager.SubInfoMode subInfoMode, string fallbackText, params string[] iconCandidates)
            {
                Name = name;
                Title = title;
                HigherIsBetter = higherIsBetter;
                InfoMode = infoMode;
                SubInfoMode = subInfoMode;
                FallbackText = fallbackText;
                IconCandidates = iconCandidates;
            }

            public bool Matches(InfoManager.InfoMode infoMode, InfoManager.SubInfoMode subInfoMode)
            {
                return InfoMode == infoMode && SubInfoMode == subInfoMode;
            }
        }

        private class IconInfo
        {
            public UITextureAtlas Atlas { get; }
            public string SpriteName { get; }
            public bool IsFallback { get; }

            public IconInfo(UITextureAtlas atlas, string spriteName, bool isFallback)
            {
                Atlas = atlas;
                SpriteName = spriteName;
                IsFallback = isFallback;
            }
        }

        private class InformationPanelItem : CustomUIButton
        {
            public InformationInfo Info { get; private set; }

            private CustomUISprite Indicator { get; set; }
            private CustomUISprite GaugeTrack { get; set; }
            private CustomUISprite GaugeFill { get; set; }

            public override void Awake()
            {
                base.Awake();

                size = new Vector2(TileSize, TileSize);
                isInteractive = true;
                clipChildren = true;

                BgAtlas = CommonTextures.Atlas;
                BgSprites = CommonTextures.PanelSmall;
                SelBgSprites = CommonTextures.PanelSmall;
                BgColors = new ColorSet(
                    new Color32(255, 255, 255, 0),
                    new Color32(255, 255, 255, 42),
                    new Color32(255, 255, 255, 72),
                    new Color32(255, 255, 255, 0),
                    new Color32(255, 255, 255, 0));
                SelBgColors = new ColorSet(
                    new Color32(255, 255, 255, 0),
                    new Color32(255, 255, 255, 42),
                    new Color32(255, 255, 255, 72),
                    new Color32(255, 255, 255, 0),
                    new Color32(255, 255, 255, 0));

                AllIconColors = Color.white;
                AllTextColors = Color.white;
                IconMode = SpriteMode.FixedSize;
                IconSize = new Vector2(InformationPanel.IconSize, InformationPanel.IconSize);
                IconPadding = new RectOffset();
                HorizontalAlignment = UIHorizontalAlignment.Center;
                VerticalAlignment = UIVerticalAlignment.Middle;
                TextHorizontalAlignment = UIHorizontalAlignment.Center;
                TextVerticalAlignment = UIVerticalAlignment.Middle;
                TextPadding = new RectOffset(0, 0, 1, 0);
                textScale = 0.56f;
                useDropShadow = true;
                dropShadowColor = new Color32(0, 0, 0, 192);
                dropShadowOffset = new Vector2(1f, -1f);

                Indicator = AddUIComponent<CustomUISprite>();
                Indicator.name = "Information indicator";
                Indicator.atlas = GetIndicatorAtlas();
                Indicator.size = new Vector2(IndicatorSize, IndicatorSize);
                Indicator.color = Color.white;
                Indicator.isInteractive = false;
                Indicator.isVisible = false;

                GaugeTrack = AddUIComponent<CustomUISprite>();
                GaugeTrack.name = "Information gauge track";
                GaugeTrack.atlas = CommonTextures.Atlas;
                GaugeTrack.spriteName = CommonTextures.EmptyWithoutBorder;
                GaugeTrack.color = new Color32(0, 0, 0, 128);
                GaugeTrack.isInteractive = false;

                GaugeFill = AddUIComponent<CustomUISprite>();
                GaugeFill.name = "Information gauge fill";
                GaugeFill.atlas = CommonTextures.Atlas;
                GaugeFill.spriteName = CommonTextures.EmptyWithoutBorder;
                GaugeFill.fillDirection = UIFillDirection.Horizontal;
                GaugeFill.isInteractive = false;

                SetGaugeSize();
            }
            public void Init(InformationInfo info)
            {
                Info = info;

                var icon = ResolveIcon(info);
                IconAtlas = icon.Atlas;
                AllIconSprites = icon.SpriteName;
                text = icon.IsFallback ? info.FallbackText : string.Empty;

                tooltip = info.Title;
                eventMouseDown += OnItemMouseDown;
                eventMouseMove += OnItemMouseMove;
                eventMouseUp += OnItemMouseUp;
                eventClick += OnItemClick;
            }
            public void RefreshValue()
            {
                if (Info == null)
                    return;

                var percentage = GetPercentage(Info.Name);
                GaugeFill.fillAmount = percentage / 100f;
                GaugeFill.color = GetGaugeColor(Info, percentage);
                SetHappinessIcon(percentage);
                SetIndicator(percentage);
                tooltip = string.Format("{0}: {1}%", Info.Title, percentage);
            }
            public void SetSelected(bool selected)
            {
                IsSelected = selected;
            }
            protected override void OnSizeChanged()
            {
                base.OnSizeChanged();
                SetGaugeSize();
            }
            private void SetGaugeSize()
            {
                if (GaugeTrack == null || GaugeFill == null)
                    return;

                if (Indicator != null)
                {
                    Indicator.size = new Vector2(IndicatorSize, IndicatorSize);
                    Indicator.relativePosition = new Vector3((width - Indicator.width) * 0.5f, (height - Indicator.height) * 0.5f);
                }

                var gaugeWidth = Mathf.Max(0f, width - 6f);
                GaugeTrack.size = new Vector2(gaugeWidth, 3f);
                GaugeFill.size = GaugeTrack.size;

                var position = new Vector3((width - gaugeWidth) * 0.5f, height - GaugeTrack.height);
                GaugeTrack.relativePosition = position;
                GaugeFill.relativePosition = position;
            }
            private void SetIndicator(int percentage)
            {
                if (Indicator == null || Info == null)
                    return;

                if (UseHappiness(Info.Name))
                {
                    Indicator.isVisible = false;
                    return;
                }

                var spriteName = GetIndicatorSpriteName(Info.Name, percentage);
                var atlas = GetIndicatorAtlas(Info.Name);

                Indicator.isVisible = atlas != null && !string.IsNullOrEmpty(spriteName) && atlas[spriteName] != null;
                if (Indicator.isVisible)
                {
                    Indicator.atlas = atlas;
                    Indicator.spriteName = spriteName;
                }
            }
            private void SetHappinessIcon(int percentage)
            {
                if (Info == null || !UseHappiness(Info.Name))
                    return;

                var spriteName = GetHappinessSpriteName(Info.Name, percentage);
                var atlas = GetIndicatorAtlas();
                if (atlas == null || string.IsNullOrEmpty(spriteName) || atlas[spriteName] == null)
                    return;

                IconAtlas = atlas;
                AllIconSprites = spriteName;
                text = string.Empty;
            }
            private void OnItemClick(UIComponent component, UIMouseEventParameter eventParam)
            {
                if (eventParam.used)
                    return;

                if ((parent as InformationPanel)?.ConsumePanelDragClick() == true)
                {
                    eventParam.Use();
                    return;
                }

                if (Info != null)
                {
                    OpenInfoTab(Info);
                    eventParam.Use();
                }
            }
            private void OnItemMouseDown(UIComponent component, UIMouseEventParameter eventParam)
            {
                (parent as InformationPanel)?.BeginPanelDrag(eventParam);
            }
            private void OnItemMouseMove(UIComponent component, UIMouseEventParameter eventParam)
            {
                (parent as InformationPanel)?.MovePanelDrag(eventParam);
            }
            private void OnItemMouseUp(UIComponent component, UIMouseEventParameter eventParam)
            {
                (parent as InformationPanel)?.EndPanelDrag(eventParam);
            }
            private static IconInfo ResolveIcon(InformationInfo info)
            {
                var atlases = Resources.FindObjectsOfTypeAll(typeof(UITextureAtlas)) as UITextureAtlas[];
                if (atlases != null)
                {
                    foreach (var candidate in info.IconCandidates)
                    {
                        if (string.IsNullOrEmpty(candidate))
                            continue;

                        foreach (var atlas in atlases)
                        {
                            if (atlas != null && atlas[candidate] != null)
                                return new IconInfo(atlas, candidate, false);
                        }
                    }
                }

                return new IconInfo(CommonTextures.Atlas, string.Empty, true);
            }
        }

        private class SettingsButton : CustomUIButton
        {
            public override void Awake()
            {
                base.Awake();

                size = new Vector2(TileSize, TileSize);
                isInteractive = true;
                clipChildren = true;

                BgAtlas = CommonTextures.Atlas;
                BgSprites = CommonTextures.PanelSmall;
                SelBgSprites = CommonTextures.PanelSmall;
                BgColors = new ColorSet(
                    new Color32(255, 255, 255, 0),
                    new Color32(255, 255, 255, 42),
                    new Color32(255, 255, 255, 72),
                    new Color32(255, 255, 255, 0),
                    new Color32(255, 255, 255, 0));
                SelBgColors = new ColorSet(
                    new Color32(255, 255, 255, 0),
                    new Color32(255, 255, 255, 42),
                    new Color32(255, 255, 255, 72),
                    new Color32(255, 255, 255, 0),
                    new Color32(255, 255, 255, 0));

                AllIconColors = Color.white;
                IconMode = SpriteMode.FixedSize;
                IconSize = new Vector2(InformationPanel.IconSize, InformationPanel.IconSize);
                IconPadding = new RectOffset();
                HorizontalAlignment = UIHorizontalAlignment.Center;
                VerticalAlignment = UIVerticalAlignment.Middle;
                AllTextColors = Color.white;
                TextHorizontalAlignment = UIHorizontalAlignment.Center;
                TextVerticalAlignment = UIVerticalAlignment.Middle;
                TextPadding = new RectOffset(0, 0, 1, 0);
                textScale = 0.65f;
                useDropShadow = true;
                dropShadowColor = new Color32(0, 0, 0, 192);
                dropShadowOffset = new Vector2(1f, -1f);
            }
            public void Init()
            {
                SetIcon();
                tooltip = "Settings";
                eventMouseDown += OnButtonMouseDown;
                eventMouseMove += OnButtonMouseMove;
                eventMouseUp += OnButtonMouseUp;
                eventClick += OnButtonClick;
            }
            public void SetSelected(bool selected)
            {
                IsSelected = selected;
            }
            private void SetIcon()
            {
                var atlas = GetIndicatorAtlas();
                if (atlas != null && atlas[SettingsIconSprite] != null)
                {
                    IconAtlas = atlas;
                    AllIconSprites = SettingsIconSprite;
                    text = string.Empty;
                }
                else
                    text = "S";
            }
            private void OnButtonClick(UIComponent component, UIMouseEventParameter eventParam)
            {
                if (eventParam.used)
                    return;

                if ((parent as InformationPanel)?.ConsumePanelDragClick() == true)
                {
                    eventParam.Use();
                    return;
                }

                ToggleSettingsMenu(this);
                SetSelected(PopupMenu != null && PopupMenu.isVisible);
                eventParam.Use();
            }
            private void OnButtonMouseDown(UIComponent component, UIMouseEventParameter eventParam)
            {
                (parent as InformationPanel)?.BeginPanelDrag(eventParam);
            }
            private void OnButtonMouseMove(UIComponent component, UIMouseEventParameter eventParam)
            {
                (parent as InformationPanel)?.MovePanelDrag(eventParam);
            }
            private void OnButtonMouseUp(UIComponent component, UIMouseEventParameter eventParam)
            {
                (parent as InformationPanel)?.EndPanelDrag(eventParam);
            }
        }

        private class SettingsMenu : CustomUIPanel
        {
            private CustomUIButton LayoutButton { get; set; }
            private CustomUIButton AllProblemsButton { get; set; }
            private CustomUISlider TransparencySlider { get; set; }
            private CustomUILabel TransparencyValue { get; set; }
            private UIComponent Source { get; set; }
            private bool IsSyncingControls { get; set; }

            public override void Awake()
            {
                base.Awake();

                name = "Information panel settings menu";
                gameObject.name = name;
                isInteractive = true;
                clipChildren = true;
                width = SettingsMenuWidth;

                Atlas = CommonTextures.Atlas;
                BackgroundSprite = CommonTextures.PanelSmall;
                BgColors = new Color32(48, 52, 54, 238);

                AutoLayout = AutoLayout.Vertical;
                AutoChildrenHorizontally = AutoLayoutChildren.Fill;
                AutoChildrenVertically = AutoLayoutChildren.Fit;
                AutoLayoutSpace = (int)SettingsMenuSpace;
                Padding = new RectOffset((int)SettingsMenuPadding, (int)SettingsMenuPadding, (int)SettingsMenuPadding, (int)SettingsMenuPadding);
            }
            public void Init(UIComponent source)
            {
                Source = source;

                AddTransparencySlider();
                LayoutButton = AddButton(GetLayoutText(), OnLayoutClick);
                AddButton("Show limits", OnLimitsClick);
                AddButton("Localize problems", OnLocalizeProblemsClick);
                AddButton("Show statistics", OnStatisticsClick);
                AllProblemsButton = AddButton(GetAllProblemsText(), OnAllProblemsClick);

                height = SettingsMenuPadding * 2f + SettingsMenuRowHeight * 5f + SettingsMenuSliderRowHeight + SettingsMenuSpace * 5f;
                SetPosition(source);
                SyncControls();
                BringToFront();
            }
            public override void Update()
            {
                base.Update();

                SyncControls();
                CloseIfClickedOutside();
            }
            public void SetPosition(UIComponent source)
            {
                Source = source ?? Source;

                var view = UIView.GetAView();
                var resolution = view?.GetScreenResolution() ?? new Vector2(1920f, 1080f);
                var sourcePosition = Source?.absolutePosition ?? Vector3.zero;

                var x = sourcePosition.x;
                var y = sourcePosition.y + (Source?.height ?? TileSize) + 4f;
                if (y + height > resolution.y)
                    y = sourcePosition.y - height - 4f;

                x = Mathf.Clamp(x, 0f, Mathf.Max(0f, resolution.x - width));
                y = Mathf.Clamp(y, 0f, Mathf.Max(0f, resolution.y - height));
                relativePosition = new Vector3(x, y);
            }
            private CustomUIButton AddButton(string text, MouseEventHandler click)
            {
                var button = AddUIComponent<CustomUIButton>();
                button.name = text;
                button.text = text;
                button.height = SettingsMenuRowHeight;
                button.textScale = 0.8f;
                button.ButtonSettingsStyle();
                button.eventClick += click;
                return button;
            }
            private void AddTransparencySlider()
            {
                var panel = AddUIComponent<CustomUIPanel>();
                panel.name = "Panel transparency";
                panel.height = SettingsMenuSliderRowHeight;
                panel.AutoLayout = AutoLayout.Disabled;
                panel.Atlas = CommonTextures.Atlas;
                panel.BackgroundSprite = CommonTextures.EmptyWithoutBorder;
                panel.isInteractive = true;

                var label = panel.AddUIComponent<CustomUILabel>();
                label.name = "Transparency label";
                label.AutoSize = AutoSize.None;
                label.size = new Vector2(160f, 18f);
                label.relativePosition = Vector3.zero;
                label.text = "Panel transparency";
                label.textScale = 0.72f;
                label.VerticalAlignment = UIVerticalAlignment.Middle;
                label.HorizontalAlignment = UIHorizontalAlignment.Left;
                label.isInteractive = false;

                TransparencyValue = panel.AddUIComponent<CustomUILabel>();
                TransparencyValue.name = "Transparency value";
                TransparencyValue.AutoSize = AutoSize.None;
                TransparencyValue.size = new Vector2(64f, 18f);
                TransparencyValue.relativePosition = new Vector3(width - Padding.horizontal - TransparencyValue.width, 0f);
                TransparencyValue.textScale = 0.72f;
                TransparencyValue.VerticalAlignment = UIVerticalAlignment.Middle;
                TransparencyValue.HorizontalAlignment = UIHorizontalAlignment.Right;
                TransparencyValue.isInteractive = false;

                TransparencySlider = panel.AddUIComponent<CustomUISlider>();
                TransparencySlider.name = "Transparency slider";
                TransparencySlider.size = new Vector2(width - Padding.horizontal, 18f);
                TransparencySlider.relativePosition = new Vector3(0f, 23f);
                TransparencySlider.Orientation = UIOrientation.Horizontal;
                TransparencySlider.MinValue = 0f;
                TransparencySlider.MaxValue = 100f;
                TransparencySlider.StepSize = 1f;
                TransparencySlider.ScrollWheelAmount = 1f;
                TransparencySlider.BgAtlas = CommonTextures.Atlas;
                TransparencySlider.BgSprite = CommonTextures.PanelSmall;
                TransparencySlider.BgColor = ComponentStyle.SettingsColor30;
                TransparencySlider.ThumbAtlas = CommonTextures.Atlas;
                TransparencySlider.ThumbSprites = CommonTextures.Circle;
                TransparencySlider.ThumbColors = new ColorSet(
                    ComponentStyle.SettingsColor85,
                    ComponentStyle.SettingsColor95,
                    ComponentStyle.NormalBlue,
                    ComponentStyle.SettingsColor85,
                    ComponentStyle.SettingsColor30);
                TransparencySlider.ThumbSize = new Vector2(18f, 18f);
                TransparencySlider.Value = Settings.InformationPanelDragBackgroundOpacity.value;
                TransparencySlider.OnSliderValueChanged += OnTransparencyChanged;
                TransparencySlider.eventMouseUp += OnTransparencyMouseUp;

                UpdateTransparencyValue(Settings.InformationPanelDragBackgroundOpacity.value);
            }
            private string GetLayoutText()
            {
                return Settings.InformationPanelVerticalLayout ? "Horizontal layout" : "Vertical layout";
            }
            private string GetAllProblemsText()
            {
                return Settings.AllProblemsToggleLabel;
            }
            private void SyncControls()
            {
                var transparency = Mathf.Clamp(Settings.InformationPanelDragBackgroundOpacity.value, 0, 100);
                UpdateTransparencyValue(transparency);

                try
                {
                    IsSyncingControls = true;
                    if (LayoutButton != null)
                        LayoutButton.text = GetLayoutText();

                    if (AllProblemsButton != null)
                        AllProblemsButton.text = GetAllProblemsText();

                    if (TransparencySlider != null && Mathf.RoundToInt(TransparencySlider.Value) != transparency)
                        TransparencySlider.Value = transparency;
                }
                finally
                {
                    IsSyncingControls = false;
                }
            }
            private void CloseIfClickedOutside()
            {
                if (!Input.GetMouseButtonDown(0) && !Input.GetMouseButtonDown(1) && !Input.GetMouseButtonDown(2))
                    return;

                var mousePosition = GetUiMousePosition();
                if (ContainsPoint(this, mousePosition) || ContainsPoint(Source, mousePosition))
                    return;

                CloseSettingsMenu();
            }
            private Vector2 GetUiMousePosition()
            {
                var view = UIView.GetAView();
                if (view != null)
                    return view.ScreenPointToGUI(Input.mousePosition / view.inputScale);

                return new Vector2(Input.mousePosition.x, Screen.height - Input.mousePosition.y);
            }
            private bool ContainsPoint(UIComponent component, Vector2 point)
            {
                if (component == null || !component.isVisible)
                    return false;

                return new Rect(component.absolutePosition, component.size).Contains(point);
            }
            private void OnLayoutClick(UIComponent component, UIMouseEventParameter eventParam)
            {
                if (eventParam.used)
                    return;

                Settings.InformationPanelVerticalLayout.value = !Settings.InformationPanelVerticalLayout.value;
                InformationPanel.RefreshLayout();
                CloseSettingsMenu();
                eventParam.Use();
            }
            private void OnTransparencyChanged(float value)
            {
                if (IsSyncingControls)
                    return;

                var transparency = Mathf.RoundToInt(value);
                Settings.InformationPanelDragBackgroundOpacity.value = transparency;
                UpdateTransparencyValue(transparency);
                InformationPanel.RefreshBackgroundOpacity();
            }
            private void OnTransparencyMouseUp(UIComponent component, UIMouseEventParameter eventParam)
            {
                CloseSettingsMenu();
                eventParam.Use();
            }
            private void UpdateTransparencyValue(int value)
            {
                if (TransparencyValue != null)
                    TransparencyValue.text = value.ToString() + "%";
            }
            private void OnLimitsClick(UIComponent component, UIMouseEventParameter eventParam)
            {
                if (eventParam.used)
                    return;

                LimitsPanel.Create(Source);
                CloseSettingsMenu();
                eventParam.Use();
            }
            private void OnLocalizeProblemsClick(UIComponent component, UIMouseEventParameter eventParam)
            {
                if (eventParam.used)
                    return;

                ProblemLocalizationPanel.Create(Source);
                CloseSettingsMenu();
                eventParam.Use();
            }
            private void OnStatisticsClick(UIComponent component, UIMouseEventParameter eventParam)
            {
                if (eventParam.used)
                    return;

                UIView.library.ShowModal("StatisticsPanel");
                CloseSettingsMenu();
                eventParam.Use();
            }
            private void OnAllProblemsClick(UIComponent component, UIMouseEventParameter eventParam)
            {
                if (eventParam.used)
                    return;

                Settings.ToggleAllProblemsDisabled();
                SyncControls();
                eventParam.Use();
            }
        }
    }
    public class ProblemPanel : CustomUIPanel
    {
        private const float HorizontalFirstRowHeight = 25f; // horizonal layout row height
        private const float HorizontalExtraRowHeight = 25f;
        private const float VerticalRowHeight = 25f; //set row height in vertical layout
        private const float ItemWidth = 66f;
        private const float HorizontalItemSpacing = 12f;
        private const float IconSize = 20f;
        private const float IconVerticalOffset = 3f;
        private const float CountOffset = IconSize + 6f; // space between icon and count text
        private const float CountTextScale = 1f;    //size of text showing problem count
        private const float RefreshInterval = 5f;

        private static ProblemPanel Instance { get; set; }

        private InformationPanel Owner { get; set; }
        private CustomUIPanel Content { get; set; }
        private UITextureAtlas NotificationAtlas { get; set; }
        private List<ProblemPanelItem> Items { get; } = new List<ProblemPanelItem>();
        private int VisibleItemCount { get; set; }
        private float LastRefreshTime { get; set; }

        public static void Create()
        {
            var panel = InformationPanel.CurrentProblemPanel;
            if (panel == null)
                return;

            if (panel.RefreshNow())
                panel.Owner?.RefreshProblemPanelLayout();
        }
        public static void Release()
        {
            ProblemLocalizationPanel.Release();

            if (Instance != null)
            {
                var instance = Instance;
                Instance = null;
                UnityEngine.Object.Destroy(instance.gameObject);
            }
        }
        public static void RefreshProblemPanel()
        {
            if (Instance != null && Instance.RefreshNow())
                Instance.Owner?.RefreshProblemPanelLayout();

            ProblemLocalizationPanel.RefreshProblemLocalizationPanel();
        }

        public void Init(InformationPanel owner)
        {
            Owner = owner;
            Instance = this;
        }
        public override void Awake()
        {
            base.Awake();

            name = "Problem panel";
            gameObject.name = name;
            isVisible = false;
            isInteractive = true;
            clipChildren = true;

            Atlas = CommonTextures.Atlas;
            BackgroundSprite = CommonTextures.EmptyWithoutBorder;
            NormalBgColor = new Color32(255, 255, 255, 0);
            HoveredBgColor = new Color32(255, 255, 255, 0);

            NotificationAtlas = TextureHelper.GetAtlas("Notifications");

            eventMouseDown += OnPanelMouseDown;
            eventMouseMove += OnPanelMouseMove;
            eventMouseUp += OnPanelMouseUp;
            eventClick += OnPanelClick;

            Content = AddUIComponent<CustomUIPanel>();
            Content.name = "Problem panel content";
            Content.Atlas = CommonTextures.Atlas;
            Content.BackgroundSprite = CommonTextures.EmptyWithoutBorder;
            Content.NormalBgColor = new Color32(255, 255, 255, 0);
            Content.HoveredBgColor = new Color32(255, 255, 255, 0);
            Content.isInteractive = true;
            Content.clipChildren = true;
            Content.eventMouseDown += OnPanelMouseDown;
            Content.eventMouseMove += OnPanelMouseMove;
            Content.eventMouseUp += OnPanelMouseUp;
            Content.eventClick += OnPanelClick;
        }
        public override void Update()
        {
            base.Update();

            if (Time.realtimeSinceStartup - LastRefreshTime >= RefreshInterval && RefreshNow())
                Owner?.RefreshProblemPanelLayout();
        }

        internal float GetLayoutHeight(bool verticalLayout, float panelWidth)
        {
            if (VisibleItemCount <= 0)
                return 0f;

            if (verticalLayout)
                return VisibleItemCount * VerticalRowHeight;

            var rows = GetHorizontalRows(panelWidth);
            return HorizontalFirstRowHeight + Mathf.Max(0, rows - 1) * HorizontalExtraRowHeight;
        }
        internal void ApplyLayout(bool verticalLayout, float panelWidth, float y)
        {
            var panelHeight = GetLayoutHeight(verticalLayout, panelWidth);
            isVisible = VisibleItemCount > 0;
            size = new Vector2(panelWidth, panelHeight);
            relativePosition = new Vector3(0f, y);

            if (Content != null)
            {
                Content.isVisible = isVisible;
                Content.size = size;
                Content.relativePosition = Vector3.zero;
            }

            if (!isVisible)
                return;

            if (verticalLayout)
                ApplyVerticalItems(panelWidth);
            else
                ApplyHorizontalItems(panelWidth);
        }
        internal bool RefreshNow()
        {
            LastRefreshTime = Time.realtimeSinceStartup;

            var layoutWidth = Mathf.Max(width, Owner?.width ?? 0f);
            var oldHeight = GetLayoutHeight(Settings.InformationPanelVerticalLayout, layoutWidth);
            var oldCount = VisibleItemCount;

            try
            {
                var counts = ReadProblemCounts();
                var sortedProblems = GetSortedProblems(counts);
                var itemIndex = 0;

                foreach (var problemCount in sortedProblems)
                {
                    var problem = problemCount.Key;
                    var count = problemCount.Value;

                    var item = GetItem(itemIndex);
                    item.Set(problem, count, NotificationAtlas);
                    item.isVisible = true;
                    itemIndex += 1;
                }

                for (var i = itemIndex; i < Items.Count; i += 1)
                    Items[i].isVisible = false;

                VisibleItemCount = itemIndex;
                isVisible = VisibleItemCount > 0;

                if (layoutWidth > 0f)
                    ApplyLayout(Settings.InformationPanelVerticalLayout, layoutWidth, relativePosition.y);
            }
            catch (Exception e)
            {
                SingletonMod<Mod>.Logger.Error(e);
            }

            var newHeight = GetLayoutHeight(Settings.InformationPanelVerticalLayout, layoutWidth);
            return oldCount != VisibleItemCount || !Mathf.Approximately(oldHeight, newHeight);
        }
        private static List<KeyValuePair<ProblemStruct, int>> GetSortedProblems(Dictionary<ProblemStruct, int> counts)
        {
            var sortedProblems = new List<KeyValuePair<ProblemStruct, int>>();
            foreach (var problem in Settings.PanelProblems)
            {
                if (counts.TryGetValue(problem, out var count) && count > 0)
                    sortedProblems.Add(new KeyValuePair<ProblemStruct, int>(problem, count));
            }

            sortedProblems.Sort((a, b) =>
            {
                var countComparison = b.Value.CompareTo(a.Value);
                if (countComparison != 0)
                    return countComparison;

                return string.Compare(Settings.GetTitle(a.Key), Settings.GetTitle(b.Key), StringComparison.CurrentCultureIgnoreCase);
            });

            return sortedProblems;
        }
        private void ApplyHorizontalItems(float panelWidth)
        {
            var columns = GetHorizontalColumns(panelWidth);
            var index = 0;
            var row = 0;
            var y = 0f;

            while (index < VisibleItemCount)
            {
                var rowHeight = row == 0 ? HorizontalFirstRowHeight : HorizontalExtraRowHeight;
                var rowCount = Mathf.Min(columns, VisibleItemCount - index);
                var rowWidth = rowCount * ItemWidth + Mathf.Max(0, rowCount - 1) * HorizontalItemSpacing;
                var x = Mathf.Max(0f, (panelWidth - rowWidth) * 0.5f);

                for (var column = 0; column < rowCount; column += 1)
                {
                    Items[index].SetLayout(new Vector2(ItemWidth, rowHeight), new Vector3(x + (ItemWidth + HorizontalItemSpacing) * column, y));
                    index += 1;
                }

                y += rowHeight;
                row += 1;
            }
        }
        private void ApplyVerticalItems(float panelWidth)
        {
            var x = Mathf.Max(0f, (panelWidth - ItemWidth) * 0.5f);

            for (var i = 0; i < VisibleItemCount; i += 1)
                Items[i].SetLayout(new Vector2(ItemWidth, VerticalRowHeight), new Vector3(x, VerticalRowHeight * i));
        }
        private int GetHorizontalColumns(float panelWidth)
        {
            return Mathf.Max(1, Mathf.FloorToInt((panelWidth + HorizontalItemSpacing) / (ItemWidth + HorizontalItemSpacing)));
        }
        private int GetHorizontalRows(float panelWidth)
        {
            if (VisibleItemCount <= 0)
                return 0;

            return Mathf.CeilToInt(VisibleItemCount / (float)GetHorizontalColumns(panelWidth));
        }
        private ProblemPanelItem GetItem(int index)
        {
            while (Items.Count <= index)
            {
                var item = Content.AddUIComponent<ProblemPanelItem>();
                item.name = "Problem panel item";
                item.Init(this);
                Items.Add(item);
            }

            return Items[index];
        }
        private void ToggleLocalization(UIMouseEventParameter eventParam)
        {
            if (eventParam.used)
                return;

            if (Owner?.ConsumeChildPanelDragClick() == true)
            {
                eventParam.Use();
                return;
            }

            ProblemLocalizationPanel.Toggle(this);
            eventParam.Use();
        }
        private void BeginDrag(UIMouseEventParameter eventParam)
        {
            Owner?.BeginChildPanelDrag(eventParam);
        }
        private void MoveDrag(UIMouseEventParameter eventParam)
        {
            Owner?.MoveChildPanelDrag(eventParam);
        }
        private void EndDrag(UIMouseEventParameter eventParam)
        {
            Owner?.EndChildPanelDrag(eventParam);
        }
        private void OnPanelMouseDown(UIComponent component, UIMouseEventParameter eventParam)
        {
            BeginDrag(eventParam);
        }
        private void OnPanelMouseMove(UIComponent component, UIMouseEventParameter eventParam)
        {
            MoveDrag(eventParam);
        }
        private void OnPanelMouseUp(UIComponent component, UIMouseEventParameter eventParam)
        {
            EndDrag(eventParam);
        }
        private void OnPanelClick(UIComponent component, UIMouseEventParameter eventParam)
        {
            ToggleLocalization(eventParam);
        }
        private static Dictionary<ProblemStruct, int> ReadProblemCounts()
        {
            var counts = new Dictionary<ProblemStruct, int>();

            var buildingManager = Singleton<BuildingManager>.instance;
            if (buildingManager != null)
            {
                var buildingBuffer = buildingManager.m_buildings.m_buffer;
                for (ushort i = 0; i < buildingBuffer.Length; i += 1)
                {
                    if ((buildingBuffer[i].m_flags & Building.Flags.Created) != 0)
                        AddPanelProblems(counts, Settings.GetBuildingPanelProblems(buildingBuffer[i]));
                }
            }

            var netManager = Singleton<NetManager>.instance;
            if (netManager != null)
            {
                var nodeBuffer = netManager.m_nodes.m_buffer;
                for (ushort i = 0; i < nodeBuffer.Length; i += 1)
                {
                    if ((nodeBuffer[i].m_flags & NetNode.Flags.Created) != 0)
                        AddProblems(counts, nodeBuffer[i].m_problems);
                }

                var segmentBuffer = netManager.m_segments.m_buffer;
                for (ushort i = 0; i < segmentBuffer.Length; i += 1)
                {
                    if ((segmentBuffer[i].m_flags & NetSegment.Flags.Created) != 0)
                        AddPanelProblems(counts, Settings.GetNetworkSegmentPanelProblems(segmentBuffer[i]));
                }
            }

            return counts;
        }
        private static void AddProblems(Dictionary<ProblemStruct, int> counts, ProblemStruct problems)
        {
            AddPanelProblems(counts, Settings.GetPanelProblems(problems));
        }
        private static void AddPanelProblems(Dictionary<ProblemStruct, int> counts, ProblemStruct problems)
        {
            if (problems.IsNone)
                return;

            foreach (var problem in Settings.PanelProblems)
            {
                if ((problems & problem).IsNone)
                    continue;

                counts.TryGetValue(problem, out var count);
                counts[problem] = count + 1;
            }
        }

        private class ProblemPanelItem : CustomUIButton
        {
            private ProblemPanel Owner { get; set; }
            private CustomUISprite Icon { get; set; }
            private CustomUILabel Count { get; set; }

            public override void Awake()
            {
                base.Awake();

                isInteractive = true;
                clipChildren = true;

                BgAtlas = CommonTextures.Atlas;
                BgSprites = CommonTextures.PanelSmall;
                BgColors = new ColorSet(
                    new Color32(255, 255, 255, 0),
                    new Color32(255, 255, 255, 42),
                    new Color32(255, 255, 255, 72),
                    new Color32(255, 255, 255, 0),
                    new Color32(255, 255, 255, 0));

                Icon = AddUIComponent<CustomUISprite>();
                Icon.size = new Vector2(ProblemPanel.IconSize, ProblemPanel.IconSize);
                Icon.color = Color.white;
                Icon.isInteractive = false;

                Count = AddUIComponent<CustomUILabel>();
                Count.AutoSize = AutoSize.None;
                Count.textScale = ProblemPanel.CountTextScale;
                Count.textColor = Color.white;
                Count.VerticalAlignment = UIVerticalAlignment.Middle;
                Count.HorizontalAlignment = UIHorizontalAlignment.Left;
                Count.useDropShadow = true;
                Count.dropShadowColor = new Color32(0, 0, 0, 192);
                Count.dropShadowOffset = new Vector2(1f, -1f);
                Count.isInteractive = false;
            }
            public void Init(ProblemPanel owner)
            {
                Owner = owner;
                eventMouseDown += OnItemMouseDown;
                eventMouseMove += OnItemMouseMove;
                eventMouseUp += OnItemMouseUp;
                eventClick += OnItemClick;
            }
            public void Set(ProblemStruct problem, int count, UITextureAtlas atlas)
            {
                var title = Settings.GetTitle(problem);
                InformationPanel.ResolveProblemIcon(problem, atlas, out var iconAtlas, out var icon);

                tooltip = title;

                Icon.atlas = iconAtlas;
                Icon.spriteName = icon;
                Count.text = count.ToString();
            }
            public void SetLayout(Vector2 itemSize, Vector3 position)
            {
                size = itemSize;
                relativePosition = position;
                RefreshContentLayout();
            }
            protected override void OnSizeChanged()
            {
                base.OnSizeChanged();
                RefreshContentLayout();
            }
            private void RefreshContentLayout()
            {
                var iconY = Icon != null ? Mathf.Max(0f, (height - Icon.height) * 0.5f) + ProblemPanel.IconVerticalOffset : 0f;

                if (Icon != null)
                    Icon.relativePosition = new Vector3(4f, iconY);

                if (Count != null)
                {
                    Count.size = new Vector2(Mathf.Max(0f, width - ProblemPanel.CountOffset), ProblemPanel.IconSize);
                    Count.relativePosition = new Vector3(ProblemPanel.CountOffset, iconY);
                }
            }
            private void OnItemMouseDown(UIComponent component, UIMouseEventParameter eventParam)
            {
                Owner?.BeginDrag(eventParam);
            }
            private void OnItemMouseMove(UIComponent component, UIMouseEventParameter eventParam)
            {
                Owner?.MoveDrag(eventParam);
            }
            private void OnItemMouseUp(UIComponent component, UIMouseEventParameter eventParam)
            {
                Owner?.EndDrag(eventParam);
            }
            private void OnItemClick(UIComponent component, UIMouseEventParameter eventParam)
            {
                Owner?.ToggleLocalization(eventParam);
            }
        }
    }
}
