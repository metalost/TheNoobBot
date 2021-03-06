﻿namespace nManager
{
    public static class Information
    {
        public const string TargetWowVersion = "7.3.2";
        public const int TargetWowBuild = 25549;
        public const int MinWowBuild = 17128 + 1; // no need to update it
        public const int MaxWowBuild = 27000; // not to be changed until we are closer
        // The Min and Max check are in case the build offset have changed and the address return a value higher than 0, it's kind of a pattern check.
        public const string Version = "MD5HashVersionForDev"; // current = 7.1.30 vs MD5HashVersionForDev
        public static string MainTitle = "The Noob Bot " + Version;
        public static string SchedulerTitle = "The Noob Scheduler for " + MainTitle;
        public static bool DevMode = !Version.Contains(".");
    }
}