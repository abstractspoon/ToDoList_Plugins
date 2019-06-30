using System;
using System.Collections.Generic;
using System.Text;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Text;
using System.Windows.Forms;
using System.Windows.Forms.VisualStyles;

using Abstractspoon.Tdl.PluginHelpers;

namespace DayViewUIExtension
{
	public class CalendarItem : Calendar.Appointment
	{
		static DateTime NullDate = new DateTime();

		// --------------------

		private DateTime m_OrgStartDate = NullDate;
		private DateTime m_OrgEndDate = NullDate;
		private DateTime m_PrevDueDate = NullDate;

		private Color m_TaskTextColor = Color.Empty;

		// --------------------

		public Boolean HasTaskTextColor
		{
			get { return !m_TaskTextColor.IsEmpty; }
		}

		public Color TaskTextColor
		{
			get
			{
				if (m_TaskTextColor.IsEmpty)
					return base.TextColor;

				return m_TaskTextColor;
			}
			set { m_TaskTextColor = value; }
		}

		public void UpdateOriginalDates()
		{
			m_OrgStartDate = StartDate;
			m_OrgEndDate = EndDate;
		}

		public void RestoreOriginalDates()
		{
			StartDate = m_OrgStartDate;
			EndDate = m_OrgEndDate;
		}

		public bool EndDateDiffersFromOriginal()
		{
			return ((EndDate - m_OrgEndDate).TotalSeconds != 0.0);
		}

		public bool StartDateDiffersFromOriginal()
		{
			return ((StartDate - m_OrgStartDate).TotalSeconds != 0.0);
		}

		public String AllocTo { get; set; }
		public Boolean IsParent { get; set; }
        public Boolean HasIcon { get; set; }
        public Boolean IsDone { get; set; }
        public Boolean IsLocked { get; set; }
        public double TimeEstimate { get; set; }
        public Task.TimeUnits TimeEstUnits { get; set; }

        // This is a hack because the underlying DayView does
        // not allow overriding the AppointmentView class
        public Rectangle IconRect { get; set; }

        public override TimeSpan Length
        {
            get
            {
                // Handle 'end of day'
                if (IsEndOfDay(EndDate))
                    return (EndDate.Date.AddDays(1) - StartDate);

                return base.Length;
            }
        }

        public TimeSpan OriginalLength
        {
            get
            {
                // Handle 'end of day'
                if (IsEndOfDay(m_OrgEndDate))
                    return (m_OrgEndDate.Date.AddDays(1) - m_OrgStartDate);

                return (m_OrgEndDate - m_OrgStartDate);
            }
        }

        public double LengthAsTimeEstimate(bool original)
        {
            var length = (original ? OriginalLength : Length);

            if (TimeEstUnits == Task.TimeUnits.Minutes)
                return length.TotalMinutes;

            if (TimeEstUnits == Task.TimeUnits.Hours)
                return length.TotalHours;

            return 0.0;
        }

        public bool TimeEstimateMatchesOriginalLength
        {
            get
            {
                return (TimeEstimate == LengthAsTimeEstimate(true));
            }
        }

        public bool TimeEstimateIsMinsOrHours
        {
            get 
            { 
                return ((TimeEstUnits == Task.TimeUnits.Minutes) || 
                        (TimeEstUnits == Task.TimeUnits.Hours)); 
            }
        }

		public static bool IsEndOfDay(DateTime date)
		{
			return (date == date.Date.AddDays(1).AddSeconds(-1));
		}

		public static bool IsStartOfDay(DateTime date)
		{
			return (date == date.Date);
		}

		public bool IsSingleDay()
		{
			return (StartDate.Date == EndDate.Date);
		}

		public override bool IsLongAppt()
		{
			return (base.IsLongAppt() || (m_OrgStartDate.Date != m_OrgEndDate.Date) ||
					((m_OrgStartDate.TimeOfDay == TimeSpan.Zero) && IsEndOfDay(m_OrgEndDate)));
		}

		public bool HasValidDates()
		{
			return ((StartDate != NullDate) &&
					(EndDate != NullDate) &&
					(EndDate > StartDate));
		}

		public bool UpdateTaskAttributes(Task task,
							   UIExtension.UpdateType type,
							   System.Collections.Generic.HashSet<UIExtension.TaskAttribute> attribs)
		{
			if (!task.IsValid())
				return false;

			UInt32 taskID = task.GetID();

			if (attribs != null)
			{
				if (attribs.Contains(UIExtension.TaskAttribute.Title))
					Title = task.GetTitle();

				if (attribs.Contains(UIExtension.TaskAttribute.DueDate))
				{
					m_PrevDueDate = task.GetDueDate(false); // always

					if (!IsDone)
						EndDate = CheckGetEndOfDay(m_PrevDueDate);
				}

				if (attribs.Contains(UIExtension.TaskAttribute.DoneDate))
				{
					bool wasDone = IsDone;
					IsDone = (task.IsDone() || task.IsGoodAsDone());

					if (IsDone)
					{
						if (!wasDone)
							m_PrevDueDate = CheckGetEndOfDay(EndDate);

						EndDate = CheckGetEndOfDay(task.GetDoneDate());
					}
					else if (wasDone && !IsDone)
					{
						EndDate = CheckGetEndOfDay(m_PrevDueDate);
					}
				}

				if (attribs.Contains(UIExtension.TaskAttribute.TimeEstimate))
				{
					Task.TimeUnits units = Task.TimeUnits.Unknown;
					TimeEstimate = task.GetTimeEstimate(ref units, false);
					TimeEstUnits = units;
				}

				if (attribs.Contains(UIExtension.TaskAttribute.StartDate))
					StartDate = task.GetStartDate(false);

				if (attribs.Contains(UIExtension.TaskAttribute.AllocTo))
					AllocTo = String.Join(", ", task.GetAllocatedTo());

				if (attribs.Contains(UIExtension.TaskAttribute.Icon))
					HasIcon = task.HasIcon();

				TaskTextColor = task.GetTextDrawingColor();
				IsLocked = task.IsLocked(true);
			}
			else
			{
				Title = task.GetTitle();
				AllocTo = String.Join(", ", task.GetAllocatedTo());
				HasIcon = task.HasIcon();
				Id = taskID;
				IsParent = task.IsParent();
				TaskTextColor = task.GetTextDrawingColor();
				DrawBorder = true;
				IsLocked = task.IsLocked(true);

				Task.TimeUnits units = Task.TimeUnits.Unknown;
				TimeEstimate = task.GetTimeEstimate(ref units, false);
				TimeEstUnits = units;

				StartDate = task.GetStartDate(false);
				IsDone = (task.IsDone() || task.IsGoodAsDone());

				m_PrevDueDate = CheckGetEndOfDay(task.GetDueDate(false));
				EndDate = (IsDone ? CheckGetEndOfDay(task.GetDoneDate()) : m_PrevDueDate);
			}

			UpdateOriginalDates();

			return true;
		}

		private DateTime CheckGetEndOfDay(DateTime date)
		{
			if (date == date.Date)
				return date.AddDays(1).AddSeconds(-1);

			// else
			return date;
		}
	}

}
