using System;
using System.Diagnostics;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using System.Text.Json;
using System.Text.Json.Serialization;
using AvatarStatsLoader;
using AvatarStatsLoader.BoneMenu;
using BoneLib;
using BoneLib.BoneMenu;
using HarmonyLib;
using Il2CppSLZ.VRMK;
using Il2CppSystem.IO;
using MelonLoader;
using MelonLoader.Preferences;
using MelonLoader.Utils;
using UnityEngine;

[assembly: CompilationRelaxations(8)]
[assembly: RuntimeCompatibility(WrapNonExceptionThrows = true)]
[assembly: Debuggable(DebuggableAttribute.DebuggingModes.IgnoreSymbolStoreSequencePoints)]
[assembly: AssemblyTitle("Avatar Stats Loader")]
[assembly: AssemblyDescription("Customized stats loader for BoneLab")]
[assembly: AssemblyConfiguration("")]
[assembly: AssemblyCompany("")]
[assembly: AssemblyProduct("AvatarStatsLoader")]
[assembly: AssemblyCopyright("Copyright c FirEmerald 2024")]
[assembly: AssemblyTrademark("")]
[assembly: ComVisible(false)]
[assembly: Guid("649c4263-395f-44aa-bb88-6841559df396")]
[assembly: AssemblyFileVersion("1.3.5")]
[assembly: MelonInfo(typeof(AvatarStatsMod), "Avatar Stats Loader", "1.3.5", "FirEmerald", "https://bonelab.thunderstore.io/package/FirEmerald/AvatarStatsLoader/")]
[assembly: TargetFramework(".NETCoreApp,Version=v6.0", FrameworkDisplayName = ".NET 6.0")]
[assembly: AssemblyVersion("1.3.5.0")]
namespace AvatarStatsLoader
{
	public static class AvatarExtensions
	{
		private static float defAgility;

		private static float defStrengthUpper;

		private static float defStrengthLower;

		private static float defVitality;

		private static float defSpeed;

		private static float defIntelligence;

		private static float loadAgility;

		private static float loadStrengthUpper;

		private static float loadStrengthLower;

		private static float loadVitality;

		private static float loadSpeed;

		private static float loadIntelligence;

		private static float defMassChest;

		private static float defMassPelvis;

		private static float defMassHead;

		private static float defMassArm;

		private static float defMassLeg;

		private static float loadMassChest;

		private static float loadMassPelvis;

		private static float loadMassHead;

		private static float loadMassArm;

		private static float loadMassLeg;

		public static void SetDefStats(this Avatar avatar)
		{
			defAgility = avatar._agility;
			defStrengthUpper = avatar._strengthUpper;
			defStrengthLower = avatar._strengthLower;
			defVitality = avatar._vitality;
			defSpeed = avatar._speed;
			defIntelligence = avatar._intelligence;
		}

		public static float GetDefAgility(this Avatar avatar)
		{
			return defAgility;
		}

		public static float GetDefStrengthUpper(this Avatar avatar)
		{
			return defStrengthUpper;
		}

		public static float GetDefStrengthLower(this Avatar avatar)
		{
			return defStrengthLower;
		}

		public static float GetDefVitality(this Avatar avatar)
		{
			return defVitality;
		}

		public static float GetDefSpeed(this Avatar avatar)
		{
			return defSpeed;
		}

		public static float GetDefIntelligence(this Avatar avatar)
		{
			return defIntelligence;
		}

		public static void SetLoadStats(this Avatar avatar)
		{
			loadAgility = avatar._agility;
			loadStrengthUpper = avatar._strengthUpper;
			loadStrengthLower = avatar._strengthLower;
			loadVitality = avatar._vitality;
			loadSpeed = avatar._speed;
			loadIntelligence = avatar._intelligence;
		}

		public static float GetLoadAgility(this Avatar avatar)
		{
			return loadAgility;
		}

		public static float GetLoadStrengthUpper(this Avatar avatar)
		{
			return loadStrengthUpper;
		}

		public static float GetLoadStrengthLower(this Avatar avatar)
		{
			return loadStrengthLower;
		}

		public static float GetLoadVitality(this Avatar avatar)
		{
			return loadVitality;
		}

