﻿using System;
using System.Collections.Generic;
using System.Data;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Runtime.InteropServices.ComTypes;
using System.Security.Cryptography;
using System.Text;

namespace PangyaAPI.Utilities
{
    [StructLayout(LayoutKind.Sequential)]
    public struct _FILETIME
    {
        public uint dwLowDateTime;
        public uint dwHighDateTime;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct SYSTEMTIME
    {
        public ushort wYear;
        public ushort wMonth;
        public ushort wDayOfWeek;
        public ushort wDay;
        public ushort wHour;
        public ushort wMinute;
        public ushort wSecond;
        public ushort wMilliseconds;
    }

    public static class SocketUtils
    {
        // Definir as constantes para os parâmetros do getsockopt
        private const int SOL_SOCKET = 1;
        private const int SO_CONNECT_TIME = 0x1006; // Código do parâmetro SO_CONNECT_TIME

        // Estrutura para armazenar o tempo de conexão
        [StructLayout(LayoutKind.Sequential)]
        public struct sockaddr
        {
            public short sa_family;
            [MarshalAs(UnmanagedType.ByValArray, SizeConst = 14)]
            public byte[] sa_data;
        }

        // P/Invoke para chamar o getsockopt
        [DllImport("ws2_32.dll", SetLastError = true)]
        private static extern int getsockopt(IntPtr s, int level, int optname, ref int optval, ref int optlen);
        public static (bool check, byte[] _buffer, int len) Read(this TcpClient client)
        {
            if (client.Client.Connected)
                return client.GetStream().Read();
            else
                return (false, new byte[0], 0);
        }
        public static bool Send(this TcpClient client, byte[] buffer, int len = 0)
        {
            try
            {
                // Debug.WriteLine("Send=>" + buffer.HexDump());
                return client.GetStream().Send(buffer, 0, len);
            }
            catch (Exception e)
            {
                throw e;
            }
        }

        public static bool Send(this NetworkStream stream, byte[] buffer, int offset, int len)
        {
            try
            {
                if (stream.CanWrite)
                    stream.Write(buffer, offset, len);

                return (true);
            }
            catch (IOException ioEx)
            {
                Debug.WriteLine($"[Send] Erro de leitura: {ioEx.Message}");
                return (false);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[Send] Erro inesperado: {ex.Message}");
                return (false);
            }
        }
        public static (bool Success, byte[] Buffer, int Length) Read(this NetworkStream stream)
        {
            if (!stream.CanRead)
                Debug.WriteLine($"[Read] Erro de leitura de stream");

            try
            {
                byte[] buffer = new byte[500000];
                int bytesRead = stream.Read(buffer, 0, buffer.Length);

                if (bytesRead == 0)
                {
                    Debug.WriteLine("[Read] Cliente desconectou durante a leitura.");
                    return (false, Array.Empty<byte>(), 0);
                }

                byte[] result = new byte[bytesRead];
                Array.Copy(buffer, result, bytesRead);

                return (true, result, bytesRead);
            }
            catch (IOException ioEx) when (ioEx.InnerException is SocketException sockEx)
            {
                Debug.WriteLine($"[Read] Socket encerrado pelo cliente: {sockEx.SocketErrorCode} - {sockEx.Message}");
                return (false, Array.Empty<byte>(), 0);
            }
            catch (IOException ioEx)
            {
                Debug.WriteLine($"[Read] Erro de leitura de stream: {ioEx.Message}");
                return (false, Array.Empty<byte>(), 0);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[Read] Erro inesperado: {ex.Message}");
                return (false, Array.Empty<byte>(), 0);
            }
        }
        // Função para obter o tempo de conexão
        public static int GetConnectTime(this TcpClient socket)
        {
            // Verifica se o socket é válido
            if (socket == null || !socket.Connected)
            {
                return -1; // _client não conectado
            }

            // Obter o tempo de conexão usando getsockopt
            int seconds = 0;
            int size_seconds = sizeof(int);

            try
            {
                // Chama o getsockopt para obter o tempo de conexão
                int result = getsockopt(socket.Client.Handle, SOL_SOCKET, SO_CONNECT_TIME, ref seconds, ref size_seconds);
                if (result == 0)  // 0 significa sucesso
                {
                    return seconds;  // Retorna o tempo de conexão em segundos
                }
                else
                {
                    return -2;  // Erro ao obter o tempo de conexão
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Erro ao obter o tempo de conexão: {ex.Message}");
                return -2;  // Erro ao obter o tempo de conexão
            }
        }
    }
    /// <summary>
    /// System time structure based on Windows internal SYSTEMTIME struct
    /// </summary>
    [StructLayout(LayoutKind.Sequential, Pack = 4, Size = 16)]
    public class _SYSTEMTIME
    {
        /// <summary>
        /// Year
        /// </summary>
        public ushort Year { get; set; }

        /// <summary>
        /// Month
        /// </summary>
        public ushort Month { get; set; }

