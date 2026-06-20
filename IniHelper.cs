using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Text;

namespace Nimaime.SMSToolkit
{
	public static class IniHelper
	{
		static string filePath = "./Nimaime.SMSToolkit.ini";
		// 写入INI文件
		[DllImport("kernel32.dll", CharSet = CharSet.Auto, SetLastError = true)]
		private static extern bool WritePrivateProfileString(string section, string key, string val, string filePath);

		// 读取INI文件
		[DllImport("kernel32.dll", CharSet = CharSet.Auto, SetLastError = true)]
		private static extern int GetPrivateProfileString(string section, string key, string def, StringBuilder retVal, int size, string filePath);

		/// <summary>
		/// 读取 INI 文件值
		/// </summary>
		public static string ReadValue(string section, string key)
		{
			StringBuilder temp = new StringBuilder(255);
			GetPrivateProfileString(section, key, "", temp, 255, filePath);
			return temp.ToString();
		}

		/// <summary>
		/// 写入 INI 文件值
		/// </summary>
		public static void WriteValue(string section, string key, string value)
		{
			WritePrivateProfileString(section, key, value, filePath);
		}
	}
}
