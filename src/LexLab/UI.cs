using LexLab.Core;
using System.Diagnostics;
using System.Drawing.Drawing2D;
using System.Drawing.Text;
using System.Text;

namespace LexLab.UI;


/// <summary>
/// 界面统一使用的一些字体、颜色与尺寸。
///
/// 关于尺寸：所有控件高度都不写死像素值，而是根据字体的实际行高动态计算
/// （Font.Height 会随 Windows 的显示缩放比例变化）。这样在 100%、125%、150%
/// 等不同缩放比例的显示器上，文字都不会被上下裁切。
/// </summary>
internal static class Ui
{
    public static readonly Font UiFont = new("微软雅黑", 9f);
    public static readonly Font UiFontSmall = new("微软雅黑", 8.5f);
    public static readonly Font TitleFont = new("微软雅黑", 9.5f, FontStyle.Bold);
    public static readonly Font MonoFont = new("Consolas", 10.5f);
    public static readonly Font MonoFontSmall = new("Consolas", 9.5f);

    public static readonly Color HeaderBack = Color.FromArgb(238, 243, 250);
    public static readonly Color BorderColor = Color.FromArgb(200, 210, 220);
    public static readonly Color AccentColor = Color.FromArgb(30, 90, 160);
    public static readonly Color WarnColor = Color.FromArgb(192, 0, 0);
    public static readonly Color OkColor = Color.FromArgb(0, 120, 60);
    public static readonly Color SplitBack = Color.FromArgb(235, 238, 242);
    public static readonly Color PanelBack = Color.FromArgb(248, 250, 252);

    // ---------------- 按字体行高计算的尺寸 ----------------

    /// <summary>单行文字所需的最小高度。</summary>
    public static int TextHeight(Font font) => font.Height;

    /// <summary>按钮高度：一行文字再加一点上下留白。</summary>
    public static int ButtonHeight => UiFont.Height + 14;

    /// <summary>放一行按钮的工具栏高度。</summary>
    public static int ToolbarHeight => ButtonHeight + 12;

    /// <summary>放一行说明文字的窄条高度。</summary>
    public static int InfoHeight => UiFontSmall.Height + 10;

    /// <summary>结果概要行的高度。</summary>
    public static int SummaryHeight => TitleFont.Height + 10;

    /// <summary>表格数据行的高度。</summary>
    public static int GridRowHeight(Font font) => font.Height + 8;

    /// <summary>分组框内容区上边距（要避开标题所占的高度）。</summary>
    public static Padding GroupPadding(Font titleFont)
        => new(8, titleFont.Height + 2, 8, 8);

    /// <summary>统一设置文本框为等宽字体，便于查看源代码。</summary>
    public static TextBox MakeCodeBox(bool readOnly = false)
    {
        return new TextBox
        {
            Multiline = true,
            ScrollBars = ScrollBars.Both,
            WordWrap = false,
            Font = MonoFont,
            ReadOnly = readOnly,
            BackColor = readOnly ? Color.FromArgb(250, 250, 250) : Color.White,
            BorderStyle = BorderStyle.FixedSingle
        };
    }

    /// <summary>创建一个统一风格的按钮（高度按字体行高计算）。</summary>
    public static Button MakeButton(string text)
    {
        var b = new Button
        {
            Text = text,
            Font = UiFont,
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            Height = ButtonHeight,
            MinimumSize = new Size(0, ButtonHeight),
            Padding = new Padding(10, 0, 10, 0),
            Margin = new Padding(0, 2, 6, 2),
            FlatStyle = FlatStyle.System,
            UseVisualStyleBackColor = true
        };
        return b;
    }

    /// <summary>创建一个会自动换行、高度自适应的工具栏。</summary>
    public static FlowLayoutPanel MakeToolbar()
    {
        return new FlowLayoutPanel
        {
            Dock = DockStyle.Top,
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            WrapContents = true,
            FlowDirection = FlowDirection.LeftToRight,
            Padding = new Padding(6, 5, 6, 5),
            Margin = new Padding(0),
            BackColor = HeaderBack
        };
    }

    /// <summary>
    /// 设置分割容器的初始分割位置。
    /// 注意：SplitContainer 在构造时尺寸为 0，此时直接给 SplitterDistance / PanelXMinSize
    /// 赋值会抛出“SplitterDistance must be between ...”异常，因此必须等控件真正
    /// 拿到尺寸之后再设置，并且只设置一次。
    /// </summary>
    public static void SetupSplit(SplitContainer split, Orientation orientation,
        double ratio, int min1, int min2)
    {
        split.Orientation = orientation;
        bool done = false;

        void Apply()
        {
            if (done) return;
            int total = orientation == Orientation.Vertical ? split.Width : split.Height;
            if (total < min1 + min2 + split.SplitterWidth + 8) return;

            try
            {
                int max = total - min2 - split.SplitterWidth;
                split.SplitterDistance = Math.Clamp((int)(total * ratio), min1, Math.Max(min1, max));
                split.Panel1MinSize = min1;
                split.Panel2MinSize = min2;
                done = true;
            }
            catch (InvalidOperationException)
            {
                // 窗口被压缩得过小时忽略，保持默认布局即可
            }
        }

        split.HandleCreated += (_, _) => Apply();
        split.SizeChanged += (_, _) => Apply();
    }

    /// <summary>
    /// 统一配置只读表格：表头高度自适应、行高按字体计算、单元格留出内边距，
    /// 从而避免文字被上下裁切。
    /// </summary>
    public static void StyleGrid(DataGridView grid, Font cellFont, Font headerFont)
    {
        grid.AllowUserToAddRows = false;
        grid.AllowUserToDeleteRows = false;
        grid.AllowUserToResizeRows = false;
        grid.ReadOnly = true;
        grid.RowHeadersVisible = false;
        grid.EnableHeadersVisualStyles = false;
        grid.BorderStyle = BorderStyle.None;
        grid.BackgroundColor = Color.White;
        grid.GridColor = Color.FromArgb(228, 232, 236);
        grid.Font = cellFont;
        grid.ScrollBars = ScrollBars.Both;

        // 表头高度由字体自动决定，避免表头文字被裁切
        grid.ColumnHeadersDefaultCellStyle.BackColor = HeaderBack;
        grid.ColumnHeadersDefaultCellStyle.Font = headerFont;
        grid.ColumnHeadersDefaultCellStyle.Alignment = DataGridViewContentAlignment.MiddleCenter;
        grid.ColumnHeadersDefaultCellStyle.Padding = new Padding(3, 3, 3, 3);
        grid.ColumnHeadersHeightSizeMode = DataGridViewColumnHeadersHeightSizeMode.AutoSize;
        grid.ColumnHeadersBorderStyle = DataGridViewHeaderBorderStyle.Single;

        // 数据行高度按字体行高计算
        grid.RowTemplate.Height = GridRowHeight(cellFont);
        grid.DefaultCellStyle.Padding = new Padding(3, 2, 3, 2);
        grid.DefaultCellStyle.SelectionBackColor = Color.FromArgb(210, 228, 250);
        grid.DefaultCellStyle.SelectionForeColor = Color.Black;
    }
}

/// <summary>
/// 状态转换图绘制控件。
///
/// 采用“圆周布局”：把各个状态均匀摆放在一个圆周上，初态用一条外部箭头指出，
/// 终态用双圈表示，转移边按标号（字符集合）绘制并在必要时画成弧线以避免重叠，
/// 自转移画成结点上方的回环。这样即使是十来状态的 NFA 也能一眼看清结构。
/// </summary>
public sealed class GraphView : Control
{
    private GraphModel? _model;
    private PointF[] _positions = Array.Empty<PointF>();
    private float _nodeRadius = 24f;

