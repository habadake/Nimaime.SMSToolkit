using MbnApi;
using System.Diagnostics;
using System.Globalization;
using System.Text;


namespace Nimaime.SMS
{
	public class SMSMethods(IMbnInterface mbn)
	{
		public class SMSMessage(string number, string msg)
		{
			/// <summary>
			/// 收发信息的号码，发送时为接收方号码，接收时为发送方号码
			/// </summary>
			public string FromTo { get; set; } = number;
			/// <summary>
			/// 信息内容
			/// </summary>
			public string Content { get; set; } = msg;
			/// <summary>
			/// 收发信息的时间戳，发送时为当前时间，接收时为短信中心的时间戳
			/// </summary>
			public DateTime Timestamp { get; set; } = DateTime.Now;
		}

		private static class Gsm7Bit
		{
			public static readonly char[] Table =
			{
				'@','£','$','¥','è','é','ù','ì','ò','Ç','\n','Ø','ø','\r','Å','å',
				'Δ','_','Φ','Γ','Λ','Ω','Π','Ψ','Σ','Θ','Ξ','€','Æ','æ','ß','É',
				' ','!','"','#','¤','%','&','\'','(',')','*','+',',','-','.','/',
				'0','1','2','3','4','5','6','7','8','9',':',';','<','=','>','?',
				'¡','A','B','C','D','E','F','G','H','I','J','K','L','M','N','O',
				'P','Q','R','S','T','U','V','W','X','Y','Z','Ä','Ö','Ñ','Ü','§',
				'¿','a','b','c','d','e','f','g','h','i','j','k','l','m','n','o',
				'p','q','r','s','t','u','v','w','x','y','z','ä','ö','ñ','ü','à'
			};

			public static char Decode(byte v)
			{
				if (v < Table.Length) return Table[v];
				return '?';
			}

			public static byte Encode(char c)
			{
				for (byte i = 0; i < Table.Length; i++)
					if (Table[i] == c) return i;
				return 0;
			}
		}

		IMbnInterface MBN { get; set; } = mbn;

		/// <summary>
		/// 发送短信
		/// </summary>
		/// <returns>requestID</returns>
		public void SendSMS(SMSMessage message)
		{
			try
			{
				IMbnSms sms = (IMbnSms)MBN;
				IMbnSmsConfiguration smsCon = sms.GetSmsConfiguration();
				string pdu = MakePDU(message.FromTo, message.Content, out int pduLen);
				var reg = (IMbnRegistration)MBN;
				sms.SmsSendPdu(pdu, (byte)pduLen, out uint reqID);
				Console.WriteLine($"[{message.Timestamp:yyyy-MM-dd HH:mm:ss}]尝试发送短信到: {message.FromTo} 内容: \n{message.Content}");
				Console.WriteLine($"[{message.Timestamp:yyyy-MM-dd HH:mm:ss}]PDU: {pdu} RequestID: {reqID}");
				Debug.WriteLine(pdu + " " + (byte)pduLen);
			}
			catch (Exception ex)
			{
				Console.WriteLine(ex.ToString());
			}
		}

		/// <summary>
		/// 发送短信
		/// </summary>
		/// <param name="num">接收方号码</param>
		/// <param name="msg">信息</param>
		public void SendSMS(string num, string msg)
		{
			SendSMS(new SMSMessage(num, msg));
		}

