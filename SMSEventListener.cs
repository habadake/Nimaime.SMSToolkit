using MbnApi;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using System.Windows;
using Microsoft.Toolkit.Uwp.Notifications;
using Nimaime.SMSToolkit;

namespace Nimaime.SMS
{
	public class SMSEventListener: IMbnSmsEvents
	{
		private IConnectionPoint smsConnectionPoint;
		private uint eventCookie = 0;
		private HashSet<uint> dicReqID = [];

		public SMSEventListener(IMbnInterface mbn)
		{
			RegisterSmsEvents((IMbnSms)mbn);
		}

		public void RegisterSmsEvents(IMbnSms smsInterface)
		{
			// 初始化 MBN 管理器
			MbnInterfaceManager mbnManager = new();

			// 【关键修正】：必须在 mbnManager 上获取 IConnectionPointContainer 接口
			IConnectionPointContainer cpc = mbnManager;

			// 获取 IMbnSmsEvents 接口的 GUID
			Guid iid = typeof(IMbnSmsEvents).GUID;

			// 查找移动短信事件的连接点
			cpc.FindConnectionPoint(ref iid, out smsConnectionPoint);

			// 注册自己（this）来接收回调通知
			smsConnectionPoint.Advise(this, out eventCookie);

			Debug.WriteLine("通过 MbnInterfaceManager 成功注册 IMbnSmsEvents！");
		}

		public void OnSetSmsConfigurationComplete(IMbnSms sms, uint requestID, int status)
		{
			Debug.WriteLine($"{requestID} Status: {status}");
		}

		public void OnSmsSendComplete(IMbnSms sms, uint requestID, int status)
		{
			if (status == 0)
			{
				Console.WriteLine($"[{DateTime.Now.ToString():yyyy-MM-dd HH:mm:ss}]短信发送成功！");
			}
			else
			{
				Console.WriteLine($"[{DateTime.Now.ToString():yyyy-MM-dd HH:mm:ss}]短信发送失败！");
			}
		}

		public void OnSmsReadComplete(IMbnSms sms, MBN_SMS_FORMAT SmsFormat, Array readMsgs, bool moreMsgs, uint requestID, int status)
		{
			Debug.WriteLine($"短信读取完成，Request ID: {requestID} Status: {status} More: {moreMsgs}");
			if (!dicReqID.Add(requestID))
			{
				Debug.WriteLine($"Request ID: {requestID} 已记录过，不再执行");
				return;
			}
			if (SmsFormat != MBN_SMS_FORMAT.MBN_SMS_FORMAT_PDU)
			{
				Debug.WriteLine($"信息非PDU格式，无法解码");
			}
			if (readMsgs is null || status != 0)
			{
				return;
			}
			foreach (var obj in readMsgs)
			{
				try
				{
					if (obj is not IMbnSmsReadMsgPdu msg) continue;
					SMSMethods.SMSMessage message = SMSMethods.ParsePDU(msg.PduData);

					Console.WriteLine($"[{message.Timestamp:yyyy-MM-dd HH:mm:ss}]来自: {message.FromTo} 内容: {message.Content}");

					new ToastContentBuilder()
						.AddArgument("requestID", requestID)
						.AddText($"来自: {message.FromTo} 时间: {message.Timestamp:yyyy-MM-dd HH:mm:ss}")
						.AddText($"{message.Content}")
						.Show();

					Forward forward = new((IMbnInterface)sms);
					forward.Execute(message);
				}
				catch (Exception ex)
				{
					Debug.WriteLine("解析失败: " + ex.Message);
				}
				return;
			}
		}

		public void OnSmsNewClass0Message(IMbnSms sms, MBN_SMS_FORMAT SmsFormat, Array readMsgs)
		{
			Debug.WriteLine($"收到0级短信");
			if (SmsFormat != MBN_SMS_FORMAT.MBN_SMS_FORMAT_PDU)
			{
				Debug.WriteLine($"信息非PDU格式，无法解码");
			}
			return;
		}

		public void OnSmsDeleteComplete(IMbnSms sms, uint requestID, int status)
		{
			return;
		}

		public void OnSmsConfigurationChange(IMbnSms sms)
		{
			return;
		}

		public void OnSmsStatusChange(IMbnSms sms)
		{
			return;
		}


		// 6. 注销事件
		public void UnregisterSmsEvents()
		{
			if (smsConnectionPoint != null && eventCookie != 0)
			{
				smsConnectionPoint.Unadvise(eventCookie);
				Marshal.ReleaseComObject(smsConnectionPoint);
				Debug.WriteLine("事件注销成功。");
			}
		}
	}
}