		public static float GetLoadSpeed(this Avatar avatar)
		{
			return loadSpeed;
		}

		public static float GetLoadIntelligence(this Avatar avatar)
		{
			return loadIntelligence;
		}

		public static void SetDefMasses(this Avatar avatar)
		{
			defMassChest = avatar._massChest;
			defMassPelvis = avatar._massPelvis;
			defMassHead = avatar._massHead;
			defMassArm = avatar._massArm;
			defMassLeg = avatar._massLeg;
		}

		public static float GetDefMassChest(this Avatar avatar)
		{
			return defMassChest;
		}

		public static float GetDefMassPelvis(this Avatar avatar)
		{
			return defMassPelvis;
		}

		public static float GetDefMassHead(this Avatar avatar)
		{
			return defMassHead;
		}

		public static float GetDefMassArm(this Avatar avatar)
		{
			return defMassArm;
		}

		public static float GetDefMassLeg(this Avatar avatar)
		{
			return defMassLeg;
		}

		public static void SetLoadMasses(this Avatar avatar)
		{
			loadMassChest = avatar._massChest;
			loadMassPelvis = avatar._massPelvis;
			loadMassHead = avatar._massHead;
			loadMassArm = avatar._massArm;
			loadMassLeg = avatar._massLeg;
		}

		public static float GetLoadMassChest(this Avatar avatar)
		{
			return loadMassChest;
		}

		public static float GetLoadMassPelvis(this Avatar avatar)
		{
			return loadMassPelvis;
		}

		public static float GetLoadMassHead(this Avatar avatar)
		{
			return loadMassHead;
		}

		public static float GetLoadMassArm(this Avatar avatar)
		{
			return loadMassArm;
		}

		public static float GetLoadMassLeg(this Avatar avatar)
		{
			return loadMassLeg;
		}

		public static bool IsEmptyRig(this Avatar avatar)
		{
			return ((Object)avatar).name == "[RealHeptaRig (Marrow1)]";
		}

		public static string GetName(this Avatar avatar)
		{
			if (((Object)avatar).name.EndsWith("(Clone)"))
			{
				string name = ((Object)avatar).name;
				int length = "(Clone)".Length;
				return name.Substring(0, name.Length - length);
			}
			return ((Object)avatar).name;
		}

		public static void RecalculateTotalMass(this Avatar avatar)
		{
			avatar._massTotal = avatar._massChest + avatar._massPelvis + avatar._massHead + (avatar._massArm + avatar._massLeg) * 2f;
		}
	}
	public class AssemblyInfo
	{
		public const string Name = "Avatar Stats Loader";

		public const string Product = "AvatarStatsLoader";

		public const string Description = "Customized stats loader for BoneLab";

		public const string Version = "1.3.5";

		public const string Author = "FirEmerald";

		public const string Copyright = "Copyright c FirEmerald 2024";

		public const string URL = "https://bonelab.thunderstore.io/package/FirEmerald/AvatarStatsLoader/";
	}
	public class AvatarStatsMod : MelonMod
	{
		internal static readonly string STATS_FOLDER = Path.Combine(MelonEnvironment.UserDataDirectory, "AvatarStats");

		internal static readonly string MASS_FOLDER = Path.Combine(MelonEnvironment.UserDataDirectory, "AvatarMass");

		internal static AvatarStatsMod instance;

		internal static MelonPreferences_Category mpCat;

		internal static MelonPreferences_Entry<float> agility;

		internal static MelonPreferences_Entry<float> strengthUpper;

		internal static MelonPreferences_Entry<float> strengthLower;

		internal static MelonPreferences_Entry<float> vitality;

		internal static MelonPreferences_Entry<float> speed;

		internal static MelonPreferences_Entry<float> intelligence;

		internal static MelonPreferences_Entry<bool> loadStats;

		internal static MelonPreferences_Entry<bool> saveStats;

		internal static MelonPreferences_Entry<float> massChest;

		internal static MelonPreferences_Entry<float> massPelvis;

		internal static MelonPreferences_Entry<float> massHead;

		internal static MelonPreferences_Entry<float> massArm;

		internal static MelonPreferences_Entry<float> massLeg;

