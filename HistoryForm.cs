using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace betterGitGUI
{
    /// <summary>
    /// 提交历史浏览窗口：左边是提交列表，右上这次提交改了哪些文件，右下选中文件的 diff。
    /// 解决"想在 GUI 里看看某次提交到底改了什么"这个高频需求。
    /// </summary>
    public class HistoryForm : Form
    {
        private ListView listCommits;
        private ListView listFiles;
        private RichTextBox boxDiff;
        private Label lblDetail;

        private readonly List<string> _hashes = new List<string>();
        private string _currentHash;
        private int _filesSeq;

        public HistoryForm()
        {
            BuildUi();
            LoadCommits();
        }

        private void BuildUi()
        {
            Text = "提交历史";
            StartPosition = FormStartPosition.CenterParent;
            ClientSize = new Size(1080, 660);
            MinimumSize = new Size(820, 520);
            Font = new Font("Microsoft YaHei UI", 9F);

            var bottom = new Panel { Dock = DockStyle.Bottom, Height = 46 };
            var btnRefresh = new Button { Text = "刷新", Location = new Point(10, 8), Size = new Size(80, 28) };
            btnRefresh.Click += (s, e) => LoadCommits();

            var btnCopy = new Button { Text = "复制哈希", Location = new Point(98, 8), Size = new Size(90, 28) };
            btnCopy.Click += (s, e) =>
            {
                if (_currentHash == null) { MessageBox.Show(this, "请先选择一条提交。"); return; }
                Clipboard.SetText(_currentHash);
            };

            var btnClose = new Button
            {
                Text = "关闭",
                Location = new Point(962, 8),
                Size = new Size(100, 28),
                Anchor = AnchorStyles.Top | AnchorStyles.Right,
            };
            btnClose.Click += (s, e) => Close();
            bottom.Controls.AddRange(new Control[] { btnRefresh, btnCopy, btnClose });

            var main = new SplitContainer
            {
                Dock = DockStyle.Fill,
                Orientation = Orientation.Vertical,
                SplitterDistance = 430,
                SplitterWidth = 5,
            };

            var labelCommits = new Label
            {
                Dock = DockStyle.Top,
                Height = 22,
                Text = "  提交（--all，最近 400 条）",
                TextAlign = ContentAlignment.MiddleLeft,
            };

            listCommits = new ListView
            {
                Dock = DockStyle.Fill,
                View = View.Details,
                FullRowSelect = true,
                MultiSelect = false,
                GridLines = true,
                HideSelection = false,
            };
            listCommits.Columns.Add("哈希", 84);
            listCommits.Columns.Add("日期", 136);
            listCommits.Columns.Add("作者", 100);
            listCommits.Columns.Add("说明", 240);
            listCommits.SelectedIndexChanged += (s, e) => OnCommitSelected();

            main.Panel1.Controls.Add(listCommits);
            main.Panel1.Controls.Add(labelCommits);

            var inner = new SplitContainer
            {
                Dock = DockStyle.Fill,
                Orientation = Orientation.Horizontal,
                SplitterDistance = 170,
                SplitterWidth = 5,
            };

            var labelFiles = new Label
            {
                Dock = DockStyle.Top,
                Height = 22,
                Text = "  本次提交改动的文件",
                TextAlign = ContentAlignment.MiddleLeft,
            };

            listFiles = new ListView
            {
                Dock = DockStyle.Fill,
                View = View.Details,
                FullRowSelect = true,
                MultiSelect = false,
                GridLines = true,
                HideSelection = false,
            };
            listFiles.Columns.Add("状态", 60);
            listFiles.Columns.Add("文件", 400);
            listFiles.SelectedIndexChanged += (s, e) => LoadFileDiff();

            inner.Panel1.Controls.Add(listFiles);
            inner.Panel1.Controls.Add(labelFiles);

            lblDetail = new Label
            {
                Dock = DockStyle.Top,
                Height = 24,
                Text = "",
                TextAlign = ContentAlignment.MiddleLeft,
                ForeColor = Color.FromArgb(70, 70, 70),
            };

            boxDiff = new RichTextBox
            {
                Dock = DockStyle.Fill,
                ReadOnly = true,
                WordWrap = false,
                ScrollBars = RichTextBoxScrollBars.Both,
                DetectUrls = false,
                HideSelection = false,
                Font = new Font("Consolas", 9F),
                BackColor = Color.White,
            };

            inner.Panel2.Controls.Add(boxDiff);
            inner.Panel2.Controls.Add(lblDetail);

            main.Panel2.Controls.Add(inner);

            Controls.Add(main);
            Controls.Add(bottom);
        }

        private async void LoadCommits()
        {
            var r = await Task.Run(() => GitRunner.RunUi(
                "log --all --exclude=refs/stash --date=format:\"%Y-%m-%d %H:%M\" "
                + "--format=\"%h%x09%ad%x09%an%x09%s\" -n 400"));

            listCommits.BeginUpdate();
            listCommits.Items.Clear();
            _hashes.Clear();

            if (!r.Success)
            {
                listCommits.EndUpdate();
                boxDiff.Text = "读取提交历史失败：\n" + r.StdErr;
                return;
            }

            using (var reader = new StringReader(r.StdOut))
            {
                string line;
                while ((line = reader.ReadLine()) != null)
                {
                    if (line.Trim().Length == 0) continue;
                    var parts = line.Split('\t');
                    if (parts.Length < 4) continue;
                    var shortHash = parts[0];
                    var item = new ListViewItem(shortHash);
                    item.SubItems.Add(parts[1]);
                    item.SubItems.Add(parts[2]);
                    item.SubItems.Add(parts[3]);
                    item.Tag = shortHash;
                    listCommits.Items.Add(item);
                    _hashes.Add(shortHash);
                }
            }
            listCommits.EndUpdate();

            if (listCommits.Items.Count > 0) listCommits.Items[0].Selected = true;
            else
            {
                boxDiff.Text = "（仓库还没有提交）";
            }
        }

        private async void OnCommitSelected()
        {
            if (listCommits.SelectedItems.Count == 0) return;
            var item = listCommits.SelectedItems[0];
            _currentHash = item.Tag as string;
            if (_currentHash == null) return;

            var hash = _currentHash;
            var seq = ++_filesSeq;

            lblDetail.Text = "  " + item.Text + "  ·  " + item.SubItems[1].Text
                           + "  ·  " + item.SubItems[2].Text + "  ·  " + item.SubItems[3].Text;
            listFiles.Items.Clear();
            boxDiff.Clear();

            var r = await Task.Run(() => GitRunner.RunUi(
                "show --name-status --format= --no-renames " + GitRunner.Quote(hash)));

            if (seq != _filesSeq) return;
            if (!r.Success)
            {
                boxDiff.Text = "读取提交内容失败：\n" + r.StdErr;
                return;
            }

            listFiles.BeginUpdate();
            using (var reader = new StringReader(r.StdOut))
            {
                string line;
                while ((line = reader.ReadLine()) != null)
                {
                    if (line.Trim().Length == 0) continue;
                    var parts = line.Split('\t');
                    if (parts.Length < 2) continue;
                    var fileItem = new ListViewItem(parts[0]);
                    fileItem.SubItems.Add(parts[1]);
                    fileItem.Tag = parts[1];
                    switch (parts[0].Substring(0, 1))
                    {
                        case "A": fileItem.ForeColor = Color.FromArgb(20, 110, 40); break;
                        case "D": fileItem.ForeColor = Color.Firebrick; break;
                        case "M": fileItem.ForeColor = Color.DarkBlue; break;
                        default: fileItem.ForeColor = Color.DarkOrange; break;
                    }
                    listFiles.Items.Add(fileItem);
                }
            }
            listFiles.EndUpdate();

            if (listFiles.Items.Count == 0)
                boxDiff.Text = "（这次提交没有文件改动，可能是一次合并提交）";
            else
                listFiles.Items[0].Selected = true;
        }

        private async void LoadFileDiff()
        {
            if (listFiles.SelectedItems.Count == 0) return;
            var path = listFiles.SelectedItems[0].Tag as string;
            if (path == null || _currentHash == null) return;

            var hash = _currentHash;
            var r = await Task.Run(() => GitRunner.RunUi(
                "show --no-color --no-ext-diff --no-textconv --format= "
                + GitRunner.Quote(hash) + " -- " + GitRunner.Quote(path)));

            var text = r.Success ? r.StdOut : "读取差异失败：\n" + r.StdErr;
            if (string.IsNullOrWhiteSpace(text)) text = "（没有可显示的差异，可能是二进制文件或空改动）";
            DiffRenderer.ApplyTo(boxDiff, DiffRenderer.BuildLines(text));
        }
    }
}
