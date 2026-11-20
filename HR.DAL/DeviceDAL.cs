using System;
using System.Collections.Generic;
using System.Data;
using System.Data.OleDb;
using HR.Entities;

namespace HR.DAL
{
    /// <summary>
    /// Fingerprint device registry (rule 9): the Devices table holds every
    /// reader (name, IP, port, serial, last sync state). All statements are
    /// parameterized; the sync-state update is a targeted single-row update.
    /// </summary>
    public static class DeviceDAL
    {
        private static Device MapRow(OleDbDataReader reader)
        {
            Device device = new Device();
            device.DeviceId = reader.GetInt32(0);
            device.DeviceName = reader.IsDBNull(1) ? null : reader.GetString(1);
            device.IpAddress = reader.IsDBNull(2) ? null : reader.GetString(2);
            device.Port = reader.IsDBNull(3) ? (int?)null : reader.GetInt32(3);
            device.SerialNumber = reader.IsDBNull(4) ? null : reader.GetString(4);
            device.LastSync = reader.IsDBNull(5) ? (DateTime?)null : reader.GetDateTime(5);
            device.LastStatus = reader.IsDBNull(6) ? null : reader.GetString(6);
            device.LastPingDelay = reader.IsDBNull(7) ? (int?)null : reader.GetInt32(7);
            device.IsActive = reader.IsDBNull(8) ? (bool?)null : reader.GetBoolean(8);
            device.Location = reader.IsDBNull(9) ? null : reader.GetString(9);
            return device;
        }

        private const string SelectColumns =
            "DeviceId, DeviceName, IpAddress, Port, SerialNumber, LastSync, LastStatus, LastPingDelay, IsActive, Location";

        /// <summary>All devices ordered by name.</summary>
        public static List<Device> GetAll(bool includeInactive)
        {
            string sql = "SELECT " + SelectColumns + " FROM Devices";
            if (!includeInactive)
            {
                sql += " WHERE IsActive <> 0";   // Jet YESNO stores True as -1; ' = 1' never matches
            }
            sql += " ORDER BY DeviceName";
            return Db.ReadList(sql, MapRow);
        }

        /// <summary>Returns one device or null when the id is unknown.</summary>
        public static Device GetById(int deviceId)
        {
            return Db.ReadOne<Device>(
                "SELECT " + SelectColumns + " FROM Devices WHERE DeviceId = ?", MapRow, Db.P(deviceId));
        }

        /// <summary>Whether another active record already carries the same serial number.</summary>
        public static bool SerialExists(string serialNumber, int? excludeDeviceId)
        {
            if (string.IsNullOrWhiteSpace(serialNumber))
            {
                return false;
            }
            string sql = "SELECT COUNT(*) FROM Devices WHERE SerialNumber = ?";
            if (excludeDeviceId != null)
            {
                sql += " AND DeviceId <> ?";
                return Db.Count(sql, Db.P(serialNumber), Db.P(excludeDeviceId.Value)) > 0;
            }
            return Db.Count(sql, Db.P(serialNumber)) > 0;
        }

        private static bool NameExists(string deviceName, int? excludeDeviceId)
        {
            string sql = "SELECT COUNT(*) FROM Devices WHERE DeviceName = ?";
            if (excludeDeviceId != null)
            {
                sql += " AND DeviceId <> ?";
                return Db.Count(sql, Db.P(deviceName), Db.P(excludeDeviceId.Value)) > 0;
            }
            return Db.Count(sql, Db.P(deviceName)) > 0;
        }

        /// <summary>Public name-uniqueness check used by the BLL validation.</summary>
        public static bool NameTaken(string deviceName, int? excludeDeviceId)
        {
            return NameExists(deviceName, excludeDeviceId);
        }

        /// <summary>Inserts a device and returns its id (Jet: SELECT @@IDENTITY).</summary>
        public static int Insert(Device device)
        {
            using (OleDbConnection connection = Db.CreateConnection())
            using (OleDbCommand command = connection.CreateCommand())
            {
                command.CommandText =
                    "INSERT INTO Devices (DeviceName, IpAddress, Port, SerialNumber, LastStatus, IsActive, Location) " +
                    "VALUES (?, ?, ?, ?, ?, ?, ?)";
                command.Parameters.Add(Db.P(device.DeviceName));
                command.Parameters.Add(Db.P(device.IpAddress));
                command.Parameters.Add(Db.P(device.Port ?? 4370));
                command.Parameters.Add(Db.P(device.SerialNumber));
                command.Parameters.Add(Db.P(device.LastStatus));
                command.Parameters.Add(Db.PBool(device.IsActive ?? true));
                command.Parameters.Add(Db.P(device.Location));
                command.ExecuteNonQuery();

                command.CommandText = "SELECT @@IDENTITY";
                return Convert.ToInt32(command.ExecuteScalar());
            }
        }

        public static void Update(Device device)
        {
            Db.ExecuteNonQuery(
                "UPDATE Devices SET DeviceName = ?, IpAddress = ?, Port = ?, SerialNumber = ?, IsActive = ?, Location = ? WHERE DeviceId = ?",
                Db.P(device.DeviceName),
                Db.P(device.IpAddress),
                Db.P(device.Port),
                Db.P(device.SerialNumber),
                Db.PBool(device.IsActive ?? true),
                Db.P(device.Location),
                Db.P(device.DeviceId));
        }

        public static void SetActive(int deviceId, bool isActive)
        {
            Db.ExecuteNonQuery("UPDATE Devices SET IsActive = ? WHERE DeviceId = ?",
                Db.P(isActive), Db.P(deviceId));
        }

        /// <summary>Writes the result of the last connection/download attempt.</summary>
        public static void UpdateSyncState(int deviceId, DateTime lastSync, string lastStatus, int? pingDelayMs)
        {
            Db.ExecuteNonQuery("UPDATE Devices SET LastSync = ?, LastStatus = ?, LastPingDelay = ? WHERE DeviceId = ?",
                Db.PDate(lastSync), Db.P(lastStatus), Db.P(pingDelayMs), Db.P(deviceId));
        }

        /// <summary>Deletes a device row; Jet refuses the delete while fingerprint logs reference it.</summary>
        public static void Delete(int deviceId)
        {
            Db.ExecuteNonQuery("DELETE FROM Devices WHERE DeviceId = ?", Db.P(deviceId));
        }
    }
}
