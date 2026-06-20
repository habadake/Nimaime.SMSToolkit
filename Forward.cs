using MbnApi;
using Nimaime.SMS;
using System;
using System.Collections.Generic;
using System.Net.Http.Json;
using System.Text;
using System.Text.RegularExpressions;
using Windows.Devices.Power;
using Windows.Devices.Sms;

namespace Nimaime.SMSToolkit
{
	public class Forward
	{
		IMbnInterface mbn;
		bool BARK_FORWARD { get; set; }
		string BARK_API { get; set; }
		bool SMS_FORWARD { get; set; }
		string SMS_TARGET { get; set; }

		public Forward(IMbnInterface @interface)
		{
			BARK_FORWARD = IniHelper.ReadValue("Forward", "BARK_FORWARD") == "TRUE";
			BARK_API = IniHelper.ReadValue("Forward", "BARK_API");
			SMS_FORWARD = IniHelper.ReadValue("Forward", "SMS_FORWARD") == "TRUE";
			SMS_TARGET = IniHelper.ReadValue("Forward", "SMS_TARGET");
			mbn = @interface;
		}

		/// <summary>
		/// 执行转发
		/// </summary>
		/// <param name="originalMessage"></param>
		/// <returns></returns>
		public bool Execute(SMSMethods.SMSMessage originalMessage)
		{
			SMSMethods sms = new(mbn);
			if (BARK_FORWARD)
			{
				SendToBark(originalMessage, BARK_API);
			}
			if (SMS_FORWARD)
			{
				string content = $"From: {originalMessage.FromTo}\nTime: {originalMessage.Timestamp}\nContent: {originalMessage.Content}";
				SMSMethods.SMSMessage message = new(SMS_TARGET, content);
				sms.SendSMS(message);
			}
			return BARK_FORWARD || SMS_FORWARD;
		}

		/// <summary>
		/// BARK 转发
		/// </summary>
		/// <param name="message"></param>
		/// <param name="uri"></param>
		/// <returns></returns>
		private static bool SendToBark(SMSMethods.SMSMessage message, string uri)
		{
			string verifyCode = "";
			// 匹配 “验证码：” 或 “验证码是” 后面的 4-6 位数字
			string pattern = @"(?:验证码|码)\D*(\d{4,6})";
			Match match = Regex.Match(message.Content, pattern);
			if (match.Success)
			{
				// Groups[1] 包含括号内匹配到的具体数字
				verifyCode = match.Groups[1].Value;
				Console.WriteLine($"$\"[{message.Timestamp:yyyy-MM-dd HH:mm:ss}]提取到验证码: {verifyCode}");
			}
			try
			{
				using HttpClient client = new();
				var payload = new
				{
					title = $"来自 {message.FromTo}",
					body = $"时间: {message.Timestamp}\n内容: {message.Content}",
					category = "SMS_FORWARD",
					sound = "alarm",
					level = "timeSensitive",
					icon = "https://img.icons8.com/?size=100&id=PEEOhACJZJG5&format=png&color=000000",
					group = "SMS_FORWARD",
					copy = verifyCode
				};
				var response = client.PostAsJsonAsync(uri, payload).Result;
				Console.WriteLine($"[{message.Timestamp:yyyy-MM-dd HH:mm:ss}]已转发到BARK: {uri})");
				return response.IsSuccessStatusCode;
			}
			catch (Exception ex)
			{
				Console.WriteLine($"[{message.Timestamp:yyyy-MM-dd HH:mm:ss}]发送到 Bark 失败: {ex.Message}");
				return false;
			}
		}
	}
}