        /// <summary>
        /// Day of Week
        /// </summary>
        public ushort DayOfWeek { get; set; }

        /// <summary>
        /// Day
        /// </summary>
        public ushort Day { get; set; }

        /// <summary>
        /// Hour
        /// </summary>
        public ushort Hour { get; set; }

        /// <summary>
        /// Minute
        /// </summary>
        public ushort Minute { get; set; }

        /// <summary>
        /// Second
        /// </summary>
        public ushort Second { get; set; }

        /// <summary>
        /// Millisecond
        /// </summary>
        public ushort MilliSecond { get; set; }

        public bool TimeActive
        {
            get
            {
                return Year > 0 && Month > 0 && DayOfWeek > 0 && Day > 0 && Hour > 0 && Minute > 0 && Second > 0 && MilliSecond > 0;
            }
        }

        public DateTime ConvertIFFToDateTime()
        {
            return new DateTime(Year, Month, Day, Hour, Minute, Second, MilliSecond);
        }
        public _SYSTEMTIME(bool opt = false)
        {
            if (opt)
                ConvertDateTimeToIFF(DateTime.Now);
        }
        public _SYSTEMTIME ConvertDateTimeToIFF(DateTime date)
        {
            Year = (ushort)date.Year;
            Month = (ushort)date.Month;
            DayOfWeek = (ushort)date.DayOfWeek;
            Day = (ushort)date.Day;
            Hour = (ushort)date.Hour;
            Minute = (ushort)date.Minute;
            Second = (ushort)date.Second;
            return this;
        }
    }

    public class Singleton<_ST> where _ST : class, new()
    {
        private static _ST myInstance = default;

        public static _ST getInstance()
        {
            try
            {
                if (myInstance == null)
                    myInstance = new _ST();

                return myInstance;
            }
            catch (Exception e)
            {

                throw e;
            }
        }

        protected Singleton()
        {
        }
    }
    public class UtilTime
    {
        /// <summary>
        /// Retorna o timestamp Unix (em segundos) da hora local atual.
        /// </summary>
        /// <returns>Timestamp Unix como uint.</returns>
        public static uint GetLocalTimeAsUnix()
        {
            DateTime localNow = DateTime.Now;
            DateTimeOffset localOffset = new DateTimeOffset(localNow);
            return (uint)localOffset.ToUnixTimeSeconds();
        }

        public static int TranslateDate(string dateSrc, ref DateTime dateDst)
        {
            if (dateSrc == null)
                throw new ArgumentNullException(nameof(dateSrc));

            if (dateDst == null)
                throw new ArgumentNullException(nameof(dateDst));

            if (string.IsNullOrEmpty(dateSrc))
            {
                dateDst = DateTime.MinValue;
                return 0;
            }

            if (DateTime.TryParseExact(dateSrc, "yyyy-MM-dd HH:mm:ss", null, System.Globalization.DateTimeStyles.None, out dateDst))
                return 0;

            return -1; // Return an appropriate error code if parsing fails
        }

        public static int TranslateTime(string dateSrc, ref DateTime dateDst)
        {
            if (dateSrc == null)
                throw new ArgumentNullException(nameof(dateSrc));

            if (dateDst == null)
                throw new ArgumentNullException(nameof(dateDst));

            if (string.IsNullOrEmpty(dateSrc))
            {
                dateDst = DateTime.MinValue;
                return 0;
            }

            if (DateTime.TryParseExact(dateSrc, "HH:mm:ss.fff", null, System.Globalization.DateTimeStyles.None, out dateDst))
                return 0;

            return -1; // Return an appropriate error code if parsing fails
        }

        // Implement the rest of the methods in a similar fashion
        // ...

        public static long GetTimeDiff(DateTime st1, DateTime st2)
        {
            TimeSpan diff = st1 - st2;
            return diff.Ticks / 10;
        }


        public static long UnixTimeConvert(DateTime? unixtime)
        {
            if (unixtime.HasValue == false || unixtime?.Ticks == 0)
            { return 0; }
            TimeSpan timeSpan = (TimeSpan)(unixtime - new DateTime(1970, 1, 1, 0, 0, 0));
            return (long)timeSpan.TotalSeconds;
        }

        public static long UnixTimeConvert(long unixtime)
        {
            // Converte Unix timestamp (segundos) para DateTime em UTC
            return DateTimeOffset.FromUnixTimeSeconds(unixtime).ToUnixTimeMilliseconds();
        }
        public static long UnixTimeConvert(DateTime dateTime)
        {
            // Garante que o DateTime seja tratado como UTC
            DateTimeOffset offset = dateTime.Kind == DateTimeKind.Unspecified
                ? new DateTimeOffset(DateTime.SpecifyKind(dateTime, DateTimeKind.Local))
                : new DateTimeOffset(dateTime);

            return offset.ToUnixTimeMilliseconds();
        }