    public GraphView()
    {
        DoubleBuffered = true;
        SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer |
                 ControlStyles.UserPaint | ControlStyles.ResizeRedraw, true);
        BackColor = Color.White;
        Font = new Font("微软雅黑", 8.5f);
    }

    /// <summary>要绘制的图形模型；设为 null 表示暂时没有图。</summary>
    public GraphModel? Model
    {
        get => _model;
        set { _model = value; Invalidate(); }
    }

    /// <summary>
    /// 按指定尺寸把当前图形绘制到给定画布上。
    /// 界面上的“导出状态图”以及实验报告配图都通过本方法生成，
    /// 保证导出的图片与屏幕上看到的效果完全一致。
    /// </summary>
    public void RenderTo(Graphics g, Size size)
    {
        var oldSize = Size;
        try
        {
            Size = size;
            using var args = new PaintEventArgs(g, new Rectangle(Point.Empty, size));
            OnPaint(args);
        }
        finally
        {
            Size = oldSize;
        }
    }

    // ---------------------------------------------------------------
    //                            绘制
    // ---------------------------------------------------------------

    protected override void OnPaint(PaintEventArgs e)
    {
        Graphics g = e.Graphics;
        g.SmoothingMode = SmoothingMode.AntiAlias;
        g.TextRenderingHint = TextRenderingHint.ClearTypeGridFit;
        g.Clear(BackColor);

        if (_model is null || _model.States.Count == 0)
        {
            using var f = new Font("微软雅黑", 10f);
            g.DrawString("（尚无可显示的状态转换图）", f, Brushes.Gray, 20, 20);
            return;
        }

        var model = _model;
        int n = model.States.Count;
        ComputeLayout(n);

        // 标题
        using (var titleFont = new Font("微软雅黑", 9.5f, FontStyle.Bold))
            g.DrawString(model.Title, titleFont, Brushes.DimGray, 10, 8);

        // 1) 先画边（结点后画，可以盖住边端，视觉更干净）
        var pairs = new HashSet<(int, int)>();
        foreach (var ed in model.Edges) pairs.Add((ed.From, ed.To));

        using var edgePen = new Pen(Color.FromArgb(70, 70, 70), 1.4f);
        foreach (var ed in model.Edges)
        {
            if (ed.From == ed.To)
                DrawSelfLoop(g, _positions[ed.From], ed.Label, edgePen);
            else
            {
                bool curved = pairs.Contains((ed.To, ed.From));   // 双向边时画弧线，避免两条线重叠
                DrawEdge(g, _positions[ed.From], _positions[ed.To], ed.Label, curved, edgePen);
            }
        }

        // 2) 再画结点
        for (int i = 0; i < n; i++) DrawNode(g, _positions[i], model.States[i]);
    }

    /// <summary>圆周布局：把 n 个状态均匀放在圆周上（单个状态放中心）。</summary>
    private void ComputeLayout(int n)
    {
        _positions = new PointF[n];
        if (n == 1) { _positions[0] = new PointF(Width / 2f, Height / 2f + 8); return; }

        float cx = Width / 2f, cy = Height / 2f + 8;
        _nodeRadius = Math.Clamp(Math.Min(Width, Height) / 24f, 15f, 26f);
        float radius = Math.Min(Width, Height) / 2f - _nodeRadius - 40f;
        if (radius < _nodeRadius + 14f) radius = _nodeRadius + 14f;

        for (int i = 0; i < n; i++)
        {
            double angle = -Math.PI / 2 + 2 * Math.PI * i / n;   // 从正上方开始顺时针排布
            _positions[i] = new PointF(
                cx + (float)(radius * Math.Cos(angle)),
                cy + (float)(radius * Math.Sin(angle)));
        }
    }

    /// <summary>画一个状态结点：普通结点单圈，终态双圈，初态另加一条指入箭头。</summary>
    private void DrawNode(Graphics g, PointF p, GraphState state)
    {
        float r = _nodeRadius;
        var rect = new RectangleF(p.X - r, p.Y - r, r * 2, r * 2);

        using var fill = new SolidBrush(state.IsAccept ? Color.FromArgb(232, 244, 255) : Color.White);
        using var border = new Pen(Color.FromArgb(30, 90, 160), 1.8f);

        g.FillEllipse(fill, rect);
        g.DrawEllipse(border, rect);
        if (state.IsAccept)                                       // 终态：双圈
        {
            float r2 = r - 3.5f;
            g.DrawEllipse(border, new RectangleF(p.X - r2, p.Y - r2, r2 * 2, r2 * 2));
        }

        // 初态：从结点外左上方画一条箭头指进来
        if (state.IsStart)
        {
            var from = new PointF(p.X - r - 30, p.Y - r - 18);
            var to = new PointF(p.X - r * 0.72f, p.Y - r * 0.72f);
            using var pen = new Pen(Color.FromArgb(200, 60, 60), 1.8f);
            g.DrawLine(pen, from, to);
            DrawArrowHead(g, to, from, Brushes.Firebrick);
        }

        using var nameFont = new Font("Consolas", Math.Max(8f, r * 0.62f), FontStyle.Bold);
        var size = g.MeasureString(state.Name, nameFont);
        g.DrawString(state.Name, nameFont, Brushes.Black,
            p.X - size.Width / 2f, p.Y - size.Height / 2f);
    }

    /// <summary>画两个状态之间的转移边（普通直线或弧线）。</summary>
    private void DrawEdge(Graphics g, PointF a, PointF b, string label, bool curved, Pen pen)
    {
        var dx = b.X - a.X;
        var dy = b.Y - a.Y;
        float len = MathF.Sqrt(dx * dx + dy * dy);
        if (len < 1f) return;

        var ux = dx / len;
        var uy = dy / len;
        var nx = -uy;
        var ny = ux;

        float r = _nodeRadius;
        var p1 = new PointF(a.X + ux * r, a.Y + uy * r);          // 起点裁剪到圆周
        var p2 = new PointF(b.X - ux * r, b.Y - uy * r);          // 终点裁剪到圆周

        PointF labelPos;
        if (curved)
        {
            // 双向边：两条边各向外侧弯出，形成一条“腰鼓形”弧线
            float off = Math.Min(30f, len * 0.30f);
            var ctrl = new PointF((a.X + b.X) / 2f + nx * off, (a.Y + b.Y) / 2f + ny * off);
            using var path = new GraphicsPath();
            path.AddBezier(p1, ctrl, ctrl, p2);
            g.DrawPath(pen, path);

            var t = 0.82f;                                        // 箭头方向取曲线末端切线
            var near = new PointF(
                (float)(Math.Pow(1 - t, 2) * p1.X + 2 * (1 - t) * t * ctrl.X + t * t * p2.X),
                (float)(Math.Pow(1 - t, 2) * p1.Y + 2 * (1 - t) * t * ctrl.Y + t * t * p2.Y));
            DrawArrowHead(g, p2, near, Brushes.DimGray);
            labelPos = new PointF((a.X + b.X) / 2f + nx * (off * 1.15f), (a.Y + b.Y) / 2f + ny * (off * 1.15f));
        }
        else
        {
            g.DrawLine(pen, p1, p2);
            DrawArrowHead(g, p2, p1, Brushes.DimGray);
            labelPos = new PointF((p1.X + p2.X) / 2f + nx * 13f, (p1.Y + p2.Y) / 2f + ny * 13f);
        }

        DrawLabel(g, label, labelPos);
    }

    /// <summary>画自转移：结点上方的回环。</summary>
    private void DrawSelfLoop(Graphics g, PointF c, string label, Pen pen)
    {
        float r = _nodeRadius;
        float h = r * 2.1f;
        var p1 = new PointF(c.X - r * 0.55f, c.Y - r * 0.83f);    // 结点左上
        var p2 = new PointF(c.X + r * 0.55f, c.Y - r * 0.83f);    // 结点右上
        var c1 = new PointF(c.X - r * 1.5f, c.Y - r - h);
        var c2 = new PointF(c.X + r * 1.5f, c.Y - r - h);

        using var path = new GraphicsPath();
        path.AddBezier(p1, c1, c2, p2);
        g.DrawPath(pen, path);
        DrawArrowHead(g, p2, c2, Brushes.DimGray);

        DrawLabel(g, label, new PointF(c.X, c.Y - r - h * 0.75f));
    }

    /// <summary>画带白色底衬的标号，避免与线条重叠看不清。</summary>
    private void DrawLabel(Graphics g, string label, PointF center)
    {
        if (string.IsNullOrEmpty(label)) return;
        var size = g.MeasureString(label, Font);
        var rect = new RectangleF(center.X - size.Width / 2f - 2, center.Y - size.Height / 2f, size.Width + 4, size.Height);
        using var back = new SolidBrush(Color.FromArgb(235, 255, 255, 255));
        g.FillRectangle(back, rect);
        g.DrawString(label, Font, Brushes.Black, rect.X + 2, rect.Y);
    }

    /// <summary>在 tip 处画一个指向 tip 的实心箭头。</summary>
    private static void DrawArrowHead(Graphics g, PointF tip, PointF from, Brush brush)
    {
        float dx = tip.X - from.X, dy = tip.Y - from.Y;
        float len = MathF.Sqrt(dx * dx + dy * dy);
        if (len < 0.001f) return;
        float ux = dx / len, uy = dy / len;
        float nx = -uy, ny = ux;

        const float size = 10f, halfWidth = 4.5f;
        var p1 = new PointF(tip.X, tip.Y);
        var p2 = new PointF(tip.X - ux * size + nx * halfWidth, tip.Y - uy * size + ny * halfWidth);
        var p3 = new PointF(tip.X - ux * size - nx * halfWidth, tip.Y - uy * size - ny * halfWidth);
        g.FillPolygon(brush, new[] { p1, p2, p3 });
    }
}

/// <summary>
/// 状态转换矩阵的显示控件：把 <see cref="TransitionMatrix"/> 渲染成一张表格。
/// 第一列是状态名（并标注初态/终态），其余各列是字母表中的各个符号。
/// </summary>
public sealed class MatrixPanel : UserControl
{
    private readonly Label _summary = new();
    private readonly DataGridView _grid = new();

