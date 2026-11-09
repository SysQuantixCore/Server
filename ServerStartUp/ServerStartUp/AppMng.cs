
using System;
using System.IO;
using System.Diagnostics;
using System.Windows.Forms;
using System.Runtime.InteropServices;
using System.Threading;

namespace ServerStartUp
{
    public class AppMng
    {
        [DllImport("user32.dll", EntryPoint = "ShowWindow", SetLastError = true)]
        static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);

        [DllImport("user32.dll", EntryPoint = "IsWindowVisible")]
        [return: MarshalAs(UnmanagedType.Bool)]
        static extern bool IsWindowVisible(IntPtr hWnd);

        public const int MaxApplications = 32;

        public ProcessStartInfo[] ListInfo =
            new ProcessStartInfo[MaxApplications];

        public Process[] ListProc =
            new Process[MaxApplications];

        private readonly object _syncRoot = new object();

        private bool IsValidIndex(int Index)
        {
            return Index >= 0 && Index < MaxApplications;
        }

        private bool IsProcessRunning(Process proc)
        {
            if (proc == null)
                return false;

            try
            {
                return !proc.HasExited;
            }
            catch (InvalidOperationException)
            {
                return false;
            }
            catch (System.ComponentModel.Win32Exception)
            {
                return false;
            }
        }

        public void Run(
            int Index,
            string FilePath,
            string Args,
            int Delay,
            int WindowStyle)
        {
            if (!IsValidIndex(Index))
                return;

            if (string.IsNullOrWhiteSpace(FilePath))
                return;

            try
            {
                string FullPath = Path.GetFullPath(FilePath);

                if (!File.Exists(FullPath))
                    return;

                lock (_syncRoot)
                {
                    if (isRunning(Index))
                        return;

                    Process oldProcess = ListProc[Index];

                    if (oldProcess != null)
                    {
                        oldProcess.Dispose();
                        ListProc[Index] = null;
                    }

                    ListInfo[Index] = new ProcessStartInfo
                    {
                        FileName = FullPath,
                        WorkingDirectory =
                            Path.GetDirectoryName(FullPath)
                            ?? Environment.CurrentDirectory,
                        Arguments = Args ?? string.Empty,
                        WindowStyle = ProcessWindowStyle.Normal,
                        UseShellExecute = false
                    };

                    ListProc[Index] = Process.Start(ListInfo[Index]);
                }

                if (ListProc[Index] == null)
                    return;

                if (Delay > 0)
                    Thread.Sleep(Delay);

                ShowSelectedWindow(Index, WindowStyle);
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    ex.Message,
                    "Erro ao iniciar aplicação",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Error);
            }
        }

        public bool Kill(int Index)
        {
            if (!IsValidIndex(Index))
                return false;

            try
            {
                lock (_syncRoot)
                {
                    Process proc = ListProc[Index];

                    if (proc == null)
                        return false;

                    if (IsProcessRunning(proc))
                    {
                        proc.Kill();
                        proc.WaitForExit(3000);

                        if (IsProcessRunning(proc))
                            return false;
                    }

                    ListProc[Index] = null;
                    proc.Dispose();

                    return true;
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    ex.Message,
                    "Erro ao encerrar aplicação",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Error);

                return false;
            }
        }

        public bool KillAll()
        {
            bool Result = false;

            for (int i = 0; i < MaxApplications; i++)
            {
                if (Kill(i))
                    Result = true;
            }

            return Result;
        }

        public bool isNull(int Index)
        {
            if (!IsValidIndex(Index))
                return true;

            return ListProc[Index] == null;
        }

        public bool isRunning(int Index)
        {
            if (!IsValidIndex(Index))
                return false;

            return IsProcessRunning(ListProc[Index]);
        }

        public bool AnyRunning()
        {
            for (int i = 0; i < MaxApplications; i++)
            {
                if (isRunning(i))
                    return true;
            }

            return false;
        }

        public int GetCount(bool HasExited)
        {
            int Count = 0;

            foreach (Process proc in ListProc)
            {
                if (proc == null)
                    continue;

                try
                {
                    if (proc.HasExited == HasExited)
                        Count++;
                }
                catch (InvalidOperationException)
                {
                }
                catch (System.ComponentModel.Win32Exception)
                {
                }
            }

            return Count;
        }

        public Boolean HideAllWindows()
        {
            try
            {
                Boolean Result = false;
                foreach (Process proc in this.ListProc)
                {
                    if (proc != null)
                    {
                        if (proc.HasExited == false)
                        {
                            Result = true;
                            ShowWindow(proc.MainWindowHandle, 0);
                        }
                    }
                }

                return Result;
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.ToString());
            }

            return false;
        }

        public Boolean ShowAllWindows()
        {
            try
            {
                Boolean Result = false;
                foreach (Process proc in this.ListProc)
                {
                    if (proc != null)
                    {
                        if (proc.HasExited == false)
                        {
                            Result = true;
                            ShowWindow(proc.MainWindowHandle, 1);
                        }
                    }
                }

                return Result;
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.ToString());
            }

            return false;
        }

        public Boolean IsWindowVisible(int Index)
        {
            if (!IsValidIndex(Index))
                return false;

            try
            {
                if (this.ListProc[Index] != null)
                {
                    if (this.ListProc[Index].HasExited == false)
                    {
                        return IsWindowVisible(
                            ListProc[Index].MainWindowHandle);
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.ToString());
            }

            return false;
        }

        public Boolean ShowSelectedWindow(int Index, int State)
        {
            try
            {
                if (this.ListProc[Index] != null)
                {
                    if (this.ListProc[Index].HasExited == false)
                    {
                        return ShowWindow(
                            ListProc[Index].MainWindowHandle, State);
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.ToString());
            }

            return false;
        }
    }
}