        public static long DateTimeToUnixTimestamp(DateTime dateTime)
        {
            return (long)(dateTime.ToUniversalTime() - new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc)).TotalSeconds;
        }

        public static DateTime UnixTimestampToDateTime(long unixTimestamp)
        {
            return new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc).AddSeconds(unixTimestamp).ToLocalTime();
        }

        // The methods for date and time differences can be converted like this:

        public static long GetHourDiff(DateTime st1, DateTime st2)
        {
            TimeSpan diff = st1 - st2;
            return (long)diff.TotalMilliseconds;
        }

        public static bool IsSameDay(DateTime st1, DateTime st2)
        {
            return st1.Date == st2.Date;
        }

        public static bool IsSameDayNow(DateTime st)
        {
            return st.Date == DateTime.Now.Date;
        }

        public static bool IsEmpty(DateTime st)
        {
            return st == DateTime.MinValue;
        }
        public static long GetLocalDateDiff(DateTime st)
        {
            DateTime local = DateTime.Now;
            TimeSpan diff = st.Date - local.Date;
            return diff.Ticks / TimeSpan.TicksPerMillisecond;
        }

        public static long GetLocalDateDiffDESC(DateTime st)
        {
            DateTime local = DateTime.Now;

            // Se st for anterior a local, a diferença será negativa
            TimeSpan diff = st - local;

            // Retorna a diferença em milissegundos
            return diff.Ticks / TimeSpan.TicksPerMillisecond;
        }

        public static long GetSystemDateDiff(DateTime st)
        {
            DateTime system = DateTime.UtcNow;
            TimeSpan diff = st.Date - system.Date;
            return diff.Ticks / TimeSpan.TicksPerMillisecond;
        }

        public static long GetSystemDateDiffDESC(DateTime st)
        {
            DateTime system = DateTime.UtcNow;
            TimeSpan diff = system.Date - st.Date;
            return diff.Ticks / TimeSpan.TicksPerMillisecond;
        }

        public static long TzLocalUnixToUnixUTC(long localUnixTime)
        {
            DateTimeOffset localTime = DateTimeOffset.FromUnixTimeSeconds(localUnixTime);
            DateTimeOffset utcTime = localTime.ToUniversalTime();
            return utcTime.ToUnixTimeSeconds();
        }

        public static string FormatDate(DateTime date)
        {
            return date.ToString("yyyy-MM-dd HH:mm:ss.fff");
        }
        // Função para traduzir data de Unix para SYSTEMTIME (UTC)
        public static int TranslateDateSystem(long timeUnix, out DateTime dateDst)
        {
            if (timeUnix == 0)
            {
                dateDst = DateTime.UtcNow; // Data atual no UTC
            }
            else
            {
                dateDst = DateTimeOffset.FromUnixTimeSeconds(timeUnix).UtcDateTime;
            }

            return 0;
        }

        // Função para traduzir data de Unix para SYSTEMTIME (Local)
        public static int TranslateDateLocal(long timeUnix, out DateTime dateDst)
        {
            if (timeUnix == 0)
            {
                dateDst = DateTime.Now; // Data atual no local da máquina
            }
            else
            {
                dateDst = DateTimeOffset.FromUnixTimeSeconds(timeUnix).LocalDateTime;
            }

            return 0;
        }

        public static string _formatDate(DateTime date)
        {
            return FormatDate(date);
        }
        // Função para formatar hora para string
        public static string FormatTime(DateTime date)
        {
            return date.ToString("HH:mm:ss.fff");
        }

        // Função para formatar data do sistema para string (UTC)
        public static string FormatDateSystem(long timeUnix)
        {
            DateTime date;
            TranslateDateSystem(timeUnix, out date);
            return date.ToString("yyyy-MM-dd HH:mm:ss.fff");
        }

        // Função para formatar data local para string
        public static string FormatDateLocal(long timeUnix)
        {
            DateTime date;
            TranslateDateLocal(timeUnix, out date);
            return date.ToString("yyyy-MM-dd HH:mm:ss.fff");
        }
        public static string formatDateLocal(long timeUnix)
        {
            DateTime date;
            TranslateDateLocal(timeUnix, out date);
            return date.ToString("yyyy-MM-dd HH:mm:ss.fff");
        }
        [DllImport("kernel32.dll", SetLastError = true)]
        public static extern bool SystemTimeToFileTime(ref SYSTEMTIME lpSystemTime, ref _FILETIME lpFileTime);

        // Função para converter System Time para Unix Timestamp
        public static long SystemTimeToUnix(DateTime st)
        {
            return (long)(st.ToUniversalTime() - new DateTime(1970, 1, 1)).TotalSeconds;
        }
        // Função para obter o sistema como Unix Timestamp
        public static long GetSystemTimeAsUnix()
        {
            return SystemTimeToUnix(DateTime.UtcNow);
        }
        [DllImport("kernel32.dll", SetLastError = true)]
        public static extern void GetLocalTime(ref SYSTEMTIME lpSystemTime);

