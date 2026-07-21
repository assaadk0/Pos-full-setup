using System;
using System.IO;
using System.IO.Ports;
using System.IO.Pipes;
using System.Text;
using System.Threading;
using System.Collections.Generic;
using System.ServiceProcess;
using System.Runtime.InteropServices;

namespace Serial2KeyboardServiceCS
{
    // =========================================================================
    // LOGGER HELPER
    // =========================================================================
    public static class Logger
    {
        private static readonly object LogLock = new object();
        private static string LogPath;

        public static void Initialize(string exeDir)
        {
            LogPath = Path.Combine(exeDir, "service.log");
        }

        public static void Log(string message)
        {
            lock (LogLock)
            {
                try
                {
                    string line = string.Format("{0:yyyy-MM-dd HH:mm:ss} - {1}{2}", DateTime.Now, message, Environment.NewLine);
                    File.AppendAllText(LogPath, line);
                }
                catch
                {
                    // Fail silently to avoid crashing the service
                }
            }
        }
    }

    // =========================================================================
    // KEYBOARD SIMULATOR (P/Invoke SendInput)
    // =========================================================================
    public static class KeyboardSimulator
    {
        [StructLayout(LayoutKind.Sequential)]
        private struct INPUT
        {
            public uint type;
            public KEYBDINPUT ki;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct KEYBDINPUT
        {
            public ushort wVk;
            public ushort wScan;
            public uint dwFlags;
            public uint time;
            public IntPtr dwExtraInfo;
        }

        private const int INPUT_KEYBOARD = 1;
        private const uint KEYEVENTF_KEYUP = 0x0002;
        private const uint KEYEVENTF_UNICODE = 0x0004;
        private const ushort VK_RETURN = 0x0D;

        [DllImport("user32.dll", SetLastError = true)]
        private static extern uint SendInput(uint nInputs, INPUT[] pInputs, int cbSize);

        public static void SendString(string text)
        {
            if (string.IsNullOrEmpty(text)) return;

            foreach (char c in text)
            {
                INPUT[] inputs = new INPUT[2];

                // Key Down
                inputs[0].type = INPUT_KEYBOARD;
                inputs[0].ki.wVk = 0;
                inputs[0].ki.wScan = c;
                inputs[0].ki.dwFlags = KEYEVENTF_UNICODE;
                inputs[0].ki.time = 0;
                inputs[0].ki.dwExtraInfo = IntPtr.Zero;

                // Key Up
                inputs[1].type = INPUT_KEYBOARD;
                inputs[1].ki.wVk = 0;
                inputs[1].ki.wScan = c;
                inputs[1].ki.dwFlags = KEYEVENTF_UNICODE | KEYEVENTF_KEYUP;
                inputs[1].ki.time = 0;
                inputs[1].ki.dwExtraInfo = IntPtr.Zero;

                SendInput(2, inputs, Marshal.SizeOf(typeof(INPUT)));
                Thread.Sleep(5); // Small delay to prevent keys arriving out of order
            }
        }

        public static void SendEnter()
        {
            INPUT[] inputs = new INPUT[2];

            // Enter Down
            inputs[0].type = INPUT_KEYBOARD;
            inputs[0].ki.wVk = VK_RETURN;
            inputs[0].ki.wScan = 0;
            inputs[0].ki.dwFlags = 0;
            inputs[0].ki.time = 0;
            inputs[0].ki.dwExtraInfo = IntPtr.Zero;

            // Enter Up
            inputs[1].type = INPUT_KEYBOARD;
            inputs[1].ki.wVk = VK_RETURN;
            inputs[1].ki.wScan = 0;
            inputs[1].ki.dwFlags = KEYEVENTF_KEYUP;
            inputs[1].ki.time = 0;
            inputs[1].ki.dwExtraInfo = IntPtr.Zero;

            SendInput(2, inputs, Marshal.SizeOf(typeof(INPUT)));
        }
    }

    // =========================================================================
    // NAMED PIPE SERVER (Session 0)
    // =========================================================================
    public class PipeServer
    {
        private NamedPipeServerStream serverStream;
        private readonly object streamLock = new object();
        private volatile bool isConnected = false;
        private volatile bool running = true;

        public bool IsConnected
        {
            get { return isConnected; }
        }

