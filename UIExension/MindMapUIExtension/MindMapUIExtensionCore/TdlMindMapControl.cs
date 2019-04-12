using System;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;
using System.Collections.Generic;
using System.Windows.Forms.VisualStyles;

using Abstractspoon.Tdl.PluginHelpers;
using Abstractspoon.Tdl.PluginHelpers.ColorUtil;

namespace MindMapUIExtension
{
    public delegate Boolean EditTaskLabelEventHandler(object sender, UInt32 taskId);
    public delegate Boolean EditTaskIconEventHandler(object sender, UInt32 taskId);
    public delegate Boolean EditTaskCompletionEventHandler(object sender, UInt32 taskId, bool completed);

	public class MindMapTaskItem
	{
		// Data
		private String m_Title;
		private UInt32 m_TaskID;
        private UInt32 m_ParentID;
		private Color m_TextColor;
		private Boolean m_HasIcon;
		private Boolean m_IsFlagged;
		private Boolean m_IsParent;
        private Boolean m_IsDone;
        private Boolean m_IsGoodAsDone;
        private Boolean m_SomeSubtasksDone;
		private Boolean m_IsLocked;

		// -----------------------------------------------------------------

		public MindMapTaskItem(String label)
		{
			m_Title = label;
			m_TaskID = 0;
            m_ParentID = 0;
			m_TextColor = new Color();
			m_HasIcon = false;
			m_IsFlagged = false;
			m_IsParent = false;
			m_IsDone = false;
            m_IsGoodAsDone = false;
            m_SomeSubtasksDone = false;
			m_IsLocked = false;
		}

		public MindMapTaskItem(Task task)
		{
			m_Title = task.GetTitle();
			m_TaskID = task.GetID();
			m_ParentID = task.GetParentID();
			m_TextColor = task.GetTextDrawingColor();
			m_HasIcon = (task.GetIcon().Length > 0);
			m_IsFlagged = task.IsFlagged(false);
			m_IsParent = task.IsParent();
            m_IsDone = task.IsDone();
            m_IsGoodAsDone = task.IsGoodAsDone();
            m_SomeSubtasksDone = task.HasSomeSubtasksDone();
			m_IsLocked = task.IsLocked(true);
		}
        
		public void FixupParentID(MindMapTaskItem parent)
		{
            m_ParentID = parent.ID;
		}

        public Boolean FixupParentalStatus(int nodeCount, UIExtension.TaskIcon taskIcons)
        {
            bool wasParent = m_IsParent;

            if (nodeCount == 0)
            {
                m_IsParent = (!m_HasIcon && taskIcons.Get(m_TaskID));
            }
            else
            {
                m_IsParent = true;
            }

            return (m_IsParent != wasParent);
        }

		public override string ToString() 
		{
			return Title;
		}

		public void Update(Task task, HashSet<UIExtension.TaskAttribute> attribs)
		{
			// TODO
		}

		public String Title 
		{ 
			get 
			{ 
#if DEBUG
				return String.Format("{0} ({1})", m_Title, m_TaskID); 
#else
				return m_Title;
#endif
			} 

			set // only works for the root
			{
				if (!IsTask && !String.IsNullOrWhiteSpace(value))
					m_Title = value;
			}
		}
		
		public UInt32 ID { get { return m_TaskID; } }
        public UInt32 ParentID { get { return m_ParentID; } }
		public Color TextColor { get { return m_TextColor; } }
		public Boolean HasIcon { get { return m_HasIcon; } }
		public Boolean IsFlagged { get { return m_IsFlagged; } }
		public Boolean IsParent { get { return m_IsParent; } }
		public Boolean IsLocked { get { return m_IsLocked; } }
        public Boolean IsTask { get { return (m_TaskID != 0); } }
        public Boolean HasSomeSubtasksDone { get { return m_SomeSubtasksDone; } }

        public Boolean IsDone(bool includeGoodAsDone) 
        { 
            if (includeGoodAsDone && m_IsGoodAsDone)
                return true;

            return m_IsDone; 
        }