    public MatrixPanel()
    {
        Dock = DockStyle.Fill;
        BackColor = Color.White;

        _summary.Dock = DockStyle.Top;
        _summary.Height = Ui.SummaryHeight;
        _summary.TextAlign = ContentAlignment.MiddleLeft;
        _summary.Font = Ui.UiFontSmall;
        _summary.ForeColor = Ui.AccentColor;
        _summary.BackColor = Ui.HeaderBack;
        _summary.Padding = new Padding(6, 0, 0, 0);
        _summary.AutoSize = false;
        _summary.AutoEllipsis = true;      // 面板过窄时用省略号收尾，而不是把文字截断

        _grid.Dock = DockStyle.Fill;
        Ui.StyleGrid(_grid, Ui.MonoFontSmall, Ui.UiFontSmall);
        _grid.SelectionMode = DataGridViewSelectionMode.CellSelect;
        // 列宽按内容自动适配（表头文字也参与计算），并设置最小宽度兜底
        _grid.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.AllCells;
        _grid.DefaultCellStyle.Alignment = DataGridViewContentAlignment.MiddleCenter;

        Controls.Add(_grid);
        Controls.Add(_summary);
    }

    /// <summary>显示一个状态转换矩阵。</summary>
    public void Show(TransitionMatrix? matrix)
    {
        _grid.Rows.Clear();
        _grid.Columns.Clear();

        if (matrix is null)
        {
            _summary.Text = "（尚未生成）";
            return;
        }

        _summary.Text = matrix.Summary + (matrix.ColumnsCompressed
            ? "　（等价字符列已合并显示）"
            : "");

        // ---- 第一列：状态名 + 备注 ----
        var stateCol = new DataGridViewTextBoxColumn
        {
            HeaderText = "状态",
            MinimumWidth = 110,
            SortMode = DataGridViewColumnSortMode.NotSortable
        };
        stateCol.DefaultCellStyle.Alignment = DataGridViewContentAlignment.MiddleLeft;
        stateCol.DefaultCellStyle.Font = Ui.UiFont;
        _grid.Columns.Add(stateCol);

        // ---- 第二列：对应 NFA 状态集合（DFA 才有意义）----
        bool hasNote2 = matrix.Note2Enabled && matrix.RowNote2.Count > 0;
        if (hasNote2)
        {
            var noteCol = new DataGridViewTextBoxColumn
            {
                HeaderText = "对应 NFA 状态集合",
                MinimumWidth = 150,
                SortMode = DataGridViewColumnSortMode.NotSortable
            };
            noteCol.DefaultCellStyle.Font = Ui.MonoFontSmall;
            noteCol.DefaultCellStyle.ForeColor = Color.FromArgb(110, 110, 110);
            _grid.Columns.Add(noteCol);
        }

        // ---- 其余列：字母表中的符号（宽度按表头与内容自动适配）----
        foreach (string c in matrix.Columns)
        {
            _grid.Columns.Add(new DataGridViewTextBoxColumn
            {
                HeaderText = c,
                MinimumWidth = 62,
                SortMode = DataGridViewColumnSortMode.NotSortable
            });
        }

        // ---- 内容 ----
        for (int i = 0; i < matrix.RowNames.Count; i++)
        {
            var cells = new List<string> { matrix.RowNames[i] + FormatNote(matrix.RowNote, i) };
            if (hasNote2) cells.Add(matrix.RowNote2[i]);
            cells.AddRange(matrix.Cells[i]);

            int idx = _grid.Rows.Add(cells.ToArray());

            // 初态 / 终态所在的行加底色，便于快速定位
            string note = matrix.RowNote[i];
            if (note.Contains("初态"))
                _grid.Rows[idx].DefaultCellStyle.BackColor = Color.FromArgb(235, 245, 255);
            if (note.Contains("终态"))
                _grid.Rows[idx].DefaultCellStyle.BackColor = Color.FromArgb(240, 255, 240);
        }
    }

    private static string FormatNote(List<string> notes, int i)
        => i < notes.Count && notes[i].Length > 0 ? $"（{notes[i]}）" : "";
}

/// <summary>
/// 词法分析结果的统一显示控件。包含三个标签页：
///   ① 单词符号明细（表格：序号 / 种别码 / 单词符号 / 种类 / 行 / 列 / 值）
///   ② 二元组序列（按指导书要求输出 (syn, token) 序列）
///   ③ 词法错误（带行号列号的错误信息）
/// 词法分析页与图片识别页共用本控件，避免两处重复实现。
/// </summary>
public sealed class LexResultView : UserControl
{
    private readonly Label _summary = new();
    private readonly TabControl _tabs = new();
    private readonly DataGridView _grid = new();
    private readonly TextBox _sequence = new();
    private readonly TextBox _errors = new();
    private LexResult? _lastResult;

    public LexResultView()
    {
        Dock = DockStyle.Fill;
        BackColor = Color.White;

        // ---------- 顶部概要 ----------
        _summary.Dock = DockStyle.Top;
        _summary.Height = Ui.SummaryHeight;
        _summary.TextAlign = ContentAlignment.MiddleLeft;
        _summary.Font = Ui.TitleFont;
        _summary.ForeColor = Ui.AccentColor;
        _summary.Padding = new Padding(6, 0, 0, 0);
        _summary.Text = "尚未进行分析。";
        _summary.BackColor = Ui.HeaderBack;

        // ---------- 标签页 ----------
        _tabs.Dock = DockStyle.Fill;
        _tabs.Font = Ui.UiFont;

        // ① 明细表格
        _grid.Dock = DockStyle.Fill;
        ConfigureGrid();
        var tabDetail = new TabPage("单词符号明细") { BackColor = Color.White, Padding = new Padding(3) };
        tabDetail.Controls.Add(_grid);

        // ② 二元组序列
        _sequence.Dock = DockStyle.Fill;
        _sequence.Multiline = true;
        _sequence.ReadOnly = true;
        _sequence.ScrollBars = ScrollBars.Both;
        _sequence.WordWrap = false;
        _sequence.Font = Ui.MonoFont;
        _sequence.BackColor = Color.FromArgb(252, 252, 252);
        _sequence.BorderStyle = BorderStyle.None;
        var tabSeq = new TabPage("二元组 (syn, token)") { BackColor = Color.White, Padding = new Padding(3) };
        tabSeq.Controls.Add(_sequence);

        // ③ 错误
        _errors.Dock = DockStyle.Fill;
        _errors.Multiline = true;
        _errors.ReadOnly = true;
        _errors.ScrollBars = ScrollBars.Both;
        _errors.WordWrap = false;
        _errors.Font = Ui.MonoFontSmall;
        _errors.ForeColor = Ui.WarnColor;
        _errors.BackColor = Color.FromArgb(255, 250, 250);
        _errors.BorderStyle = BorderStyle.None;
        var tabErr = new TabPage("词法错误") { BackColor = Color.White, Padding = new Padding(3) };
        tabErr.Controls.Add(_errors);

        _tabs.Controls.Add(tabDetail);
        _tabs.Controls.Add(tabSeq);
        _tabs.Controls.Add(tabErr);

        Controls.Add(_tabs);      // Fill 的先加
        Controls.Add(_summary);   // 再加 Top 的
    }

    private void ConfigureGrid()
    {
        Ui.StyleGrid(_grid, Ui.UiFont, Ui.TitleFont);
        _grid.SelectionMode = DataGridViewSelectionMode.FullRowSelect;
        _grid.MultiSelect = true;
        _grid.AutoGenerateColumns = false;
        _grid.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.None;

        // 各列的宽度策略：
        //   · 短列（序号/行/列/种别码）按内容自适应，并给出最小宽度，避免被压得过窄；
        //   · “单词符号”列用 Fill 占满剩余宽度，长标识符、长字符串也能看全；
        //   · “种类”列内容较长，给出较宽的最小宽度。
        void AddColumn(string header, string prop, int minWidth,
            DataGridViewAutoSizeColumnMode mode, string? align = null)
        {
            var col = new DataGridViewTextBoxColumn
            {
                HeaderText = header,
                DataPropertyName = prop,
                SortMode = DataGridViewColumnSortMode.NotSortable,
                AutoSizeMode = mode,
                MinimumWidth = minWidth
            };
            if (align == "c") col.DefaultCellStyle.Alignment = DataGridViewContentAlignment.MiddleCenter;
            _grid.Columns.Add(col);
        }

        AddColumn("序号", nameof(Token.Index), 56,
            DataGridViewAutoSizeColumnMode.AllCells, "c");
        AddColumn("种别码 syn", nameof(Token.Syn), 84,
            DataGridViewAutoSizeColumnMode.AllCells, "c");
        AddColumn("单词符号 token", nameof(Token.Lexeme), 150,
            DataGridViewAutoSizeColumnMode.AllCells);
        AddColumn("种类", nameof(Token.Description), 150,
            DataGridViewAutoSizeColumnMode.Fill);
        AddColumn("值", nameof(Token.ValueText), 90,
            DataGridViewAutoSizeColumnMode.AllCells);
        AddColumn("行", nameof(Token.Line), 52,
            DataGridViewAutoSizeColumnMode.AllCells, "c");
        AddColumn("列", nameof(Token.Column), 52,
            DataGridViewAutoSizeColumnMode.AllCells, "c");

        _grid.CellFormatting += Grid_CellFormatting;
    }