        public static void GetLocalTime(out DateTime time)
        {
            time = DateTime.Now;
        }

        public static DateTime UnixToSystemTime(long unixTime)
        {
            return DateTimeOffset.FromUnixTimeSeconds(unixTime).DateTime;
        }

        public static long GetLocalTimeDiffDESC(DateTime time)
        {
            return _GetLocalTimeDiffDESC(time).Ticks;
        }

        public static TimeSpan _GetLocalTimeDiffDESC(DateTime time)
        {
            if (DateTime.Now >= time)
                return DateTime.Now - time;
            else
                return time - DateTime.Now;
        }

        public static long GetLocalTimeDiff(DateTime dateTime)
        {
            // Cada Tick = 100 nanosegundos = 0.1 microssegundo
            return DateTime.Now.Ticks - dateTime.Ticks;
        }

        public static DateTime UnixUTCToTzLocalTime(long timeUnix)
        {
            // Converte Unix timestamp para DateTime UTC
            DateTime utc = DateTimeOffset.FromUnixTimeSeconds(timeUnix).UtcDateTime;

            // Converte UTC para horário local
            DateTime tzLocal = TimeZoneInfo.ConvertTimeFromUtc(utc, TimeZoneInfo.Local);

            return tzLocal;
        }

        public static long TzLocalTimeToUnixUTC(DateTime dt)
        {
            return new DateTimeOffset(dt).ToUnixTimeSeconds();
        }

    }
    public static class Tools
    {
        public static T IfCompare<T>(bool expression, T trueValue, T falseValue)
        {
            if (expression)
            {
                return trueValue;
            }
            else
            {
                return falseValue;
            }
        }
        public static KeyValuePair<TKey, TValue> insert<TKey, TValue>(this Dictionary<TKey, TValue> pairs, TKey key, TValue value)
        {
            if (pairs.ContainsKey(key))
                pairs[key] = value; // Atualiza o valor se a chave já existir
            else
                pairs.Add(key, value); // Adiciona a chave se não existir

            return new KeyValuePair<TKey, TValue>(key, value);
        }

        public static bool TryAdd<TKey, TValue>(this Dictionary<TKey, TValue> pairs, TKey key, TValue value)
        {
            if (pairs.ContainsKey(key))
                pairs[key] = value; // Atualiza o valor se a chave já existir
            else
                pairs.Add(key, value); // Adiciona a chave se não existir

            return pairs.ContainsKey(key);
        }


        public static KeyValuePair<TKey, TValue> insert<TKey, TValue>(this Dictionary<TKey, TValue> pairs, Tuple<TKey, TValue> tuple)
        {
            if (pairs.ContainsKey(tuple.Item1))
                pairs[tuple.Item1] = tuple.Item2; // Atualiza o valor se a chave já existir
            else
                pairs.Add(tuple.Item1, tuple.Item2); // Adiciona a chave se não existir

            return new KeyValuePair<TKey, TValue>(tuple.Item1, tuple.Item2);
        }

        public static bool empty<TKey, TValue>(this Dictionary<TKey, TValue> pairs)
        {
            return !pairs.Any(); // Retorna true se o dicionário estiver vazio
        }
        public static bool empty<TKey, TValue>(this Dictionary<TKey, List<TValue>> pairs)
        {
            return !pairs.Any(); // Retorna true se o dicionário estiver vazio
        }
        public static KeyValuePair<TKey, TValue> end<TKey, TValue>(this Dictionary<TKey, TValue> pairs)
        {
            try
            {
                if (pairs.Count > 0)
                    return pairs.Last(); // Retorna true se o dicionário estiver vazio
                else
                    return new KeyValuePair<TKey, TValue>();
            }
            catch (Exception ex)
            {
                Debug.WriteLine(Environment.StackTrace);
                throw ex;
            }
        }

        public static KeyValuePair<TKey, TValue> find<TKey, TValue>(this Dictionary<TKey, TValue> pairs, object value)
        {
            if (value is TKey keyValue)
            {
                return pairs.FirstOrDefault(c => EqualityComparer<TKey>.Default.Equals(c.Key, keyValue));
            }
            if (value is TValue val)
            {
                return pairs.FirstOrDefault(c => EqualityComparer<TValue>.Default.Equals(c.Value, val));
            }

            return default; // Retorna o valor padrão (default) se não encontrar
        }



        public static KeyValuePair<TKey, TValue> begin<TKey, TValue>(this Dictionary<TKey, TValue> pairs)
        {
            return pairs.FirstOrDefault(); // Retorna true se o dicionário estiver vazio
        }


        public static T end<T>(this List<T> pairs)
        {
            return pairs.Last(); // Retorna true se o dicionário estiver vazio
        }

        public static T find<T>(this List<T> pairs, T value)
        {
            return pairs.FirstOrDefault(c => EqualityComparer<T>.Default.Equals(c, value));
        }