		public bool ProcessTaskUpdate(Task task, HashSet<UIExtension.TaskAttribute> attribs)
		{
			if (task.GetID() != m_TaskID)
				return false;

			if (attribs.Contains(UIExtension.TaskAttribute.Title))
				m_Title = task.GetTitle();

			if (attribs.Contains(UIExtension.TaskAttribute.Icon))
				m_HasIcon = (task.GetIcon().Length > 0);

			if (attribs.Contains(UIExtension.TaskAttribute.Flag))
				m_IsFlagged = task.IsFlagged(false);

			if (attribs.Contains(UIExtension.TaskAttribute.Color))
				m_TextColor = task.GetTextDrawingColor();

            if (attribs.Contains(UIExtension.TaskAttribute.SubtaskDone))
                m_SomeSubtasksDone = task.HasSomeSubtasksDone();

            if (attribs.Contains(UIExtension.TaskAttribute.DoneDate))
                m_IsDone = task.IsDone();

			m_IsParent = task.IsParent();
			m_IsLocked = task.IsLocked(true);
            m_IsGoodAsDone = task.IsGoodAsDone();

			return true;
		}
	}

	// ------------------------------------------------------------

	[System.ComponentModel.DesignerCategory("")]

	public class TdlMindMapControl : MindMapControl
	{
		public event EditTaskLabelEventHandler      EditTaskLabel;
        public event EditTaskIconEventHandler       EditTaskIcon;
        public event EditTaskCompletionEventHandler EditTaskDone;

		// From Parent
		private Translator m_Trans;
		private UIExtension.TaskIcon m_TaskIcons;

		private UIExtension.SelectionRect m_SelectionRect;
		private Dictionary<UInt32, MindMapTaskItem> m_Items;

		private Boolean m_ShowParentAsFolder;
		private Boolean m_TaskColorIsBkgnd;
		private Boolean m_IgnoreMouseClick;
        private Boolean m_ShowCompletionCheckboxes;
		private Boolean m_StrikeThruDone;

		private TreeNode m_PreviouslySelectedNode;
		private Timer m_EditTimer;
        private Font m_BoldLabelFont, m_DoneLabelFont, m_BoldDoneLabelFont;
        private Size m_CheckboxSize;

		// -------------------------------------------------------------------------

		public TdlMindMapControl(Translator trans, UIExtension.TaskIcon icons)
		{
			m_Trans = trans;
			m_TaskIcons = icons;

			m_SelectionRect = new UIExtension.SelectionRect();
			m_Items = new Dictionary<UInt32, MindMapTaskItem>();

			m_TaskColorIsBkgnd = false;
			m_IgnoreMouseClick = false;
			m_ShowParentAsFolder = false;
            m_ShowCompletionCheckboxes = true;
			m_StrikeThruDone = true;

			m_EditTimer = new Timer();
			m_EditTimer.Interval = 500;
			m_EditTimer.Tick += new EventHandler(OnEditLabelTimer);

            using (Graphics graphics = Graphics.FromHwnd(Handle))
                m_CheckboxSize = CheckBoxRenderer.GetGlyphSize(graphics, CheckBoxState.UncheckedNormal);
		}
        
        public void SetStrikeThruDone(bool strikeThruDone)
		{
			m_StrikeThruDone = strikeThruDone;

			if (m_BoldLabelFont != null)
				SetFont(m_BoldLabelFont.Name, (int)m_BoldLabelFont.Size, m_StrikeThruDone);
		}

		public new void SetFont(String fontName, int fontSize)
		{
			SetFont(fontName, fontSize, m_StrikeThruDone);
		}