    /// <summary>按单词种类给整行着色，便于在长列表中快速分辨不同类型的单词。</summary>
    private void Grid_CellFormatting(object? sender, DataGridViewCellFormattingEventArgs e)
    {
        if (e.RowIndex < 0 || e.RowIndex >= _grid.Rows.Count) return;
        if (_grid.Rows[e.RowIndex].DataBoundItem is not Token t) return;

        e.CellStyle.ForeColor = t.Category switch
        {
            TokenCategory.Keyword => Color.FromArgb(0, 90, 170),
            TokenCategory.Identifier => Color.FromArgb(40, 40, 40),
            TokenCategory.Integer or TokenCategory.Real => Color.FromArgb(140, 70, 10),
            TokenCategory.String => Color.FromArgb(0, 128, 80),
            TokenCategory.Operator => Color.FromArgb(120, 60, 160),
            _ => Ui.WarnColor
        };
    }

    /// <summary>显示一次词法分析的结果。</summary>
    public void ShowResult(LexResult result, string sourceName)
    {
        _lastResult = result;
        _grid.DataSource = null;
        _grid.DataSource = result.Tokens;

        _sequence.Text = result.Tokens.Count == 0
            ? "（没有识别出任何单词）"
            : result.ToSequenceString();

        if (result.Errors.Count == 0)
        {
            _errors.Text = "未发现词法错误。";
            _errors.ForeColor = Ui.OkColor;
        }
        else
        {
            _errors.ForeColor = Ui.WarnColor;
            var sb = new StringBuilder();
            for (int i = 0; i < result.Errors.Count; i++)
                sb.AppendLine($"[{i + 1}] {result.Errors[i]}");
            _errors.Text = sb.ToString();
        }

        _summary.Text = BuildSummary(result, sourceName);
    }

    public void Clear()
    {
        _lastResult = null;
        _grid.DataSource = null;
        _sequence.Text = "";
        _errors.Text = "";
        _summary.Text = "尚未进行分析。";
    }

    /// <summary>把明细表格的内容导出成制表符分隔的文本行。</summary>
    public IEnumerable<string> DetailLines
    {
        get
        {
            if (_lastResult is null) yield break;
            foreach (var t in _lastResult.Tokens)
                yield return $"{t.Index}\t{t.Syn}\t{t.Lexeme}\t{t.Description}\t{t.ValueText}\t{t.Line}\t{t.Column}";
        }
    }

    /// <summary>生成概要行：单词总数 + 各类单词个数 + 错误个数。</summary>
    private static string BuildSummary(LexResult result, string sourceName)
    {
        var tokens = result.Tokens;
        string Counts()
        {
            int kw = tokens.Count(t => t.Category == TokenCategory.Keyword);
            int id = tokens.Count(t => t.Category == TokenCategory.Identifier);
            int num = tokens.Count(t => t.Category is TokenCategory.Integer or TokenCategory.Real);
            int str = tokens.Count(t => t.Category == TokenCategory.String);
            int op = tokens.Count(t => t.Category == TokenCategory.Operator);
            return $"关键字 {kw}，标识符 {id}，数值常量 {num}，字符串 {str}，运算符/界符 {op}";
        }

        string name = string.IsNullOrWhiteSpace(sourceName) ? "" : $"【{sourceName}】";
        return $"{name}共 {tokens.Count} 个单词（{Counts()}）；词法错误 {result.Errors.Count} 个";
    }

    /// <summary>当前显示的二元组序列文本，供导出使用。</summary>
    public string SequenceText => _sequence.Text;

    /// <summary>当前显示的错误文本，供导出使用。</summary>
    public string ErrorText => _errors.Text;
}


/// <summary>
/// 程序入口。整个实验系统为单一 WinForms 应用，启动后显示主窗口，
/// 主窗口内含三个功能页：词法分析、正规式自动机构造、图片识别与分析。
/// </summary>
internal static class Program
{
    [STAThread]
    private static void Main()
    {
        // WinForms 标准初始化（视觉样式、文本渲染、DPI 等由 csproj 中的
        // Application* 属性在编译期生成）
        ApplicationConfiguration.Initialize();

        // 全局未处理异常提示，避免老师演示时程序直接闪退
        Application.ThreadException += (_, e) =>
            MessageBox.Show(e.Exception.ToString(), "程序异常",
                MessageBoxButtons.OK, MessageBoxIcon.Error);

        Application.Run(new UI.MainForm());
    }
}

/// <summary>
/// 主窗口。《编译原理》实验一“词法分析”的集成演示程序，含三个功能页：
///   ① 词法分析          —— 基础实验要求（配置驱动的词法分析器 + 词法错误处理）
///   ② 正规式自动机构造  —— 探索实验（一）：正规式 → NFA → DFA → 最简 DFA
///   ③ 图片识别并分析    —— 探索实验（二）：源代码图片 OCR 后再做词法分析
/// </summary>
public sealed class MainForm : Form
{
    private readonly TabControl _tabs = new();
    private readonly ToolStripStatusLabel _status = new();
    private readonly ToolStripStatusLabel _configLabel = new();

    private LexPanel _lexPanel = null!;
    private RegexPanel _regexPanel = null!;
    private OcrPanel _ocrPanel = null!;

    private LexerConfig _config = null!;
    private string _configPath = "";

    public MainForm()
    {
        Text = "LexLab 词法分析实验系统 —— 《编译原理》实验一";
        StartPosition = FormStartPosition.CenterScreen;
        MinimumSize = new Size(820, 560);
        Font = Ui.UiFont;
        BackColor = Color.White;

        // 初始窗口大小按屏幕工作区自适应（高 DPI 缩放后屏幕可用区域会变小，
        // 直接写死 1280×820 可能超出屏幕，导致窗口被裁切）
        var work = Screen.PrimaryScreen?.WorkingArea ?? new Rectangle(0, 0, 1280, 800);
        Size = new Size(
            Math.Min(1280, Math.Max(820, (int)(work.Width * 0.92))),
            Math.Min(860, Math.Max(560, (int)(work.Height * 0.92))));

        BuildUi();
        ReloadConfig(showMessage: false);
        FormClosed += (_, _) => Cleanup();
    }

    // ==================================================================
    //                              界面搭建
    // ==================================================================

    private void BuildUi()
    {
        var menu = BuildMenu();
        var status = BuildStatusStrip();

        _tabs.Dock = DockStyle.Fill;
        _tabs.Font = Ui.UiFont;
        _tabs.Padding = new Point(14, 5);

        _lexPanel = new LexPanel { StatusChanged = SetStatus };
        _regexPanel = new RegexPanel { StatusChanged = SetStatus };
        _ocrPanel = new OcrPanel { StatusChanged = SetStatus };

        var tab1 = new TabPage("① 词法分析") { BackColor = Color.White, Padding = new Padding(4) };
        tab1.Controls.Add(_lexPanel);
        var tab2 = new TabPage("② 正规式→NFA→DFA→最简DFA") { BackColor = Color.White, Padding = new Padding(4) };
        tab2.Controls.Add(_regexPanel);
        var tab3 = new TabPage("③ 图片识别并分析") { BackColor = Color.White, Padding = new Padding(4) };
        tab3.Controls.Add(_ocrPanel);

        _tabs.Controls.Add(tab1);
        _tabs.Controls.Add(tab2);
        _tabs.Controls.Add(tab3);

        Controls.Add(_tabs);
        Controls.Add(status);
        Controls.Add(menu);
    }

    private MenuStrip BuildMenu()
    {
        var menu = new MenuStrip { Font = Ui.UiFont, BackColor = Color.FromArgb(248, 250, 252) };

        var file = new ToolStripMenuItem("文件(&F)");
        var openSource = new ToolStripMenuItem("打开源程序…", null, (_, _) =>
        {
            _tabs.SelectedIndex = 0;
            _lexPanel.OpenSourceFileDialog();
        });
        var exit = new ToolStripMenuItem("退出", null, (_, _) => Close());
        file.DropDownItems.Add(openSource);
        file.DropDownItems.Add(new ToolStripSeparator());
        file.DropDownItems.Add(exit);

        var configMenu = new ToolStripMenuItem("配置(&C)");
        configMenu.DropDownItems.Add(new ToolStripMenuItem("重新载入配置文件", null,
            (_, _) => ReloadConfig(showMessage: true)));
        configMenu.DropDownItems.Add(new ToolStripMenuItem("用记事本打开配置文件…", null,
            (_, _) => OpenConfigWithNotepad()));
        configMenu.DropDownItems.Add(new ToolStripMenuItem("恢复为默认配置", null,
            (_, _) => RestoreDefaultConfig()));
        configMenu.DropDownItems.Add(new ToolStripSeparator());
        configMenu.DropDownItems.Add(new ToolStripMenuItem("如何扩展种别码…", null,
            (_, _) => ShowConfigHelp()));

        var help = new ToolStripMenuItem("帮助(&H)");
        help.DropDownItems.Add(new ToolStripMenuItem("关于本程序", null, (_, _) => ShowAbout()));

        menu.Items.Add(file);
        menu.Items.Add(configMenu);
        menu.Items.Add(help);
        return menu;
    }