		internal static MelonPreferences_Entry<bool> loadMasses;

		internal static MelonPreferences_Entry<bool> saveMasses;

		internal static Avatar currentAvatar = null;

		internal static bool isLoadingAvatarValues = false;

		internal static readonly JsonSerializerOptions jsonOpts = new JsonSerializerOptions
		{
			WriteIndented = true,
			AllowTrailingCommas = true
		};

		public AvatarStatsMod()
		{
			instance = this;
		}

		public override void OnInitializeMelon()
		{
			Hooking.OnSwitchAvatarPostfix += delegate(Avatar avatar)
			{
				if ((Object)(object)avatar != (Object)null && !avatar.IsEmptyRig())
				{
					if ((Object)(object)avatar != (Object)(object)currentAvatar)
					{
						Log("Setting avatar to " + avatar.GetName());
						isLoadingAvatarValues = true;
						agility.DefaultValue = avatar.GetDefAgility();
						agility.Value = avatar._agility;
						strengthUpper.DefaultValue = avatar.GetDefStrengthUpper();
						strengthUpper.Value = avatar._strengthUpper;
						strengthLower.DefaultValue = avatar.GetDefStrengthLower();
						strengthLower.Value = avatar._strengthLower;
						vitality.DefaultValue = avatar.GetDefVitality();
						vitality.Value = avatar._vitality;
						speed.DefaultValue = avatar.GetDefSpeed();
						speed.Value = avatar._speed;
						intelligence.DefaultValue = avatar.GetDefIntelligence();
						intelligence.Value = avatar._intelligence;
						massChest.DefaultValue = avatar.GetDefMassChest();
						massChest.Value = avatar._massChest;
						massPelvis.DefaultValue = avatar.GetDefMassPelvis();
						massPelvis.Value = avatar._massPelvis;
						massHead.DefaultValue = avatar.GetDefMassHead();
						massHead.Value = avatar._massHead;
						massArm.DefaultValue = avatar.GetDefMassArm();
						massArm.Value = avatar._massArm;
						massLeg.DefaultValue = avatar.GetDefMassLeg();
						massLeg.Value = avatar._massLeg;
						currentAvatar = avatar;
						isLoadingAvatarValues = false;
					}
				}
				else
				{
					Log("Setting avatar to null");
					currentAvatar = null;
				}
			};
			mpCat = MelonPreferences.CreateCategory("AvatarStatsMod");
			agility = mpCat.CreateEntry<float>("agility", 0f, "Agility", "Determines how fast an avatar can acclerate or decelerate.", false, true, (ValueValidator)null, (string)null);
			strengthUpper = mpCat.CreateEntry<float>("strengthUpper", 0f, "Arm strength", "Determines the arm strength, affecting weapon holding and climbing.", false, true, (ValueValidator)null, (string)null);
			strengthLower = mpCat.CreateEntry<float>("strengthLower", 0f, "Leg strength", "Determines leg strength, affecting running and jumping.", false, true, (ValueValidator)null, (string)null);
			vitality = mpCat.CreateEntry<float>("vitality", 0f, "Vitality", "Determines how much damage an avatar takes.", false, true, (ValueValidator)null, (string)null);
			speed = mpCat.CreateEntry<float>("speed", 0f, "Speed", "Determines how fast an avatar can run.", false, true, (ValueValidator)null, (string)null);
			intelligence = mpCat.CreateEntry<float>("intelligence", 0f, "Intelligence", "Currently has no effect.", false, true, (ValueValidator)null, (string)null);
			((MelonEventBase<LemonAction<float, float>>)(object)agility.OnEntryValueChanged).Subscribe((LemonAction<float, float>)refreshAvatarAct, int.MaxValue, false);
			((MelonEventBase<LemonAction<float, float>>)(object)strengthUpper.OnEntryValueChanged).Subscribe((LemonAction<float, float>)refreshAvatarAct, int.MaxValue, false);
			((MelonEventBase<LemonAction<float, float>>)(object)strengthLower.OnEntryValueChanged).Subscribe((LemonAction<float, float>)refreshAvatarAct, int.MaxValue, false);
			((MelonEventBase<LemonAction<float, float>>)(object)vitality.OnEntryValueChanged).Subscribe((LemonAction<float, float>)refreshAvatarAct, int.MaxValue, false);
			((MelonEventBase<LemonAction<float, float>>)(object)speed.OnEntryValueChanged).Subscribe((LemonAction<float, float>)refreshAvatarAct, int.MaxValue, false);
			((MelonEventBase<LemonAction<float, float>>)(object)intelligence.OnEntryValueChanged).Subscribe((LemonAction<float, float>)refreshAvatarAct, int.MaxValue, false);
			loadStats = mpCat.CreateEntry<bool>("loadStats", false, "Reload stats", "loads the previously loaded stats of the current avatar into the preferences.", false, false, (ValueValidator)null, (string)null);
			((MelonEventBase<LemonAction<bool, bool>>)(object)loadStats.OnEntryValueChanged).Subscribe((LemonAction<bool, bool>)delegate(bool prev, bool cur)
			{
				if (cur)
				{
					LoadStatValues();
					((MelonPreferences_Entry)loadStats).ResetToDefault();
				}
			}, 0, false);
			saveStats = mpCat.CreateEntry<bool>("saveStats", false, "Save stats", "Saves the current stat preferences into the override file of the current avatar.", false, false, (ValueValidator)null, (string)null);
			((MelonEventBase<LemonAction<bool, bool>>)(object)saveStats.OnEntryValueChanged).Subscribe((LemonAction<bool, bool>)delegate(bool prev, bool cur)
			{
				if (cur)
				{
					SaveStatsToFile();
					((MelonPreferences_Entry)saveStats).ResetToDefault();
				}
			}, 0, false);
			massChest = mpCat.CreateEntry<float>("massChest", 0f, "Chest mass", "Chest mass of the last loaded avatar.", false, true, (ValueValidator)null, (string)null);
			massPelvis = mpCat.CreateEntry<float>("massPelvis", 0f, "Pelvis mass", "Pelvis mass of the last loaded avatar.", false, true, (ValueValidator)null, (string)null);
			massHead = mpCat.CreateEntry<float>("massHead", 0f, "Head mass", "Head mass of the last loaded avatar.", false, true, (ValueValidator)null, (string)null);
			massArm = mpCat.CreateEntry<float>("massArm", 0f, "Arm mass", "Arm mass of the last loaded avatar.", false, true, (ValueValidator)null, (string)null);
			massLeg = mpCat.CreateEntry<float>("massLeg", 0f, "Leg mass", "Leg mass of the last loaded avatar.", false, true, (ValueValidator)null, (string)null);
			((MelonEventBase<LemonAction<float, float>>)(object)massChest.OnEntryValueChanged).Subscribe((LemonAction<float, float>)refreshAvatarAct, int.MaxValue, false);
			((MelonEventBase<LemonAction<float, float>>)(object)massPelvis.OnEntryValueChanged).Subscribe((LemonAction<float, float>)refreshAvatarAct, int.MaxValue, false);
			((MelonEventBase<LemonAction<float, float>>)(object)massHead.OnEntryValueChanged).Subscribe((LemonAction<float, float>)refreshAvatarAct, int.MaxValue, false);
			((MelonEventBase<LemonAction<float, float>>)(object)massArm.OnEntryValueChanged).Subscribe((LemonAction<float, float>)refreshAvatarAct, int.MaxValue, false);
			((MelonEventBase<LemonAction<float, float>>)(object)massLeg.OnEntryValueChanged).Subscribe((LemonAction<float, float>)refreshAvatarAct, int.MaxValue, false);
			loadMasses = mpCat.CreateEntry<bool>("loadMasses", false, "Reload masses", "Reloads the mass of the current avatar into the preferences.", false, false, (ValueValidator)null, (string)null);
			((MelonEventBase<LemonAction<bool, bool>>)(object)loadMasses.OnEntryValueChanged).Subscribe((LemonAction<bool, bool>)delegate(bool prev, bool cur)
			{
				if (cur)
				{
					LoadMassValues();
					((MelonPreferences_Entry)loadMasses).ResetToDefault();
				}
			}, 0, false);
			saveMasses = mpCat.CreateEntry<bool>("saveMasses", false, "Save masses", "Saves the current mass preferences into the override file of the current avatar.", false, false, (ValueValidator)null, (string)null);
			((MelonEventBase<LemonAction<bool, bool>>)(object)saveMasses.OnEntryValueChanged).Subscribe((LemonAction<bool, bool>)delegate(bool prev, bool cur)
			{
				if (cur)
				{
					SaveMassesToFile();
					((MelonPreferences_Entry)saveMasses).ResetToDefault();
				}
			}, 0, false);
			mpCat.SaveToFile(true);
			StatsBoneMenu.Init();
			MassesBoneMenu.Init();
			static void refreshAvatarAct(float prev, float cur)
			{
				RefreshAvatarStats();
			}
		}