        public static T begin<T>(this List<T> pairs)
        {
            return pairs.FirstOrDefault(); // Retorna true se o dicionário estiver vazio
        }


        public static bool empty<T>(this List<T> pairs)
        {
            return !pairs.Any(); // Retorna true se o dicionário estiver vazio
        }

        public static IEnumerable<List<T>> Chunk<T>(this IEnumerable<T> source, int size)
        {
            var chunk = new List<T>(size);
            foreach (var item in source)
            {
                chunk.Add(item);
                if (chunk.Count == size)
                {
                    yield return chunk;
                    chunk = new List<T>(size);
                }
            }
            if (chunk.Count > 0)
                yield return chunk;
        }

        public static void ClearArray(this Array array)
        {
            if (array != null)
                Array.Clear(array, 0, array.Length);
        }

        public static int SizeOf<T>(T structure)
        {
            return Marshal.SizeOf((object)structure);
        }

        public static int SizeOf(Type t)
        {
            return Marshal.SizeOf(t);
        }

        public static T reinterpret_cast<T>(object obj) where T : class
        {
            return obj as T;
        }

        public static byte[] StructArrayToByteArray<T>(T[] array) where T : struct
        {
            int size = Marshal.SizeOf<T>() * array.Length;
            byte[] buffer = new byte[size];
            IntPtr ptr = Marshal.AllocHGlobal(size);

            try
            {
                for (int i = 0; i < array.Length; i++)
                {
                    Marshal.StructureToPtr(array[i], ptr + i * Marshal.SizeOf<T>(), false);
                }
                Marshal.Copy(ptr, buffer, 0, size);
            }
            finally
            {
                Marshal.FreeHGlobal(ptr);
            }

            return buffer;
        }

        public static T memcpy<T>(byte[] buffer, int offset = 0) where T : class
        {
            int size = Marshal.SizeOf<T>();

            if (buffer == null || buffer.Length < offset + size)
                throw new ArgumentException("Buffer inválido ou muito pequeno para o tipo especificado.");

            byte[] raw = new byte[size];
            Buffer.BlockCopy(buffer, offset, raw, 0, size);

            GCHandle handle = GCHandle.Alloc(raw, GCHandleType.Pinned);
            try
            {
                return Marshal.PtrToStructure<T>(handle.AddrOfPinnedObject());
            }
            finally
            {
                handle.Free();
            }
        }

        public static ushort[] CopyShortToUShort(short[] source)
        {
            // Converte o short[] para ushort[]
            ushort[] dest = new ushort[source.Length];
            for (int i = 0; i < source.Length; i++)
            {
                dest[i] = (ushort)source[i];
            }
            return dest;
        }

        public static void Shuffle<T>(List<T> list)
        {
            Random rng = new Random((int)DateTime.Now.Ticks);
            int n = list.Count;
            while (n > 1)
            {
                n--;
                int k = rng.Next(n + 1);
                T value = list[k];
                list[k] = list[n];
                list[n] = value;
            }
        }
        public static byte[] Slice(this byte[] source, uint startIndex)
        {
            byte[] result = new byte[source.Length - startIndex];
            Buffer.BlockCopy(source, (int)startIndex, result, 0, (int)(source.Length - startIndex));
            return result;
        }

        public static byte[] memcpy(this byte[] source, int len)
        {
            byte[] result = new byte[len];

            Buffer.BlockCopy(source, 0, result, 0, len);
            return result;
        }

        public static void BlockCpy(this byte[] source, byte[] dest, int index)
        {
            for (int i = 0; i < index; i++)
            {
                dest[index + i] = source[i];
            }
        }

        public static void BlockCpy(this byte[] source, byte[] dest, int index, int len)
        {
            for (int i = 0; i < len; i++)
            {
                dest[i] = source[i];
            }
        }

        public static int SizeOf<T>()
        {
            return Marshal.SizeOf(typeof(T));
        }
        public static string GetString(this byte[] array)
        {
            return Encoding.GetEncoding("Shift_JIS").GetString(array).TrimEnd('\0');
        }

        public static void SetString(this byte[] array, string value)
        {
            ClearArray(array);
            var bytes = Encoding.GetEncoding("Shift_JIS").GetBytes(value ?? string.Empty);
            Array.Copy(bytes, array, Math.Min(bytes.Length, array.Length));
        }