		protected void SetFont(String fontName, int fontSize, bool strikeThruDone)
		{
			bool baseFontChange = ((m_BoldLabelFont == null) || (m_BoldLabelFont.Name != fontName) || (m_BoldLabelFont.Size != fontSize));
            bool doneFontChange = (baseFontChange || (m_BoldDoneLabelFont.Strikeout != strikeThruDone));

            if (baseFontChange)
                m_BoldLabelFont = new Font(fontName, fontSize, FontStyle.Bold);

            if (doneFontChange)
            {
                if (strikeThruDone)
                {
                    m_BoldDoneLabelFont = new Font(fontName, fontSize, FontStyle.Bold | FontStyle.Strikeout);
                    m_DoneLabelFont = new Font(fontName, fontSize, FontStyle.Strikeout);
                }
                else
                {
                    m_BoldDoneLabelFont = m_BoldLabelFont;
                    m_DoneLabelFont = null;
                }
            }

            if ((baseFontChange || doneFontChange) && RefreshItemFont(RootNode, true))
                RecalculatePositions();
            
            base.SetFont(fontName, fontSize);
        }

		public void UpdateTasks(TaskList tasks,
								UIExtension.UpdateType type,
								HashSet<UIExtension.TaskAttribute> attribs)
		{
			switch (type)
			{
				case UIExtension.UpdateType.Edit:
				case UIExtension.UpdateType.New:
					UpdateTaskAttributes(tasks, attribs);
					break;

				case UIExtension.UpdateType.Delete:
				case UIExtension.UpdateType.All:
					RebuildTreeView(tasks);
					break;

				case UIExtension.UpdateType.Unknown:
					return;
			}
		}

		public Boolean SelectNodeWasPreviouslySelected
		{
			get { return (SelectedNode == m_PreviouslySelectedNode); }
		}

		public Boolean TaskColorIsBackground
		{
			get { return m_TaskColorIsBkgnd; }
			set
			{
				if (m_TaskColorIsBkgnd != value)
				{
					m_TaskColorIsBkgnd = value;
					Invalidate();
				}
			}
		}

		public Boolean ShowParentsAsFolders
		{
			get { return m_ShowParentAsFolder; }
			set
			{
				if (m_ShowParentAsFolder != value)
				{
					m_ShowParentAsFolder = value;
					Invalidate();
				}
			}
		}

        public Boolean ShowCompletionCheckboxes
        {
            get { return m_ShowCompletionCheckboxes; }
            set
            {
                if (m_ShowCompletionCheckboxes != value)
                {
                    m_ShowCompletionCheckboxes = value;
                    RecalculatePositions();
                }
            }
        }

        public bool WantTaskUpdate(UIExtension.TaskAttribute attrib)
        {
            switch (attrib)
            {
                // Note: lock state is always provided
                case UIExtension.TaskAttribute.Title:
                case UIExtension.TaskAttribute.Icon:
                case UIExtension.TaskAttribute.Flag:
                case UIExtension.TaskAttribute.Color:
                case UIExtension.TaskAttribute.DoneDate:
			    case UIExtension.TaskAttribute.Position:
			    case UIExtension.TaskAttribute.SubtaskDone:
					return true;
            }

            // all else
            return false;
        }
        		
		public UInt32 HitTest(Point screenPos)
		{
			var clientPos = PointToClient(screenPos);
			var node = HitTestPositions(clientPos);

			if (node != null)
				return UniqueID(node);
			
			// else
			return 0;
		}

		public new Rectangle GetSelectedItemLabelRect()
		{
			var labelRect = base.GetSelectedItemLabelRect();

			labelRect.X += GetExtraWidth(SelectedNode);

			return labelRect;
		}

		public Boolean CanMoveTask(UInt32 taskId, UInt32 destParentId, UInt32 destPrevSiblingId)
		{
			if (FindNode(taskId) == null)
				return false;

			if (FindNode(destParentId) == null)
				return false;

			if ((destPrevSiblingId != 0) && (FindNode(destPrevSiblingId) == null))
				return false;

			return true;
		}