		public static void RefreshAvatarStats()
		{
			if (isLoadingAvatarValues || !((Object)(object)currentAvatar != (Object)null))
			{
				return;
			}
			Log("Refreshing " + currentAvatar.GetName());
			try
			{
				Player.RigManager.SwapAvatar(currentAvatar);
			}
			catch (Exception ex)
			{
				Error("An error occurred attempting to refresh avatar stats.", ex);
			}
		}

		public static void SaveStatsToFile()
		{
			if ((Object)(object)currentAvatar != (Object)null)
			{
				if (!Directory.Exists(STATS_FOLDER))
				{
					DirectoryInfo val = Directory.CreateDirectory(STATS_FOLDER);
					Log("Avatar stats folder did not exist, created at " + ((FileSystemInfo)val).Name);
				}
				string text = Path.Combine(STATS_FOLDER, currentAvatar.GetName() + ".json");
				Log("Saving stats to " + text);
				File.WriteAllText(text, JsonSerializer.Serialize(new AvatarStats(agility.Value, strengthUpper.Value, strengthLower.Value, vitality.Value, speed.Value, intelligence.Value), jsonOpts));
			}
		}

		internal static void LoadStatValues()
		{
			if ((Object)(object)currentAvatar != (Object)null)
			{
				LoadStatValues(currentAvatar);
			}
		}

