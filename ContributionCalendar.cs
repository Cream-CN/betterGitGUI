using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace betterGitGUI
{
    /// <summary>
    /// GitHub 贡献图样式的选日控件：53 列 × 7 行的方格热力图，最后一周就是本周。
    ///
    /// 交互：单击切换某一天的选中状态，按住拖动可以连续涂选/擦除；
    /// 绿色深浅对应「当天计划生成的提交数」，所以预览之后格子的浓淡就是最终贡献图的样子。
    /// 未来的日期画成浅灰且不可选（未来的提交不会进入 GitHub 贡献图）。
    /// </summary>
    public class ContributionCalendar : Control
    {
        /// <summary>选中日期集合发生变化时触发。</summary>
        public event EventHandler SelectionChanged;

        private const int CellSize = 13;
        private const int CellGap = 3;
        private const int Step = CellSize + CellGap;
        private const int ColumnCount = 53;
        private const int RowCount = 7;
        private const int LeftPadding = 30;   // 留给左侧「一 / 三 / 五」的宽度
        private const int TopPadding = 18;    // 留给顶部月份标签的高度

        private static readonly Color EmptyColor = Color.FromArgb(235, 237, 240);
        private static readonly Color FutureColor = Color.FromArgb(246, 247, 249);
        /// <summary>已选、但这次计划里没有提交的日期（比如按文件拆分时提交比天数少）。</summary>
        private static readonly Color IdleSelectedColor = Color.FromArgb(217, 242, 224);
        private static readonly Color HoverRing = Color.FromArgb(87, 96, 106);
        private static readonly Color TodayRing = Color.FromArgb(31, 35, 40);
        private static readonly Color LabelColor = Color.FromArgb(87, 96, 106);

        /// <summary>GitHub 的四档绿：1 个 / 2-3 个 / 4-6 个 / 7 个以上。</summary>
        private static readonly Color[] LevelColors = new Color[]
        {
            Color.FromArgb(155, 233, 168),
            Color.FromArgb(64, 196, 99),
            Color.FromArgb(48, 161, 78),
            Color.FromArgb(33, 110, 57),
        };

        private static readonly Font MonthFont = new Font("Microsoft YaHei UI", 7.5F);
        private static readonly Font DayFont = new Font("Microsoft YaHei UI", 7.5F);

        private DateTime _start;                  // 第一列（最左侧一周）的周日
        private DateTime _todayDate;
        private readonly HashSet<DateTime> _selected = new HashSet<DateTime>();
        private Dictionary<DateTime, int> _counts; // 预览后每天的提交数，用来决定格子深浅

        private DateTime? _hover;
        private bool _dragging;
        private bool _paintSelecting;             // 拖动时本次是「涂上」还是「擦掉」
        private ToolTip _tip;

        public ContributionCalendar()
        {
            SetStyle(ControlStyles.OptimizedDoubleBuffer
                   | ControlStyles.AllPaintingInWmPaint
                   | ControlStyles.UserPaint
                   | ControlStyles.ResizeRedraw
                   | ControlStyles.Selectable, true);
            UpdateStyles();

            BackColor = Color.White;
            Size = new Size(LeftPadding + (ColumnCount - 1) * Step + CellSize + 2,
                            TopPadding + (RowCount - 1) * Step + CellSize + 2);
            MinimumSize = Size;

            _tip = new ToolTip();
            _tip.ReshowDelay = 200;

            ResetRange();
        }

        // ---------- 对外接口 ----------

        /// <summary>已选日期（按时间升序）。</summary>
        public List<DateTime> SelectedDays
        {
            get
            {
                var list = new List<DateTime>(_selected);
                list.Sort();
                return list;
            }
        }

        public int SelectedDayCount { get { return _selected.Count; } }

        public bool HasSelection { get { return _selected.Count > 0; } }

        /// <summary>清空所有选择。</summary>
        public void ClearSelection()
        {
            if (_selected.Count == 0) return;
            _selected.Clear();
            _counts = null;
            Invalidate();
            RaiseChanged();
        }

        /// <summary>选中「含今天在内」的最近 days 天。</summary>
        public void SelectRecentDays(int days)
        {
            if (days < 1) days = 1;
            if (days > ColumnCount * RowCount) days = ColumnCount * RowCount;
            var today = DateTime.Today;
            var list = new List<DateTime>();
            for (int i = 0; i < days; i++) list.Add(today.AddDays(-i));
            SetSelectedDays(list, true);
        }

        /// <summary>直接设定选中集合（超出范围或未来的日期会被忽略）。</summary>
        public void SetSelectedDays(IEnumerable<DateTime> days, bool raiseEvent)
        {
            if (days == null) return;
            _selected.Clear();
            var today = DateTime.Today;
            foreach (var d in days)
            {
                var day = d.Date;
                if (day > today || day < _start) continue;
                _selected.Add(day);
            }
            Invalidate();
            if (raiseEvent) RaiseChanged();
        }

        /// <summary>预览生成后，把「每天几个提交」传进来，格子随之变浓变浅。</summary>
        public void SetDayCounts(Dictionary<DateTime, int> counts)
        {
            _counts = counts;
            Invalidate();
        }

        /// <summary>清除深浅信息，回到统一的绿色。</summary>
        public void ClearDayCounts()
        {
            if (_counts == null) return;
            _counts = null;
            Invalidate();
        }

        // ---------- 时间范围 ----------

        private static DateTime WeekStartOf(DateTime d)
        {
            // 网格和 GitHub 一致：一行一周，周一列到周日列，以周日开头
            return d.Date.AddDays(-(int)d.Date.DayOfWeek);
        }

        private void ResetRange()
        {
            _todayDate = DateTime.Today;
            _start = WeekStartOf(_todayDate).AddDays(-7 * (ColumnCount - 1));
        }

        private void EnsureRange()
        {
            if (_todayDate != DateTime.Today)
            {
                var selected = SelectedDays;
                ResetRange();
                // 日期窗口整体前移之后，滑出窗口的老日期就不该再算作「已选」
                var today = _todayDate;
                foreach (var d in selected)
                {
                    if (d < _start || d > today) _selected.Remove(d);
                }
            }
        }

        // ---------- 绘制 ----------

        protected override void OnPaint(PaintEventArgs e)
        {
            EnsureRange();

            var g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.ClearTypeGridFit;
            g.Clear(BackColor);

            DrawMonthLabels(g);
            DrawWeekdayLabels(g);
            DrawCells(g);
        }

        private void DrawMonthLabels(Graphics g)
        {
            int lastMonth = -1;
            float lastX = float.MinValue;
            using (var brush = new SolidBrush(LabelColor))
            {
                for (int c = 0; c < ColumnCount; c++)
                {
                    var d = _start.AddDays(c * 7);
                    if (d.Month == lastMonth) continue;
                    lastMonth = d.Month;
                    float x = LeftPadding + c * Step;
                    // 相邻两个月份标签挨得太近会被挤成一团，直接跳过
                    if (x < lastX + 26F) continue;
                    lastX = x;
                    g.DrawString(d.Month + "月", MonthFont, brush, x, 0F);
                }
            }
        }

        private void DrawWeekdayLabels(Graphics g)
        {
            string[] names = new string[] { "日", "一", "二", "三", "四", "五", "六" };
            using (var brush = new SolidBrush(LabelColor))
            {
                for (int r = 1; r < RowCount; r += 2) // 和 GitHub 一样只标一、三、五
                {
                    float y = TopPadding + r * Step;
                    var size = g.MeasureString(names[r], DayFont);
                    g.DrawString(names[r], DayFont, brush,
                        LeftPadding - 4 - size.Width, y + (CellSize - size.Height) / 2F);
                }
            }
        }

        private void DrawCells(Graphics g)
        {
            for (int c = 0; c < ColumnCount; c++)
            {
                for (int r = 0; r < RowCount; r++)
                {
                    var day = _start.AddDays(c * 7 + r);
                    var rect = new Rectangle(LeftPadding + c * Step, TopPadding + r * Step, CellSize, CellSize);

                    Color fill;
                    if (day > _todayDate) fill = FutureColor;
                    else if (_selected.Contains(day)) fill = LevelColorOf(day);
                    else fill = EmptyColor;

                    FillRoundRect(g, rect, 2, fill);

                    // 今天描一圈深色边框，方便定位；鼠标悬停的格子描灰框
                    if (day == _todayDate || (_hover.HasValue && _hover.Value == day))
                    {
                        var ring = day == _todayDate ? TodayRing : HoverRing;
                        using (var pen = new Pen(ring, 1.3F))
                            g.DrawPath(pen, RoundRectPath(Rectangle.Inflate(rect, 0, 0), 2));
                    }
                }
            }
        }

        private Color LevelColorOf(DateTime day)
        {
            // 还没生成预览：统一用中等绿，表示「已选」
            if (_counts == null) return LevelColors[1];

            int n = _counts.ContainsKey(day) ? _counts[day] : 0;
            if (n <= 0) return IdleSelectedColor; // 选了，但这次计划没轮到它
            if (n == 1) return LevelColors[0];
            if (n <= 3) return LevelColors[1];
            if (n <= 6) return LevelColors[2];
            return LevelColors[3];
        }

        private static void FillRoundRect(Graphics g, Rectangle r, int radius, Color color)
        {
            using (var path = RoundRectPath(r, radius))
            using (var brush = new SolidBrush(color))
                g.FillPath(brush, path);
        }

        private static GraphicsPath RoundRectPath(Rectangle r, int radius)
        {
            var path = new GraphicsPath();
            int d = radius * 2;
            if (d <= 0 || d > r.Width || d > r.Height)
            {
                path.AddRectangle(r);
                return path;
            }
            path.AddArc(r.X, r.Y, d, d, 180, 90);
            path.AddArc(r.Right - d, r.Y, d, d, 270, 90);
            path.AddArc(r.Right - d, r.Bottom - d, d, d, 0, 90);
            path.AddArc(r.X, r.Bottom - d, d, d, 90, 90);
            path.CloseFigure();
            return path;
        }

        // ---------- 交互 ----------

        private DateTime? HitTest(Point p)
        {
            int relX = p.X - LeftPadding;
            int relY = p.Y - TopPadding;
            if (relX < 0 || relY < 0) return null;

            int c = relX / Step;
            int r = relY / Step;
            if (c < 0 || c >= ColumnCount || r < 0 || r >= RowCount) return null;
            // 落在格子之间的空隙里不算
            if (relX % Step >= CellSize || relY % Step >= CellSize) return null;

            var day = _start.AddDays(c * 7 + r);
            if (day > _todayDate) return null;
            return day;
        }

        protected override void OnMouseDown(MouseEventArgs e)
        {
            base.OnMouseDown(e);
            if (e.Button != MouseButtons.Left) return;

            Focus();
            var day = HitTest(e.Location);
            if (day == null) return;

            _dragging = true;
            _paintSelecting = !_selected.Contains(day.Value);
            Capture = true;
            _tip.Hide(this);
            Apply(day.Value, _paintSelecting);
        }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            base.OnMouseMove(e);
            var day = HitTest(e.Location);

            if (_dragging)
            {
                if (day != null) Apply(day.Value, _paintSelecting);
                return;
            }

            if (day != _hover)
            {
                _hover = day;
                Invalidate();
                // 只在换格子的时候刷新提示，否则跟着鼠标一直重弹会闪
                if (day.HasValue)
                    _tip.Show(BuildTip(day.Value), this, e.X + 12, e.Y + 14, 2500);
                else
                    _tip.Hide(this);
            }
        }

        private string BuildTip(DateTime day)
        {
            string[] weekNames = new string[] { "周日", "周一", "周二", "周三", "周四", "周五", "周六" };
            string head = day.ToString("yyyy-MM-dd") + " " + weekNames[(int)day.DayOfWeek];
            if (!_selected.Contains(day))
                return head + "\n未选：单击选中，按住拖动可连续涂选";
            if (_counts != null && _counts.ContainsKey(day))
                return head + "\n已选 · 计划 " + _counts[day] + " 个提交";
            return head + "\n已选（再点一下取消）";
        }

        protected override void OnMouseUp(MouseEventArgs e)
        {
            base.OnMouseUp(e);
            if (e.Button != MouseButtons.Left) return;
            _dragging = false;
            Capture = false;
        }

        protected override void OnMouseLeave(EventArgs e)
        {
            base.OnMouseLeave(e);
            if (_dragging) return;
            if (_hover != null)
            {
                _hover = null;
                Invalidate();
            }
            _tip.Hide(this);
        }

        private void Apply(DateTime day, bool select)
        {
            bool changed = select ? _selected.Add(day) : _selected.Remove(day);
            if (!changed) return;
            _counts = null; // 选择变了，旧的提交数分布就作废，等下次预览再画
            Invalidate();
            RaiseChanged();
        }

        private void RaiseChanged()
        {
            if (SelectionChanged != null) SelectionChanged(this, EventArgs.Empty);
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing && _tip != null)
            {
                _tip.Dispose();
                _tip = null;
            }
            base.Dispose(disposing);
        }
    }
}
