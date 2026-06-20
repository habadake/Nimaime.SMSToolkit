using MbnApi;
using Nimaime.SMS;
using System.Runtime.InteropServices;
using System.Text;
using Windows.Networking.NetworkOperators;

namespace Nimaime.SMSToolkit
{
	internal class Program
	{
		static IMbnInterface mbn;
		static void Main(string[] args)
		{
			// 1. 初始化接口管理器
			IMbnInterfaceManager mbnManager = (IMbnInterfaceManager)new MbnInterfaceManager();
			IMbnInterface[] interfaces = (IMbnInterface[])mbnManager.GetInterfaces();

			if (interfaces == null || interfaces.Length == 0)
			{
				Console.WriteLine("未找到任何移动宽带接口。请确保设备已连接并正确安装驱动程序。");
				return;
			}

			foreach (IMbnInterface @interface in interfaces)
			{
				mbn = @interface; // 保存第一个接口用于后续操作
				MBN_INTERFACE_CAPS caps = mbn.GetInterfaceCapability();
				
				Console.WriteLine($"{caps.model} 制造商: {caps.manufacturer} 设备ID: {caps.deviceID}");
				
				Console.WriteLine(GetFormattedProviderInfo(mbn, false));
				IMbnSubscriberInformation subscriber = mbn.GetSubscriberInformation();
				string tel = "";
				foreach (string number in subscriber.TelephoneNumbers)
				{
					tel += number + ", ";
					break;
				}
				tel = tel.TrimEnd(',', ' ');
				string iccid = subscriber.SimIccID;
				tel += $" ICCID: {iccid}";
				Console.WriteLine(tel);
				Console.WriteLine();
				Console.WriteLine("========");
				// only use the first interface for SMS listening
				break;
			}
			if (args.Length > 0)
			{
				SMSMethods.SMSMessage message = new("", "");
				for (int i = 0; i < args.Length; i++)
				{
					string arg = args[i];

					switch (arg)
					{
						case "-t":
						case "--to":
							if (i + 1 < args.Length)
								message.FromTo = args[++i];
							break;

						case "-c":
						case "--content":
							if (i + 1 < args.Length)
								message.Content = args[++i];
							break;
					}
				}

				// 基本校验
				if (string.IsNullOrWhiteSpace(message.FromTo))
				{
					Console.WriteLine("缺少参数 -t (手机号)");
					return;
				}

				if (string.IsNullOrWhiteSpace(message.Content))
				{
					Console.WriteLine("缺少参数 -c (短信内容)");
					return;
				}

				SMSMethods sms = new(mbn);
				sms.SendSMS(message);
			}
			else
			{
				RunForever();
			}
		}

		/// <summary>
		/// 永远运行程序，保持事件监听器活跃
		/// </summary>
		static void RunForever()
		{
			MBN_INTERFACE_CAPS caps = mbn.GetInterfaceCapability();
			// 注册短信事件监听器
			SMSEventListener smsListener = new(mbn);
			Console.WriteLine($"[{DateTime.Now.ToString():yyyy-MM-dd HH:mm:ss}]已注册短信事件监听器到接口 {caps.model} [{caps.deviceID}]");
			while (true)
			{
				Console.Title = GetFormattedProviderInfo(mbn, true);
				Thread.Sleep(10000);
			}
		}

		static string GetFormattedProviderInfo(IMbnInterface mbn, bool simple = false)
		{
			MBN_PROVIDER provider = mbn.GetHomeProvider();
			IMbnSignal signal = (IMbnSignal)mbn;
			uint strength = signal.GetSignalStrength();
			long dBm = strength == 0 ? 0 : -113 + 2 * strength;
			IMbnRegistration reg = (IMbnRegistration)mbn;
			MBN_DATA_CLASS dataClass = (MBN_DATA_CLASS)reg.GetCurrentDataClass();
			if (simple)
			{
				return $"{provider.providerName} {GetDataClassString(dataClass)} ({dBm} dBm)";
			}
			else
			{
				return $"运营商：{provider.providerName}({provider.providerID}) {GetDataClassString(dataClass)} ({dBm} dBm)";
			}
		}

		public static string GetDataClassString(MBN_DATA_CLASS dataClass)
		{
			return dataClass switch
			{
				MBN_DATA_CLASS.MBN_DATA_CLASS_NONE => "无连接",
				MBN_DATA_CLASS.MBN_DATA_CLASS_GPRS => "GPRS",
				MBN_DATA_CLASS.MBN_DATA_CLASS_EDGE => "EDGE",
				MBN_DATA_CLASS.MBN_DATA_CLASS_UMTS => "UMTS",
				MBN_DATA_CLASS.MBN_DATA_CLASS_HSDPA => "H+",
				MBN_DATA_CLASS.MBN_DATA_CLASS_HSUPA => "H+",
				MBN_DATA_CLASS.MBN_DATA_CLASS_LTE => "4G LTE",
				MBN_DATA_CLASS.MBN_DATA_CLASS_5G_NSA => "5G NSA",
				MBN_DATA_CLASS.MBN_DATA_CLASS_5G_SA => "5G SA",
				MBN_DATA_CLASS.MBN_DATA_CLASS_1XRTT => "1x RTT",
				MBN_DATA_CLASS.MBN_DATA_CLASS_1XEVDO => "1x EVDO",
				MBN_DATA_CLASS.MBN_DATA_CLASS_1XEVDO_REVA => "1x EVDO_A",
				MBN_DATA_CLASS.MBN_DATA_CLASS_1XEVDV => "1x EVDV",
				MBN_DATA_CLASS.MBN_DATA_CLASS_3XRTT => "3x RTT",
				MBN_DATA_CLASS.MBN_DATA_CLASS_1XEVDO_REVB => "1x EVDO_B",
				MBN_DATA_CLASS.MBN_DATA_CLASS_UMB => "UMB",
				MBN_DATA_CLASS.MBN_DATA_CLASS_CUSTOM => "Unknown",
				_ => "Unknown"
			};
		}
	}
}
