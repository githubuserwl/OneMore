﻿//************************************************************************************************
// Copyright © 2022 Steven M Cohn.  All rights reserved.
//************************************************************************************************

#pragma warning disable S3011 // Reflection should not be used to increase accessibility...

namespace River.OneMoreAddIn.UI
{
	using Newtonsoft.Json;
	using River.OneMoreAddIn;
	using River.OneMoreAddIn.Helpers.Office;
	using River.OneMoreAddIn.Settings;
	using System;
	using System.Collections.Generic;
	using System.ComponentModel;
	using System.Drawing;
	using System.IO;
	using System.Reflection;
	using System.Runtime.InteropServices;
	using System.Windows.Forms;
	using Resx = Properties.Resources;


	/// <summary>
	/// Identifies the available themes for the application
	/// </summary>
	internal enum ThemeMode
	{
		System,
		Light,
		Dark,
		User
	}


	/// <summary>
	/// Defines themes and projects onto UI elements
	/// </summary>
	internal class ThemeManager
	{

		private const string CustomThemeFile = "OneMoreTheme.json";
		private static ThemeManager instance;


		private ThemeManager()
		{
			Colors = new Dictionary<string, Color>();
		}


		/// <summary>
		/// Gets a value indicating whether the theme is set to dark mode
		/// </summary>
		[JsonProperty]
		public bool DarkMode { get; private set; }


		[JsonProperty]
		private Dictionary<string, Color> Colors { get; set; }


		public static ThemeManager Instance => instance ??= new ThemeManager();


		#region Convenience Properties
		[JsonIgnore]
		public Color BackColor => Colors[nameof(BackColor)];
		[JsonIgnore]
		public Color ForeColor => Colors[nameof(ForeColor)];
		[JsonIgnore]
		public Color Border => Colors[nameof(Border)];
		[JsonIgnore]
		public Color Highlight => Colors[nameof(Highlight)];
		[JsonIgnore]
		public Color Control => Colors[nameof(Control)];
		[JsonIgnore]
		public Color IconColor => Colors[nameof(IconColor)];
		[JsonIgnore]
		public Color ButtonBack => Colors[nameof(ButtonBack)];
		[JsonIgnore]
		public Color ButtonFore => Colors[nameof(ButtonFore)];
		[JsonIgnore]
		public Color ButtonDisabled => Colors[nameof(ButtonDisabled)];
		[JsonIgnore]
		public Color ButtonBorder => Colors[nameof(ButtonBorder)];
		[JsonIgnore]
		public Color ButtonHotBack => Colors[nameof(ButtonHotBack)];
		[JsonIgnore]
		public Color ButtonHotBorder => Colors[nameof(ButtonHotBorder)];
		[JsonIgnore]
		public Color ButtonPressBorder => Colors[nameof(ButtonPressBorder)];

		[JsonIgnore]
		public Color LinkColor => Colors[nameof(LinkColor)];
		[JsonIgnore]
		public Color HoverColor => Colors[nameof(HoverColor)];

		[JsonIgnore]
		public Color InfoBack => Colors[nameof(InfoBack)];
		#endregion Convenience Properties

		#region Native
		private const int DWMWA_USE_IMMERSIVE_DARK_MODE = 20;

		[DllImport("dwmapi.dll", PreserveSig = true)]
		private static extern int DwmSetWindowAttribute(
			IntPtr hwnd, int attr, ref bool attrValue, int attrSize);
		#endregion Native


		/// <summary>
		/// 
		/// </summary>
		/// <param name="container"></param>
		public void InitializeTheme(ContainerControl container)
		{
			var provider = new SettingsProvider();
			var mode = provider.Theme;

			var designMode = false;
			if (container is Component component)
			{
				// pull DesignMode protected property from container
				// so we can support the Visual Studio Forms Designer
				designMode = (bool)container.GetType()
					.BaseType
					.GetProperty("DesignMode", BindingFlags.Instance | BindingFlags.NonPublic)?
					.GetValue(component);
			}

			if ((mode != ThemeMode.User) || !LoadColors())
			{
				DarkMode = !designMode &&
					(mode == ThemeMode.Dark ||
					(mode == ThemeMode.System && Office.SystemDefaultDarkMode()));

				// set colors...

				var cache = JsonConvert.DeserializeObject<ThemeManager>(
					DarkMode ? Resx.DarkTheme : Resx.LightTheme,
					new ColorConverter());

				Colors.Clear();
				Colors = cache.Colors;
			}

			// apply colors...

			if (container is MoreForm form)
			{
				if (form.FormBorderStyle != FormBorderStyle.None)
				{
					var value = DarkMode; // true=dark, false=normal

					DwmSetWindowAttribute(
						container.Handle, DWMWA_USE_IMMERSIVE_DARK_MODE,
						ref value, Marshal.SizeOf(value));
				}

				form.OnThemeChange();
			}
			else if (container is MoreUserControl control)
			{
				control.OnThemeChange();
			}

			if (container != null)
			{
				container.BackColor = InfoBack;
				container.ForeColor = ForeColor;

				Colorize(container.Controls);
			}
		}


		public static bool HasCustomTheme()
		{
			var path = Path.Combine(PathHelper.GetAppDataPath(), CustomThemeFile);
			return File.Exists(path);
		}


		private bool LoadColors()
		{
			var path = Path.Combine(PathHelper.GetAppDataPath(), CustomThemeFile);
			if (File.Exists(path))
			{
				try
				{
					var json = File.ReadAllText(path);
					var cache = JsonConvert.DeserializeObject<ThemeManager>(json, new ColorConverter());

					Colors.Clear();
					Colors = cache.Colors;
					DarkMode = cache.DarkMode;
					return true;
				}
				catch (Exception exc)
				{
					Logger.Current.WriteLine("error loading custom theme file", exc);
				}
			}

			return false;
		}


		private void Colorize(Control.ControlCollection controls)
		{
			foreach (Control control in controls)
			{
				control.BackColor = BackColor;
				control.ForeColor = ForeColor;

				if (control.Controls.Count > 0)
				{
					Colorize(control.Controls);
				}

				if (control is ListView view)
				{
					foreach (ListViewItem item in view.Items)
					{
						item.BackColor = InfoBack;
						item.ForeColor = ForeColor;
					}
				}
				else if (control is MoreLinkLabel label)
				{
					label.LinkColor = LinkColor;
					label.HoverColor = HoverColor;
					label.ActiveLinkColor = LinkColor;
				}
			}
		}


		#region ColorConverter
		private sealed class ColorConverter : JsonConverter<Color>
		{
			public override void WriteJson(JsonWriter writer, Color value, JsonSerializer serializer)
			{
				writer.WriteValue(value.IsKnownColor
					? value.Name
					: $"#{value.R:X2}{value.G:X2}{value.B:X2}");
			}

			public override Color ReadJson(
				JsonReader reader, Type objectType, Color existingValue,
				bool hasExistingValue, JsonSerializer serializer)
			{
				return ColorTranslator.FromHtml((string)reader.Value);
			}
		}
		#endregion ColorConverter
	}
}
