namespace NoProblems
{
	public class Localize
	{
		public static System.Globalization.CultureInfo Culture {get; set;}
		public static ModsCommon.LocalizeManager LocaleManager {get;} = new ModsCommon.LocalizeManager("Localize", typeof(Localize).Assembly);

		/// <summary>
		/// Disable problem notifications
		/// </summary>
		public static string Mod_Description => LocaleManager.GetString("Mod_Description", Culture);

		/// <summary>
		/// [UPDATED] Added Plaza & Promenades DLC support.
		/// </summary>
		public static string Mod_WhatsNewMessage2_0 => LocaleManager.GetString("Mod_WhatsNewMessage2_0", Culture);

		/// <summary>
		/// [UPDATED] Added the ability to remove problem flags at all, which will affect buildings behavior. Co
		/// </summary>
		public static string Mod_WhatsNewMessage2_1 => LocaleManager.GetString("Mod_WhatsNewMessage2_1", Culture);

		/// <summary>
		/// [UPDATED] New settings UI style.
		/// </summary>
		public static string Mod_WhatsNewMessage2_2 => LocaleManager.GetString("Mod_WhatsNewMessage2_2", Culture);

		/// <summary>
		/// Airport notifications
		/// </summary>
		public static string Settings_AirportGroup => LocaleManager.GetString("Settings_AirportGroup", Culture);

		/// <summary>
		/// Campus notifications
		/// </summary>
		public static string Settings_CampusGroup => LocaleManager.GetString("Settings_CampusGroup", Culture);

		/// <summary>
		/// Disable all
		/// </summary>
		public static string Settings_DisableAll => LocaleManager.GetString("Settings_DisableAll", Culture);

		/// <summary>
		/// Disable entire group
		/// </summary>
		public static string Settings_DisableEntireGrope => LocaleManager.GetString("Settings_DisableEntireGrope", Culture);

		/// <summary>
		/// Disasters notifications
		/// </summary>
		public static string Settings_DisastersGroup => LocaleManager.GetString("Settings_DisastersGroup", Culture);

		/// <summary>
		/// Enable all
		/// </summary>
		public static string Settings_EnableAll => LocaleManager.GetString("Settings_EnableAll", Culture);

		/// <summary>
		/// Enable entire group
		/// </summary>
		public static string Settings_EnableEntireGrope => LocaleManager.GetString("Settings_EnableEntireGrope", Culture);

		/// <summary>
		/// Fishing notifications
		/// </summary>
		public static string Settings_FishingGroup => LocaleManager.GetString("Settings_FishingGroup", Culture);

		/// <summary>
		/// General notifications
		/// </summary>
		public static string Settings_GeneralGroup => LocaleManager.GetString("Settings_GeneralGroup", Culture);

		/// <summary>
		/// Industry notifications
		/// </summary>
		public static string Settings_IndustryGroup => LocaleManager.GetString("Settings_IndustryGroup", Culture);

		/// <summary>
		/// Not connected notifications
		/// </summary>
		public static string Settings_NotConnectedGroup => LocaleManager.GetString("Settings_NotConnectedGroup", Culture);

		/// <summary>
		/// Other notifications
		/// </summary>
		public static string Settings_OtherGroup => LocaleManager.GetString("Settings_OtherGroup", Culture);

		/// <summary>
		/// Parks notifications
		/// </summary>
		public static string Settings_ParksGroup => LocaleManager.GetString("Settings_ParksGroup", Culture);

		/// <summary>
		/// Pedestrian area notifications
		/// </summary>
		public static string Settings_PedestrianGroup => LocaleManager.GetString("Settings_PedestrianGroup", Culture);

		/// <summary>
		/// Сonsumption notifications
		/// </summary>
		public static string Settings_СonsumptionGroup => LocaleManager.GetString("Settings_СonsumptionGroup", Culture);

		/// <summary>
		/// Disable "{0}"
		/// </summary>
		public static string Setting_DisableProblem => LocaleManager.GetString("Setting_DisableProblem", Culture);

		/// <summary>
		/// Hide any
		/// </summary>
		public static string Setting_HideAny => LocaleManager.GetString("Setting_HideAny", Culture);

		/// <summary>
		/// {0} —  this option just hides any notifications, but buildings behavior is kept.
		/// </summary>
		public static string Setting_HideDescription => LocaleManager.GetString("Setting_HideDescription", Culture);

		/// <summary>
		/// Hide normal, but show major
		/// </summary>
		public static string Setting_HideNormal => LocaleManager.GetString("Setting_HideNormal", Culture);

		/// <summary>
		/// Hide option
		/// </summary>
		public static string Setting_HideType => LocaleManager.GetString("Setting_HideType", Culture);

		/// <summary>
		/// Remove problem flags
		/// </summary>
		public static string Setting_Remove => LocaleManager.GetString("Setting_Remove", Culture);

		/// <summary>
		/// Toggle visibility
		/// </summary>
		public static string Setting_ToggleShortcut => LocaleManager.GetString("Setting_ToggleShortcut", Culture);
	}
}