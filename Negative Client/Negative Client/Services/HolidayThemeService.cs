using System;
using System.Windows.Media;

namespace Negative_Client.Services
{
    public enum HolidayVisualTheme
    {
        Default,
        Christmas,
        Halloween,
        Valentine,
        WomensDay,
        Birthday,
        NewYear
    }


    public sealed class HolidayVisualState
    {
        public HolidayVisualTheme Theme { get; init; } =
            HolidayVisualTheme.Default;

        public string DisplayName { get; init; } =
            "Predeterminado";

        public Color PrimaryColor { get; init; } =
            Color.FromRgb(79, 195, 215);

        public Color SecondaryColor { get; init; } =
            Color.FromRgb(145, 231, 245);

        public bool ShowBalloons { get; init; }

        public bool ShowFireworks { get; init; }
    }


    public static class HolidayThemeService
    {
        public static DateTimeOffset GetMonterreyNow() =>
            LauncherHolidayCalendar.GetMonterreyNow();

        public static HolidayVisualState GetAutomaticState() =>
            GetAutomaticState(GetMonterreyNow().Date);

        public static HolidayVisualState GetAutomaticState(DateTime localDate)
        {
            LauncherHolidaySchedule schedule = LauncherHolidayCalendar.GetAutomaticState(localDate);
            HolidayVisualState palette = GetPreviewState(
                Enum.Parse<HolidayVisualTheme>(schedule.Theme.ToString()));

            return new HolidayVisualState
            {
                Theme = palette.Theme,
                DisplayName = palette.DisplayName,
                PrimaryColor = palette.PrimaryColor,
                SecondaryColor = palette.SecondaryColor,
                ShowBalloons = schedule.ShowBalloons,
                ShowFireworks = schedule.ShowFireworks
            };
        }

        public static HolidayVisualState GetPreviewState(HolidayVisualTheme theme)
        {
            LauncherHolidaySchedule schedule = LauncherHolidayCalendar.GetPreviewState(
                Enum.Parse<LauncherHolidayTheme>(theme.ToString()));

            return new HolidayVisualState
            {
                Theme = Enum.Parse<HolidayVisualTheme>(schedule.Theme.ToString()),
                DisplayName = schedule.DisplayName,
                PrimaryColor = ParseColor(schedule.PrimaryHex),
                SecondaryColor = ParseColor(schedule.SecondaryHex),
                ShowBalloons = schedule.ShowBalloons,
                ShowFireworks = schedule.ShowFireworks
            };
        }

        private static Color ParseColor(string hex)
        {
            byte red = Convert.ToByte(hex.Substring(1, 2), 16);
            byte green = Convert.ToByte(hex.Substring(3, 2), 16);
            byte blue = Convert.ToByte(hex.Substring(5, 2), 16);
            return Color.FromRgb(red, green, blue);
        }

        public static TimeSpan GetDelayUntilNextMonterreyMidnight() =>
            LauncherHolidayCalendar.GetDelayUntilNextMonterreyMidnight();
    }
}