		internal static void LoadStatValues(Avatar avatar)
		{
			agility.Value = avatar._agility;
			strengthUpper.Value = avatar._strengthUpper;
			strengthLower.Value = avatar._strengthLower;
			vitality.Value = avatar._vitality;
			speed.Value = avatar._speed;
			intelligence.Value = avatar._intelligence;
		}

		public static void SaveMassesToFile()
		{
			if ((Object)(object)currentAvatar != (Object)null)
			{
				if (!Directory.Exists(MASS_FOLDER))
				{
					DirectoryInfo val = Directory.CreateDirectory(MASS_FOLDER);
					Log("Avatar masses folder did not exist, created at " + ((FileSystemInfo)val).Name);
				}
				string text = Path.Combine(MASS_FOLDER, currentAvatar.GetName() + ".json");
				Log("Saving masses to " + text);
				File.WriteAllText(text, JsonSerializer.Serialize(new AvatarMass(massChest.Value, massPelvis.Value, massHead.Value, massArm.Value, massLeg.Value), jsonOpts));
			}
		}

		internal static void LoadMassValues()
		{
			if ((Object)(object)currentAvatar != (Object)null)
			{
				LoadMassValues(currentAvatar);
			}
		}

		internal static void LoadMassValues(Avatar avatar)
		{
			massChest.Value = avatar._massChest;
			massPelvis.Value = avatar._massPelvis;
			massHead.Value = avatar._massHead;
			massArm.Value = avatar._massArm;
			massLeg.Value = avatar._massLeg;
		}

		internal static void Log(string str)
		{
			((MelonBase)instance).LoggerInstance.Msg(str);
		}

		internal static void Log(string str, Exception ex)
		{
			((MelonBase)instance).LoggerInstance.Msg(str, new object[1] { ex });
		}

		internal static void Log(object obj)
		{
			((MelonBase)instance).LoggerInstance.Msg(obj?.ToString() ?? "null");
		}

		internal static void Warn(string str)
		{
			((MelonBase)instance).LoggerInstance.Warning(str);
		}

