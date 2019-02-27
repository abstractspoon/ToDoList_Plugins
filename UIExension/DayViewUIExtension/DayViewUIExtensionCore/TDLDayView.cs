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

		public override DateTime EndDate
		{
			get
			{
				return base.EndDate;
			}
			set
			{
				// Handle 'end of day'
				if ((value != DateTime.MinValue) && (value.Date == value))
					base.EndDate = value.AddSeconds(-1);
				else
					base.EndDate = value;
			}
		}

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
						EndDate = m_PrevDueDate;
				}

				if (attribs.Contains(UIExtension.TaskAttribute.DoneDate))
				{
					bool wasDone = IsDone;
					IsDone = (task.IsDone() || task.IsGoodAsDone());

					if (IsDone)
					{
						if (!wasDone)
							m_PrevDueDate = EndDate;

						EndDate = task.GetDoneDate();
					}
					else if (wasDone && !IsDone)
					{
						EndDate = m_PrevDueDate;
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

				m_PrevDueDate = task.GetDueDate(false);
				EndDate = (IsDone ? task.GetDoneDate() : m_PrevDueDate);
			}

			UpdateOriginalDates();

			return true;
		}
	}

	//////////////////////////////////////////////////////////////////////////////////

	public class TDLDayView : Calendar.DayView
    {
        private UInt32 m_SelectedTaskID = 0;

        private Boolean m_HideParentTasks = true;
        private Boolean m_HideTasksWithoutTimes = true;
        private Boolean m_HideTasksSpanningWeekends = false;
        private Boolean m_HideTasksSpanningDays = false;

		private System.Collections.Generic.Dictionary<UInt32, CalendarItem> m_Items;
        private TDLRenderer m_Renderer;

		// ----------------------------------------------------------------
    
        public Boolean ReadOnly { get; set; }

        public TDLDayView(UIExtension.TaskIcon taskIcons)
        {
            hourLabelWidth = DPIScaling.Scale(hourLabelWidth);
            hourLabelIndent = DPIScaling.Scale(hourLabelIndent);
            dayHeadersHeight = DPIScaling.Scale(dayHeadersHeight);
            appointmentGripWidth = DPIScaling.Scale(appointmentGripWidth);
            headerBorder = DPIScaling.Scale(headerBorder);
            longAppointmentSpacing = DPIScaling.Scale(longAppointmentSpacing);
            minSlotHeight = DPIScaling.Scale(5);
            
            m_Renderer = new TDLRenderer(Handle, taskIcons);
			m_Items = new System.Collections.Generic.Dictionary<UInt32, CalendarItem>();

            InitializeComponent();
            RefreshHScrollSize();
        }

        protected void InitializeComponent()
        {
            Calendar.DrawTool drawTool = new Calendar.DrawTool();
            drawTool.DayView = this;

            this.ActiveTool = drawTool;
            this.AllowInplaceEditing = true;
            this.AllowNew = false;
            this.AmPmDisplay = true;
            this.Anchor = (System.Windows.Forms.AnchorStyles.Bottom |
                                     System.Windows.Forms.AnchorStyles.Left |
                                     System.Windows.Forms.AnchorStyles.Right);
            this.AppHeightMode = Calendar.DayView.AppHeightDrawMode.TrueHeightAll;
            this.DaysToShow = 7;
            this.DrawAllAppBorder = false;
            this.Location = new System.Drawing.Point(0, 0);
            this.MinHalfHourApp = false;
            this.Name = "m_dayView";
            this.Renderer = m_Renderer;
            this.SelectionEnd = new System.DateTime(((long)(0)));
            this.SelectionStart = new System.DateTime(((long)(0)));
            this.Size = new System.Drawing.Size(798, 328);
            this.SlotsPerHour = 4;
            this.TabIndex = 0;
            this.Text = "m_dayView";
            this.WorkingHourEnd = 19;
            this.WorkingHourStart = 9;
            this.WorkingMinuteEnd = 0;
            this.WorkingMinuteStart = 0;
            this.ReadOnly = false;

			this.ResolveAppointments += new Calendar.ResolveAppointmentsEventHandler(this.OnResolveAppointments);
            this.SelectionChanged += new Calendar.AppointmentEventHandler(this.OnSelectionChanged);
            this.WeekChange += new Calendar.WeekChangeEventHandler(OnWeekChanged);

        }

        public bool IsTaskWithinRange(UInt32 dwTaskID)
        {
			CalendarItem item;

			if (m_Items.TryGetValue(dwTaskID, out item))
                return IsItemWithinRange(item, StartDate, EndDate);

            return false;
        }

        public bool IsTaskDisplayable(UInt32 dwTaskID)
        {
            if (dwTaskID == 0)
                return false;

			CalendarItem item;

			if (!m_Items.TryGetValue(dwTaskID, out item))
                return false;

            return IsItemDisplayable(item);
        }

        public Boolean HideParentTasks
        {
            get { return m_HideParentTasks; }
            set
            {
                if (value != m_HideParentTasks)
                {
                    m_HideParentTasks = value;
                    FixupSelection(false);
                }
            }
        }

        public Boolean HideTasksWithoutTimes
        {
            get { return m_HideTasksWithoutTimes; }
            set
            {
                if (value != m_HideTasksWithoutTimes)
                {
                    m_HideTasksWithoutTimes = value;
                    FixupSelection(false);
                }
            }
        }

        public Boolean HideTasksSpanningWeekends
        {
            get { return m_HideTasksSpanningWeekends; }
            set
            {
                if (value != m_HideTasksSpanningWeekends)
                {
                    m_HideTasksSpanningWeekends = value;
                    FixupSelection(false);
                }
            }
        }

        public Boolean HideTasksSpanningDays
        {
            get { return m_HideTasksSpanningDays; }
            set
            {
                if (value != m_HideTasksSpanningDays)
                {
                    m_HideTasksSpanningDays = value;
                    FixupSelection(false);
                }
            }
        }

        public bool IsItemDisplayable(CalendarItem item)
        {
            if (item == null)
                return false;

            if (HideParentTasks && item.IsParent)
                return false;

            if (!item.HasValidDates())
                return false;

            if (HideTasksSpanningWeekends)
            {
                if (DateUtil.WeekOfYear(item.StartDate) != DateUtil.WeekOfYear(item.EndDate))
                return false;
            }

            if (HideTasksSpanningDays)
            {
                if (item.StartDate.Date != item.EndDate.Date)
                return false;
            }

            if (HideTasksWithoutTimes)
            {
                if (CalendarItem.IsStartOfDay(item.StartDate) && CalendarItem.IsEndOfDay(item.EndDate))
                return false;
            }

            return true;
        }

        public UInt32 GetSelectedTaskID()
        {
            if (!IsTaskDisplayable(m_SelectedTaskID))
                return 0;
            
            return m_SelectedTaskID;
        }

        public void FixupSelection(bool scrollToTask)
        {
            UInt32 prevSelTaskID = SelectedAppointmentId;
            UInt32 selTaskID = GetSelectedTaskID();

            if (selTaskID > 0)
            {
                CalendarItem item;

                if (m_Items.TryGetValue(selTaskID, out item))
                {
                    if (scrollToTask)
                    {
                        if (item.StartDate != DateTime.MinValue)
                        {
                            StartDate = item.StartDate;
                            SelectedAppointment = item;

                            ScrollToTop();
                            return;
                        }
                    }
                    else if (IsItemWithinRange(item, StartDate, EndDate))
                    {
                        SelectedAppointment = item;
                        return;
                    }
                }
            }

            // all else
            SelectedAppointment = null;

            // Notify parent of changes
            if (SelectedAppointmentId != prevSelTaskID)
                RaiseSelectionChanged();
        }

		public bool SelectTask(UInt32 dwTaskID)
		{
            m_SelectedTaskID = dwTaskID;
            FixupSelection(true);

			return (GetSelectedTaskID() != 0);
		}

        public void GoToToday()
        {
            StartDate = DateTime.Now;
        }

		public UIExtension.HitResult HitTest(Int32 xScreen, Int32 yScreen)
		{
			System.Drawing.Point pt = PointToClient(new System.Drawing.Point(xScreen, yScreen));
			Calendar.Appointment appointment = GetAppointmentAt(pt.X, pt.Y);

			if (appointment != null)
			{
				return UIExtension.HitResult.Task;
			}
			else if (GetTrueRectangle().Contains(pt))
			{
				return UIExtension.HitResult.Tasklist;
			}

			// else
			return UIExtension.HitResult.Nowhere;
		}

		public bool GetSelectedItemLabelRect(ref Rectangle rect)
		{
			if (GetAppointmentRect(SelectedAppointment, ref rect))
			{
				CalendarItem selItem = (SelectedAppointment as CalendarItem);

				bool hasIcon = m_Renderer.TaskHasIcon(selItem);

				if (SelectedAppointment.IsLongAppt())
				{
					// Gripper
					if (SelectedAppointment.StartDate >= StartDate)
						rect.X += 8;

					if (hasIcon)
						rect.X += 16;

					rect.X += 1;
					rect.Height += 1;
				}
				else
				{
					if (hasIcon)
					{
						rect.X += 18;
					}
					else
					{
						// Gripper
						rect.X += 8;
					}

					rect.X += 1;
					rect.Y += 1;

					rect.Height = (GetFontHeight() + 4); // 4 = border
				}
				
				return true;
			}

			// else
			return false;
		}

    	private bool IsItemWithinRange(CalendarItem item, DateTime startDate, DateTime endDate)
		{
            if (HideParentTasks && item.IsParent)
                return false;

			if (!item.HasValidDates())
				return false;

			bool startDateInRange = IsDateWithinRange(item.StartDate, startDate, endDate);
			bool endDateInRange = IsDateWithinRange(item.EndDate, startDate, endDate);

			// As a bare minimum, at least one of the task's dates must fall in the week
			if (!startDateInRange && !endDateInRange)
				return false;

            if (HideTasksSpanningWeekends)
            {
				if (!startDateInRange || !endDateInRange)
                    return false;
            }

            if (HideTasksSpanningDays)
            {
                if (item.StartDate.Date != item.EndDate.Date)
                    return false;
            }

			if (HideTasksWithoutTimes)
			{
				if (CalendarItem.IsStartOfDay(item.StartDate) && CalendarItem.IsEndOfDay(item.EndDate))
					return false;
			}

            return true;
		}

		private bool IsDateWithinRange(DateTime date, DateTime startDate, DateTime endDate)
		{
			return ((date.Date >= startDate) && (date.Date < endDate));
		}

		public void UpdateTasks(TaskList tasks,
						UIExtension.UpdateType type,
						System.Collections.Generic.HashSet<UIExtension.TaskAttribute> attribs)
		{
            UInt32 selTaskId = SelectedAppointmentId;

			switch (type)
			{
				case UIExtension.UpdateType.Delete:
				case UIExtension.UpdateType.All:
					// Rebuild
					m_Items.Clear();
                    SelectedAppointment = null;
					break;

				case UIExtension.UpdateType.New:
				case UIExtension.UpdateType.Edit:
					// In-place update
					break;
			}

			Task task = tasks.GetFirstTask();

			while (task.IsValid() && ProcessTaskUpdate(task, type, attribs))
				task = task.GetNextTask();

			SelectionStart = SelectionEnd;

            AdjustScrollbar();
            Invalidate();
        }

		private bool ProcessTaskUpdate(Task task,
									   UIExtension.UpdateType type,
									   System.Collections.Generic.HashSet<UIExtension.TaskAttribute> attribs)
		{
			if (!task.IsValid())
				return false;

			CalendarItem item;
			UInt32 taskID = task.GetID();

			if (m_Items.TryGetValue(taskID, out item))
			{
				item.UpdateTaskAttributes(task, type, attribs);
			}
			else
			{
				item = new CalendarItem();
				item.UpdateTaskAttributes(task, type, null);
			}

			m_Items[taskID] = item;

			// Process children
			Task subtask = task.GetFirstSubtask();

			while (subtask.IsValid() && ProcessTaskUpdate(subtask, type, attribs))
				subtask = subtask.GetNextTask();

			return true;
		}

		public Boolean TaskColorIsBackground
		{
			get { return m_Renderer.TaskColorIsBackground; }
			set
			{
				if (m_Renderer.TaskColorIsBackground != value)
				{
					m_Renderer.TaskColorIsBackground = value;
					Invalidate();
				}
			}
		}

		public Boolean ShowParentsAsFolder
		{
			get { return m_Renderer.ShowParentsAsFolder; }
			set
			{
				if (m_Renderer.ShowParentsAsFolder != value)
				{
					m_Renderer.ShowParentsAsFolder = value;
					Invalidate();
				}
			}
		}

        public void SetFont(String fontName, int fontSize)
        {
            m_Renderer.SetFont(fontName, fontSize);

            LongAppointmentHeight = Math.Max(m_Renderer.BaseFont.Height + 4, 17);
        }
        
        public int GetFontHeight()
        {
            return m_Renderer.GetFontHeight();
        }

   		public void SetUITheme(UITheme theme)
		{
            m_Renderer.Theme = theme;
            Invalidate(true);
		}

		public override DateTime GetDateAt(int x, bool longAppt)
		{
			DateTime date = base.GetDateAt(x, longAppt);

			if (longAppt && (date >= EndDate))
			{
				date = EndDate.AddSeconds(-1);
			}

			return date;
		}

        public override TimeSpan GetTimeAt(int y)
        {
            TimeSpan time = base.GetTimeAt(y);
            
            if (time == new TimeSpan(1, 0, 0, 0))
                time = time.Subtract(new TimeSpan(0, 0, 1));

            return time;
        }

		protected override void DrawAppointment(Graphics g, Rectangle rect, Calendar.Appointment appointment, bool isSelected, Rectangle gripRect)
		{
			// Our custom gripper bar
			gripRect = rect;
			gripRect.Inflate(-2, -2);
			gripRect.Width = 5;

			// If the start date precedes the start of the week then extend the
			// draw rect to the left so the edge is clipped and likewise for the right.
			if (appointment.StartDate < StartDate)
			{
                rect.X -= 4;
                rect.Width += 4;

				gripRect.X = rect.X;
				gripRect.Width = 0;
			}
			else if (appointment.EndDate >= EndDate)
			{
				rect.Width += 5;
			}
			
			m_Renderer.DrawAppointment(g, rect, appointment, isSelected, gripRect);
		}

		private void OnResolveAppointments(object sender, Calendar.ResolveAppointmentsEventArgs args)
		{
			var appts =	new System.Collections.Generic.List<Calendar.Appointment>();

			foreach (System.Collections.Generic.KeyValuePair<UInt32, CalendarItem> item in m_Items)
			{
				if (IsItemWithinRange(item.Value, args.StartDate, args.EndDate))
					appts.Add(item.Value);
			}

			args.Appointments = appts;
		}

        private void OnSelectionChanged(object sender, Calendar.AppointmentEventArgs args)
        {
            if (args.Appointment != null)
                m_SelectedTaskID = args.Appointment.Id;
        }

        private void OnWeekChanged(object sender, Calendar.WeekChangeEventArgs args)
        {
            FixupSelection(false);
        }

        protected override void OnGotFocus(EventArgs e)
        {
            base.OnGotFocus(e);

            Invalidate();
            Update();
        }

        protected override void OnLostFocus(EventArgs e)
        {
            base.OnLostFocus(e);

            Invalidate();
            Update();
        }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            var selTool = ActiveTool as Calendar.SelectionTool;

            if (selTool == null)
                selTool = new Calendar.SelectionTool();

            if (selTool.IsResizing())
            {
                if (ReadOnly)
                    return;
            }
            else // Extra-over cursor handling
            {
                Calendar.Appointment appointment = GetAppointmentAt(e.Location.X, e.Location.Y);
                Cursor = Cursors.Default;

                if (!ReadOnly && (appointment != null))
                {
                    selTool.DayView = this;
                    selTool.UpdateCursor(e, appointment);

                    var taskItem = (appointment as CalendarItem);

                    if (taskItem != null)
                    {
                        Cursor temp = null;

                        if (taskItem.IsLocked)
                        {
                            temp = UIExtension.AppCursor(UIExtension.AppCursorType.LockedTask);
                        }
                        else if (taskItem.IconRect.Contains(e.Location))
                        {
                            temp = UIExtension.HandCursor();
                        }

                        if (temp != null)
                            Cursor = temp;
                    }
                }
            }

            base.OnMouseMove(e);
        }
	}
}