		public static string MakePDU(string phoneNumber, string msgText, out int pduLen)
		{
			bool sevenBit = true;
			for (int i = 0; i < msgText.Length && sevenBit; i++)
			{
				char c = msgText[i];
				if ((c < ' ' || c > 'z') && c != '\n')
					sevenBit = false;
			}

			const int Max7bitMsgLen = 160;
			const int MaxUnicodeMsgLen = 70;
			int maxMsgLen = sevenBit ? Max7bitMsgLen : MaxUnicodeMsgLen;

			if (msgText.Length > maxMsgLen)
				msgText = msgText.Substring(0, maxMsgLen);

			StringBuilder pdu = new();
			pdu.Append("00");                          // Service Center Adress (SCA)
			pdu.Append("01");                          // PDU-type
			pdu.Append("00");                          // Message Reference (MR)
			pdu.Append(EncodePhone(phoneNumber));      // Destination Adress (DA)
			pdu.Append("00");                          // Protocol Identifier (PID)

			byte dcs;                                  // Data Coding Scheme (DCS)
			List<byte> ud;                             // User Data (UD)
			byte udl;                                  // User Data Length (UDL)

			if (sevenBit)
			{
				dcs = 0x00;
				ud = Encode7bitText(msgText);
				udl = (byte)msgText.Length;
			}
			else
			{
				dcs = 0x08;
				ud = EncodeUnicodeText(msgText);
				udl = (byte)ud.Count;
			}

			pdu.Append(dcs.ToString("X2"));
			pdu.Append(udl.ToString("X2"));

			foreach (byte b in ud)
				pdu.Append(b.ToString("X2"));

			pduLen = (pdu.Length - 2) / 2;
			return pdu.ToString();
		}

		public static SMSMessage ParsePDU(string pdu = "0491640003240BA17176748491F2000862600271905023046D4B8BD5")
		{
			int index = 0;

			// =========================
			// 1. SMSC
			// =========================
			int smscLen = Convert.ToInt32(pdu.Substring(index, 2), 16);
			index += 2;

			string smsc = "";
			if (smscLen > 0)
			{
				smsc = DecodeSemiOctet(pdu.Substring(index + 2, (smscLen - 1) * 2));
				index += smscLen * 2;
			}

			Debug.WriteLine("SMSC#" + smsc);

			// =========================
			// 2. PDU type
			// =========================
			string firstOctet = pdu.Substring(index, 2);
			index += 2;

			bool isUdh = (Convert.ToInt32(firstOctet, 16) & 0x40) != 0;

			// =========================
			// 3. Sender
			// =========================
			int senderLen = Convert.ToInt32(pdu.Substring(index, 2), 16);
			index += 2;

			index += 2; // type

			string sender = DecodeSemiOctet(
				pdu.Substring(index, senderLen % 2 == 0 ? senderLen : senderLen + 1)
			);

			index += senderLen % 2 == 0 ? senderLen : senderLen + 1;

			Debug.WriteLine("Sender:" + sender);

			// =========================
			// 4. PID
			// =========================
			string pid = pdu.Substring(index, 2);
			index += 2;

			Debug.WriteLine("TP_PID:" + pid);

			// =========================
			// 5. DCS
			// =========================
			string dcs = pdu.Substring(index, 2);
			index += 2;

			int alphabet = GetAlphabet(dcs);

			Debug.WriteLine("Alphabet:" + (alphabet == 16 ? "UCS2(16)bit" : alphabet == 7 ? "GSM7" : "8bit"));

			// =========================
			// 6. Timestamp
			// =========================
			DateTime dt = DecodeTimestamp(pdu.Substring(index, 14));
			index += 14;

			Debug.WriteLine("TimeStamp:" + dt.ToString("yyyy-MM-dd HH:mm:ss"));

			// =========================
			// 7. UDL
			// =========================
			int udl = Convert.ToInt32(pdu.Substring(index, 2), 16);
			index += 2;

			// =========================
			// 8. User Data
			// =========================
			string ud = pdu[index..];

			string message;

			if (alphabet == 16)
			{
				message = DecodeUcs2(ud[..(udl * 2)]);
			}
			else
			{
				message = DecodeGsm7(ud, udl);
			}

			Debug.WriteLine(message);
			Debug.WriteLine("Length:" + message.Length);

			SMSMessage smsMessage = new(sender, message)
			{
				Timestamp = dt
			};
			return smsMessage;
		}

		public static string DecodeGsm7(string hex, int septetCount)
		{
			byte[] bytes = new byte[hex.Length / 2];

			for (int i = 0; i < bytes.Length; i++)
				bytes[i] = Convert.ToByte(hex.Substring(i * 2, 2), 16);

			int carryBits = 0;
			int carryOver = 0;

			StringBuilder result = new StringBuilder();

			foreach (byte b in bytes)
			{
				int current = ((b << carryBits) & 0x7F) | carryOver;
				result.Append(Gsm7Bit.Decode((byte)current));

				carryOver = b >> (7 - carryBits);
				carryBits++;

				if (carryBits == 7)
				{
					result.Append(Gsm7Bit.Decode((byte)carryOver));
					carryBits = 0;
					carryOver = 0;
				}
			}

			return result.ToString();
		}

