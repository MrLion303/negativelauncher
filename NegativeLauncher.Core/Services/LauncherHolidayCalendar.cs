using System;

namespace Negative_Client.Services
{
    public enum LauncherHolidayTheme
    {
        Default,
        Christmas,
        Halloween,
        Valentine,
        WomensDay,
        Birthday,
        NewYear
    }

    public sealed record LauncherHolidaySchedule
    {
        public LauncherHolidayTheme Theme { get; init; } = LauncherHolidayTheme.Default;
        public string DisplayName { get; init; } = "Predeterminado";
        public string PrimaryHex { get; init; } = "#4FC3D7";
        public string SecondaryHex { get; init; } = "#91E7F5";
        public bool ShowBalloons { get; init; }
        public bool ShowFireworks { get; init; }
    }

    /// <summary>
    /// Calendario visual compartido entre WPF y Avalonia para evitar diferencias entre plataformas.
    /// Las fechas se interpretan según la hora fija de Monterrey (UTC-6).
    /// </summary>
    public static class LauncherHolidayCalendar
    {
        private static readonly TimeSpan MonterreyOffset = TimeSpan.FromHours(-6);

        public static DateTimeOffset GetMonterreyNow() =>
            DateTimeOffset.UtcNow.ToOffset(MonterreyOffset);

        public static LauncherHolidaySchedule GetAutomaticState() =>
            GetAutomaticState(GetMonterreyNow().Date);

        public static LauncherHolidaySchedule GetAutomaticState(DateTime localDate)
        {
            int month = localDate.Month;
            int day = localDate.Day;

            bool christmasPeriod = (month == 12 && day >= 1) || (month == 1 && day <= 15);
            bool newYearFireworks = (month == 12 && day == 31) || (month == 1 && day == 1);

            if (christmasPeriod)
            {
                LauncherHolidaySchedule christmas = GetPreviewState(LauncherHolidayTheme.Christmas);
                return christmas with { ShowFireworks = newYearFireworks };
            }

            if ((month == 10 && day >= 20) || (month == 11 && day <= 4))
                return GetPreviewState(LauncherHolidayTheme.Halloween);

            if (month == 2 && day >= 10 && day <= 15)
                return GetPreviewState(LauncherHolidayTheme.Valentine);

            if (month == 3 && day == 8)
                return GetPreviewState(LauncherHolidayTheme.WomensDay);

            if (month == 3 && day == 27)
                return GetPreviewState(LauncherHolidayTheme.Birthday);

            return GetPreviewState(LauncherHolidayTheme.Default);
        }

        public static LauncherHolidaySchedule GetPreviewState(LauncherHolidayTheme theme) =>
            theme switch
            {
                LauncherHolidayTheme.Christmas => new()
                {
                    Theme = LauncherHolidayTheme.Christmas,
                    DisplayName = "Navidad",
                    PrimaryHex = "#E83646",
                    SecondaryHex = "#34BC5E"
                },
                LauncherHolidayTheme.Halloween => new()
                {
                    Theme = LauncherHolidayTheme.Halloween,
                    DisplayName = "Halloween",
                    PrimaryHex = "#FF8018",
                    SecondaryHex = "#FFBE4A"
                },
                LauncherHolidayTheme.Valentine => new()
                {
                    Theme = LauncherHolidayTheme.Valentine,
                    DisplayName = "San Valentín",
                    PrimaryHex = "#FF4391",
                    SecondaryHex = "#FF8BBF"
                },
                LauncherHolidayTheme.WomensDay => new()
                {
                    Theme = LauncherHolidayTheme.WomensDay,
                    DisplayName = "8 de marzo",
                    PrimaryHex = "#EE4690",
                    SecondaryHex = "#FF97CA"
                },
                LauncherHolidayTheme.Birthday => new()
                {
                    Theme = LauncherHolidayTheme.Birthday,
                    DisplayName = "27 de marzo",
                    PrimaryHex = "#4FC3D7",
                    SecondaryHex = "#91E7F5",
                    ShowBalloons = true
                },
                LauncherHolidayTheme.NewYear => new()
                {
                    Theme = LauncherHolidayTheme.NewYear,
                    DisplayName = "Año Nuevo",
                    PrimaryHex = "#F6C945",
                    SecondaryHex = "#4ECAF6",
                    ShowFireworks = true
                },
                _ => new()
                {
                    Theme = LauncherHolidayTheme.Default,
                    DisplayName = "Predeterminado",
                    PrimaryHex = "#4FC3D7",
                    SecondaryHex = "#91E7F5"
                }
            };

        public static TimeSpan GetDelayUntilNextMonterreyMidnight()
        {
            DateTimeOffset now = GetMonterreyNow();
            DateTimeOffset nextMidnight = new(now.Date.AddDays(1), MonterreyOffset);
            TimeSpan delay = nextMidnight.ToUniversalTime() - DateTimeOffset.UtcNow;
            return delay < TimeSpan.FromSeconds(1) ? TimeSpan.FromSeconds(1) : delay;
        }
    }
}