        public static T ToObject<T>(this DataRow dataRow)
     where T : new()
        {
            T item = new T();
            foreach (DataColumn column in dataRow.Table.Columns)
            {
                if (dataRow[column] != DBNull.Value)
                {
                    PropertyInfo prop = item.GetType().GetProperty(column.ColumnName);
                    if (prop != null)
                    {
                        object result = Convert.ChangeType(dataRow[column], prop.PropertyType);
                        prop.SetValue(item, result, null);
                        continue;
                    }
                    else
                    {
                        FieldInfo fld = item.GetType().GetField(column.ColumnName);
                        if (fld != null)
                        {
                            object result = Convert.ChangeType(dataRow[column], fld.FieldType);
                            fld.SetValue(item, result);
                        }
                    }
                }
            }
            return item;
        }
        /// <summary>
        /// return size 
        /// </summary>
        /// <typeparam name="T">name struct</typeparam>
        /// <param name="structure">for struct</param>
        /// <returns></returns>
        public static int SizeOfS<T>(this T structure)
        {
            return Marshal.SizeOf((object)structure);
        }
        /// <summary>
        /// return size
        /// </summary>
        /// <param name="structure">class</param>
        /// <returns></returns>
        public static int SizeOfC(this Type structure)
        {
            return Marshal.SizeOf(structure);
        }



        public static bool empty(this string a)
        {
            return string.IsNullOrEmpty(a);
        }

        public static int size(this string a)
        {
            return a.Length;
        }
        public static bool IsTrue<T>(this T a)
        {
            return Convert.ToBoolean(a);
        }

        public static T[] InitializeWithDefaultInstances<T>(uint length) where T : class, new()
        {
            T[] array = new T[length];
            for (int i = 0; i < length; i++)
            {
                array[i] = new T();
            }
            return array;
        }

        public static string[] InitializeStringArrayWithDefaultInstances(int length)
        {
            string[] array = new string[length];
            for (int i = 0; i < length; i++)
            {
                array[i] = "";
            }
            return array;
        }

        public static T[] PadWithNull<T>(int length, T[] existingItems) where T : class
        {
            if (length > existingItems.Length)
            {
                T[] array = new T[length];

                for (int i = 0; i < existingItems.Length; i++)
                {
                    array[i] = existingItems[i];
                }

                return array;
            }
            else
                return existingItems;
        }

        public static T[] PadValueTypeArrayWithDefaultInstances<T>(int length, T[] existingItems) where T : struct
        {
            if (length > existingItems.Length)
            {
                T[] array = new T[length];

                for (int i = 0; i < existingItems.Length; i++)
                {
                    array[i] = existingItems[i];
                }

                return array;
            }
            else
                return existingItems;
        }

        public static T[] PadReferenceTypeArrayWithDefaultInstances<T>(int length, T[] existingItems) where T : class, new()
        {
            if (length > existingItems.Length)
            {
                T[] array = new T[length];

                for (int i = 0; i < existingItems.Length; i++)
                {
                    array[i] = existingItems[i];
                }

                for (int i = existingItems.Length; i < length; i++)
                {
                    array[i] = new T();
                }

                return array;
            }
            else
                return existingItems;
        }

        public static string[] PadStringArrayWithDefaultInstances(int length, string[] existingItems)
        {
            if (length > existingItems.Length)
            {
                string[] array = new string[length];

                for (int i = 0; i < existingItems.Length; i++)
                {
                    array[i] = existingItems[i];
                }

                for (int i = existingItems.Length; i < length; i++)
                {
                    array[i] = "";
                }

                return array;
            }
            else
                return existingItems;
        }

        public static void DeleteArray<T>(T[] array) where T : System.IDisposable
        {
            foreach (T element in array)
            {
                if (element != null)
                    element.Dispose();
            }
        }
        public static string MD5Hash(this string text)
        {
            if (text.Length >= 32)
            {
                new Exception("input text is MD5 Hash");
            }
            MD5 md5 = new MD5CryptoServiceProvider();

            //compute hash from the bytes of text  
            md5.ComputeHash(Encoding.ASCII.GetBytes(text));

            //get hash result after compute it  
            byte[] result = md5.Hash;

            StringBuilder strBuilder = new StringBuilder();
            for (int i = 0; i < result.Length; i++)
            {
                //change it into 2 hexadecimal digits  
                //for each byte  
                strBuilder.Append(result[i].ToString("x2"));
            }

            return strBuilder.ToString().ToUpper();
        }

        public static int[] ProcuraNoArquivo(this byte[] bytes, string procurar)
        {
            int[] position = new int[2];
            for (int i = 0; i < bytes.Length; i++)
            {
                //Lê string na posição
                var stringPosicao = Encoding.UTF8.GetString(bytes, i, procurar.Length);

                if (procurar == stringPosicao)
                    position.ToList().Add(i); //Posição encontrada;
            }
            return position;
        }
        public static string StringFormat(string Format, object[] Args)
        {
            return string.Format(Format, Args);
        }


        public static string GetMethodName(MethodBase methodBase)
        {
            string str = methodBase.Name + "(";
            foreach (ParameterInfo info in methodBase.GetParameters())
            {
                string[] textArray1 = new string[] { str, info.ParameterType.Name, " ", info.Name, ", " };
                str = string.Concat(textArray1);
            }
            return str.Remove(str.Length - 2) + ")";
        }
        /// <summary>
        /// Divide uma lista em várias listas
        /// </summary>
        public static List<List<T>> Split<T>(this List<T> source, int tamanhoPorLista)
        {
            return source
                .Select((x, i) => new { Index = i, Value = x })
                .GroupBy(x => x.Index / tamanhoPorLista)
                .Select(x => x.Select(v => v.Value).ToList())
                .ToList();
        }