        public void Stop()
        {
            running = false;
            try
            {
                if (serverStream != null)
                {
                    serverStream.Close();
                }
            }
            catch
            {
                // Fail silently
            }
        }

        public void Loop()
        {
            while (running)
            {
                try
                {
                    serverStream = new NamedPipeServerStream(
                        "Serial2KeyboardPipe",
                        PipeDirection.Out,
                        1,
                        PipeTransmissionMode.Byte,
                        PipeOptions.None
                    );

                    Logger.Log("Named Pipe Server waiting for connection...");
                    serverStream.WaitForConnection();
                    Logger.Log("Named Pipe Client connected.");
                    isConnected = true;

                    while (running && serverStream.IsConnected)
                    {
                        Thread.Sleep(200);
                    }
                }
                catch (Exception ex)
                {
                    if (running)
                    {
                        Logger.Log(string.Format("PipeServer error: {0}", ex.Message));
                    }
                }
                finally
                {
                    isConnected = false;
                    if (serverStream != null)
                    {
                        try { serverStream.Close(); } catch {}
                        serverStream = null;
                    }
                }
                Thread.Sleep(1000);
            }
        }

        public void Send(string data)
        {
            lock (streamLock)
            {
                if (isConnected && serverStream != null && serverStream.IsConnected)
                {
                    try
                    {
                        byte[] bytes = Encoding.UTF8.GetBytes(data + "\n");
                        serverStream.Write(bytes, 0, bytes.Length);
                        serverStream.Flush();
                        Logger.Log(string.Format("Broadcasted over pipe: {0}", data));
                    }
                    catch (Exception ex)
                    {
                        Logger.Log(string.Format("Failed to write to pipe: {0}", ex.Message));
                    }
                }
                else
                {
                    Logger.Log(string.Format("Dropped data (No pipe client connected): {0}", data));
                }
            }
        }
    }