		public Boolean MoveTask(UInt32 taskId, UInt32 destParentId, UInt32 destPrevSiblingId)
		{
			BeginUpdate();

            var node = FindNode(taskId);
   			var prevParentNode = node.Parent;

			var destParentNode = FindNode(destParentId);
			var destPrevSiblingNode = FindNode(destPrevSiblingId);

			if ((node == null) || (destParentNode == null))
				return false;

			var srcParentNode = node.Parent;
			int srcPos = srcParentNode.Nodes.IndexOf(node);

			srcParentNode.Nodes.RemoveAt(srcPos);

			int destPos = 0; // insert at top
			
			if (destPrevSiblingNode != null)
				destPos = (destParentNode.Nodes.IndexOf(destPrevSiblingNode) + 1);

			destParentNode.Nodes.Insert(destPos, node);

            FixupParentalStatus(destParentNode, 1);
            FixupParentalStatus(prevParentNode, -1);

            FixupParentID(node, destParentNode);

			SelectedNode = node;

			EndUpdate();
			EnsureItemVisible(Item(node));

			return true;
		}

		public bool GetTask(UIExtension.GetTask getTask, ref UInt32 taskID)
		{
			TreeNode node = FindNode(taskID);

			switch (getTask)
			{
				case UIExtension.GetTask.GetNextTask:
					if (node.NextVisibleNode != null)
					{
						taskID = UniqueID(node.NextVisibleNode);
						return true;
					}
					break;

				case UIExtension.GetTask.GetPrevTask:
					if (node.PrevVisibleNode != null)
					{
						taskID = UniqueID(node.PrevVisibleNode);
						return true;
					}
					break;

				case UIExtension.GetTask.GetNextTopLevelTask:
					{
						var topLevelParent = TopLevelParent(node);

						if ((topLevelParent != null) && (topLevelParent.NextNode != null))
						{
							taskID = UniqueID(topLevelParent.NextNode);
							return true;
						}
					}
					break;

				case UIExtension.GetTask.GetPrevTopLevelTask:
					{
						var topLevelParent = TopLevelParent(node);

						if ((topLevelParent != null) && (topLevelParent.PrevNode != null))
						{
							taskID = UniqueID(topLevelParent.PrevNode);
							return true;
						}
					}
					break;
			}

			// all else
			return false;
		}

        public Boolean CanSaveToImage()
        {
            return !IsEmpty();
        }
        		
        // Internal ------------------------------------------------------------

        override protected int ScaleByDPIFactor(int value)
        {
            return DPIScaling.Scale(value);
        }

        private bool RefreshItemFont(TreeNode node, Boolean andChildren)
        {
            var taskItem = TaskItem(node);

            if (taskItem == null)
                return false;

            Font curFont = node.NodeFont, newFont = null;

            if (taskItem.IsTask)
            {
                if (taskItem.ParentID == 0)
                {
                    if (taskItem.IsDone(false))
                        newFont = m_BoldDoneLabelFont;
                    else
                        newFont = m_BoldLabelFont;
                }
                else if (taskItem.IsDone(false))
                {
                    newFont = m_DoneLabelFont;
                }
            }

            bool fontChange = (newFont != curFont);

            if (fontChange)
                node.NodeFont = newFont;
            
            // children
            if (andChildren)
            {
                foreach (TreeNode childNode in node.Nodes)
                    fontChange |= RefreshItemFont(childNode, true);
            }

            return fontChange;
        }

		protected MindMapTaskItem TaskItem(TreeNode node)
		{
			if (node == null)
				return null;

			return (ItemData(node) as MindMapTaskItem);
		}

		protected MindMapTaskItem TaskItem(Object itemData)
		{
			if (itemData == null)
				return null;

			return (itemData as MindMapTaskItem);
		}

		protected TreeNode TopLevelParent(TreeNode node)
		{
			while ((node != null) && !IsRoot(node))
			{
				var parentNode = node.Parent;

				if (IsRoot(parentNode))
					return node;

				node = parentNode;
			}

			// else node was already null or root
			return null;
		}