		internal static void Warn(string str, Exception ex)
		{
			((MelonBase)instance).LoggerInstance.Warning(str, new object[1] { ex });
		}

		internal static void Warn(object obj)
		{
			((MelonBase)instance).LoggerInstance.Warning(obj?.ToString() ?? "null");
		}

		internal static void Error(string str)
		{
			((MelonBase)instance).LoggerInstance.Error(str);
		}

		internal static void Error(string str, Exception ex)
		{
			((MelonBase)instance).LoggerInstance.Error(str, ex);
		}

		internal static void Error(object obj)
		{
			((MelonBase)instance).LoggerInstance.Error(obj?.ToString() ?? "null");
		}
	}
	[HarmonyPatch(typeof(Avatar), "ComputeBaseStats")]
	public static class AvatarComputeStatChange
	{
		public static void Postfix(Avatar __instance)
		{
			if (__instance.IsEmptyRig())
			{
				return;
			}
			string name = __instance.GetName();
			if ((Object)(object)__instance == (Object)(object)AvatarStatsMod.currentAvatar)
			{
				AvatarStatsMod.Log("Overriding stats for " + name + " with values from preferences.");
				__instance._agility = AvatarStatsMod.agility.Value;
				__instance._strengthUpper = AvatarStatsMod.strengthUpper.Value;
				__instance._strengthLower = AvatarStatsMod.strengthLower.Value;
				__instance._vitality = AvatarStatsMod.vitality.Value;
				__instance._speed = AvatarStatsMod.speed.Value;
				__instance._intelligence = AvatarStatsMod.intelligence.Value;
				return;
			}
			__instance.SetDefStats();
			AvatarStatsMod.Log("Load stats: " + name);
			if (Directory.Exists(AvatarStatsMod.STATS_FOLDER))
			{
				string text = Path.Combine(AvatarStatsMod.STATS_FOLDER, name + ".json");
				if (File.Exists(text))
				{
					AvatarStatsMod.Log("Overriding stats with values from " + text);
					JsonSerializer.Deserialize<AvatarStats>(File.ReadAllText(text), AvatarStatsMod.jsonOpts).Apply(__instance);
				}
			}
			__instance.SetLoadStats();
		}
	}
	public class AvatarStats
	{
		[JsonInclude]
		public float agility;

		[JsonInclude]
		public float strengthUpper;

		[JsonInclude]
		public float strengthLower;

		[JsonInclude]
		public float vitality;

		[JsonInclude]
		public float speed;

		[JsonInclude]
		public float intelligence;

		public AvatarStats()
		{
		}

		public AvatarStats(float agility, float strengthUpper, float strengthLower, float vitality, float speed, float intelligence)
		{
			this.agility = agility;
			this.strengthUpper = strengthUpper;
			this.strengthLower = strengthLower;
			this.vitality = vitality;
			this.speed = speed;
			this.intelligence = intelligence;
		}

		public AvatarStats(Avatar avatar)
		{
			agility = avatar._agility;
			strengthUpper = avatar._strengthUpper;
			strengthLower = avatar._strengthLower;
			vitality = avatar._vitality;
			speed = avatar._speed;
			intelligence = avatar._intelligence;
		}

		public void Apply(Avatar avatar)
		{
			avatar._agility = agility;
			avatar._strengthUpper = strengthUpper;
			avatar._strengthLower = strengthLower;
			avatar._vitality = vitality;
			avatar._speed = speed;
			avatar._intelligence = intelligence;
		}
	}
	[HarmonyPatch(typeof(Avatar), "ComputeMass")]
	public static class AvatarComputeMassChange
	{
		public static void Postfix(Avatar __instance, float normalizeTo82)
		{
			if (__instance.IsEmptyRig())
			{
				return;
			}
			string name = __instance.GetName();
			if ((Object)(object)__instance == (Object)(object)AvatarStatsMod.currentAvatar)
			{
				AvatarStatsMod.Log("Overriding mass for " + name + " with values from preferences.");
				__instance._massChest = AvatarStatsMod.massChest.Value;
				__instance._massPelvis = AvatarStatsMod.massPelvis.Value;
				__instance._massHead = AvatarStatsMod.massHead.Value;
				__instance._massArm = AvatarStatsMod.massArm.Value;
				__instance._massLeg = AvatarStatsMod.massLeg.Value;
				__instance.RecalculateTotalMass();
				return;
			}
			__instance.SetDefMasses();
			AvatarStatsMod.Log("Load mass: " + name);
			if (Directory.Exists(AvatarStatsMod.MASS_FOLDER))
			{
				string text = Path.Combine(AvatarStatsMod.MASS_FOLDER, ".json");
				if (File.Exists(text))
				{
					AvatarStatsMod.Log("Overriding mass with values from " + text);
					JsonSerializer.Deserialize<AvatarMass>(File.ReadAllText(text), AvatarStatsMod.jsonOpts).Apply(__instance);
				}
			}
			__instance.SetLoadMasses();
		}
	}
	public class AvatarMass
	{
		[JsonInclude]
		public float massChest;