    // =========================================================================
    // NAMED PIPE CLIENT (User Session Agent)
    // =========================================================================
    public static class PipeClient
    {
        public static void RunAgent()
        {
            Logger.Log("Agent started. Attempting to connect to Pipe Server...");
            while (true)
            {
                try
                {
                    using (NamedPipeClientStream clientStream = new NamedPipeClientStream(".", "Serial2KeyboardPipe", PipeDirection.In))
                    {
                        clientStream.Connect(5000);
                        Logger.Log("Connected to Pipe Server.");
                        using (StreamReader reader = new StreamReader(clientStream, Encoding.UTF8))
                        {
                            string line;
                            while ((line = reader.ReadLine()) != null)
                            {
                                line = line.Trim();
                                if (!string.IsNullOrEmpty(line))
                                {
                                    Logger.Log(string.Format("Agent received: {0}. Simulating typing.", line));
                                    KeyboardSimulator.SendString(line);
                                    KeyboardSimulator.SendEnter();
                                }
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    Logger.Log(string.Format("Agent error/disconnection: {0}. Retrying in 2 seconds...", ex.Message));
                }
                Thread.Sleep(2000);
            }
        }
    }

    // =========================================================================
    // MULTI-PORT SERIAL MONITOR
    // =========================================================================
    public class SerialMonitor
    {
        private class PortReader
        {
            public string PortName { get; private set; }
            public int BaudRate { get; private set; }
            public Action<string> OnLineReceived { get; private set; }
            public volatile bool StopRequested = false;
            private Thread thread;
            private SerialPort port;

            public PortReader(string portName, int baudRate, Action<string> onLineReceived)
            {
                PortName = portName;
                BaudRate = baudRate;
                OnLineReceived = onLineReceived;
            }

            public void Start()
            {
                thread = new Thread(Run);
                thread.IsBackground = true;
                thread.Name = string.Format("Reader-{0}", PortName);
                thread.Start();
            }

            public void Stop()
            {
                StopRequested = true;
                if (port != null && port.IsOpen)
                {
                    try { port.Close(); } catch {}
                }
                if (thread != null)
                {
                    thread.Join(1000);
                }
            }

            private void Run()
            {
                Logger.Log(string.Format("Reader thread started for {0}.", PortName));
                StringBuilder buffer = new StringBuilder();
                while (!StopRequested)
                {
                    try
                    {
                        port = new SerialPort(PortName, BaudRate);
                        port.ReadTimeout = 500;
                        port.DtrEnable = true;
                        port.RtsEnable = true;
                        port.Open();
                        Logger.Log(string.Format("Successfully opened serial port {0} at {1} baud with DTR/RTS enabled.", PortName, BaudRate));

                        while (!StopRequested && port.IsOpen)
                        {
                            try
                            {
                                if (port.BytesToRead > 0)
                                {
                                    string data = port.ReadExisting();
                                    foreach (char c in data)
                                    {
                                        if (c == '\r')
                                        {
                                            string line = buffer.ToString().Trim();
                                            buffer.Length = 0; // Clear buffer
                                            if (!string.IsNullOrEmpty(line))
                                            {
                                                OnLineReceived(line);
                                            }
                                        }
                                        else if (c != '\n')
                                        {
                                            buffer.Append(c);
                                        }
                                    }
                                }
                                else
                                {
                                    Thread.Sleep(50);
                                }
                            }
                            catch (System.TimeoutException)
                            {
                                // Expected read timeout, just loop again
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        Logger.Log(string.Format("Error on port {0}: {1}", PortName, ex.Message));
                        break;
                    }
                    finally
                    {
                        if (port != null)
                        {
                            try { port.Close(); } catch {}
                            port.Dispose();
                            port = null;
                        }
                    }
                    Thread.Sleep(1000);
                }
                Logger.Log(string.Format("Reader thread exited for {0}.", PortName));
            }
        }

        private readonly int baudRate;
        private readonly Action<string> onLineReceived;
        private readonly Dictionary<string, PortReader> activeReaders = new Dictionary<string, PortReader>();
        private readonly object readersLock = new object();
        private volatile bool running = true;

        public SerialMonitor(int baudRate, Action<string> onLineReceived)
        {
            this.baudRate = baudRate;
            this.onLineReceived = onLineReceived;
        }

        public void Start()
        {
            Thread t = new Thread(MonitorLoop);
            t.IsBackground = true;
            t.Name = "SerialMonitorLoop";
            t.Start();
        }

        public void Stop()
        {
            running = false;
            lock (readersLock)
            {
                foreach (PortReader reader in activeReaders.Values)
                {
                    reader.Stop();
                }
                activeReaders.Clear();
            }
        }

        private void MonitorLoop()
        {
            Logger.Log("Serial Monitor loop started.");
            while (running)
            {
                try
                {
                    string[] ports = SerialPort.GetPortNames();
                    lock (readersLock)
                    {
                        // Clean up disconnected ports
                        List<string> toRemove = new List<string>();
                        foreach (string port in activeReaders.Keys)
                        {
                            if (Array.IndexOf(ports, port) < 0 || activeReaders[port].StopRequested)
                            {
                                toRemove.Add(port);
                            }
                        }

                        foreach (string port in toRemove)
                        {
                            Logger.Log(string.Format("Port {0} is no longer available. Stopping reader.", port));
                            activeReaders[port].Stop();
                            activeReaders.Remove(port);
                        }

                        // Add new ports
                        foreach (string port in ports)
                        {
                            if (!activeReaders.ContainsKey(port))
                            {
                                Logger.Log(string.Format("New serial port detected: {0}. Launching reader.", port));
                                PortReader reader = new PortReader(port, baudRate, onLineReceived);
                                activeReaders.Add(port, reader);
                                reader.Start();
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    Logger.Log(string.Format("Error in SerialMonitor loop: {0}", ex.Message));
                }
                Thread.Sleep(2000);
            }
            Logger.Log("Serial Monitor loop exited.");
        }
    }

    // =========================================================================
    // WINDOWS NATIVE SERVICE IMPLEMENTATION
    // =========================================================================
    public class MyService : ServiceBase
    {
        private PipeServer pipeServer;
        private SerialMonitor serialMonitor;
        private Thread agentCheckThread;
        private volatile bool running = false;

        // P/Invoke definitions for spawning agent in user session
        [StructLayout(LayoutKind.Sequential)]
        private struct STARTUPINFO
        {
            public int cb;
            public string lpReserved;
            public string lpDesktop;
            public string lpTitle;
            public int dwX;
            public int dwY;
            public int dwXSize;
            public int dwYSize;
            public int dwXCountChars;
            public int dwYCountChars;
            public int dwFillAttribute;
            public int dwFlags;
            public short wShowWindow;
            public short cbReserved2;
            public IntPtr lpReserved2;
            public IntPtr hStdInput;
            public IntPtr hStdOutput;
            public IntPtr hStdError;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct PROCESS_INFORMATION
        {
            public IntPtr hProcess;
            public IntPtr hThread;
            public int dwProcessId;
            public int dwThreadId;
        }

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern bool CloseHandle(IntPtr hObject);

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern uint WTSGetActiveConsoleSessionId();

        [DllImport("wtsapi32.dll", SetLastError = true)]
        private static extern bool WTSQueryUserToken(uint SessionId, out IntPtr phToken);

        [DllImport("advapi32.dll", SetLastError = true, CharSet = CharSet.Auto)]
        private static extern bool CreateProcessAsUser(
            IntPtr hToken,
            string lpApplicationName,
            string lpCommandLine,
            IntPtr lpProcessAttributes,
            IntPtr lpThreadAttributes,
            bool bInheritHandles,
            uint dwCreationFlags,
            IntPtr lpEnvironment,
            string lpCurrentDirectory,
            ref STARTUPINFO lpStartupInfo,
            out PROCESS_INFORMATION lpProcessInformation
        );

        [DllImport("advapi32.dll", SetLastError = true)]
        private static extern bool DuplicateTokenEx(
            IntPtr hExistingToken,
            uint dwDesiredAccess,
            IntPtr lpTokenAttributes,
            int ImpersonationLevel,
            int TokenType,
            out IntPtr phNewToken
        );

        [DllImport("userenv.dll", SetLastError = true, CharSet = CharSet.Auto)]
        private static extern bool CreateEnvironmentBlock(
            out IntPtr lpEnvironment,
            IntPtr hToken,
            bool bInherit
        );

        [DllImport("userenv.dll", SetLastError = true)]
        private static extern bool DestroyEnvironmentBlock(IntPtr lpEnvironment);

        public MyService()
        {
            ServiceName = "Serial2KeyboardService";
        }

        protected override void OnStart(string[] args)
        {
            Logger.Log("Service is starting...");
            running = true;

            // Start Named Pipe Server
            pipeServer = new PipeServer();
            Thread pipeThread = new Thread(pipeServer.Loop);
            pipeThread.IsBackground = true;
            pipeThread.Name = "PipeServerLoop";
            pipeThread.Start();

            // Load Baud Rate from config.ini
            int baudRate = GetBaudRate();

            // Start Serial Port Monitor
            serialMonitor = new SerialMonitor(baudRate, delegate(string line) {
                pipeServer.Send(line);
            });
            serialMonitor.Start();

            // Start Agent Checker Thread
            agentCheckThread = new Thread(AgentCheckLoop);
            agentCheckThread.IsBackground = true;
            agentCheckThread.Name = "AgentChecker";
            agentCheckThread.Start();

            Logger.Log("Service started successfully.");
        }

        protected override void OnStop()
        {
            Logger.Log("Service is stopping...");
            running = false;

            if (serialMonitor != null) serialMonitor.Stop();
            if (pipeServer != null) pipeServer.Stop();
            if (agentCheckThread != null) agentCheckThread.Join(1000);

            Logger.Log("Service stopped successfully.");
        }

        private int GetBaudRate()
        {
            string exeDir = Path.GetDirectoryName(typeof(MyService).Assembly.Location);
            string configPath = Path.Combine(exeDir, "config.ini");
            if (File.Exists(configPath))
            {
                try
                {
                    string[] lines = File.ReadAllLines(configPath);
                    foreach (string line in lines)
                    {
                        if (line.StartsWith("BaudRate", StringComparison.OrdinalIgnoreCase))
                        {
                            string[] parts = line.Split('=');
                            if (parts.Length == 2)
                            {
                                int val;
                                if (int.TryParse(parts[1].Trim(), out val))
                                {
                                    return val;
                                }
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    Logger.Log(string.Format("Error reading config.ini: {0}. Using default 9600.", ex.Message));
                }
            }
            return 9600; // Default
        }

        private void AgentCheckLoop()
        {
            string currentExe = typeof(MyService).Assembly.Location;
            Logger.Log(string.Format("Agent checker started. Monitoring agent executable: {0}", currentExe));

            while (running)
            {
                try
                {
                    // If no agent client is connected to our named pipe, try to launch it
                    if (!pipeServer.IsConnected)
                    {
                        uint sessionId = WTSGetActiveConsoleSessionId();
                        // 0xFFFFFFFF represents INVALID_SESSION_ID
                        if (sessionId != 0xFFFFFFFF)
                        {
                            IntPtr userToken = IntPtr.Zero;
                            if (WTSQueryUserToken(sessionId, out userToken))
                            {
                                Logger.Log(string.Format("User token active for session {0}. Spawning user-session agent...", sessionId));
                                bool success = LaunchAgentInUserSession(userToken, currentExe);
                                CloseHandle(userToken);

                                if (success)
                                {
                                    Logger.Log("Agent launch API succeeded.");
                                }
                                else
                                {
                                    Logger.Log("Agent launch API failed.");
                                }
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    Logger.Log(string.Format("Error in AgentCheckLoop: {0}", ex.Message));
                }
                Thread.Sleep(5000);
            }
        }

        private bool LaunchAgentInUserSession(IntPtr userToken, string exePath)
        {
            IntPtr primaryToken = IntPtr.Zero;
            IntPtr envBlock = IntPtr.Zero;
            try
            {
                // 1. Duplicate the token to a primary token
                // TOKEN_ALL_ACCESS = 0xF01FF, SecurityImpersonation = 2, TokenPrimary = 1
                if (!DuplicateTokenEx(userToken, 0xF01FF, IntPtr.Zero, 2, 1, out primaryToken))
                {
                    Logger.Log(string.Format("DuplicateTokenEx failed. Error: {0}", Marshal.GetLastWin32Error()));
                    return false;
                }

                // 2. Create the environment block for the user
                if (!CreateEnvironmentBlock(out envBlock, primaryToken, false))
                {
                    Logger.Log(string.Format("CreateEnvironmentBlock failed. Error: {0}", Marshal.GetLastWin32Error()));
                    envBlock = IntPtr.Zero;
                }

                STARTUPINFO si = new STARTUPINFO();
                si.cb = Marshal.SizeOf(si);
                si.lpDesktop = @"WinSta0\Default"; // Run in interactive desktop

                PROCESS_INFORMATION pi = new PROCESS_INFORMATION();

                string commandLine = string.Format("\"{0}\" --agent", exePath);
                string workingDir = Path.GetDirectoryName(exePath);

                uint creationFlags = 0;
                if (envBlock != IntPtr.Zero)
                {
                    creationFlags |= 0x00000400; // CREATE_UNICODE_ENVIRONMENT
                }

                bool result = CreateProcessAsUser(
                    primaryToken,
                    null,
                    commandLine,
                    IntPtr.Zero,
                    IntPtr.Zero,
                    false,
                    creationFlags,
                    envBlock,
                    workingDir,
                    ref si,
                    out pi
                );

                if (result)
                {
                    CloseHandle(pi.hProcess);
                    CloseHandle(pi.hThread);
                    return true;
                }
                else
                {
                    Logger.Log(string.Format("CreateProcessAsUser failed. Error: {0}", Marshal.GetLastWin32Error()));
                    return false;
                }
            }
            catch (Exception ex)
            {
                Logger.Log(string.Format("LaunchAgentInUserSession exception: {0}", ex.Message));
                return false;
            }
            finally
            {
                if (envBlock != IntPtr.Zero)
                {
                    DestroyEnvironmentBlock(envBlock);
                }
                if (primaryToken != IntPtr.Zero)
                {
                    CloseHandle(primaryToken);
                }
            }
        }
    }

    // =========================================================================
    // ENTRY POINT
    // =========================================================================
    public static class Program
    {
        public static void Main(string[] args)
        {
            string exeDir = Path.GetDirectoryName(typeof(Program).Assembly.Location);
            Logger.Initialize(exeDir);

            if (args.Length > 0 && args[0] == "--agent")
            {
                // Run in Agent Mode (within interactive user session)
                bool createdNew;
                using (Mutex mutex = new Mutex(true, "Local\\Serial2KeyboardAgentMutex", out createdNew))
                {
                    if (!createdNew)
                    {
                        // Agent already running in this session, exit
                        return;
                    }
                    PipeClient.RunAgent();
                }
            }
            else
            {
                // Run in Service Mode (Session 0)
                ServiceBase.Run(new MyService());
            }
        }
    }
}
