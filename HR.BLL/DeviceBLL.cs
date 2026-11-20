using System;
using System.Collections.Generic;
using System.Net;
using HR.DAL;
using HR.Entities;
using HR.Helpers;

namespace HR.BLL
{
    /// <summary>
    /// Fingerprint device registry operations (rule 9). Every write requires
    /// "Devices.Manage" and is audit-logged. Deletion is refused while the
    /// device still carries raw fingerprint logs — deactivate instead (rule 5
    /// behaviour applied to devices: no destructive loss of audit history).
    /// </summary>
    public static class DeviceBLL
    {
        public static List<Device> GetAll(bool includeInactive)
        {
            return DeviceDAL.GetAll(includeInactive);
        }

        public static Device GetById(int deviceId)
        {
            return DeviceDAL.GetById(deviceId);
        }

        /// <summary>Adds a device (permission "Devices.Manage"); audited.</summary>
        public static int Add(Device device)
        {
            AppContext.RequirePermission("Devices.Manage");
            Validate(device, true);
            int deviceId = DeviceDAL.Insert(device);
            AuditBLL.Log("Device.Add", "Devices", deviceId.ToString(), null, Describe(device));
            return deviceId;
        }

        /// <summary>Updates a device (permission "Devices.Manage"); audited with old/new values.</summary>
        public static void Update(Device device)
        {
            AppContext.RequirePermission("Devices.Manage");
            Validate(device, false);
            Device old = DeviceDAL.GetById(device.DeviceId);
            if (old == null)
            {
                throw new BusinessException("الجهاز غير موجود (رُبما حُذف من محطة أخرى).");
            }
            DeviceDAL.Update(device);
            AuditBLL.Log("Device.Update", "Devices", device.DeviceId.ToString(), Describe(old), Describe(device));
        }

        /// <summary>Activates or deactivates a device; audited.</summary>
        public static void Toggle(int deviceId, bool isActive)
        {
            AppContext.RequirePermission("Devices.Manage");
            Device existing = DeviceDAL.GetById(deviceId);
            if (existing == null)
            {
                throw new BusinessException("الجهاز غير موجود.");
            }
            DeviceDAL.SetActive(deviceId, isActive);
            AuditBLL.Log(isActive ? "Device.Activate" : "Device.Deactivate", "Devices",
                deviceId.ToString(), "IsActive=" + (existing.IsActive == true), "IsActive=" + isActive);
        }

        /// <summary>Deletes a device; refused when raw fingerprint logs reference it.</summary>
        public static void Delete(int deviceId)
        {
            AppContext.RequirePermission("Devices.Manage");
            Device existing = DeviceDAL.GetById(deviceId);
            if (existing == null)
            {
                throw new BusinessException("الجهاز غير موجود.");
            }
            if (FingerprintDAL.CountByDevice(deviceId) > 0)
            {
                throw new BusinessException("لا يمكن حذف جهاز مرتبط بسجلات بصمة — عطّله بدلاً من حذفه للحفاظ على تاريخ السجلات.");
            }
            DeviceDAL.Delete(deviceId);
            AuditBLL.Log("Device.Delete", "Devices", deviceId.ToString(), Describe(existing), null);
        }

        private static void Validate(Device device, bool isNew)
        {
            if (device == null)
            {
                throw new BusinessException("لا يوجد جهاز للحفظ.");
            }
            if (string.IsNullOrWhiteSpace(device.DeviceName))
            {
                throw new BusinessException("اسم الجهاز إلزامي.");
            }
            device.DeviceName = device.DeviceName.Trim();
            if (device.DeviceName.Length > 60)
            {
                throw new BusinessException("اسم الجهاز يجب ألا يتجاوز 60 حرفًا.");
            }
            if (DeviceDAL.NameTaken(device.DeviceName, isNew ? (int?)null : device.DeviceId))
            {
                throw new BusinessException("اسم الجهاز '" + device.DeviceName + "' مستخدَم بالفعل.");
            }
            if (string.IsNullOrWhiteSpace(device.IpAddress))
            {
                throw new BusinessException("عنوان IP للجهاز إلزامي.");
            }
            device.IpAddress = device.IpAddress.Trim();
            IPAddress parsedAddress;
            if (!IPAddress.TryParse(device.IpAddress, out parsedAddress) || device.IpAddress.Length > 15)
            {
                throw new BusinessException("عنوان IP غير صحيح — استخدم صيغة مثل 192.168.1.201.");
            }
            if (device.Port == null || device.Port.Value < 1 || device.Port.Value > 65535)
            {
                throw new BusinessException("المنفذ يجب أن يكون رقمًا بين 1 و 65535 (الافتراضي لأجهزة ZKTeco هو 4370).");
            }
            if (!string.IsNullOrEmpty(device.SerialNumber) && device.SerialNumber.Trim().Length > 40)
            {
                throw new BusinessException("الرقم التسلسلي يجب ألا يتجاوز 40 حرفًا.");
            }
            device.SerialNumber = string.IsNullOrWhiteSpace(device.SerialNumber) ? null : device.SerialNumber.Trim();
            if (!string.IsNullOrEmpty(device.Location) && device.Location.Trim().Length > 100)
            {
                throw new BusinessException("الموقع يجب ألا يتجاوز 100 حرف.");
            }
            device.Location = string.IsNullOrWhiteSpace(device.Location) ? null : device.Location.Trim();
        }

        private static string Describe(Device device)
        {
            return "Name=" + device.DeviceName + ", IP=" + device.IpAddress + ", Port=" + (device.Port ?? 0) +
                ", Serial=" + (device.SerialNumber ?? "-") +
                ", Active=" + (device.IsActive == true) +
                (string.IsNullOrEmpty(device.Location) ? "" : ", Location=" + device.Location);
        }
    }
}