		protected void UpdateTaskAttributes(TaskList tasks,
								HashSet<UIExtension.TaskAttribute> attribs)
		{
			var rootItem = TaskItem(RootNode);

			if ((rootItem != null) && !rootItem.IsTask)
			{
				var projName = GetProjectName(tasks);

				if (!projName.Equals(rootItem.Title))
				{
					rootItem.Title = projName;
					RootNode.Text = projName;
				}
			}

			var changedTaskIds = new HashSet<UInt32>();
			Task task = tasks.GetFirstTask();

			while (task.IsValid() && ProcessTaskUpdate(task, attribs, changedTaskIds))
				task = task.GetNextTask();

			if (attribs.Contains(UIExtension.TaskAttribute.Title))
			{
				foreach (var id in changedTaskIds)
					RefreshNodeLabel(id, false);
			}

			RecalculatePositions();
		}

		private bool ProcessTaskUpdate(Task task,
									   HashSet<UIExtension.TaskAttribute> attribs,
									   HashSet<UInt32> taskIds)
		{
			if (!task.IsValid())
				return false;

			MindMapTaskItem item;
            UInt32 taskId = task.GetID();
			bool newTask = !m_Items.TryGetValue(taskId, out item);

			if (newTask)
			{
				var parentId = task.GetParentTask().GetID();
				var parentNode = FindNode(parentId);

				AddTaskToTree(task, parentNode, true);
			}
			else if (item.ProcessTaskUpdate(task, attribs))
			{
				// Process children
				Task subtask = task.GetFirstSubtask();

				while (subtask.IsValid() && ProcessTaskUpdate(subtask, attribs, taskIds))
					subtask = subtask.GetNextTask();
			}
			else
			{
				return false;
			}

			taskIds.Add(task.GetID());

			return true;
		}

		protected void RebuildTreeView(TaskList tasks)
		{
			// Snapshot the expanded tasks so we can restore them afterwards
			var expandedIDs = GetExpandedItems(RootNode);

			// And the selection
			var selID = UniqueID(SelectedNode);
			
			Clear();

			if (tasks.GetTaskCount() == 0)
				return;

			BeginUpdate();

			var task = tasks.GetFirstTask();
			bool taskIsRoot = !task.GetNextTask().IsValid(); // no siblings

			TreeNode rootNode = null;

			if (taskIsRoot)
			{
                var taskItem = new MindMapTaskItem(task);

                m_Items.Add(taskItem.ID, taskItem);
				rootNode = AddRootNode(taskItem, taskItem.ID);

                RefreshItemFont(rootNode, false);

				// First Child
				AddTaskToTree(task.GetFirstSubtask(), rootNode);
			}
			else
			{
				// There is more than one 'root' task so create a real root parent
				var projName = GetProjectName(tasks);
				rootNode = AddRootNode(new MindMapTaskItem(projName));

				AddTaskToTree(task, rootNode);
			}

			// Restore expanded state
			if (!SetExpandedItems(expandedIDs))
				rootNode.Expand();

			EndUpdate();
			SetSelectedNode(selID);
		}

		protected String GetProjectName(TaskList tasks)
		{
			String rootName = tasks.GetProjectName();

			if (!String.IsNullOrWhiteSpace(rootName))
				return rootName;

			// else
			return m_Trans.Translate("Root");
		}

		protected List<UInt32> GetExpandedItems(TreeNode node)
		{
			List<UInt32> expandedIDs = null;

			if ((node != null) && node.IsExpanded)
			{
				expandedIDs = new List<UInt32>();
    
                if (!IsRoot(node))
    				expandedIDs.Add(UniqueID(node));

				foreach (TreeNode child in node.Nodes)
				{
					var childIDs = GetExpandedItems(child);

					if (childIDs != null)
						expandedIDs.AddRange(childIDs);
				}
			}

			return expandedIDs;
		}

		protected Boolean SetExpandedItems(List<UInt32> expandedNodes)
		{
            if (expandedNodes == null)
                return false;

            if (expandedNodes.Count == 0)
                return false;

            bool someSucceeded = false;

			foreach (var id in expandedNodes)
			{
				var node = FindNode(id);

                if (node != null)
                {
                    node.Expand();
                    someSucceeded = true;
                }
			}

            return someSucceeded;
		}

