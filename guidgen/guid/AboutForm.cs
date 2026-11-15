using System;
using System.Drawing;
using System.Windows.Forms;

namespace guid
{
	/// <summary>
	/// 关于对话框：只展示程序图标、程序名称和代码来源说明。
	/// 控件布局在代码里一次性搭好（这样的固定小对话框用设计器反而更繁琐）。
	/// 图标位图由主窗体传入并复用，这里不负责释放。
	/// </summary>
	internal sealed class AboutForm : Form
	{
		private readonly PictureBox _picture;
		private readonly Label _lblName;
		private readonly Label _lblCredit;
		private readonly Button _btnOk;

		public AboutForm(Icon icon, Bitmap iconImage)
		{
			_picture = new PictureBox();
			_lblName = new Label();
			_lblCredit = new Label();
			_btnOk = new Button();

			InitializeLayout(icon, iconImage);
		}

		private void InitializeLayout(Icon icon, Bitmap iconImage)
		{
			SuspendLayout();

			// 程序图标
			_picture.Location = new Point(128, 20);
			_picture.Size = new Size(64, 64);
			_picture.SizeMode = PictureBoxSizeMode.CenterImage;
			_picture.Image = iconImage;
			_picture.TabStop = false;

			// 程序名称
			_lblName.Font = new Font("Microsoft YaHei UI", 14F, FontStyle.Bold);
			_lblName.Location = new Point(16, 94);
			_lblName.Size = new Size(288, 28);
			_lblName.Text = "GUID生成器";
			_lblName.TextAlign = ContentAlignment.MiddleCenter;

			// 代码来源说明
			_lblCredit.ForeColor = SystemColors.GrayText;
			_lblCredit.Location = new Point(16, 124);
			_lblCredit.Size = new Size(288, 20);
			_lblCredit.Text = "代码部分系人工智能技术合成";
			_lblCredit.TextAlign = ContentAlignment.MiddleCenter;

			// 确定
			_btnOk.DialogResult = DialogResult.OK;
			_btnOk.Location = new Point(120, 160);
			_btnOk.Size = new Size(80, 28);
			_btnOk.Text = "确定";
			_btnOk.UseVisualStyleBackColor = true;

			AcceptButton = _btnOk;
			CancelButton = _btnOk;

			AutoScaleDimensions = new SizeF(96F, 96F);
			AutoScaleMode = AutoScaleMode.Dpi;
			ClientSize = new Size(320, 208);
			Controls.Add(_picture);
			Controls.Add(_lblName);
			Controls.Add(_lblCredit);
			Controls.Add(_btnOk);
			Font = new Font("Microsoft YaHei UI", 9F);
			FormBorderStyle = FormBorderStyle.FixedDialog;
			Icon = icon;
			MaximizeBox = false;
			MinimizeBox = false;
			ShowInTaskbar = false;
			StartPosition = FormStartPosition.CenterParent;
			Text = "关于";

			ResumeLayout(false);
			PerformLayout();
		}

		protected override void Dispose(bool disposing)
		{
			if (disposing)
			{
				// 位图归主窗体所有，只解除引用
				_picture.Image = null;
			}
			base.Dispose(disposing);
		}
	}
}
