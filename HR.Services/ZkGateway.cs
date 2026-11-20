using System;
using System.Reflection;
using System.Runtime.InteropServices;

namespace HR.Services
{
    /// <summary>
    /// Late-bound COM gateway to the ZKTeco zkemkeeper SDK (rule 9).
    /// The CZKEM class is created from its ProgID instead of a build-time
    /// COMReference, so the solution compiles on any machine and the SDK is
    /// only required at the moment a device operation actually runs; engine
    /// methods are invoked through IDispatch with ByRef arguments returned in
    /// the argument array. SSR_* variants are tried first (string enroll
    /// numbers) with the legacy int-based variants as automatic fallback.
    /// Throws InvalidOperationException with an Arabic message for every
    /// SDK-level failure; the BLL translates it into a BusinessException.
    /// </summary>
    public sealed class ZkGateway : IDisposable
    {
        private const string ProgId = "zkemkeeper.CZKEM";

        private object _com;
        private Type _comType;
        private bool? _useLegacyLogApi;
        private bool? _useLegacyUserApi;

        private ZkGateway(object com, Type comType)
        {
            _com = com;
            _comType = comType;
        }

        /// <summary>Creates the COM object, or fails with a clear Arabic error when the SDK is missing.</summary>
        public static ZkGateway Create()
        {
            Type comType = Type.GetTypeFromProgID(ProgId);
            if (comType == null)
            {
                throw new InvalidOperationException(
                    "مكتبة أجهزة البصمة ZKTeco (zkemkeeper) غير مثبتة على هذا الجهاز. ثبّت ZKTeco SDK الرسمي (x86) ثم أعد المحاولة.");
            }
            try
            {
                object com = Activator.CreateInstance(comType);
                return new ZkGateway(com, comType);
            }
            catch (COMException ex)
            {
                throw new InvalidOperationException("تعذر إنشاء كائن الاتصال بمكتبة البصمة (zkemkeeper).", ex);
            }
        }

        /// <summary>Invokes an IDispatch member; ByRef arguments are written back into the args array.</summary>
        private object Invoke(string memberName, params object[] args)
        {
            try
            {
                return _comType.InvokeMember(memberName, BindingFlags.InvokeMethod, null, _com, args);
            }
            catch (TargetInvocationException ex)
            {
                throw new InvalidOperationException("فشل استدعاء '" + memberName + "' في مكتبة البصمة.", ex.InnerException ?? ex);
            }
            catch (COMException ex)
            {
                throw new InvalidOperationException("فشل استدعاء '" + memberName + "' في مكتبة البصمة (0x" + ex.ErrorCode.ToString("X8") + ").", ex);
            }
            catch (MissingMemberException ex)
            {
                throw new InvalidOperationException("المكتبة المثبتة لا تدعم الدالة '" + memberName + "'.", ex);
            }
        }

        /// <summary>Device error code of the last failed operation (GetLastError).</summary>
        public int LastErrorCode()
        {
            object[] args = new object[] { 0 };
            Invoke("GetLastError", args);
            return Convert.ToInt32(args[0]);
        }

        /// <summary>Connects over TCP/IP to a device (Connect_Net).</summary>
        public bool ConnectNet(string ipAddress, int port)
        {
            return ConvertToBool(Invoke("Connect_Net", ipAddress, port));
        }

        public void Disconnect()
        {
            Invoke("Disconnect");
        }

        /// <summary>Freezes/releases the device UI while data is being read.</summary>
        public bool EnableDevice(int machineNumber, bool enable)
        {
            return ConvertToBool(Invoke("EnableDevice", machineNumber, enable));
        }

        public void RefreshData(int machineNumber)
        {
            Invoke("RefreshData", machineNumber);
        }

        /// <summary>Reads the device serial number (GetSerialNumber).</summary>
        public string GetSerialNumber(int machineNumber)
        {
            object[] args = new object[] { machineNumber, null };
            Invoke("GetSerialNumber", args);
            return ConvertToText(args[1]);
        }

        /// <summary>Loads the device log buffer; iterate with ReadNextLog afterwards.</summary>
        public bool ReadGeneralLogData(int machineNumber)
        {
            return ConvertToBool(Invoke("ReadGeneralLogData", machineNumber));
        }