		protected override Boolean IsAcceptableDropTarget(Object draggedItemData, Object dropTargetItemData, DropPos dropPos, bool copy)
		{
			if (dropPos == MindMapControl.DropPos.On)
				return !TaskItem(dropTargetItemData).IsLocked;

			// else
			return true;
		}

		protected override Boolean IsAcceptableDragSource(Object itemData)
		{
			return !TaskItem(itemData).IsLocked;
		}

		protected override Boolean DoDrop(MindMapDragEventArgs e)
		{
			TreeNode prevParentNode = e.dragged.node.Parent;

			if (!base.DoDrop(e) || e.copyItem)
				return false;

			if (e.targetParent.node != prevParentNode)
			{
                // Fixup parent states
                // Note: our tree nodes haven't been moved yet but 
                // the application will have updated it's data structures
                // so we have to account for this in the node count passed
                FixupParentalStatus(e.targetParent.node, 1);
                FixupParentalStatus(prevParentNode, -1);

                FixupParentID(e.dragged.node, e.targetParent.node);
			}

			return true;
		}

        void FixupParentalStatus(TreeNode parentNode, int nodeCountOffset)
        {
            var parentItem = TaskItem(parentNode);

            if (parentItem != null)
                parentItem.FixupParentalStatus((parentNode.Nodes.Count + nodeCountOffset), m_TaskIcons);
        }

        void FixupParentID(TreeNode node, TreeNode parent)
        {
            var taskItem = TaskItem(node);

            if (taskItem != null)
            {
                taskItem.FixupParentID(TaskItem(parent));
                RefreshItemFont(node, false);
            }
        }

        protected override void DrawNodeLabel(Graphics graphics, String label, Rectangle rect,
											  NodeDrawState nodeState, NodeDrawPos nodePos,
                                              Font nodeFont, Object itemData)
		{
            var taskItem = (itemData as MindMapTaskItem);
            bool isSelected = (nodeState != NodeDrawState.None);

            if (taskItem.IsTask) // real task
            {
                // Checkbox
                Rectangle checkRect = CalcCheckboxRect(rect);

                if (m_ShowCompletionCheckboxes)
                    CheckBoxRenderer.DrawCheckBox(graphics, checkRect.Location, GetItemCheckboxState(taskItem));

			    // Task icon
                if (TaskHasIcon(taskItem))
                {
                    Rectangle iconRect = CalcIconRect(rect);

                    if (m_TaskIcons.Get(taskItem.ID))
                        m_TaskIcons.Draw(graphics, iconRect.X, iconRect.Y);

                    rect.Width = (rect.Right - iconRect.Right - 2);
                    rect.X = iconRect.Right + 2;
                }
                else if (m_ShowCompletionCheckboxes)
                {
                    rect.Width = (rect.Right - checkRect.Right - 2);
                    rect.X = checkRect.Right + 2;
                }
            }
			
			// Text background
            Brush textColor = SystemBrushes.WindowText;
            Brush backColor = null;
            Color taskColor = taskItem.TextColor;

			if (!taskColor.IsEmpty)
			{
				if (m_TaskColorIsBkgnd && !isSelected && !taskItem.IsDone(true))
				{
					backColor = new SolidBrush(taskColor);
					textColor = new SolidBrush(DrawingColor.GetBestTextColor(taskColor));
				}
				else
				{
					if (nodeState != MindMapControl.NodeDrawState.None)
						taskColor = DrawingColor.SetLuminance(taskColor, 0.3f);

					textColor = new SolidBrush(taskColor);
				}
			}

			switch (nodeState)
			{
				case NodeDrawState.Selected:
                    m_SelectionRect.Draw(graphics, rect.X, rect.Y, rect.Width, rect.Height, this.Focused);
					break;

				case NodeDrawState.DropTarget:
                    m_SelectionRect.Draw(graphics, rect.X, rect.Y, rect.Width, rect.Height, false);
					break;

                case NodeDrawState.None:
                    {
                        if (backColor != null)
                        {
                            var prevSmoothing = graphics.SmoothingMode;
                            graphics.SmoothingMode = SmoothingMode.None;

                            graphics.FillRectangle(backColor, rect);
                            graphics.SmoothingMode = prevSmoothing;
                        }
                        
                        if (DebugMode())
                            graphics.DrawRectangle(new Pen(Color.Green), rect);
                    }
                    break;
			}

			// Text
			var format = DefaultLabelFormat(nodePos, isSelected);

            graphics.DrawString(label, nodeFont, textColor, rect, format);
		}