		[JsonInclude]
		public float massPelvis;

		[JsonInclude]
		public float massHead;

		[JsonInclude]
		public float massArm;

		[JsonInclude]
		public float massLeg;

		public AvatarMass()
		{
		}

		public AvatarMass(float massChest, float massPelvis, float massHead, float massArm, float massLeg)
		{
			this.massChest = massChest;
			this.massPelvis = massPelvis;
			this.massHead = massHead;
			this.massArm = massArm;
			this.massLeg = massLeg;
		}

		public AvatarMass(Avatar avatar)
		{
			massChest = avatar._massChest;
			massPelvis = avatar._massPelvis;
			massHead = avatar._massHead;
			massArm = avatar._massArm;
			massLeg = avatar._massLeg;
		}

		public void Apply(Avatar avatar)
		{
			avatar._massChest = massChest;
			avatar._massPelvis = massPelvis;
			avatar._massHead = massHead;
			avatar._massArm = massArm;
			avatar._massLeg = massLeg;
			avatar.RecalculateTotalMass();
		}
	}
}
namespace AvatarStatsLoader.BoneMenu
{
	internal class EntryMenu
	{
		public readonly Page menu;

		public readonly FloatElement incrementOne;

		public readonly FloatElement incrementPointOne;

		public readonly FloatElement incrementPointZeroOne;

		public readonly FloatElement incrementPointZeroZeroOne;

		public readonly FunctionElement setToOne;

		public readonly FunctionElement loadFromAvatar;

		public readonly FunctionElement loadFromAvatarCalculated;

		public EntryMenu(Page parentMenu, string name, Func<float> getFromLoaded, MelonPreferences_Entry<float> entry)
		{
			//IL_0025: Unknown result type (might be due to invalid IL or missing references)
			//IL_00b2: Unknown result type (might be due to invalid IL or missing references)
			//IL_00d9: Unknown result type (might be due to invalid IL or missing references)
			//IL_0100: Unknown result type (might be due to invalid IL or missing references)
			EntryMenu entryMenu = this;
			menu = parentMenu.CreatePage(name, Color.white);
			incrementOne = MakeIncrement(menu, 1f, entry);
			incrementPointOne = MakeIncrement(menu, 0.1f, entry);
			incrementPointZeroOne = MakeIncrement(menu, 0.01f, entry);
			incrementPointZeroZeroOne = MakeIncrement(menu, 0.01f, entry);
			setToOne = menu.CreateFunction("Set to 1.0", Color.white, delegate
			{
				entry.Value = 1f;
			});
			loadFromAvatar = menu.CreateFunction("Load from avatar's loaded value", Color.white, delegate
			{
				entry.Value = getFromLoaded();
			});
			loadFromAvatarCalculated = menu.CreateFunction("Load from avatar's computed value", Color.white, delegate
			{
				((MelonPreferences_Entry)entry).ResetToDefault();
			});
			((MelonEventBase<LemonAction<float, float>>)(object)entry.OnEntryValueChanged).Subscribe((LemonAction<float, float>)delegate(float prev, float cur)
			{
				if (cur != entryMenu.incrementOne.Value)
				{
					entryMenu.incrementOne.Value = cur;
				}
				if (cur != entryMenu.incrementPointOne.Value)
				{
					entryMenu.incrementPointOne.Value = cur;
				}
				if (cur != entryMenu.incrementPointZeroOne.Value)
				{
					entryMenu.incrementPointZeroOne.Value = cur;
				}
				if (cur != entryMenu.incrementPointZeroZeroOne.Value)
				{
					entryMenu.incrementPointZeroZeroOne.Value = cur;
				}
			}, int.MaxValue, false);
		}