        /// <summary>
        /// Reads the next buffered attendance log. Enroll code is a string
        /// (SSR_GetGeneralLogData) with the legacy int variant as fallback;
        /// InOutFlag is the raw device flag (0 = in, 1 = out on most models).
        /// </summary>
        public bool ReadNextLog(int machineNumber, out string enrollCode, out int inOutFlag, out DateTime logTime)
        {
            enrollCode = null;
            inOutFlag = 0;
            logTime = DateTime.MinValue;

            if (_useLegacyLogApi == null)
            {
                try
                {
                    object[] args = new object[] { machineNumber, null, null, null, null, null, null, null, null, null, null };
                    bool ok = ConvertToBool(Invoke("SSR_GetGeneralLogData", args));
                    if (!ok)
                    {
                        return false;
                    }
                    enrollCode = ConvertToText(args[1]);
                    inOutFlag = ConvertToInt(args[3]);
                    logTime = BuildTime(args[4], args[5], args[6], args[7], args[8], args[9]);
                    _useLegacyLogApi = false;
                    return true;
                }
                catch (InvalidOperationException)
                {
                    _useLegacyLogApi = true;
                }
            }
            if (_useLegacyLogApi == true)
            {
                object[] legacyArgs = new object[] { machineNumber, null, null, null, null, null, null, null, null, null, null };
                bool legacyOk = ConvertToBool(Invoke("GetGeneralLogData", legacyArgs));
                if (!legacyOk)
                {
                    return false;
                }
                enrollCode = ConvertToText(legacyArgs[1]);
                inOutFlag = ConvertToInt(legacyArgs[3]);
                logTime = BuildTime(legacyArgs[4], legacyArgs[5], legacyArgs[6], legacyArgs[7], legacyArgs[8], legacyArgs[9]);
                return true;
            }
            return false;
        }

        /// <summary>Loads the device user list buffer; iterate with ReadNextUser afterwards.</summary>
        public bool ReadAllUserId(int machineNumber)
        {
            return ConvertToBool(Invoke("ReadAllUserID", machineNumber));
        }

        /// <summary>Reads the next user of the device (SSR string enroll with legacy int fallback).</summary>
        public bool ReadNextUser(int machineNumber, out string enrollCode, out string fullName, out int privilege, out bool enabled)
        {
            enrollCode = null;
            fullName = null;
            privilege = 0;
            enabled = true;

            if (_useLegacyUserApi == null)
            {
                try
                {
                    object[] args = new object[] { machineNumber, null, null, null, null, null };
                    bool ok = ConvertToBool(Invoke("SSR_GetAllUserInfo", args));
                    if (!ok)
                    {
                        return false;
                    }
                    enrollCode = ConvertToText(args[1]);
                    fullName = ConvertToText(args[2]);
                    privilege = ConvertToInt(args[4]);
                    enabled = args[5] == null || ConvertToBool(args[5]);
                    _useLegacyUserApi = false;
                    return true;
                }
                catch (InvalidOperationException)
                {
                    _useLegacyUserApi = true;
                }
            }
            if (_useLegacyUserApi == true)
            {
                object[] legacyArgs = new object[] { machineNumber, null, null, null, null };
                bool legacyOk = ConvertToBool(Invoke("GetAllUserInfo", legacyArgs));
                if (!legacyOk)
                {
                    return false;
                }
                enrollCode = ConvertToText(legacyArgs[1]);
                fullName = ConvertToText(legacyArgs[2]);
                privilege = ConvertToInt(legacyArgs[4]);
                enabled = legacyArgs[5] == null || ConvertToBool(legacyArgs[5]);
                return true;
            }
            return false;
        }

        private static DateTime BuildTime(object yearValue, object monthValue, object dayValue,
            object hourValue, object minuteValue, object secondValue)
        {
            try
            {
                return new DateTime(ConvertToInt(yearValue), ConvertToInt(monthValue), ConvertToInt(dayValue),
                    ConvertToInt(hourValue), ConvertToInt(minuteValue), ConvertToInt(secondValue));
            }
            catch (ArgumentException ex)
            {
                throw new InvalidOperationException("طابع وقت غير صالح قادم من جهاز البصمة (Y=" + ConvertToInt(yearValue) +
                    ", M=" + ConvertToInt(monthValue) + ", D=" + ConvertToInt(dayValue) + ", H=" + ConvertToInt(hourValue) +
                    ", Min=" + ConvertToInt(minuteValue) + ", S=" + ConvertToInt(secondValue) + "). صحّح ساعة الجهاز.", ex);
            }
        }

        private static bool ConvertToBool(object value)
        {
            return value != null && Convert.ToBoolean(value);
        }

        private static int ConvertToInt(object value)
        {
            if (value == null || value == DBNull.Value)
            {
                return 0;
            }
            return Convert.ToInt32(value);
        }

        private static string ConvertToText(object value)
        {
            if (value == null || value == DBNull.Value)
            {
                return null;
            }
            return Convert.ToString(value);
        }

        public void Dispose()
        {
            if (_com != null)
            {
                try
                {
                    Disconnect();
                }
                catch (Exception)
                {
                    // A failed disconnect must not mask the result of the operation
                    // the caller just finished; the COM object is released below.
                }
                Marshal.ReleaseComObject(_com);
                _com = null;
                _comType = null;
            }
        }
    }
}
