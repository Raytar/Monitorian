﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;

namespace Monitorian.Models.Monitor
{
	/// <summary>
	/// Physical monitor controlled by DDC/CI (external monitor)
	/// </summary>
	internal class DdcMonitorItem : MonitorItem
	{
		private readonly SafePhysicalMonitorHandle _handle;
		private readonly bool _isLowLevel;

		public DdcMonitorItem(
			string deviceInstanceId,
			string description,
			byte displayIndex,
			byte monitorIndex,
			SafePhysicalMonitorHandle handle,
			bool isLowLevel = false) : base(
				deviceInstanceId: deviceInstanceId,
				description: description,
				displayIndex: displayIndex,
				monitorIndex: monitorIndex,
				isAccessible: true)
		{
			this._handle = handle ?? throw new ArgumentNullException(nameof(handle));
			this._isLowLevel = isLowLevel;
		}

		private readonly object _lock = new object();

		public override bool UpdateBrightness(int brightness = -1)
		{
			lock (_lock)
			{
				this.Brightness = MonitorConfiguration.GetBrightness(_handle, _isLowLevel);
				return (0 <= this.Brightness);
			}
		}

		public override bool SetBrightness(int brightness)
		{
			if ((brightness < 0) || (100 < brightness))
				throw new ArgumentOutOfRangeException(nameof(brightness), "The brightness must be within 0 to 100.");

			lock (_lock)
			{
				if (MonitorConfiguration.SetBrightness(_handle, brightness, _isLowLevel))
				{
					this.Brightness = brightness;
					return true;
				}
				return false;
			}
		}

		#region IDisposable

		private bool _isDisposed = false;

		protected override void Dispose(bool disposing)
		{
			lock (_lock)
			{
				if (_isDisposed)
					return;

				if (disposing)
				{
					// Free any other managed objects here.
					_handle.Dispose();
				}

				// Free any unmanaged objects here.
				_isDisposed = true;

				base.Dispose(disposing);
			}
		}

		#endregion
	}

	internal class SafePhysicalMonitorHandle : SafeHandle
	{
		public SafePhysicalMonitorHandle(IntPtr handle) : base(IntPtr.Zero, true)
		{
			this.handle = handle; // IntPtr.Zero may be a valid handle.
		}

		public override bool IsInvalid => false; // The validity cannot be checked by the handle.

		protected override bool ReleaseHandle()
		{
			return MonitorConfiguration.DestroyPhysicalMonitor(handle);
		}
	}
}