        public static void PrintError(MethodBase methodBase, string msg)
        {
            string[] textArray1 = new string[] { "[", methodBase.DeclaringType.ToString(), "::", GetMethodName(methodBase), "]" };
            Console.WriteLine(string.Concat(textArray1));
            Console.WriteLine("Error : " + msg);
        }
        public static int Checksum(string dataToCalculate)
        {
            byte[] byteToCalculate = Encoding.ASCII.GetBytes(dataToCalculate);
            int checksum = 0;
            foreach (byte chData in byteToCalculate)
            {
                checksum += chData;
            }
            checksum &= 0xff;
            return checksum;
        }
        public static void DebugDump(this byte[] bytes)
        {
            System.Diagnostics.Debug.WriteLine(Environment.NewLine);
            System.Diagnostics.Debug.WriteLine("Debug: " + bytes.HexDump());
            System.Diagnostics.Debug.WriteLine(Environment.NewLine);
        }
        // this method from https://www.codeproject.com/Articles/36747/Quick-and-Dirty-HexDump-of-a-Byte-Array
        public static string HexDump(this byte[] bytes, int bytesPerLine = 16)
        {
            if (bytes == null) return "<null>";
            var bytesLength = bytes.Length;

            var HexChars = "0123456789ABCDEF".ToCharArray();

            var firstHexColumn =
                8 // 8 characters for the address
                + 3; // 3 spaces

            var firstCharColumn = firstHexColumn
                                  + bytesPerLine * 3 // - 2 digit for the hexadecimal value and 1 space
                                  + (bytesPerLine - 1) / 8 // - 1 extra space every 8 characters from the 9th
                                  + 2; // 2 spaces 

            var lineLength = firstCharColumn
                             + bytesPerLine // - characters to show the ascii value
                             + Environment.NewLine.Length; // Carriage return and line feed (should normally be 2)

            var line = (new string(' ', lineLength - Environment.NewLine.Length) + Environment.NewLine).ToCharArray();
            var expectedLines = (bytesLength + bytesPerLine - 1) / bytesPerLine;
            var result = new StringBuilder(expectedLines * lineLength);

            for (var i = 0; i < bytesLength; i += bytesPerLine)
            {
                line[0] = HexChars[(i >> 28) & 0xF];
                line[1] = HexChars[(i >> 24) & 0xF];
                line[2] = HexChars[(i >> 20) & 0xF];
                line[3] = HexChars[(i >> 16) & 0xF];
                line[4] = HexChars[(i >> 12) & 0xF];
                line[5] = HexChars[(i >> 8) & 0xF];
                line[6] = HexChars[(i >> 4) & 0xF];
                line[7] = HexChars[(i >> 0) & 0xF];

                var hexColumn = firstHexColumn;
                var charColumn = firstCharColumn;

                for (var j = 0; j < bytesPerLine; j++)
                {
                    if (j > 0 && (j & 7) == 0) hexColumn++;
                    if (i + j >= bytesLength)
                    {
                        line[hexColumn] = ' ';
                        line[hexColumn + 1] = ' ';
                        line[charColumn] = ' ';
                    }
                    else
                    {
                        var b = bytes[i + j];
                        line[hexColumn] = HexChars[(b >> 4) & 0xF];
                        line[hexColumn + 1] = HexChars[b & 0xF];
                        line[charColumn] = b < 32 ? '·' : (char)b;
                    }

                    hexColumn += 3;
                    charColumn++;
                }

                result.Append(line);
            }

            return result.ToString();
        }

        public static string getBetween(string strSource, string strStart, string strEnd)
        {
            int Start, End;
            if (strSource.Contains(strStart) && strSource.Contains(strEnd))
            {
                Start = strSource.IndexOf(strStart, 0) + strStart.Length;
                End = strSource.IndexOf(strEnd, Start);
                return strSource.Substring(Start, End - Start);
            }
            else
            {
                return "";
            }
        }
        public static string ObjectToString(this object values)
        {
            string data = "";
            foreach (var propertyInfo in values.GetType().GetProperties())
            {
                var propertyName = propertyInfo.Name;
                var propertyValue = propertyInfo.GetValue(values);
                data += ($"{propertyName}={propertyValue}");
                data += "\n";
            }
            return data;
        }
        private static string TranslateClient(string input, string languagePair)
        {
            string url = String.Format("http://www.google.com/translate_t?hl=en&ie=UTF8&text={0}&langpair={1}", input, languagePair);

            string result = String.Empty;

            using (WebClient webClient = new WebClient())
            {
                webClient.Encoding = Encoding.UTF8;
                result = webClient.DownloadString(url);
            }
            string BeginText = "<span id=result_box class=\"short_text\"><span title=\"" + input + "\" onmouseover=\"this.style.backgroundColor = '#ebeff9'\" onmouseout=\"this.style.backgroundColor = '#fff'\">";

            string text = getBetween(result, "<span id=result_box class=\"short_text\">", "</span>");
            return text;
        }