		public static string DecodeSemiOctet(string hex)
		{
			StringBuilder sb = new StringBuilder();

			for (int i = 0; i < hex.Length; i += 2)
			{
				char a = hex[i];
				char b = hex[i + 1];

				sb.Append(b);
				if (a != 'F') sb.Append(a);
			}

			return sb.ToString();
		}

		public static string DecodeUcs2(string hex)
		{
			byte[] data = new byte[hex.Length / 2];

			for (int i = 0; i < data.Length; i++)
				data[i] = Convert.ToByte(hex.Substring(i * 2, 2), 16);

			return Encoding.BigEndianUnicode.GetString(data);
		}

		private static DateTime DecodeTimestamp(string ts)
		{
			// YYMMDDhhmmss swap BCD
			static string Swap(string s)
			{
				return "" + s[1] + s[0];
			}

			string strDateTime = "20" + 
				   Swap(ts[..2]) + "-" +
				   Swap(ts.Substring(2, 2)) + "-" +
				   Swap(ts.Substring(4, 2)) + " " +
				   Swap(ts.Substring(6, 2)) + ":" +
				   Swap(ts.Substring(8, 2)) + ":" +
				   Swap(ts.Substring(10, 2));
			return DateTime.ParseExact(strDateTime, "yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture);
		}

		private static int GetAlphabet(string dcs)
		{
			int v = Convert.ToInt32(dcs, 16);
			if ((v & 0x0C) == 0x08) return 16;
			if ((v & 0x0C) == 0x04) return 8;
			return 7;
		}

		/// <summary>
		/// 编码手机号码为PDU格式
		/// </summary>
		/// <param name="phoneNumber"></param>
		/// <returns></returns>
		private static string EncodePhone(string phoneNumber)
		{
			StringBuilder result = new StringBuilder();
			int phoneLen = phoneNumber.Length;

			if (phoneLen > 0)
			{
				if (phoneNumber[0] == '+')
				{
					phoneNumber = phoneNumber.Substring(1);
					result.Append("91");
					phoneLen--;
				}
				else
					result.Append("81");

				int i = 1;
				while (i < phoneLen)
				{
					result.Append(phoneNumber[i]);
					result.Append(phoneNumber[i - 1]);
					i += 2;
				}
				if (i == phoneLen)
				{
					result.Append('F');
					result.Append(phoneNumber[i - 1]);
				}
			}

			return phoneLen.ToString("X2") + result.ToString();
		}

		/// <summary>
		/// 编码文本为7-bit编码
		/// </summary>
		/// <param name="text"></param>
		/// <returns></returns>
		private static List<byte> Encode7bitText(string text)
		{
			List<byte> result = new List<byte>();
			byte[] bytes = Encoding.Default.GetBytes(text);

			byte bit = 7; // от 7 до 1
			int i = 0;
			int len = bytes.Length;
			while (i < len)
			{
				byte sym = (byte)(bytes[i] & 0x7F);
				byte nextSym = i < len - 1 ? (byte)(bytes[i + 1] & 0x7F) : (byte)0;
				byte code = (byte)((sym >> (7 - bit)) | (nextSym << bit));

				if (bit == 1)
				{
					i++;
					bit = 7;
				}
				else
					bit--;

				result.Add(code);
				i++;
			}

			return result;
		}

		/// <summary>
		/// 编码文本为16-bit编码
		/// </summary>
		/// <param name="text"></param>
		/// <returns></returns>
		private static List<byte> EncodeUnicodeText(string text)
		{
			List<byte> result = new List<byte>();

			for (int i = 0; i < text.Length; i++)
			{
				int val = char.ConvertToUtf32(text, i);
				result.Add((byte)(val >> 8 & 0xFF));
				result.Add((byte)(val & 0xFF));
			}

			return result;
		}
	}
}
