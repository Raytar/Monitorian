﻿using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Management;
using System.Runtime.Serialization;
using System.Text;
using System.Threading.Tasks;

namespace Monitorian.Models.Monitor
{
	/// <summary>
	/// MSMonitorClass Functions
	/// </summary>
	internal class MSMonitor
	{
		#region Type

		[DataContract]
		public class DesktopItem
		{
			[DataMember(Order = 0)]
			public string DeviceInstanceId { get; private set; }

			[DataMember(Order = 1)]
			public string Description { get; private set; }

			[DataMember(Order = 2)]
			public byte[] BrightnessLevels { get; private set; }

			public DesktopItem(
				string deviceInstanceId,
				string description)
			{
				this.DeviceInstanceId = deviceInstanceId;
				this.Description = description;
			}

			public DesktopItem(
				string deviceInstanceId,
				string description,
				byte[] brightnessLevels) : this(
					deviceInstanceId: deviceInstanceId,
					description: description)
			{
				this.BrightnessLevels = brightnessLevels;
			}
		}

		#endregion

		public static IEnumerable<DesktopItem> EnumerateDesktopMonitors()
		{
			var monitors = new List<DesktopItem>();

			using (var @class = new ManagementClass("Win32_DesktopMonitor"))
			using (var instances = @class.GetInstances())
			{
				foreach (ManagementObject instance in instances)
				{
					using (instance)
					{
						var pnpDeviceId = (string)instance.GetPropertyValue("PNPDeviceID");
						if (string.IsNullOrWhiteSpace(pnpDeviceId))
							continue;

						var description = (string)instance.GetPropertyValue("Description");
						if (string.IsNullOrWhiteSpace(description))
							continue;

						monitors.Add(new DesktopItem(
							deviceInstanceId: pnpDeviceId,
							description: description));
					}
				}
			}

			using (var @class = new ManagementClass(@"root\wmi", "WmiMonitorBrightness", null))
			using (var instances = @class.GetInstances())
			using (var enumerator = instances.GetEnumerator())
			{
				while (true)
				{
					try
					{
						// ManagementObjectCollection.ManagementObjectEnumerator.MoveNext method for 
						// WmiMonitorBrightness instance may throw a ManagementException when called
						// immediately after resume.
						if (!enumerator.MoveNext())
							break;
					}
					catch (ManagementException ex) when (ex.ErrorCode == ManagementStatus.NotSupported)
					{
						Debug.WriteLine($"Failed to retrieve data by WmiMonitorBrightness." + Environment.NewLine
							+ ex);
						yield break;
					}

					using (var instance = (ManagementObject)enumerator.Current)
					{
						var instanceName = (string)instance.GetPropertyValue("InstanceName");
						var monitor = monitors.FirstOrDefault(x => instanceName.StartsWith(x.DeviceInstanceId, StringComparison.OrdinalIgnoreCase));
						if (monitor == null)
							continue;

						var level = (byte[])instance.GetPropertyValue("Level");

						//Debug.WriteLine($"DeviceInstanceId: {monitor.DeviceInstanceId}");
						//Debug.WriteLine($"Description: {monitor.Description}");
						//Debug.WriteLine($"Level length: {level.Length}");
						//Debug.WriteLine($"Active (unreliable): {(bool)instance["Active"]}");

						yield return new DesktopItem(
							deviceInstanceId: monitor.DeviceInstanceId,
							description: monitor.Description,
							brightnessLevels: level);
					}
				}
			}
		}

		public static int GetBrightness(string deviceInstanceId)
		{
			if (string.IsNullOrWhiteSpace(deviceInstanceId))
				throw new ArgumentNullException(nameof(deviceInstanceId));

			using (var searcher = GetSearcher("WmiMonitorBrightness"))
			using (var instances = searcher.Get())
			{
				foreach (ManagementObject instance in instances)
				{
					using (instance)
					{
						var instanceName = (string)instance.GetPropertyValue("InstanceName");
						if (instanceName.StartsWith(deviceInstanceId, StringComparison.OrdinalIgnoreCase))
							return (byte)instance.GetPropertyValue("CurrentBrightness");
					}
				}
				return -1;
			}
		}

		public static bool SetBrightness(string deviceInstanceId, int brightness, int timeout = int.MaxValue)
		{
			if (string.IsNullOrWhiteSpace(deviceInstanceId))
				throw new ArgumentNullException(nameof(deviceInstanceId));
			if ((brightness < 0) || (100 < brightness))
				throw new ArgumentOutOfRangeException(nameof(brightness), $"{nameof(brightness)} must be in the range of 0 to 100.");

			using (var searcher = GetSearcher("WmiMonitorBrightnessMethods"))
			using (var instances = searcher.Get())
			{
				foreach (ManagementObject instance in instances)
				{
					using (instance)
					{
						var instanceName = (string)instance.GetPropertyValue("InstanceName");
						if (instanceName.StartsWith(deviceInstanceId, StringComparison.OrdinalIgnoreCase))
						{
							object result = instance.InvokeMethod("WmiSetBrightness", new object[] { (uint)timeout, (byte)brightness });

							var isSuccess = (result == null); // Return value will be null if succeeded.
							if (!isSuccess)
							{
								var errorCode = (uint)result;
								isSuccess = (errorCode == 0);
								if (!isSuccess)
								{
									Debug.WriteLine($"Failed to set brightness. ({errorCode})");
								}
							}
							return isSuccess;
						}
					}
				}
				return false;
			}
		}

		private static ManagementObjectSearcher GetSearcher(string className) =>
			new ManagementObjectSearcher(new ManagementScope(@"root\wmi"), new SelectQuery(className));
	}
}