        public static void ReflectorClass(object obj)
        {
            var stringname = "";
            PropertyInfo[] properties = obj.GetType().GetProperties();
            foreach (var propertyInfo in properties)
            {
                stringname += "\n" + propertyInfo.Name + " = " + propertyInfo.GetValue(obj, null);
                //System.Diagnostics.Debug.WriteLine(propertyInfo.Name +" = " + propertyInfo.GetValue(obj, null));
                //                stringname = "";
            }
            System.IO.File.WriteAllText(System.IO.Directory.GetCurrentDirectory() + "\\card.txt", stringname);

        }

        public static string ByteArrayToString(byte[] ba)
        {
            StringBuilder stringBuilder = new StringBuilder(checked(ba.Length * 2));
            foreach (byte b in ba)
            {
                stringBuilder.AppendFormat("{0:x2}", b);
            }
            return stringBuilder.ToString();
        }

        public static string ByteToString(byte ba)
        {
            StringBuilder stringBuilder = new StringBuilder(2);
            stringBuilder.AppendFormat("{0:x2}", ba);
            return stringBuilder.ToString();
        }

        public static byte[] c_str(this string _string)
        {
            return Encoding.UTF7.GetBytes(_string);
        }


        public static List<string> lerArquivo(string arquivo, ref byte[] Inicio, ref long Qtd, int totalB)
        {
            byte[] array = File.ReadAllBytes(arquivo);
            List<byte> list = new List<byte>();
            List<string> list2 = new List<string>();
            checked
            {
                int num = array.Length - 1;
                int num2 = 0;
                while (true)
                {
                    int num3 = num2;
                    int num4 = num;
                    if (num3 > num4)
                    {
                        break;
                    }
                    if (num2 < 8)
                    {
                        list.Add(array[num2]);
                    }
                    list2.Add(ByteToString(array[num2]));
                    num2++;
                }
                Inicio = list.ToArray();
                string value = ByteToString(list[3]) + ByteToString(list[2]) + ByteToString(list[1]) + ByteToString(list[0]);
                Qtd = Convert.ToInt32(value, 16);
                if (verificarEstrutura(array.Length, (int)Qtd, totalB))
                {
                    return list2;
                }
                return list2;
            }
        }

        public static object lerArquivoCauldron(string arquivo, ref byte[] Inicio, ref long Qtd, int totalB)
        {
            byte[] array = File.ReadAllBytes(arquivo);
            List<byte> list = new List<byte>();
            List<string> list2 = new List<string>();
            checked
            {
                int num = array.Length - 1;
                int num2 = 0;
                while (true)
                {
                    int num3 = num2;
                    int num4 = num;
                    if (num3 > num4)
                    {
                        break;
                    }
                    if (num2 < 8)
                    {
                        list.Add(array[num2]);
                    }
                    list2.Add(ByteToString(array[num2]));
                    num2++;
                }
                Inicio = list.ToArray();
                string value = ByteToString(list[1]) + ByteToString(list[0]);
                Qtd = Convert.ToInt32(value, 16);
                if (verificarEstrutura(array.Length, (int)Qtd, totalB))
                {
                    return list2;
                }
                return false;
            }
        }

        public static bool verificarEstrutura(int bytes, int qtd, int total)
        {
            checked
            {
                double number = (double)(bytes + 8) / (double)total;
                if ((Convert.ToInt32(number) < (double)(qtd + 100)) & (Convert.ToInt32(number) > (double)(qtd - 100)))
                {
                    return true;
                }
                return false;
            }
        }

        public static object StringToByte(string Str)
        {
            ASCIIEncoding aSCIIEncoding = new ASCIIEncoding();
            return aSCIIEncoding.GetBytes(Str);
        }


        public static List<List<string>> dividirArquivo(List<string> Lista, int tamanho)
        {
            List<List<string>> list = new List<List<string>>();
            List<string> list2 = new List<string>();
            new List<byte>();
            int num = 0;
            int num2 = 0;
            checked
            {
                int num3 = Lista.Count - 1;
                int num4 = 0;
                while (true)
                {
                    int num5 = num4;
                    int num6 = num3;
                    if (num5 > num6)
                    {
                        break;
                    }
                    if (num4 >= 8)
                    {
                        if (num < tamanho - 1)
                        {
                            num++;
                        }
                        else
                        {
                            num2++;
                            num = 0;
                        }
                        list2.Add(Lista[num4]);
                        if (unchecked(num == 0 && num2 > 0))
                        {
                            list.Add(list2);
                            list2 = new List<string>();
                        }
                    }
                    num4++;
                }
                return list;
            }
        }

    }
}
