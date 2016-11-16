using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using Mono.Unix;

namespace DiscordBot
{
    public interface IExitSignal
    {
        event EventHandler Exit;
    }

    public class UnixExitSignal : IExitSignal
    {
        public event EventHandler Exit;

        readonly UnixSignal[] signals = new UnixSignal[]{
            new UnixSignal(Mono.Unix.Native.Signum.SIGTERM),
            new UnixSignal(Mono.Unix.Native.Signum.SIGINT),
            new UnixSignal(Mono.Unix.Native.Signum.SIGUSR1)
        };

        public UnixExitSignal()
        {
            Task.Factory.StartNew(() =>
            {
                // blocking call to wait for any kill signal
                int index = UnixSignal.WaitAny(signals, -1);

                Exit?.Invoke(null, EventArgs.Empty);
            });
        }
    }

    public class WinExitSignal : IExitSignal
    {
        public event EventHandler Exit;

        [DllImport("Kernel32")]
        public static extern bool SetConsoleCtrlHandler(HandlerRoutine Handler, bool Add);

        // A delegate type to be used as the handler routine
        // for SetConsoleCtrlHandler.
        public delegate bool HandlerRoutine(CtrlTypes CtrlType);

        // An enumerated type for the control messages
        // sent to the handler routine.
        public enum CtrlTypes
        {
            CTRL_C_EVENT = 0,
            CTRL_BREAK_EVENT,
            CTRL_CLOSE_EVENT,
            CTRL_LOGOFF_EVENT = 5,
            CTRL_SHUTDOWN_EVENT
        }

        /// <summary>
        /// Need this as a member variable to avoid it being garbage collected.
        /// </summary>
        private HandlerRoutine m_hr;

        public WinExitSignal()
        {
            m_hr = ConsoleCtrlCheck;

            SetConsoleCtrlHandler(m_hr, true);

        }

        /// <summary>
        /// Handle the ctrl types
        /// </summary>
        /// <param name="ctrlType"></param>
        /// <returns></returns>
        private bool ConsoleCtrlCheck(CtrlTypes ctrlType)
        {
            switch (ctrlType)
            {
                case CtrlTypes.CTRL_C_EVENT:
                case CtrlTypes.CTRL_BREAK_EVENT:
                case CtrlTypes.CTRL_CLOSE_EVENT:
                case CtrlTypes.CTRL_LOGOFF_EVENT:
                case CtrlTypes.CTRL_SHUTDOWN_EVENT:
                    Exit?.Invoke(this, EventArgs.Empty);
                    break;
            }

            return true;
        }
    }
}