		private static FloatElement MakeIncrement(Page page, float increment, MelonPreferences_Entry<float> entry)
		{
			//IL_001f: Unknown result type (might be due to invalid IL or missing references)
			return page.CreateFloat("+/-" + increment, Color.white, 0f, increment, float.NegativeInfinity, float.PositiveInfinity, delegate(float value)
			{
				if (value != entry.Value)
				{
					entry.Value = value;
				}
			});
		}
	}
	internal class MassesBoneMenu
	{
		public static Page menu;

		public static EntryMenu massChest;

		public static EntryMenu massPelvis;

		public static EntryMenu massHead;

		public static EntryMenu massArm;

		public static EntryMenu massLeg;

		public static FunctionElement saveMasses;

		public static void Init()
		{
			//IL_000a: Unknown result type (might be due to invalid IL or missing references)
			//IL_013d: Unknown result type (might be due to invalid IL or missing references)
			menu = Page.Root.CreatePage("Avatar Mass", Color.white);
			massChest = new EntryMenu(menu, "Chest Mass", () => AvatarStatsMod.currentAvatar.GetLoadMassChest(), AvatarStatsMod.massChest);
			massPelvis = new EntryMenu(menu, "Pelvis Mass", () => AvatarStatsMod.currentAvatar.GetLoadMassPelvis(), AvatarStatsMod.massPelvis);
			massHead = new EntryMenu(menu, "Head Mass", () => AvatarStatsMod.currentAvatar.GetLoadMassHead(), AvatarStatsMod.massHead);
			massArm = new EntryMenu(menu, "Arm Mass", () => AvatarStatsMod.currentAvatar.GetLoadMassArm(), AvatarStatsMod.massArm);
			massLeg = new EntryMenu(menu, "Leg Mass", () => AvatarStatsMod.currentAvatar.GetLoadMassLeg(), AvatarStatsMod.massLeg);
			saveMasses = menu.CreateFunction("Save masses", Color.white, AvatarStatsMod.SaveMassesToFile);
		}
	}
	internal class StatsBoneMenu
	{
		public static Page menu;

		public static EntryMenu agility;

		public static EntryMenu strengthUpper;

		public static EntryMenu strengthLower;

		public static EntryMenu vitality;

		public static EntryMenu speed;

		public static EntryMenu intelligence;

		public static FunctionElement saveStats;

		public static void Init()
		{
			//IL_000a: Unknown result type (might be due to invalid IL or missing references)
			//IL_0175: Unknown result type (might be due to invalid IL or missing references)
			menu = Page.Root.CreatePage("Avatar Stats", Color.white);
			agility = new EntryMenu(menu, "Agility", () => AvatarStatsMod.currentAvatar.GetLoadAgility(), AvatarStatsMod.agility);
			strengthUpper = new EntryMenu(menu, "Strength Upper", () => AvatarStatsMod.currentAvatar.GetLoadStrengthUpper(), AvatarStatsMod.strengthUpper);
			strengthLower = new EntryMenu(menu, "Strength Lower", () => AvatarStatsMod.currentAvatar.GetLoadStrengthLower(), AvatarStatsMod.strengthLower);
			vitality = new EntryMenu(menu, "Vitality", () => AvatarStatsMod.currentAvatar.GetLoadVitality(), AvatarStatsMod.vitality);
			speed = new EntryMenu(menu, "Speed", () => AvatarStatsMod.currentAvatar.GetLoadSpeed(), AvatarStatsMod.speed);
			intelligence = new EntryMenu(menu, "Intelligence", () => AvatarStatsMod.currentAvatar.GetLoadIntelligence(), AvatarStatsMod.intelligence);
			saveStats = menu.CreateFunction("Save stats", Color.white, AvatarStatsMod.SaveStatsToFile);
		}
	}
}
