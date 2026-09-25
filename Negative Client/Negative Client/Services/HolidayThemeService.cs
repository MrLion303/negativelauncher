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
        // Monterrey usa UTC-6. Se usa un offset fijo para que los cambios
        // visuales ocurran exactamente a medianoche de GMT-6.
        private static readonly TimeSpan MonterreyOffset =
            TimeSpan.FromHours(-6);


        public static DateTimeOffset GetMonterreyNow()
        {
            return DateTimeOffset.UtcNow
                .ToOffset(MonterreyOffset);
        }


        public static HolidayVisualState GetAutomaticState()
        {
            DateTime localDate =
                GetMonterreyNow().Date;


            return GetAutomaticState(
                localDate);
        }


        public static HolidayVisualState GetAutomaticState(
            DateTime localDate)
        {
            int month =
                localDate.Month;

            int day =
                localDate.Day;


            bool christmas =
                (month == 12 && day >= 1) ||
                (month == 1 && day <= 15);


            bool newYearFireworks =
                (month == 12 && day == 31) ||
                (month == 1 && day == 1);


            if (christmas)
            {
                HolidayVisualState christmasState =
                    GetPreviewState(
                        HolidayVisualTheme.Christmas);


                return new HolidayVisualState
                {
                    Theme =
                        christmasState.Theme,

                    DisplayName =
                        christmasState.DisplayName,

                    PrimaryColor =
                        christmasState.PrimaryColor,

                    SecondaryColor =
                        christmasState.SecondaryColor,

                    ShowFireworks =
                        newYearFireworks,

                    ShowBalloons =
                        false
                };
            }


            if (month == 10 && day >= 20 ||
                month == 11 && day <= 4)
            {
                return GetPreviewState(
                    HolidayVisualTheme.Halloween);
            }


            if (month == 2 &&
                day >= 10 &&
                day <= 15)
            {
                return GetPreviewState(
                    HolidayVisualTheme.Valentine);
            }


            if (month == 3 &&
                day == 8)
            {
                return GetPreviewState(
                    HolidayVisualTheme.WomensDay);
            }


            if (month == 3 &&
                day == 27)
            {
                return GetPreviewState(
                    HolidayVisualTheme.Birthday);
            }


            return GetPreviewState(
                HolidayVisualTheme.Default);
        }


        public static HolidayVisualState GetPreviewState(
            HolidayVisualTheme theme)
        {
            return theme switch
            {
                HolidayVisualTheme.Christmas =>
                    new HolidayVisualState
                    {
                        Theme =
                            HolidayVisualTheme.Christmas,

                        DisplayName =
                            "Navidad",

                        PrimaryColor =
                            Color.FromRgb(232, 54, 70),

                        SecondaryColor =
                            Color.FromRgb(52, 188, 94)
                    },


                HolidayVisualTheme.Halloween =>
                    new HolidayVisualState
                    {
                        Theme =
                            HolidayVisualTheme.Halloween,

                        DisplayName =
                            "Halloween",

                        PrimaryColor =
                            Color.FromRgb(255, 128, 24),

                        SecondaryColor =
                            Color.FromRgb(255, 190, 74)
                    },


                HolidayVisualTheme.Valentine =>
                    new HolidayVisualState
                    {
                        Theme =
                            HolidayVisualTheme.Valentine,

                        DisplayName =
                            "San Valentín",

                        PrimaryColor =
                            Color.FromRgb(255, 67, 145),

                        SecondaryColor =
                            Color.FromRgb(255, 139, 191)
                    },


                HolidayVisualTheme.WomensDay =>
                    new HolidayVisualState
                    {
                        Theme =
                            HolidayVisualTheme.WomensDay,

                        DisplayName =
                            "8 de marzo",

                        PrimaryColor =
                            Color.FromRgb(238, 70, 144),

                        SecondaryColor =
                            Color.FromRgb(255, 151, 202)
                    },


                HolidayVisualTheme.Birthday =>
                    new HolidayVisualState
                    {
                        Theme =
                            HolidayVisualTheme.Birthday,

                        DisplayName =
                            "27 de marzo",

                        PrimaryColor =
                            Color.FromRgb(79, 195, 215),

                        SecondaryColor =
                            Color.FromRgb(145, 231, 245),

                        ShowBalloons =
                            true
                    },


                HolidayVisualTheme.NewYear =>
                    new HolidayVisualState
                    {
                        Theme =
                            HolidayVisualTheme.NewYear,

                        DisplayName =
                            "Año Nuevo",

                        PrimaryColor =
                            Color.FromRgb(246, 201, 69),

                        SecondaryColor =
                            Color.FromRgb(78, 202, 246),

                        ShowFireworks =
                            true
                    },


                _ =>
                    new HolidayVisualState
                    {
                        Theme =
                            HolidayVisualTheme.Default,

                        DisplayName =
                            "Predeterminado",

                        PrimaryColor =
                            Color.FromRgb(79, 195, 215),

                        SecondaryColor =
                            Color.FromRgb(145, 231, 245)
                    }
            };
        }


        public static TimeSpan GetDelayUntilNextMonterreyMidnight()
        {
            DateTimeOffset localNow =
                GetMonterreyNow();


            DateTime nextDate =
                localNow.Date
                    .AddDays(1);


            DateTimeOffset nextMidnight =
                new DateTimeOffset(
                    nextDate,
                    MonterreyOffset);


            TimeSpan delay =
                nextMidnight.ToUniversalTime() -
                DateTimeOffset.UtcNow;


            if (delay <
                TimeSpan.FromSeconds(1))
            {
                return TimeSpan.FromSeconds(1);
            }


            return delay;
        }
    }
}