        CheckBoxState GetItemCheckboxState(MindMapTaskItem taskItem)
        {
            if (taskItem.IsDone(false))
                return CheckBoxState.CheckedNormal;

            if (taskItem.HasSomeSubtasksDone)
                return CheckBoxState.MixedNormal;

            // else
            return CheckBoxState.UncheckedNormal;
        }

		protected Boolean NodeHasIcon(TreeNode node)
		{
			return TaskHasIcon(TaskItem(node));
		}

        protected Boolean NodeIsTask(TreeNode node)
        {
            return TaskItem(node).IsTask;
        }

		protected Boolean TaskHasIcon(MindMapTaskItem taskItem)
		{
			return ((m_TaskIcons != null) &&
					(taskItem != null) &&
					taskItem.IsTask &&
					(taskItem.HasIcon || (m_ShowParentAsFolder && taskItem.IsParent)));
		}

		protected override void DrawNodeConnection(Graphics graphics, Point ptFrom, Point ptTo)
		{
			int midX = ((ptFrom.X + ptTo.X) / 2);

			graphics.DrawBezier(new Pen(base.ConnectionColor), 
								ptFrom,
								new Point(midX, ptFrom.Y),
								new Point(midX, ptTo.Y),
								ptTo);
		}

        private Rectangle CalcCheckboxRect(Rectangle labelRect)
        {
            if (!m_ShowCompletionCheckboxes)
                return Rectangle.Empty;

            int left = labelRect.X;
            int top = (CentrePoint(labelRect).Y - (m_CheckboxSize.Height / 2));

            return new Rectangle(left, top, m_CheckboxSize.Width, m_CheckboxSize.Height);
        }

        private Rectangle CalcIconRect(Rectangle labelRect)
		{
            int left = (labelRect.X + 2);
            
            if (m_ShowCompletionCheckboxes)
                left += m_CheckboxSize.Width;

            int imageSize = ScaleByDPIFactor(16);
            int top = (CentrePoint(labelRect).Y - (imageSize / 2));

            return new Rectangle(left, top, imageSize, imageSize);
		}

		private new void Clear()
		{
			m_Items.Clear();

			base.Clear();
		}

		private bool AddTaskToTree(Task task, TreeNode parent, bool select = false)
		{
			if (!task.IsValid())
				return true; // not an error

			var taskID = task.GetID();
			var taskItem = new MindMapTaskItem(task);

			var node = AddNode(taskItem, parent, taskID);

			if (node == null)
				return false;

            RefreshItemFont(node, false);

			// First Child
			if (!AddTaskToTree(task.GetFirstSubtask(), node))
				return false;

			// First Sibling
			if (!AddTaskToTree(task.GetNextTask(), parent))
				return false;

			m_Items.Add(taskID, taskItem);

			if (select)
				SelectedNode = node;

			return true;
		}

		protected override int GetExtraWidth(TreeNode node)
		{
            int extraWidth = 2;
            var taskItem = TaskItem(node);

            if (m_ShowCompletionCheckboxes && taskItem.IsTask)
                extraWidth += m_CheckboxSize.Width;

			if (TaskHasIcon(taskItem))
				extraWidth += (ScaleByDPIFactor(16) + 2);

			return extraWidth;
		}

		protected override int GetMinItemHeight()
		{
            return (ScaleByDPIFactor(16) + 2);
		}