    private StatusStrip BuildStatusStrip()
    {
        var strip = new StatusStrip { Font = Ui.UiFontSmall, BackColor = Color.FromArgb(248, 250, 252) };
        _status.Text = "就绪。";
        _status.Spring = true;
        _status.TextAlign = ContentAlignment.MiddleLeft;
        _configLabel.BorderSides = ToolStripStatusLabelBorderSides.Left;
        _configLabel.IsLink = true;
        _configLabel.LinkBehavior = LinkBehavior.HoverUnderline;
        _configLabel.Click += (_, _) => OpenConfigWithNotepad();
        strip.Items.Add(_status);
        strip.Items.Add(_configLabel);
        return strip;
    }

    private void SetStatus(string text) => _status.Text = text;

    // ==================================================================
    //                          配置文件管理
    // ==================================================================

    /// <summary>
    /// 重新载入词法配置。配置文件约定放在 exe 同目录（lexer.config.json），
    /// 老师或用例可以直接用记事本修改，不需要重新编译程序。
    /// </summary>
    private void ReloadConfig(bool showMessage)
    {
        try
        {
            _configPath = LexerConfig.EnsureExists();      // 不存在时自动生成默认配置
            _config = LexerConfig.Load(_configPath);
            ApplyConfigSafely();

            _configLabel.Text = $"配置：{LexerConfig.FileName}（关键字 {_config.Keywords.Count} 项，运算符 {_config.Operators.Count} 项）";
            _configLabel.ToolTipText = _configPath + "（单击可用记事本打开）";
            SetStatus("词法配置已载入。");

            if (showMessage)
                MessageBox.Show(
                    $"配置已重新载入。\r\n\r\n文件：{_configPath}\r\n" +
                    $"关键字 {_config.Keywords.Count} 项，运算符/界符 {_config.Operators.Count} 项。",
                    "重新载入配置", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }
        catch (Exception ex)
        {
            _config = LexerConfig.CreateDefault();
            ApplyConfigSafely();
            _configLabel.Text = "配置：内置默认（外部配置读取失败）";
            MessageBox.Show(
                $"读取配置文件失败，已改为使用内置默认配置。\r\n\r\n{ex.Message}",
                "配置错误", MessageBoxButtons.OK, MessageBoxIcon.Warning);
        }
    }

    private void ApplyConfigSafely()
    {
        if (_lexPanel is not null) _lexPanel.ApplyConfig(_config);
        if (_ocrPanel is not null) _ocrPanel.ApplyConfig(_config);
    }

    private void OpenConfigWithNotepad()
    {
        try
        {
            if (!File.Exists(_configPath)) ReloadConfig(showMessage: false);
            Process.Start(new ProcessStartInfo("notepad.exe", $"\"{_configPath}\"") { UseShellExecute = true });
            SetStatus("已用记事本打开配置文件，修改保存后请点击“配置 → 重新载入配置文件”。");
        }
        catch (Exception ex)
        {
            MessageBox.Show("打开配置文件失败：" + ex.Message, "错误", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    private void RestoreDefaultConfig()
    {
        if (MessageBox.Show("将把配置文件恢复为内置默认内容（会覆盖当前文件），是否继续？",
                "恢复默认配置", MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes) return;

        try
        {
            File.WriteAllText(_configPath, LexerConfig.DefaultTemplate, new System.Text.UTF8Encoding(true));
            ReloadConfig(showMessage: true);
        }
        catch (Exception ex)
        {
            MessageBox.Show("写入配置文件失败：" + ex.Message, "错误", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    private void ShowConfigHelp()
    {
        string exeDir = LexerConfig.ExeDirectory;
        MessageBox.Show(
            "本程序的单词种别码全部来自 exe 同目录下的配置文件，扩展种别码无须修改代码：\r\n\r\n" +
            "1) 新增关键字：在 \"keywords\" 中加一项，如  \"switch\": 64\r\n" +
            "   标识符扫描结束后会整体查这张表，因此新增关键字立即生效。\r\n\r\n" +
            "2) 新增运算符/界符：在 \"operators\" 中加一项，如  \"<<=\": 90\r\n" +
            "   扫描器按“最长匹配”识别，多字符符号会被整体识别。\r\n\r\n" +
            "3) 修改标识符构成字符集：修改 \"chars\" 中的 identifierStart / identifierPart。\r\n" +
            "4) 修改注释与字符串规则：修改 \"comments\" / \"strings\"。\r\n" +
            "5) 数值常量规则（十六进制前缀、指数是否必须带小数点与符号）：修改 \"numbers\"。\r\n\r\n" +
            "6) 配置文件支持 // 注释与末尾多余逗号，便于手工维护。\r\n" +
            "7) 文件位置：\r\n" + Path.Combine(exeDir, LexerConfig.FileName),
            "如何扩展种别码", MessageBoxButtons.OK, MessageBoxIcon.Information);
    }

    private void ShowAbout()
    {
        MessageBox.Show(
            "LexLab 词法分析实验系统\r\n" +
            "《编译原理》实验一\r\n\r\n" +
            "已实现内容：\r\n" +
            "  ① 基础实验：配置驱动的 C 语言子集词法分析器（关键字、标识符、\r\n" +
            "     10/16 进制整型常量、带指数双精度常量、字符串、两类注释）\r\n" +
            "     以及词法错误检测（给出的错误信息含行号、列号与出错片段）。\r\n" +
            "  ② 探索实验（一）：正规式 → NFA（Thompson）→ DFA（子集法）\r\n" +
            "     → 最简 DFA（分割法），输出状态转换矩阵与状态转换图。\r\n" +
            "  ③ 探索实验（二）：源代码图片 OCR 识别后再做词法分析。\r\n\r\n" +
            "运行环境：Windows 10/11 x64，程序为单文件发布，无需安装任何运行环境。",
            "关于 LexLab", MessageBoxButtons.OK, MessageBoxIcon.Information);
    }

    private void Cleanup()
    {
        try { _ocrPanel.ReleaseResources(); } catch { /* 退出时忽略 */ }
        try { CodeOcr.Shutdown(); } catch { /* 退出时忽略 */ }
    }
}

/// <summary>
/// “词法分析”页：左边编辑源程序，右边显示单词序列、明细与错误。
/// </summary>
public sealed class LexPanel : UserControl
{
    private readonly TextBox _source = Ui.MakeCodeBox();
    private readonly LexResultView _result = new();
    private readonly ComboBox _sampleBox = new();
    private readonly Label _sourceInfo = new();

    private LexerConfig _config = LexerConfig.CreateDefault();

    /// <summary>状态栏回调：把一句提示显示到主窗口底部。</summary>
    public Action<string>? StatusChanged { get; set; }

    public LexPanel()
    {
        Dock = DockStyle.Fill;
        BackColor = Color.White;
        BuildUi();
    }

    /// <summary>主窗口重新载入配置后同步到本页。</summary>
    public void ApplyConfig(LexerConfig config)
    {
        _config = config;
        StatusChanged?.Invoke("已重新载入词法配置。");
    }

    // ==================================================================
    //                              界面搭建
    // ==================================================================

    private void BuildUi()
    {
        // ---------------- 顶部工具栏（自动换行、高度自适应，避免按钮文字被裁切） ----------------
        var toolbar = Ui.MakeToolbar();

        var btnOpen = Ui.MakeButton("打开源文件…");
        btnOpen.Click += (_, _) => OpenSourceFileDialog();

        var btnAnalyze = Ui.MakeButton("开始词法分析");
        btnAnalyze.Font = new Font(Ui.UiFont, FontStyle.Bold);
        btnAnalyze.Click += (_, _) => Analyze();

        var btnClear = Ui.MakeButton("清空");
        btnClear.Click += (_, _) => { _source.Clear(); _result.Clear(); };

        var btnExport = Ui.MakeButton("导出结果…");
        btnExport.Click += (_, _) => ExportResult();

        var lblSample = new Label
        {
            Text = "内置示例：",
            Font = Ui.UiFont,
            AutoSize = true,
            Margin = new Padding(14, 0, 2, 0),
            TextAlign = ContentAlignment.MiddleLeft
        };
        _sampleBox.Font = Ui.UiFont;
        _sampleBox.DropDownStyle = ComboBoxStyle.DropDownList;
        _sampleBox.Width = Ui.UiFont.Height * 18;
        _sampleBox.Margin = new Padding(0, 2, 0, 2);
        foreach (var s in Samples.All) _sampleBox.Items.Add(s.Name);
        _sampleBox.SelectedIndex = 0;
        _sampleBox.SelectedIndexChanged += (_, _) => LoadSample();

        // 让标签、按钮在中线对齐
        lblSample.Height = Ui.ButtonHeight;
        toolbar.Controls.AddRange(new Control[]
        {
            btnOpen, btnAnalyze, btnClear, btnExport, lblSample, _sampleBox
        });

        // ---------------- 源程序区域 ----------------
        var sourceGroup = new GroupBox
        {
            Dock = DockStyle.Fill,
            Text = "源程序（C 语言子集，可任意编辑或粘贴）",
            Font = Ui.TitleFont,
            Padding = Ui.GroupPadding(Ui.TitleFont),
            BackColor = Color.White
        };
        _source.Dock = DockStyle.Fill;
        _source.TextChanged += (_, _) => UpdateSourceInfo();
        sourceGroup.Controls.Add(_source);

        _sourceInfo.Dock = DockStyle.Bottom;
        _sourceInfo.Height = Ui.InfoHeight;
        _sourceInfo.Font = Ui.UiFontSmall;
        _sourceInfo.ForeColor = Color.DimGray;
        _sourceInfo.TextAlign = ContentAlignment.MiddleLeft;
        _sourceInfo.Padding = new Padding(4, 0, 0, 0);
        sourceGroup.Controls.Add(_sourceInfo);

        // ---------------- 结果区域 ----------------
        var resultGroup = new GroupBox
        {
            Dock = DockStyle.Fill,
            Text = "词法分析结果",
            Font = Ui.TitleFont,
            Padding = Ui.GroupPadding(Ui.TitleFont),
            BackColor = Color.White
        };
        resultGroup.Controls.Add(_result);

        var split = new SplitContainer
        {
            Dock = DockStyle.Fill,
            SplitterWidth = 6,
            BackColor = Ui.SplitBack
        };
        split.Panel1.Controls.Add(sourceGroup);
        split.Panel2.Controls.Add(resultGroup);
        Ui.SetupSplit(split, Orientation.Horizontal, 0.38,
            Ui.ButtonHeight * 3, Ui.ButtonHeight * 4);

        Controls.Add(split);
        Controls.Add(toolbar);

        LoadSample();
    }

    // ==================================================================
    //                              功能实现
    // ==================================================================

    private void LoadSample()
    {
        int index = Math.Clamp(_sampleBox.SelectedIndex, 0, Samples.All.Count - 1);
        _source.Text = Samples.All[index].Code.Replace("\r\n", "\n").Replace("\n", "\r\n");
        _source.SelectionStart = 0;
        UpdateSourceInfo();
    }

    private void UpdateSourceInfo()
    {
        int lines = _source.TextLength == 0 ? 0 : _source.Lines.Length;
        _sourceInfo.Text = $"共 {lines} 行，{_source.TextLength} 个字符";
    }

    /// <summary>弹出“打开源文件”对话框（菜单栏也会调用本方法）。</summary>
    public void OpenSourceFileDialog()
    {
        using var dlg = new OpenFileDialog
        {
            Title = "打开源程序文件",
            Filter = "源程序文件 (*.c;*.h;*.cpp;*.txt)|*.c;*.h;*.cpp;*.txt|所有文件 (*.*)|*.*"
        };
        if (dlg.ShowDialog(FindForm()) != DialogResult.OK) return;

        try
        {
            // 尽量按 UTF-8 读取，失败则退回到系统默认编码（避免中文注释乱码）
            string text;
            try { text = File.ReadAllText(dlg.FileName, new UTF8Encoding(false, true)); }
            catch (DecoderFallbackException) { text = File.ReadAllText(dlg.FileName, Encoding.Default); }

            _source.Text = text.Replace("\r\n", "\n").Replace("\n", "\r\n");
            _sourceInfo.Text = $"已载入：{Path.GetFileName(dlg.FileName)}　" + _sourceInfo.Text;
            StatusChanged?.Invoke($"已打开源文件：{dlg.FileName}");
        }
        catch (Exception ex)
        {
            MessageBox.Show("读取源文件失败：" + ex.Message, "错误", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    /// <summary>执行词法分析并显示结果。</summary>
    public void Analyze()
    {
        string code = _source.Text;
        if (code.Trim().Length == 0)
        {
            MessageBox.Show("请先输入或载入源程序。", "提示", MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }

        var sw = Stopwatch.StartNew();
        LexResult result;
        try
        {
            result = new Lexer(code, _config).Analyze();
        }
        catch (Exception ex)
        {
            MessageBox.Show("词法分析过程中出现异常：" + ex.Message, "错误",
                MessageBoxButtons.OK, MessageBoxIcon.Error);
            return;
        }
        sw.Stop();

        _result.ShowResult(result, "词法分析");
        StatusChanged?.Invoke(
            $"词法分析完成：共 {result.Tokens.Count} 个单词，{result.Errors.Count} 个词法错误，耗时 {sw.Elapsed.TotalMilliseconds:0.00} ms。");
    }

    private void ExportResult()
    {
        using var dlg = new SaveFileDialog
        {
            Title = "导出词法分析结果",
            Filter = "文本文件 (*.txt)|*.txt",
            FileName = "词法分析结果.txt"
        };
        if (dlg.ShowDialog(FindForm()) != DialogResult.OK) return;

        try
        {
            var sb = new StringBuilder();
            sb.AppendLine("==================== 词法分析结果 ====================");
            sb.AppendLine("========== 一、单词符号明细 ==========");
            sb.AppendLine("序号\t种别码\t单词符号\t种类\t值\t行\t列");
            if (_result.SequenceText.Length == 0) { /* 尚未分析 */ }
            foreach (var line in _result.DetailLines) sb.AppendLine(line);

            sb.AppendLine();
            sb.AppendLine("========== 二、二元组序列 (syn, token) ==========");
            sb.AppendLine(_result.SequenceText);

            sb.AppendLine();
            sb.AppendLine("========== 三、词法错误 ==========");
            sb.AppendLine(_result.ErrorText);

            File.WriteAllText(dlg.FileName, sb.ToString(), new UTF8Encoding(true));
            StatusChanged?.Invoke("结果已导出到：" + dlg.FileName);
        }
        catch (Exception ex)
        {
            MessageBox.Show("导出失败：" + ex.Message, "错误", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }
}

/// <summary>
/// “正规式自动机构造”页（探索实验一）。
/// 输入任意正规式，程序依次完成：
///   ① 用 Thompson 构造法建立 ε-NFA；
///   ② 用子集法确定化为 DFA；
///   ③ 用分割法化简得到最简 DFA；
/// 并输出三张状态转换矩阵与三张状态转换图。
/// </summary>
public sealed class RegexPanel : UserControl
{
    private readonly TextBox _pattern = new();
    private readonly ComboBox _preset = new();
    private readonly CheckBox _compress = new();
    private readonly Label _info = new();

    private readonly MatrixPanel _nfaMatrix = new();
    private readonly MatrixPanel _dfaMatrix = new();
    private readonly MatrixPanel _minMatrix = new();
    private readonly GraphView _graph = new();
    private readonly ComboBox _graphSelector = new();

    private Nfa? _nfa;
    private Dfa? _dfa;
    private Dfa? _minDfa;

    public Action<string>? StatusChanged { get; set; }

    public RegexPanel()
    {
        Dock = DockStyle.Fill;
        BackColor = Color.White;
        BuildUi();
    }

    /// <summary>常用正规式示例。</summary>
    private static readonly (string Name, string Pattern)[] Presets =
    {
        ("标识符 [A-Za-z_][A-Za-z0-9_]*", "[A-Za-z_][A-Za-z0-9_]*"),
        ("无符号整数 [0-9]+", "[0-9]+"),
        ("十六进制整数 0[xX][0-9A-Fa-f]+", "0[xX][0-9A-Fa-f]+"),
        ("双精度常量 [0-9]+.[0-9]*([eE][+-][0-9]+)?", "[0-9]+.[0-9]*([eE][+-][0-9]+)?"),
        ("教材经典例 (a|b)*abb", "(a|b)*abb"),
        ("以 a 开头以 b 结尾 a(a|b)*b", "a(a|b)*b"),
        ("闭包与或 a*b|c", "a*b|c")
    };

    // ==================================================================
    //                              界面搭建
    // ==================================================================

    private void BuildUi()
    {
        // ---------------- 顶部输入区（自动换行，窄窗口/高 DPI 下不会互相遮挡） ----------------
        var top = new FlowLayoutPanel
        {
            Dock = DockStyle.Top,
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            WrapContents = true,
            FlowDirection = FlowDirection.LeftToRight,
            Padding = new Padding(8, 6, 8, 6),
            Margin = new Padding(0),
            BackColor = Ui.HeaderBack
        };

        var lbl = new Label
        {
            Text = "正规式：",
            Font = Ui.TitleFont,
            AutoSize = true,
            Height = Ui.ButtonHeight,
            Margin = new Padding(0, 2, 4, 2),
            TextAlign = ContentAlignment.MiddleLeft
        };

        _pattern.Font = Ui.MonoFont;
        _pattern.Width = Ui.UiFont.Height * 26;
        _pattern.Margin = new Padding(0, 2, 8, 2);
        _pattern.Text = Presets[0].Pattern;
        _pattern.KeyDown += (_, e) => { if (e.KeyCode == Keys.Enter) { e.SuppressKeyPress = true; Build(); } };

        var btnBuild = Ui.MakeButton("构造并转换");
        btnBuild.Font = new Font(Ui.UiFont, FontStyle.Bold);
        btnBuild.Click += (_, _) => Build();

        var lblPreset = new Label
        {
            Text = "常用正规式：",
            Font = Ui.UiFont,
            AutoSize = true,
            Height = Ui.ButtonHeight,
            Margin = new Padding(12, 2, 4, 2),
            TextAlign = ContentAlignment.MiddleLeft
        };

        _preset.Font = Ui.UiFont;
        _preset.DropDownStyle = ComboBoxStyle.DropDownList;
        _preset.Width = Ui.UiFont.Height * 22;
        _preset.Margin = new Padding(0, 3, 8, 2);
        foreach (var p in Presets) _preset.Items.Add(p.Name);
        _preset.SelectedIndex = 0;
        _preset.SelectedIndexChanged += (_, _) =>
        {
            if (_preset.SelectedIndex >= 0) _pattern.Text = Presets[_preset.SelectedIndex].Pattern;
        };

        _compress.Text = "合并等价字符列";
        _compress.Font = Ui.UiFont;
        _compress.AutoSize = true;
        _compress.Height = Ui.ButtonHeight;
        _compress.Margin = new Padding(4, 2, 4, 2);
        _compress.TextAlign = ContentAlignment.MiddleLeft;
        _compress.Checked = true;
        _compress.CheckedChanged += (_, _) => RefreshMatrices();

        var hint = new Label
        {
            Text = "支持的运算：连接、或 |、闭包 *（实验扩展：分组 ()、正闭包 +、可选 ?、字符类 [A-Za-z0-9_]、转义 \\x）",
            Font = Ui.UiFontSmall,
            ForeColor = Color.DimGray,
            AutoSize = true,
            Height = Ui.InfoHeight,
            Margin = new Padding(2, 4, 0, 0),
            TextAlign = ContentAlignment.MiddleLeft
        };

        top.Controls.AddRange(new Control[] { lbl, _pattern, btnBuild, lblPreset, _preset, _compress, hint });

        // ---------------- 结果说明行 ----------------
        _info.Dock = DockStyle.Top;
        _info.Height = Ui.SummaryHeight;
        _info.TextAlign = ContentAlignment.MiddleLeft;
        _info.Font = Ui.UiFontSmall;
        _info.ForeColor = Ui.AccentColor;
        _info.Padding = new Padding(10, 0, 0, 0);
        _info.BackColor = Ui.PanelBack;
        _info.Text = "请在左上方输入正规式后点击“构造并转换”。";

        // ---------------- 左侧：三个状态转换矩阵 ----------------
        // 标签页标题刻意取得短一些：高 DPI 缩放后标题会变宽，标题过长会被裁切
        var tabNfa = new TabPage("① NFA 矩阵") { BackColor = Color.White, Padding = new Padding(3) };
        tabNfa.Controls.Add(_nfaMatrix);
        var tabDfa = new TabPage("② DFA 矩阵") { BackColor = Color.White, Padding = new Padding(3) };
        tabDfa.Controls.Add(_dfaMatrix);
        var tabMin = new TabPage("③ 最简 DFA 矩阵") { BackColor = Color.White, Padding = new Padding(3) };
        tabMin.Controls.Add(_minMatrix);

        var matrixTabs = new TabControl { Dock = DockStyle.Fill, Font = Ui.UiFont };
        matrixTabs.Controls.Add(tabNfa);
        matrixTabs.Controls.Add(tabDfa);
        matrixTabs.Controls.Add(tabMin);

        var matrixGroup = new GroupBox
        {
            Dock = DockStyle.Fill,
            Text = "状态转换矩阵",
            Font = Ui.TitleFont,
            Padding = new Padding(8, 6, 8, 8),
            BackColor = Color.White
        };
        matrixGroup.Controls.Add(matrixTabs);

        // ---------------- 右侧：状态转换图 ----------------
        var graphHeader = new FlowLayoutPanel
        {
            Dock = DockStyle.Top,
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            WrapContents = true,
            Padding = new Padding(2, 2, 2, 4),
            Margin = new Padding(0),
            BackColor = Color.White
        };

        var lblGraph = new Label
        {
            Text = "显示：",
            Font = Ui.UiFont,
            AutoSize = true,
            Height = Ui.ButtonHeight,
            Margin = new Padding(0, 2, 4, 2),
            TextAlign = ContentAlignment.MiddleLeft
        };
        _graphSelector.Font = Ui.UiFont;
        _graphSelector.DropDownStyle = ComboBoxStyle.DropDownList;
        _graphSelector.Width = Ui.UiFont.Height * 18;
        _graphSelector.Margin = new Padding(0, 3, 8, 2);
        _graphSelector.Items.AddRange(new object[] { "NFA 状态转换图", "DFA 状态转换图", "最简 DFA 状态转换图" });
        _graphSelector.SelectedIndex = 0;
        _graphSelector.SelectedIndexChanged += (_, _) => RefreshGraph();

        var btnSavePng = Ui.MakeButton("导出状态图…");
        btnSavePng.Click += (_, _) => ExportGraph();

        graphHeader.Controls.AddRange(new Control[] { lblGraph, _graphSelector, btnSavePng });

        _graph.Dock = DockStyle.Fill;

        var graphGroup = new GroupBox
        {
            Dock = DockStyle.Fill,
            Text = "状态转换图（初态用指入箭头表示，终态用双圈表示）",
            Font = Ui.TitleFont,
            Padding = Ui.GroupPadding(Ui.TitleFont),
            BackColor = Color.White
        };
        graphGroup.Controls.Add(_graph);
        graphGroup.Controls.Add(graphHeader);

        var split = new SplitContainer
        {
            Dock = DockStyle.Fill,
            SplitterWidth = 6,
            BackColor = Ui.SplitBack
        };
        split.Panel1.Controls.Add(matrixGroup);
        split.Panel2.Controls.Add(graphGroup);
        Ui.SetupSplit(split, Orientation.Vertical, 0.56,
            Ui.ButtonHeight * 8, Ui.ButtonHeight * 9);

        Controls.Add(split);
        Controls.Add(_info);
        Controls.Add(top);
    }

    // ==================================================================
    //                              核心流程
    // ==================================================================

    /// <summary>构造并转换：正规式 → NFA → DFA → 最简 DFA。</summary>
    private void Build()
    {
        string pattern = _pattern.Text.Trim();
        if (pattern.Length == 0)
        {
            MessageBox.Show("请输入正规式。", "提示", MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }

        try
        {
            RegexNode ast = RegexParser.Parse(pattern, out var alphabet);
            _nfa = AutomataBuilder.BuildNfa(ast, alphabet);
            _dfa = AutomataBuilder.Determinize(_nfa);
            _minDfa = DfaMinimizer.Minimize(_dfa);
        }
        catch (RegexFormatException ex)
        {
            MessageBox.Show("正规式有误：" + ex.Message, "正规式错误",
                MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }
        catch (Exception ex)
        {
            MessageBox.Show("构造自动机时出现异常：" + ex.Message, "错误",
                MessageBoxButtons.OK, MessageBoxIcon.Error);
            return;
        }

        int minLive = _minDfa.States.Count(s => !s.IsDead);
        _info.Text =
            $"正规式：{pattern}　　字母表 Σ = {CharSetSpec.Describe(_nfa.Alphabet)}" +
            $"　　状态数：NFA {_nfa.States.Count} → DFA {_dfa.States.Count} → 最简 DFA {minLive}" +
            $"　　（初态 {Dfa.StateName(_minDfa.Start)}）";

        RefreshMatrices();
        RefreshGraph();

        StatusChanged?.Invoke(
            $"自动机构造完成：NFA {_nfa.States.Count} 个状态，DFA {_dfa.States.Count} 个状态，最简 DFA {minLive} 个状态。");
    }

    private void RefreshMatrices()
    {
        if (_nfa is null || _dfa is null || _minDfa is null) return;

        bool compress = _compress.Checked;
        _nfaMatrix.Show(_nfa.ToMatrix(compress));
        _dfaMatrix.Show(_dfa.ToMatrix("DFA 状态转换矩阵（确定化后）", compress));
        _minMatrix.Show(_minDfa.ToMatrix("最简 DFA 状态转换矩阵", compress));
    }

    private void RefreshGraph()
    {
        if (_nfa is null || _dfa is null || _minDfa is null) { _graph.Model = null; return; }

        _graph.Model = _graphSelector.SelectedIndex switch
        {
            1 => _dfa.ToGraph("DFA 状态转换图（确定化后）"),
            2 => _minDfa.ToGraph("最简 DFA 状态转换图"),
            _ => _nfa.ToGraph()
        };
    }

    private void ExportGraph()
    {
        if (_graph.Model is null)
        {
            MessageBox.Show("请先生成自动机。", "提示", MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }

        using var dlg = new SaveFileDialog
        {
            Title = "导出状态转换图",
            Filter = "PNG 图片 (*.png)|*.png",
            FileName = "状态转换图.png"
        };
        if (dlg.ShowDialog(FindForm()) != DialogResult.OK) return;

        try
        {
            var size = new Size(Math.Max(600, _graph.Width), Math.Max(420, _graph.Height));
            using var bmp = new Bitmap(size.Width, size.Height);
            using (var g = Graphics.FromImage(bmp))
            {
                g.Clear(Color.White);
                _graph.RenderTo(g, size);
            }
            bmp.Save(dlg.FileName, System.Drawing.Imaging.ImageFormat.Png);
            StatusChanged?.Invoke("状态转换图已导出到：" + dlg.FileName);
        }
        catch (Exception ex)
        {
            MessageBox.Show("导出失败：" + ex.Message, "错误", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }
}

/// <summary>
/// “图片识别并分析”页（探索实验二）。
/// 流程：载入源代码图片 → 自动预处理（反色/放大/可选二值化）→ OCR 识别出代码文本
/// → 对识别出的代码执行词法分析，输出与基础实验一致的单词序列。
/// 识别结果允许手工修正后再次分析，这样即使 OCR 有个别误识也不影响做后续实验。
/// </summary>
public sealed class OcrPanel : UserControl
{
    private readonly PictureBox _preview = new();
    private readonly Label _imageInfo = new();
    private readonly TextBox _code = Ui.MakeCodeBox();
    private readonly LexResultView _result = new();
    private readonly CheckBox _autoInvert = new();
    private readonly CheckBox _binarize = new();
    private readonly Button _btnRecognize = Ui.MakeButton("识别并分析");

    private LexerConfig _config = LexerConfig.CreateDefault();
    private Bitmap? _bitmap;
    private string? _imagePath;

    public Action<string>? StatusChanged { get; set; }

    public OcrPanel()
    {
        Dock = DockStyle.Fill;
        BackColor = Color.White;
        BuildUi();
    }

    public void ApplyConfig(LexerConfig config) => _config = config;

    // ==================================================================
    //                              界面搭建
    // ==================================================================

    private void BuildUi()
    {
        // ---------------- 顶部工具栏（自动换行、高度自适应） ----------------
        var toolbar = Ui.MakeToolbar();

        var btnOpen = Ui.MakeButton("打开图片…");
        btnOpen.Click += (_, _) => OpenImage();

        _btnRecognize.Font = new Font(Ui.UiFont, FontStyle.Bold);
        _btnRecognize.Click += async (_, _) => await RecognizeAndAnalyzeAsync();

        var btnAnalyze = Ui.MakeButton("仅做词法分析");
        btnAnalyze.Click += (_, _) => AnalyzeCode();

        _autoInvert.Text = "自动反色（暗色主题截图）";
        _autoInvert.Font = Ui.UiFont;
        _autoInvert.Checked = true;
        _autoInvert.AutoSize = true;
        _autoInvert.Height = Ui.ButtonHeight;
        _autoInvert.Margin = new Padding(14, 2, 0, 2);
        _autoInvert.TextAlign = ContentAlignment.MiddleLeft;

        _binarize.Text = "二值化增强";
        _binarize.Font = Ui.UiFont;
        _binarize.AutoSize = true;
        _binarize.Height = Ui.ButtonHeight;
        _binarize.Margin = new Padding(10, 2, 0, 2);
        _binarize.TextAlign = ContentAlignment.MiddleLeft;

        toolbar.Controls.AddRange(new Control[]
        {
            btnOpen, _btnRecognize, btnAnalyze, _autoInvert, _binarize
        });

        // ---------------- 左：图片 ----------------
        _preview.Dock = DockStyle.Fill;
        _preview.SizeMode = PictureBoxSizeMode.Zoom;
        _preview.BackColor = Color.FromArgb(245, 246, 248);
        _preview.BorderStyle = BorderStyle.FixedSingle;

        _imageInfo.Dock = DockStyle.Bottom;
        _imageInfo.Height = Ui.InfoHeight;
        _imageInfo.Font = Ui.UiFontSmall;
        _imageInfo.ForeColor = Color.DimGray;
        _imageInfo.Padding = new Padding(4, 0, 0, 0);
        _imageInfo.TextAlign = ContentAlignment.MiddleLeft;
        _imageInfo.Text = "尚未载入图片。";

        var imageGroup = new GroupBox
        {
            Dock = DockStyle.Fill,
            Text = "源代码图片",
            Font = Ui.TitleFont,
            Padding = Ui.GroupPadding(Ui.TitleFont),
            BackColor = Color.White
        };
        imageGroup.Controls.Add(_preview);
        imageGroup.Controls.Add(_imageInfo);

        // ---------------- 右上：识别出的代码 ----------------
        _code.Dock = DockStyle.Fill;
        var codeGroup = new GroupBox
        {
            Dock = DockStyle.Fill,
            Text = "识别出的源代码（可手工修正后再分析）",
            Font = Ui.TitleFont,
            Padding = Ui.GroupPadding(Ui.TitleFont),
            BackColor = Color.White
        };
        codeGroup.Controls.Add(_code);

        // ---------------- 右下：词法分析结果 ----------------
        var resultGroup = new GroupBox
        {
            Dock = DockStyle.Fill,
            Text = "词法分析结果",
            Font = Ui.TitleFont,
            Padding = Ui.GroupPadding(Ui.TitleFont),
            BackColor = Color.White
        };
        resultGroup.Controls.Add(_result);

        var rightSplit = new SplitContainer
        {
            Dock = DockStyle.Fill,
            SplitterWidth = 6,
            BackColor = Ui.SplitBack
        };
        rightSplit.Panel1.Controls.Add(codeGroup);
        rightSplit.Panel2.Controls.Add(resultGroup);
        Ui.SetupSplit(rightSplit, Orientation.Horizontal, 0.42,
            Ui.ButtonHeight * 3, Ui.ButtonHeight * 4);

        var split = new SplitContainer
        {
            Dock = DockStyle.Fill,
            SplitterWidth = 6,
            BackColor = Ui.SplitBack
        };
        split.Panel1.Controls.Add(imageGroup);
        split.Panel2.Controls.Add(rightSplit);
        Ui.SetupSplit(split, Orientation.Vertical, 0.40,
            Ui.ButtonHeight * 8, Ui.ButtonHeight * 11);

        Controls.Add(split);
        Controls.Add(toolbar);
    }

    // ==================================================================
    //                              功能实现
    // ==================================================================

    private void OpenImage()
    {
        using var dlg = new OpenFileDialog
        {
            Title = "打开包含源代码的图片",
            Filter = "图片文件 (*.png;*.jpg;*.jpeg;*.bmp;*.gif;*.tif;*.tiff)|*.png;*.jpg;*.jpeg;*.bmp;*.gif;*.tif;*.tiff|所有文件 (*.*)|*.*"
        };
        if (dlg.ShowDialog(FindForm()) != DialogResult.OK) return;

        try
        {
            var loaded = new Bitmap(dlg.FileName);
            _bitmap?.Dispose();
            _bitmap = loaded;
            _imagePath = dlg.FileName;
            _preview.Image = _bitmap;
            _imageInfo.Text = $"文件：{Path.GetFileName(dlg.FileName)}　尺寸：{_bitmap.Width}×{_bitmap.Height} 像素";
            StatusChanged?.Invoke("已载入图片：" + dlg.FileName);
        }
        catch (Exception ex)
        {
            MessageBox.Show("打开图片失败：" + ex.Message, "错误", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    /// <summary>识别图片并在识别结果上做词法分析。</summary>
    private async Task RecognizeAndAnalyzeAsync()
    {
        if (_bitmap is null)
        {
            MessageBox.Show("请先点击“打开图片…”载入一张包含源代码的图片。", "提示",
                MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }

        if (!CodeOcr.IsAvailable)
        {
            MessageBox.Show(
                "找不到 OCR 语言数据（tessdata\\eng.traineddata）。\r\n" +
                "请确认程序目录完整；语言数据已随程序一起打包，正常解压后即可使用。",
                "OCR 不可用", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        var options = new OcrOptions
        {
            AutoInvert = _autoInvert.Checked,
            Binarize = _binarize.Checked
        };

        _btnRecognize.Enabled = false;
        string oldText = _btnRecognize.Text;
        _btnRecognize.Text = "识别中…";
        Cursor = Cursors.WaitCursor;
        StatusChanged?.Invoke("正在进行图片识别，请稍候…");

        try
        {
            var bitmap = _bitmap;
            OcrResult ocr = await Task.Run(() => CodeOcr.Recognize(bitmap, options));

            _code.Text = ocr.Text.Replace("\r\n", "\n").Replace("\n", "\r\n");
            StatusChanged?.Invoke(
                $"图片识别完成（平均置信度 {ocr.MeanConfidence:P0}，{ocr.LineCount} 行，{ocr.Info}）。");

            AnalyzeCode();
        }
        catch (Exception ex)
        {
            MessageBox.Show("图片识别失败：" + ex.Message, "错误", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
        finally
        {
            Cursor = Cursors.Default;
            _btnRecognize.Text = oldText;
            _btnRecognize.Enabled = true;
        }
    }

    /// <summary>对当前文本框中的代码做词法分析。</summary>
    private void AnalyzeCode()
    {
        string code = _code.Text;
        if (code.Trim().Length == 0)
        {
            MessageBox.Show("还没有可分析的代码，请先识别图片或手工输入。", "提示",
                MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }

        try
        {
            var result = new Lexer(code, _config).Analyze();
            _result.ShowResult(result, "图片识别");
            StatusChanged?.Invoke(
                $"词法分析完成：共 {result.Tokens.Count} 个单词，{result.Errors.Count} 个词法错误。");
        }
        catch (Exception ex)
        {
            MessageBox.Show("词法分析失败：" + ex.Message, "错误", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    /// <summary>释放图片资源。</summary>
    public void ReleaseResources()
    {
        _preview.Image = null;
        _bitmap?.Dispose();
        _bitmap = null;
    }
}
