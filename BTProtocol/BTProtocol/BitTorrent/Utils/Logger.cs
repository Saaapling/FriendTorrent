using System;

namespace BTProtocol.BitTorrent
{
    internal class Logger
    {
        public enum LoggingLevel : int
        {
            Noise = 0,
            Debug = 1,
            Info = 2,
            Error = 3,
            Critical = 4,
        }

        public enum DebugFlags : int
        {
            None = 0,
            Tracker = 1,
            Downloading = 2,
            Seeding = 4,
            All = int.MaxValue,
        }

        private LoggingLevel level;
        private int debug_flags;

        public Logger(LoggingLevel level, int flags)
        {
            this.level = level;
            debug_flags = flags;
        }

        public void SetLoggingLevel(LoggingLevel level)
        {
            this.level = level;
        }

        public void Noise(string message, DebugFlags origin = DebugFlags.All)
        {
            if (level <= LoggingLevel.Noise)
            {
                if ((debug_flags & (int)origin) != 0)
                {
                    Console.WriteLine(message);
                }
            }
        }

        public void Debug(string message, DebugFlags origin=DebugFlags.All)
        {
            if (level <= LoggingLevel.Debug)
            {
                if ((debug_flags & (int) origin) != 0){
                    Console.WriteLine(message);
                }
            }
        }

        public void Info(string message)
        {
            if (level <= LoggingLevel.Info)
            {
                Console.WriteLine(message);
            }
        }

        public void Error(string message)
        {
            if (level <= LoggingLevel.Error)
            {
                Console.WriteLine(message);
            }
        }


        public void Critical(string message)
        {
            if (level <= LoggingLevel.Critical)
            {
                Console.WriteLine(message);
            }
        }
    }
}
