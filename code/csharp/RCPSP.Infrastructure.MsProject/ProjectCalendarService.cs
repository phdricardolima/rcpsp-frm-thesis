using System;
using MSProject = Microsoft.Office.Interop.MSProject;

namespace RCPSP.Infrastructure.MsProject
{
    public sealed class ProjectCalendarService
    {
        public const string ContinuousCalendarName = "RCPSP_7D_8H_CONTINUO";

        public MSProject.Calendar EnsureContinuousCalendar(
            MSProject.Project project,
            MSProject.Application app)
        {
            if (project == null)
                throw new ArgumentNullException(nameof(project));

            if (app == null)
                throw new ArgumentNullException(nameof(app));

            MSProject.Calendar calendar = FindBaseCalendar(project, ContinuousCalendarName);

            if (calendar == null)
            {
                bool created = app.BaseCalendarCreate(ContinuousCalendarName, Type.Missing);
                if (!created)
                    throw new InvalidOperationException(
                        "Could not create the continuous base calendar.");

                calendar = FindBaseCalendar(project, ContinuousCalendarName);

                if (calendar == null)
                    throw new InvalidOperationException(
                        "The calendar was created, but was not found in the project.");
            }

            ConfigureSevenDaysEightHours(calendar);
            ApplyCalendarToProject(app, project, calendar.Name);

            return calendar;
        }

        private static MSProject.Calendar FindBaseCalendar(
            MSProject.Project project,
            string calendarName)
        {
            if (project.BaseCalendars == null)
                return null;

            foreach (MSProject.Calendar cal in project.BaseCalendars)
            {
                if (cal == null)
                    continue;

                if (string.Equals(cal.Name, calendarName, StringComparison.OrdinalIgnoreCase))
                    return cal;
            }

            return null;
        }

        private static void ConfigureSevenDaysEightHours(MSProject.Calendar calendar)
        {
            ConfigureWorkingDay(calendar.WeekDays[MSProject.PjWeekday.pjSunday], 8, 0, 16, 0);
            ConfigureWorkingDay(calendar.WeekDays[MSProject.PjWeekday.pjMonday], 8, 0, 16, 0);
            ConfigureWorkingDay(calendar.WeekDays[MSProject.PjWeekday.pjTuesday], 8, 0, 16, 0);
            ConfigureWorkingDay(calendar.WeekDays[MSProject.PjWeekday.pjWednesday], 8, 0, 16, 0);
            ConfigureWorkingDay(calendar.WeekDays[MSProject.PjWeekday.pjThursday], 8, 0, 16, 0);
            ConfigureWorkingDay(calendar.WeekDays[MSProject.PjWeekday.pjFriday], 8, 0, 16, 0);
            ConfigureWorkingDay(calendar.WeekDays[MSProject.PjWeekday.pjSaturday], 8, 0, 16, 0);
        }

        private static void ConfigureWorkingDay(
            MSProject.WeekDay weekDay,
            int startHour,
            int startMinute,
            int finishHour,
            int finishMinute)
        {
            if (weekDay == null)
                return;

            weekDay.set_Working(true);

            DateTime start = DateTime.Today
                .AddHours(startHour)
                .AddMinutes(startMinute);

            DateTime finish = DateTime.Today
                .AddHours(finishHour)
                .AddMinutes(finishMinute);

            weekDay.Shift1.Start = start;
            weekDay.Shift1.Finish = finish;

            weekDay.Shift2.Clear();
            weekDay.Shift3.Clear();
        }

        private static void ApplyCalendarToProject(
            MSProject.Application app,
            MSProject.Project project,
            string calendarName)
        {
            app.ProjectSummaryInfo(
                project,
                Type.Missing,
                Type.Missing,
                Type.Missing,
                Type.Missing,
                Type.Missing,
                Type.Missing,
                Type.Missing,
                Type.Missing,
                Type.Missing,
                Type.Missing,
                Type.Missing,
                calendarName,
                Type.Missing,
                Type.Missing,
                Type.Missing
            );

            project.HoursPerDay = 8;
            project.HoursPerWeek = 56;
            project.DaysPerMonth = 30;
        }
    }
}