		protected override void OnMouseClick(MouseEventArgs e)
		{
			base.OnMouseClick(e);

			if (e.Button != MouseButtons.Left)
				return;

			if (m_IgnoreMouseClick)
			{
				m_IgnoreMouseClick = false;
				return;
			}

			TreeNode node = HitTestPositions(e.Location);

			if ((node == null) || (node != SelectedNode) || !NodeIsTask(node))
				return;

			if (HitTestExpansionButton(node, e.Location))
				return;

            var taskItem = TaskItem(node);

            if (!ReadOnly && !taskItem.IsLocked)
            {
                if (HitTestCheckbox(node, e.Location))
                {
                    if (EditTaskDone != null)
                        EditTaskDone(this, UniqueID(SelectedNode), !taskItem.IsDone(false));
                }
                else if (HitTestIcon(node, e.Location))
                {
                    // Performing icon editing from a 'MouseUp' or 'MouseClick' event 
                    // causes the edit icon dialog to fail to correctly get focus but
                    // counter-intuitively it works from 'MouseDown'
                    //if (EditTaskIcon != null)
                    //    EditTaskIcon(this, UniqueID(SelectedNode));
                }
                else if (SelectNodeWasPreviouslySelected)
			    {
			        if (EditTaskLabel != null)
				        m_EditTimer.Start();
                }
            }
		}

        protected bool HitTestCheckbox(TreeNode node, Point point)
        {
            if (!m_ShowCompletionCheckboxes)
                return false;

            return CalcCheckboxRect(GetItemLabelRect(node)).Contains(point);
        }

        protected bool HitTestIcon(TreeNode node, Point point)
        {
			var taskItem = TaskItem(node);

			if (taskItem.IsLocked || !TaskHasIcon(taskItem))
				return false;

			// else
			return CalcIconRect(GetItemLabelRect(node)).Contains(point);
        }

		protected override void OnMouseDown(MouseEventArgs e)
		{
			m_EditTimer.Stop();

			switch (e.Button)
			{
				case MouseButtons.Left:
					{
						// Cache the previous selected item
						m_PreviouslySelectedNode = SelectedNode;
                        m_IgnoreMouseClick = false;

						TreeNode hit = HitTestPositions(e.Location);

                        if (hit != null)
                        {
                            m_IgnoreMouseClick = HitTestExpansionButton(hit, e.Location);

                            // Performing icon editing from a 'MouseUp' or 'MouseClick' event 
                            // causes the edit icon dialog to fail to correctly get focus but
                            // counter-intuitively it works from 'MouseDown'
                            if (!m_IgnoreMouseClick && !ReadOnly && HitTestIcon(hit, e.Location))
                            {
                                if (EditTaskIcon != null)
                                    EditTaskIcon(this, UniqueID(SelectedNode));
                            }
                        }
					}
					break;

				case MouseButtons.Right:
					{
						TreeNode hit = HitTestPositions(e.Location);

						if (hit != null)
							SelectedNode = hit;
					}
					break;
			}

			base.OnMouseDown(e);
		}

		protected void OnEditLabelTimer(object sender, EventArgs e)
		{
			m_EditTimer.Stop();

			if (EditTaskLabel != null)
				EditTaskLabel(this, UniqueID(SelectedNode));
		}

		protected override void OnMouseMove(MouseEventArgs e)
		{
 			base.OnMouseMove(e);

			var node = HitTestPositions(e.Location);

			if (!ReadOnly && (node != null) && !HitTestExpansionButton(node, e.Location))
			{
				var taskItem = TaskItem(node);

				if (taskItem != null)
                {
                    Cursor cursor = null;

                    if (taskItem.IsLocked)
                    {
                        cursor = UIExtension.AppCursor(UIExtension.AppCursorType.LockedTask);
                    }
                    else if (TaskHasIcon(taskItem) && HitTestIcon(node, e.Location))
                    {
                        cursor = UIExtension.HandCursor();
                    }
                    
                    if (cursor != null)
                    {
                        Cursor = cursor;
                        return;
                    }
				}
			}

			// all else
			Cursor = Cursors.Arrow;
		}
	}
}

